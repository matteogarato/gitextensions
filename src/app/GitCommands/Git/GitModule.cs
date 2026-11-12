using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands.Config;
using GitCommands.Git.Extensions;
using GitCommands.Patches;
using GitCommands.Remotes;
using GitCommands.Settings;
using GitCommands.Utils;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUIPluginInterfaces;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;
using GitStash = GitExtensions.Extensibility.Git.GitStash;

namespace GitCommands.Git;

/// <summary>
///     Provides manipulation with git module.
///     <remarks>Several instances may be created for submodules.</remarks>
/// </summary>
[DebuggerDisplay("GitModule ( {" + nameof(WorkingDir) + "} )")]
public sealed partial class GitModule : IGitModule
{
    private const string _gitError = "Git Error";

    private static readonly Encoding _defaultEncoding = new UTF8Encoding(false);
    private static readonly SearchValues<char> _spaceAndTabSearchValues = SearchValues.Create(' ', '\t');

    // the amount of lines we must skip in order to get to an annotated tag's message when doing git cat-file -p <tag_name>
    private static readonly int _standardCatFileTagHeaderLength = 5;

    public static readonly string NoNewLineAtTheEnd = "\\ No newline at end of file";
    private static readonly GitTreeParser _gitTreeParser = new();

    // Encoding that let us read all bytes without replacing any char
    // It is using to read output of commands, which may consist of:
    // 1) commit header (message, author, ...) encoded in CommitEncoding, recoded to LogOutputEncoding or not dependent of
    //    pretty parameter (pretty=raw - recoded, pretty=format - not recoded)
    // 2) file content encoded in its original encoding
    // 3) file path (file name is encoded in system default encoding),
    //    when core.quotepath is on, every non ASCII character is escaped
    //    with \ followed by its code as a three digit octal number
    // 4) branch, tag name, errors, warnings, hints encoded in system default encoding
    public static readonly Encoding LosslessEncoding = Encoding.GetEncoding("ISO-8859-1"); // is any better?
    private readonly IGitExecutor _executor;

    private readonly IGitExecutorProvider _executorProvider;
    private readonly GetAllChangedFilesOutputParser _getAllChangedFilesOutputParser;
    private readonly Lock _gitCommonLock = new();
    private readonly IIndexLockManager _indexLockManager;
    private readonly Lock _lock = new();
    private readonly IRevisionDiffProvider _revisionDiffProvider = new RevisionDiffProvider();

    private DistributedSettings? _effectiveSettings;

    private string? _gitCommonDirectory;

    private DistributedSettings? _localSettings;
    private FrozenDictionary<string, Color>? _remoteColors;

    /// <summary>
    ///     Name of the WSL distro for the GitExecutable, empty string for the app native Windows Git executable.
    ///     This can be seen as the Git "instance" identifier.
    /// </summary>
    public GitModule(IGitExecutorProvider executorProvider, string? workingDir)
    {
        _executorProvider = executorProvider;
        _executor = executorProvider.GetExecutor(workingDir ?? "");
        WorkingDirGitDir = _executor.GetGitDirectory();
        _indexLockManager = new IndexLockManager(this);
        _getAllChangedFilesOutputParser = new GetAllChangedFilesOutputParser(() => this);

        // If this is a submodule, populate relevant properties.
        // If this is not a submodule, these will all be null.
        (SuperprojectModule, SubmodulePath) = InitialiseSubmoduleProperties();

        return;

        (GitModule? superprojectModule, string? submodulePath) InitialiseSubmoduleProperties()
        {
            if (!IsValidGitWorkingDir())
            {
                return (null, null);
            }

            var currentPath = WorkingDir.RemoveTrailingPathSeparator();

            // Try to find an ancestor path that contains a .gitmodules file and is a valid work dir
            var superprojectPath = PathUtil.FindAncestors(currentPath).FirstOrDefault(HasGitModulesFile);

            // If we didn't find it, but there's a .git file in the current folder, look for a gitdir:
            // line in that file that points to the location of the .git folder
            var gitDir = Path.Join(WorkingDir, ".git");
            if (superprojectPath is null && File.Exists(gitDir))
            {
                IEnumerable<string> lines;
                try
                {
                    lines = File.ReadLines(gitDir);
                }
                catch (IOException)
                {
                    // If we cannot read the .git file, assume it's not a submodule
                    // See also special handling of WSL .git symbolic links in PathUtil.IsWslLink()
                    // Symbolic links to submodule .git is not expected and not supported.
                    return (null, null);
                }

                foreach (var line in lines)
                {
                    const string gitdir = "gitdir:";

                    if (line.StartsWith(gitdir))
                    {
                        var gitPath = line[gitdir.Length..].Trim();
                        var pos = gitPath.IndexOf("/.git/modules/", StringComparison.Ordinal);
                        if (pos != -1)
                        {
                            gitPath = gitPath[..(pos + 1)].Replace('/', '\\');
                            gitPath = Path.GetFullPath(Path.Combine(WorkingDir, gitPath));
                            if (HasGitModulesFile(gitPath))
                            {
                                superprojectPath = gitPath;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(superprojectPath) ||
                !currentPath.ToPosixPath().StartsWith(superprojectPath.ToPosixPath()))
            {
                return (null, null);
            }

            var submodulePath = currentPath[superprojectPath.Length..].ToPosixPath();
            ConfigFile configFile = new(Path.Join(superprojectPath, ".gitmodules"));

            foreach (var configSection in configFile.ConfigSections)
            {
                if (configSection.GetValue("path") == submodulePath.ToPosixPath())
                {
                    GitModule superprojectModule = new(_executorProvider, superprojectPath);

                    return (superprojectModule, submodulePath);
                }
            }

            return (null, submodulePath);

            static bool HasGitModulesFile(string path)
            {
                return File.Exists(Path.Join(path, ".gitmodules")) && IsValidGitWorkingDir(path);
            }
        }
    }

    public static CommandCache GitCommandCache { get; } = new();

    // Parse lines of format:
    //
    // 69a7c7a40230346778e7eebed809773a6bc45268 refs/heads/master
    // 69a7c7a40230346778e7eebed809773a6bc45268 refs/remotes/origin/master
    // 366dfba1abf6cb98d2934455713f3d190df2ba34 refs/tags/2.51
    //
    // Lines may also use \t as a column delimiter, such as output of "ls-remote --heads origin".
    [GeneratedRegex(@"^(?<objectid>[0-9a-f]{40})[ \t](?<refname>.+)$",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex RefRegex { get; }

    [GeneratedRegex(@"^(?<objectid>[0-9a-f]{40}) (?<origlinenum>\d+) (?<finallinenum>\d+)",
        RegexOptions.ExplicitCapture)]
    private static partial Regex HeaderRegex { get; }

    [GeneratedRegex(
        @"^(?<name>[^\t]+)\t(?<url>.+?) \((?<direction>fetch|push)\)(?:(?# ignore trailing options)\s*\[[^\]]*])?$",
        RegexOptions.ExplicitCapture)]
    private static partial Regex RemoteVerboseLineRegex { get; }

    [GeneratedRegex(@"(\\(?<octal>[0-7]{3}))+", RegexOptions.ExplicitCapture)]
    private static partial Regex EscapedOctalCodePointRegex { get; }

    [GeneratedRegex(@"^(?<code>[\-+U ])(?<sha>[0-9a-f]{40}) (?<path>.+) \((?<branch>.+)\)$",
        RegexOptions.ExplicitCapture)]
    private static partial Regex ShaRegex { get; }

    [GeneratedRegex(@"^\s*(?<count>\d+)\s+(?<name>.*)$", RegexOptions.ExplicitCapture)]
    private static partial Regex ShortlogRegex { get; }

    /// <summary>
    ///     Gets the system encoding.
    /// </summary>
    public static Encoding SystemEncoding
    {
        get => GitExecutor.SystemEncoding;
    }

    /// <summary>
    ///     Gets the location of .git directory for the current working folder.
    /// </summary>
    private string WorkingDirGitDir { get; set; }

    public DistributedSettings EffectiveSettings
    {
        get
        {
            if (_effectiveSettings is null)
            {
                lock (_lock)
                {
                    _effectiveSettings ??= DistributedSettings.CreateEffective(this);
                }
            }

            return _effectiveSettings;
        }
    }

    public DistributedSettings LocalSettings
    {
        get
        {
            if (_localSettings is null)
            {
                lock (_lock)
                {
                    _localSettings ??=
                        new DistributedSettings(null, EffectiveSettings.SettingsCache, SettingLevel.Local);
                }
            }

            return _localSettings;
        }
    }

    private GitEncodingSettingsGetter GitEncodingSettingsGetter
    {
        get
        {
            if (field is null)
            {
                lock (_lock)
                {
                    field ??= new GitEncodingSettingsGetter(new EffectiveGitConfigSettings(GitExecutable));
                }
            }

            return field;
        }
    }

    private ISettingsValueGetter EffectiveGitConfigSettings
    {
        get => GitEncodingSettingsGetter.SettingsValueGetter;
    }

    private IGitConfigSettingsGetter LocalGitConfigSettings
    {
        get => field ??= new GitConfigSettings(GitExecutable, GitSettingLevel.Local);
    }

    /// <summary>
    ///     GitVersion for the default GitExecutable.
    /// </summary>
    IGitVersion IGitModule.GitVersion
    {
        get => GitVersion.CurrentVersion(GitExecutable);
    }

    string IGitModule.WorkingDir
    {
        get => _executor.WorkingDir;
    }

    IExecutable IGitModule.GitExecutable
    {
        get => _executor.GitExecutable;
    }

    IGitCommandRunner IGitModule.GitCommandRunner
    {
        get => _executor.GitCommandRunner;
    }

    string IGitModule.GetSelectedBranch(bool emptyIfDetached = false)
    {
        return Commands.GetSelectedBranch(_executor, emptyIfDetached);
    }

    /// <summary>
    ///     Gets the location of .git directory for the current working folder.
    /// </summary>
    string IGitModule.WorkingDirGitDir
    {
        get => WorkingDirGitDir;
    }

    /// <inherit />
    string IGitModule.GetPathForGitExecution(string? path)
    {
        return PathUtil.GetPathForGitExecution(path, _executor.WslDistro);
    }

    /// <inherit />
    string IGitModule.GetWindowsPath(string path)
    {
        return PathUtil.GetWindowsPath(path, _executor.WslDistro);
    }

    string? IGitModule.SubmodulePath { get; }

    IGitModule? IGitModule.SuperprojectModule { get; }

    IGitModule IGitModule.GetTopModule()
    {
        IGitModule topModule = this;
        while (topModule.SuperprojectModule is not null)
        {
            topModule = topModule.SuperprojectModule;
        }

        return topModule;
    }

    SettingsSource IGitModule.GetEffectiveSettings()
    {
        return EffectiveSettings;
    }

    SettingsSource IGitModule.GetLocalSettings()
    {
        return LocalSettings;
    }

    Encoding IGitModule.FilesEncoding
    {
        get => GitEncodingSettingsGetter.FilesEncoding ?? _defaultEncoding;
    }

    Encoding IGitModule.CommitEncoding
    {
        get => GitEncodingSettingsGetter.CommitEncoding ?? _defaultEncoding;
    }

    Encoding IGitModule.LogOutputEncoding
    {
        get => GitEncodingSettingsGetter.LogOutputEncoding ?? CommitEncoding;
    }

    IEnumerable<(string Setting, string Value)> IGitModule.GetAllLocalSettings()
    {
        return LocalGitConfigSettings.GetAllValues();
    }

    void IGitModule.InvalidateGitSettings()
    {
        EffectiveGitConfigSettings.Invalidate();
        LocalGitConfigSettings.Invalidate();
    }

    /// <summary>
    ///     Indicates whether the <see cref="IGitExecutor.WorkingDir" /> contains a git repository.
    /// </summary>
    bool IGitModule.IsValidGitWorkingDir()
    {
        return IsValidGitWorkingDir(WorkingDir);
    }

    /// <summary>
    ///     Asks git to resolve the given relativePath.
    ///     git special folders are located in different directories depending on the kind of repo: submodule, worktree, main.
    ///     See https://git-scm.com/docs/git-rev-parse#Documentation/git-rev-parse.txt---git-pathltpathgt.
    /// </summary>
    /// <param name="relativePath">A path relative to the .git directory.</param>
    /// <returns>An absolute path in Windows format.</returns>
    string IGitModule.ResolveGitInternalPath(string relativePath)
    {
        GitArgumentBuilder args = new("rev-parse") { "--git-path", relativePath.Quote() };
        var gitPath = GitExecutable.GetOutput(args).Trim();

        if (gitPath.StartsWith(".git/"))
        {
            gitPath = Path.Combine(_executor.GetGitDirectory(), gitPath[".git/".Length..]);
        }

        return GetWindowsPath(gitPath);
    }

    /// <summary>
    ///     Returns git common directory.
    ///     https://git-scm.com/docs/git-rev-parse#Documentation/git-rev-parse.txt---git-common-dir.
    /// </summary>
    string IGitModule.GitCommonDirectory
    {
        get
        {
            if (string.IsNullOrEmpty(WorkingDir))
            {
                // No directory for this module, so the common directory must be empty too
                // (should not be retrieved from the GE start directory)
                return "";
            }

            // Get a cache of the common dir
            // Lock needed as the command is called rapidly when creating the module
            if (_gitCommonDirectory is not null)
            {
                return _gitCommonDirectory;
            }

            lock (_gitCommonLock)
            {
                if (_gitCommonDirectory is null)
                {
                    GitArgumentBuilder args = new("rev-parse") { "--git-common-dir" };
                    var result = GitExecutable.Execute(args, throwOnErrorExit: false);

                    var dir = GetWindowsPath(result.StandardOutput).Trim();

                    if (!result.ExitedSuccessfully || dir == ".git" || dir == "." || !Directory.Exists(dir))
                    {
                        dir = _executor.GetGitDirectory();
                    }

                    _gitCommonDirectory = dir;
                }
            }

            return _gitCommonDirectory;
        }
    }

    bool IGitModule.IsBareRepository()
    {
        return WorkingDir == _executor.GetGitDirectory();
    }

    bool IGitModule.IsSubmodule(string submodulePath)
    {
        GitArgumentBuilder args = new("submodule") { "status", submodulePath };
        var result = GitExecutable.Execute(args, throwOnErrorExit: false);

        return result.ExitedSuccessfully || IsSubmoduleRemoved();

        bool IsSubmoduleRemoved()
        {
            return result.StandardOutput.StartsWith("No submodule mapping found in .gitmodules for path");
        }
    }

    bool IGitModule.HasSubmodules()
    {
        return GetSubmodulesLocalPaths(recursive: false).Any();
    }

    /// <inheritdoc />
    IReadOnlyList<string> IGitModule.GetSubmodulesLocalPaths(bool recursive = true)
    {
        List<string> localPaths = [];
        try
        {
            DoGetSubmodulesLocalPaths(this, "", localPaths, recursive);
        }
        catch (GitConfigurationException)
        {
            // swallow any exceptions here, any config exceptions would have been shown to the user already
        }

        return localPaths;

        void DoGetSubmodulesLocalPaths(IGitModule module, string parentPath, List<string> paths, bool recurse)
        {
            List<string> submodulePaths =
                [.. GetSubmodulePaths(module).Select(p => Path.Combine(parentPath, p).ToPosixPath())];

            paths.AddRange(submodulePaths);

            if (recurse)
            {
                foreach (var submodulePath in submodulePaths)
                {
                    DoGetSubmodulesLocalPaths(GetSubmodule(submodulePath), submodulePath, paths, recurse);
                }
            }
        }

        IEnumerable<string> GetSubmodulePaths(IGitModule module)
        {
            var configFile = module.GetSubmodulesConfigFile();
            return configFile.ConfigSections
                .Select(section => section.GetValue("path").Trim()).Where(path => !string.IsNullOrWhiteSpace(path));
        }
    }

    bool IGitModule.EditNotes(ObjectId commitId)
    {
        GitArgumentBuilder arguments = new("notes") { "edit", commitId };
        var editor = GetEffectiveSetting("core.editor").ToLower();
        var createWindow = !editor.Contains("gitextensions") && !editor.Contains("notepad");

        return GitExecutable.RunCommand(arguments, createWindow: createWindow, throwOnErrorExit: false);
    }

    bool IGitModule.InTheMiddleOfConflictedMerge(bool throwOnErrorExit = true)
    {
        GitArgumentBuilder args = new("ls-files") { "-z", "--unmerged" };

        // Do not report errors for commands called in the background
        var result = GitExecutable.Execute(args, throwOnErrorExit: throwOnErrorExit);
        return result.ExitedSuccessfully && !string.IsNullOrEmpty(result.StandardOutput);
    }

    bool IGitModule.HandleConflictSelectSide(string fileName, string side)
    {
        Directory.SetCurrentDirectory(WorkingDir);
        GitArgumentBuilder args = new("checkout-index")
        {
            "-f", $"--stage={GetSide(side)}", "--", fileName.ToPosixPath().QuoteNe()
        };
        var output = GitExecutable.GetOutput(args);

        if (!string.IsNullOrEmpty(output))
        {
            return false;
        }

        args = new GitArgumentBuilder("add") { "--", fileName.ToPosixPath().QuoteNe() };
        output = GitExecutable.GetOutput(args);
        return string.IsNullOrEmpty(output);
    }

    bool IGitModule.HandleConflictsSaveSide(string fileName, string saveAsFileName, string side)
    {
        Directory.SetCurrentDirectory(WorkingDir);

        GitArgumentBuilder args = new("checkout-index")
        {
            $"--stage={GetSide(side)}", "--temp", "--", fileName.ToPosixPath().QuoteNe()
        };
        var output = GitExecutable.GetOutput(args);

        if (string.IsNullOrEmpty(output))
        {
            return false;
        }

        if (!output.StartsWith(".merge_file_"))
        {
            return false;
        }

        // Parse temporary file name from command line result
        var splitResult = output.Split(Delimiters.TabAndLineFeedAndCarriageReturn,
            StringSplitOptions.RemoveEmptyEntries);
        if (splitResult.Length != 2)
        {
            return false;
        }

        var temporaryFileName = splitResult[0].Trim();

        if (!File.Exists(temporaryFileName))
        {
            return false;
        }

        var retValue = false;
        try
        {
            if (File.Exists(saveAsFileName))
            {
                File.Delete(saveAsFileName);
            }

            File.Move(temporaryFileName, saveAsFileName);
            retValue = true;
        }
        catch
        {
        }
        finally
        {
            if (File.Exists(temporaryFileName))
            {
                File.Delete(temporaryFileName);
            }
        }

        return retValue;
    }

    void IGitModule.SaveBlobAs(string saveAs, string blob, CancellationToken cancellationToken = default)
    {
        ThreadHelper.FileAndForget(() => SaveBlobAsAsync(saveAs, blob, cancellationToken));
    }

    async Task IGitModule.SaveBlobAsAsync(string saveAs, string blob, CancellationToken cancellationToken)
    {
        using var blobStream = await GetFileStreamAsync(blob, cancellationToken);
        if (blobStream is null)
        {
            return;
        }

        var blobData = blobStream.ToArray();
        if (GetEffectiveSetting<AutoCrlfType>("core.autocrlf") is AutoCrlfType.True)
        {
            if (!FileHelper.IsBinaryFileName(this, saveAs) && !FileHelper.IsBinaryFileAccordingToContent(blobData))
            {
                blobData = GitConvert.ConvertCrLfToWorktree(blobData);
            }
        }

        using var stream = File.Create(saveAs);
        await stream.WriteAsync(blobData.AsMemory(0, blobData.Length), cancellationToken);
    }

    (string? BaseFile, string? LocalFile, string? RemoteFile) IGitModule.CheckoutConflictedFiles(
        ConflictData unmergedData)
    {
        Directory.SetCurrentDirectory(WorkingDir);

        var processId = Environment.ProcessId;
        var baseFile = CheckoutPart(1, "BASE", unmergedData.Base.Filename);
        var localFile = CheckoutPart(2, "LOCAL", unmergedData.Local.Filename);
        var remoteFile = CheckoutPart(3, "REMOTE", unmergedData.Remote.Filename);

        return (baseFile, localFile, remoteFile);

        string? CheckoutPart(int part, string side, string? unmerged)
        {
            var fileName = GetConflictPartFileName(unmergedData.Filename, side, processId);

            if (unmerged is not null)
            {
                GitArgumentBuilder args = new("checkout-index")
                {
                    "--temp", $"--stage={part}", "--", unmergedData.Filename.QuoteNe()
                };

                // Check out the part to a temporary file
                var output = GitExecutable.GetOutput(args);

                var tempFile = Path.Combine(WorkingDir, output.SubstringUntil('\t'));

                fileName = FindAvailableFileName(Path.Combine(WorkingDir, fileName));

                try
                {
                    File.Move(tempFile, fileName);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }

                return fileName;
            }

            return File.Exists(fileName) ? fileName : null;
        }

        static string FindAvailableFileName(string basePath)
        {
            return GetAvailableFileName(basePath, File.Exists);
        }
    }

    async Task<ConflictData> IGitModule.GetConflictAsync(string? filename)
    {
        return (await GetConflictsAsync(filename)).SingleOrDefault();
    }

    async Task<List<ConflictData>> IGitModule.GetConflictsAsync(string? filename = "")
    {
        filename = filename.ToPosixPath();

        List<ConflictData> list = [];
        GitArgumentBuilder args = new("ls-files") { "-z", "--unmerged", "--", filename.QuoteNe() };

        // ignore non-zero exit code, e.g. in case of missing submodule
        var result = await GitExecutable.ExecuteAsync(args, throwOnErrorExit: false).ConfigureAwait(false);
        var unmerged =
            result.StandardOutput.Split(Delimiters.NullAndLineFeed, StringSplitOptions.RemoveEmptyEntries);

        InlineArray3<ConflictedFileData> item = new();

        string? prevItemName = null;

        foreach (var line in unmerged)
        {
            var findSecondWhitespace = line.IndexOfAny(_spaceAndTabSearchValues);
            var fileStage = findSecondWhitespace >= 0 ? line.AsSpan(findSecondWhitespace).Trim() : "";

            findSecondWhitespace = fileStage.IndexOfAny(_spaceAndTabSearchValues);

            var hash = findSecondWhitespace >= 0 ? fileStage[..findSecondWhitespace].Trim() : "";
            fileStage = findSecondWhitespace >= 0 ? fileStage[findSecondWhitespace..].Trim() : "";

            if (fileStage.Length > 2 && int.TryParse(fileStage[..1], out var stage) && stage is >= 1 and <= 3)
            {
                string itemName = new(fileStage[2..]);
                if (prevItemName != itemName && prevItemName is not null)
                {
                    list.Add(new ConflictData(item[0], item[1], item[2]));
                    item = new InlineArray3<ConflictedFileData>();
                }

                item[stage - 1] = new ConflictedFileData(ObjectId.Parse(hash), itemName);
                prevItemName = itemName;
            }
        }

        if (prevItemName is not null)
        {
            list.Add(new ConflictData(item[0], item[1], item[2]));
        }

        return list;
    }

    async Task<Dictionary<IGitRef, IGitItem?>> IGitModule.GetSubmoduleItemsForEachRefAsync(string? filename,
        bool noLocks = false)
    {
        const int maxSuperRefCount = 100;
        var refsFilter = (AppSettings.ShowSuperprojectBranches ? RefsFilter.Heads : RefsFilter.NoFilter)
                         | (AppSettings.ShowSuperprojectRemoteBranches
                             ? RefsFilter.Remotes
                             : RefsFilter.NoFilter)
                         | (AppSettings.ShowSuperprojectTags ? RefsFilter.Tags : RefsFilter.NoFilter);
        if (refsFilter == RefsFilter.NoFilter)
        {
            return [];
        }

        string? command = Commands.GetRefs(refsFilter, noLocks, GitRefsSortBy.Committerdate,
            GitRefsSortOrder.Descending, maxSuperRefCount);
        var refList = await GitExecutable.GetOutputAsync(command).ConfigureAwait(false);
        var refs = ParseRefs(refList);

        return refs.ToDictionary(r => r, r => GetSubmoduleCommitHash(filename.ToPosixPath(), r.Name));
    }

    int? IGitModule.GetCommitCount(string parent, string child, bool cache = false, bool throwOnErrorExit = true)
    {
        if (parent == child)
        {
            return 0;
        }

        GitArgumentBuilder args = new("rev-list") { parent.Quote(), $"^{child}".Quote(), "--count", "--" };
        var result = GitExecutable.Execute(args, cache: cache ? GitCommandCache : null,
            throwOnErrorExit: throwOnErrorExit);
        var output = result.StandardOutput;

        if (int.TryParse(output, out var commitCount))
        {
            return commitCount;
        }

        return null;
    }

    (int? First, int? Second) IGitModule.GetCommitRangeDiffCount(ObjectId firstId, ObjectId secondId)
    {
        var second = secondId.IsArtificial ? "HEAD" : secondId.ToString();
        var cache = !firstId.IsArtificial && !secondId.IsArtificial;
        return GetRevListLeftRightCount(firstId, second, cache, false);
    }

    string IGitModule.GetCommitCountString(ObjectId fromId, string to)
    {
        var cache = !fromId.IsArtificial && ObjectId.TryParse(to, out var toId) && !toId.IsArtificial;
        (var added, var removed) = GetRevListLeftRightCount(fromId, to, cache);

        if (removed is null || added is null)
        {
            return "";
        }

        if (removed == 0 && added == 0)
        {
            return "=";
        }

        // similar to AheadBehindData
        return $"(+{added}-{removed})";
    }

    void IGitModule.RunGitK()
    {
        if (!OperatingSystem.IsWindows())
        {
            new Executable("gitk", WorkingDir).Start(createWindow: true);
        }
        else
        {
            // locate gitk based on location of git executable
            var cmd = AppSettings.GitCommand
                .Replace("bin\\git.exe", "cmd\\gitk")
                .Replace("bin/git.exe", "cmd/gitk")
                .Replace("git.exe", "gitk")
                .Replace("git.cmd", "gitk");

            new Executable("cmd.exe", WorkingDir).Start($"/c \"\"{cmd}\" --branches --tags --remotes\"");
        }
    }

    void IGitModule.RunGui()
    {
        ArgumentBuilder args;
        if (!OperatingSystem.IsWindows())
        {
            args = new GitArgumentBuilder("gui");
            _ = GitExecutable.Start(args, true);
        }
        else
        {
            args =
            [
                "/c",
                $"\"{AppSettings.GitCommand.QuoteNe()}",
                "gui\""
            ];
            new Executable("cmd.exe", WorkingDir).Start(args);
        }
    }

    void IGitModule.RunMergeTool(string? fileName = "", string? customTool = null)
    {
        ThreadHelper.JoinableTaskFactory.Run(() => RunMergeToolAsync(fileName, customTool));
    }

    bool IGitModule.IsMerge(ObjectId objectId)
    {
        return GetParents(objectId).Count > 1;
    }

    GitRevision IGitModule.GetRevision(ObjectId objectId = default, bool shortFormat = false, bool loadRefs = false)
    {
        var revision = new RevisionReader(this, true).GetRevision(objectId.IsZero ? null! : objectId.ToString(),
            !shortFormat, true, default)!;

        if (loadRefs)
        {
            revision.Refs = GetRefs(RefsFilter.NoFilter)
                .Where(r => r.ObjectId == revision.ObjectId)
                .ToList();
        }

        return revision;
    }

    IReadOnlyList<ObjectId> IGitModule.GetParents(ObjectId objectId)
    {
        if (objectId.IsArtificial)
        {
            // For WorkTree could Index be returned, but for Index HEAD should be available anyway
            throw new InvalidOperationException(nameof(objectId));
        }

        GitArgumentBuilder args = new("rev-parse") { $"{objectId}^@".Quote() };
        return Array.ConvertAll(GitExecutable.Execute(args, cache: GitCommandCache)
                .StandardOutput
                .Split(Delimiters.NullAndLineFeed, StringSplitOptions.RemoveEmptyEntries),
            id => ObjectId.Parse(id));
    }

    IReadOnlyList<GitRevision> IGitModule.GetParentRevisions(ObjectId objectId)
    {
        return GetParents(objectId)
            .Select(parent => GetRevision(parent, shortFormat: true))
            .ToList();
    }

    string? IGitModule.ShowObject(ObjectId objectId, bool returnRaw)
    {
        var gitOutput = GitExecutable
            .GetOutput($"show {objectId}", cache: GitCommandCache, outputEncoding: LosslessEncoding);
        return returnRaw ? gitOutput : ReEncodeShowString(gitOutput);
    }

    void IGitModule.DeleteTag(string tagName)
    {
        GitArgumentBuilder args = new("tag") { "-d", tagName.QuoteNe() };
        GitExecutable.RunCommand(args);
    }

    /// <summary>
    ///     Gets the commit ID of the currently checked out commit.
    ///     If the repo is bare, has no commits, detached head or is corrupt, a zero <see cref="ObjectId" /> is returned.
    /// </summary>
    ObjectId IGitModule.GetCurrentCheckout()
    {
        GitArgumentBuilder args = new("rev-parse") { "HEAD" };
        var result = GitExecutable.Execute(args, throwOnErrorExit: false);

        return result.ExitedSuccessfully && ObjectId.TryParse(result.StandardOutput, 0, out var objectId)
            ? objectId
            : default;
    }

    bool IGitModule.TryResolvePartialCommitId(string objectIdPrefix, out ObjectId objectId)
    {
        // If the prefix is already a full SHA1 then return immediately without invoking a git process.
        if (ObjectId.TryParse(objectIdPrefix, out objectId))
        {
            return true;
        }

        GitArgumentBuilder args = new("rev-parse") { "--verify", "--quiet", $"{objectIdPrefix}^{{commit}}".Quote() };
        var result = GitExecutable.Execute(args, throwOnErrorExit: false);
        var output = result.StandardOutput.AsSpan().Trim();

        if (output.StartsWith(objectIdPrefix) && ObjectId.TryParse(output, out objectId))
        {
            return true;
        }

        objectId = default;
        return false;
    }

    async Task<(char Code, ObjectId CommitId)> IGitModule.GetSuperprojectCurrentCheckoutAsync()
    {
        if (string.IsNullOrEmpty(SuperprojectModule?.WorkingDir))
        {
            return (' ', default);
        }

        GitArgumentBuilder args = new("submodule") { "status", "--cached", SubmodulePath.Quote() };
        var output = await SuperprojectModule.GitExecutable.GetOutputAsync(args).ConfigureAwait(false);
        var lines = output.Split(Delimiters.LineFeed);

        if (lines.Length == 0)
        {
            return (' ', default);
        }

        var submodule = lines[0];

        if (submodule.Length < ObjectId.Sha1CharCount + 3
            || !ObjectId.TryParse(submodule, 1, out var commitId))
        {
            return (' ', default);
        }

        return (submodule[0], commitId);
    }

    bool IGitModule.ExistsMergeCommit(string? startRev, string? endRev)
    {
        if (string.IsNullOrEmpty(startRev) || string.IsNullOrEmpty(endRev))
        {
            return false;
        }

        GitArgumentBuilder args = new("rev-list")
        {
            "--parents", "--no-walk", "--min-parents=2", $"{startRev}..{endRev}".Quote()
        };

        // Could fail if pulling interactively from remote where the specified branch does not exist
        var mergeCommitsOutput = GitExecutable.Execute(args, throwOnErrorExit: false).StandardOutput;
        return !string.IsNullOrWhiteSpace(mergeCommitsOutput);
    }

    ISubmodulesConfigFile IGitModule.GetSubmodulesConfigFile()
    {
        return new ConfigFile($"{WorkingDir}.gitmodules");
    }

    string? IGitModule.GetCurrentSubmoduleLocalPath()
    {
        if (SuperprojectModule is null)
        {
            return null;
        }

        DebugHelpers.Assert(WorkingDir.StartsWith(SuperprojectModule.WorkingDir),
            "Submodule working dir should start with super-project's working dir");

        return Path.GetDirectoryName(
            WorkingDir[SuperprojectModule.WorkingDir.Length..]).ToPosixPath();
    }

    string IGitModule.GetSubmoduleFullPath(string? localPath)
    {
        if (localPath is null)
        {
            DebugHelpers.Fail("No path for submodule - incorrectly parsed status?");
            return "";
        }

        var dir = Path.Combine(WorkingDir, localPath.EnsureTrailingPathSeparator());
        return Path.GetFullPath(dir); // fix slashes
    }

    IGitModule IGitModule.GetSubmodule(string? submoduleName)
    {
        return GetSubmodule(submoduleName);
    }

    IEnumerable<IGitSubmoduleInfo?> IGitModule.GetSubmodulesInfo()
    {
        ISubmodulesConfigFile? configFile;
        try
        {
            configFile = GetSubmodulesConfigFile();
        }
        catch (GitConfigurationException)
        {
            // swallow any exceptions here, any config exceptions would have been shown to the user already
            configFile = null;
        }

        if (configFile is null)
        {
            yield return null;
        }

        GitArgumentBuilder args = new("submodule") { "status" };
        var result = GitExecutable.Execute(args);
        var lines = result.StandardOutput.LazySplit('\n');

        string? lastLine = null;

        foreach (var line in lines)
        {
            if (line == lastLine)
            {
                continue;
            }

            lastLine = line;

            if (TryParseSubmoduleInfo(line, out var info))
            {
                yield return info;
            }
        }

        bool TryParseSubmoduleInfo(string s, [NotNullWhen(true)] out GitSubmoduleInfo? info)
        {
            // Parse an output line from `git submodule status`. Lines have one of the following forms:
            //
            //  6f213088cf4343efe4c570d87659b5f87fc05a4b Externals/Git.hub (heads/master)
            // -ed1dbf01e32ffe6c0b84210183cc2ff6ca448717 Externals/NBug (heads/master)
            // +0daff15503915230aa9436c0fee6a95d5bf3273f Externals/conemu-inside (heads/master)
            // U6868f2b4a39fc894c44711c8903407da596acbf5 GitExtensionsDoc (heads/master)
            //
            // The first character of each line is a prefix with the following meanings:
            //
            // - ' ' if the submodule is initialised and has no changes
            // - '-' if the submodule is not initialized
            // - '+' if the currently checked out submodule commit does not match the SHA-1 found in the index of the containing repository
            // - 'U' if the submodule has merge conflicts
            //
            // Then we have:
            //
            // - the SHA-1 of the currently checked out commit of the submodule
            // - the submodule path
            // - the output of git describe for the SHA-1

            var match = ShaRegex.Match(s);

            if (!match.Success)
            {
                info = default;
                return false;
            }

            var code = match.Groups["code"].ValueSpan[0];
            var localPath = match.Groups["path"].Value;
            var branch = match.Groups["branch"].Value;

            if (!ObjectId.TryParse(match.Groups["sha"].ValueSpan, out var currentCommitId))
            {
                info = default;
                return false;
            }

            Validates.NotNull(configFile);

            var configSection =
                configFile.ConfigSections.FirstOrDefault(section => section.GetValue("path").Trim() == localPath);

            Assumes.True(configSection is not null,
                $"`git submodule status` returned submodule \"{localPath}\" that was not found in .gitmodules");
            Assumes.True(configSection.SubSection is not null, "Config section must have a non-null sub-section");

            var name = configSection.SubSection.Trim();
            var remotePath = configFile.GetPathValue($"submodule.{name}.url").Trim();

            info = new GitSubmoduleInfo(
                name, localPath, remotePath, currentCommitId, branch,
                code != '-',
                code != '+');
            return true;
        }
    }

    /// <summary>
    ///     Reset all changes to HEAD
    /// </summary>
    /// <param name="clean">Clean non ignored files.</param>
    /// <param name="onlyWorkTree">Reset only WorkTree files.</param>
    /// <returns><see langword="true" /> if executed.</returns>
    bool IGitModule.ResetAllChanges(bool clean, bool onlyWorkTree = false)
    {
        if (onlyWorkTree)
        {
            GitArgumentBuilder args = new("checkout") { "--", "." };
            GitExecutable.GetOutput(args);
        }
        else
        {
            // Reset all changes.
            Reset(ResetMode.Hard);
        }

        if (clean)
        {
            Clean(CleanMode.OnlyNonIgnored, directories: true);
        }

        return true;
    }

    void IGitModule.Reset(ResetMode mode, string? file = null)
    {
        GitExecutable.RunCommand(Commands.Reset(mode, null, file));
    }

    /// <summary>
    ///     Reset changes to the selected files.
    /// </summary>
    /// <param name="resetId">Id to reset to, null for HEAD</param>
    /// <param name="selectedItems">Items to reset.</param>
    /// <param name="resetAndDelete">Delete new (and renamed) files.</param>
    /// <param name="fullPathResolver">
    ///     <see cref="IFullPathResolver" />
    /// </param>
    /// <param name="output">Error messages from the reset.</param>
    /// <param name="progressAction">Action when unstaging files (to update a progress bar).</param>
    /// <returns><see langword="true" /> if successfully executed</returns>
    bool IGitModule.ResetChanges(ObjectId resetId, IReadOnlyList<GitItemStatus> selectedItems, bool resetAndDelete,
        IFullPathResolver fullPathResolver, out StringBuilder output,
        Action<BatchProgressEventArgs>? progressAction = null)
    {
        if (resetId.IsArtificial && resetId != ObjectId.IndexId)
        {
            throw new InvalidOperationException(nameof(resetId));
        }

        // unstage first (to reset conflicts)
        if (resetId != ObjectId.IndexId)
        {
            Lazy<List<GitItemStatus>> initialStatus = new(() => [.. GetAllChangedFilesWithSubmodulesStatus()]);
            List<GitItemStatus> filesToUnstage = [];
            foreach (var item in selectedItems)
            {
                if (item.Staged == StagedStatus.Index)
                {
                    filesToUnstage.Add(item);
                }
                else if (initialStatus.Value.FirstOrDefault(i => i.Name == item.Name && i.Staged == StagedStatus.Index)
                         is { } gitStatus)
                {
                    filesToUnstage.Add(gitStatus);
                }
            }

            BatchUnstageFiles(filesToUnstage, progressAction);
        }

        List<string> filesInUse = [];
        List<string> filesToCheckout = [];
        List<string> filesToReset = [];
        List<string> filesCannotCheckout = [];
        HashSet<GitItemStatus> deletedItems = [];
        output = new StringBuilder();
        Lazy<List<GitItemStatus>> postUnstageStatus = new(() => [.. GetAllChangedFilesWithSubmodulesStatus()]);

        foreach (var item in selectedItems)
        {
            if (resetAndDelete && (DeletableItem(item)
                                   || (resetId != ObjectId.IndexId &&
                                       postUnstageStatus.Value.Any(i => DeletableItem(i) && i.Name == item.Name))))
            {
                try
                {
                    var path = fullPathResolver.Resolve(item.Name);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        deletedItems.Add(item);
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                        deletedItems.Add(item);
                    }
                }
                catch (IOException)
                {
                    filesInUse.Add(item.Name);
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            if (resetId == ObjectId.IndexId)
            {
                if (deletedItems.Contains(item) || !postUnstageStatus.Value.Any(i => i.Name == item.Name))
                {
                    // Already removed (for instance new file)
                    continue;
                }

                if (UnmergedIndex(item, postUnstageStatus))
                {
                    filesCannotCheckout.Add(item.Name);
                    continue;
                }

                filesToCheckout.Add(item.IsRenamed ? item.OldName! : item.Name);
            }
            else if (!item.IsNew && !postUnstageStatus.Value.Any(i => i.IsNew && i.Name == item.Name))
            {
                if (!resetId.IsZero || UnmergedNotIndex(item, postUnstageStatus))
                {
                    filesToCheckout.Add(item.IsRenamed ? item.OldName! : item.Name);
                }
                else
                {
                    // reset to head
                    filesToReset.Add(item.IsRenamed ? item.OldName! : item.Name);
                }
            }
        }

        output.Append(CheckoutIndexFiles(filesToReset));
        output.Append(CheckoutFiles(filesToCheckout, resetId, force: false));

        const char bullet = '\u2022';
        const char noBreakSpace = '\u00a0';
        var separator = $"{Environment.NewLine}{bullet}{noBreakSpace}";
        if (filesInUse.Count > 0)
        {
            output.Append(
                $"The following files are currently in use and will not be reset:{separator}{string.Join(separator, filesInUse)}");
        }

        if (filesCannotCheckout.Count > 0)
        {
            output.Append(
                $"The following files are unmerged and will not be reset:{separator}{string.Join(separator, filesCannotCheckout)}");
        }

        return true;

        static bool DeletableItem(GitItemStatus item)
        {
            return item.IsNew || item.IsRenamed;
        }

        // For normal commits: 'git-checkout <objectId> --' must be used for Unmerged but will not work on e.g. SkipWorktree.
        static bool UnmergedNotIndex(GitItemStatus item, Lazy<List<GitItemStatus>> status)
        {
            return item.IsUnmerged && !status.Value.Any(i => !i.IsDeleted && !i.IsNew && i.Name == item.Name);
        }

        // 'git-checkout --' must be used for Index (git-reset will copy HEAD to Index, git-restore from 2.25 could be used).
        // However, Unmerged (Conflict) files cannot be checked out to Index.
        static bool UnmergedIndex(GitItemStatus item, Lazy<List<GitItemStatus>> status)
        {
            return status.Value.Any(i =>
                       (i.IsUnmerged || (i.IsNew && i.Staged == item.Staged)) && i.Name == item.Name) ||
                   !status.Value.Any(i => i.Name == item.Name);
        }
    }

    /// <summary>
    ///     Delete index.lock in the current working folder.
    /// </summary>
    /// <param name="includeSubmodules">
    ///     If <see langword="true" /> all submodules will be scanned for index.lock files and have them delete, if found.
    /// </param>
    /// <exception cref="FileDeleteException">Unable to delete specific index.lock.</exception>
    void IGitModule.UnlockIndex(bool includeSubmodules)
    {
        _indexLockManager.UnlockIndex(includeSubmodules);
    }

    string IGitModule.FormatPatch(string from, string to, string output, int? start = null)
    {
        return GitExecutable.GetOutput(
            new GitArgumentBuilder("format-patch")
            {
                "--find-renames",
                "--find-copies",
                "--break-rewrites",
                { start is not null, $"--start-number {start}" },
                { !string.IsNullOrEmpty(from), $"{from.Quote()}..{to.Quote()}", $"--root {to.Quote()}" },
                $"-o {GetPathForGitExecution(output).Quote()}"
            });
    }

    string IGitModule.CheckoutFiles(IReadOnlyList<string> files, ObjectId objectId, bool force)
    {
        if (files.Count == 0 || (objectId.IsArtificial && objectId != ObjectId.IndexId))
        {
            return "";
        }

        // Reset to index has no objectId string
        string revStr;
        if (objectId == ObjectId.IndexId)
        {
            revStr = "";
        }
        else if (objectId.IsZero)
        {
            var headObjectId = RevParse("HEAD");
            if (headObjectId.IsZero)
            {
                throw new InvalidOperationException("Cannot checkout files because HEAD could not be resolved.");
            }

            revStr = headObjectId.ToString();
        }
        else
        {
            revStr = objectId.ToString();
        }

        // Run batch arguments to work around max command line length on Windows. Fix #6593
        // 3: double quotes + ' '
        // See https://referencesource.microsoft.com/#system/services/monitoring/system/diagnostics/Process.cs,1952
        return GitExecutable.RunBatchCommand(new GitArgumentBuilder("checkout") { { force, "--force" }, revStr, "--" }
                .BuildBatchArgumentsForFiles(files))
            ?.StandardOutput ?? "";
    }

    string IGitModule.GetPuttyKeyFileForRemote(string? remote)
    {
        if (string.IsNullOrEmpty(remote) ||
            string.IsNullOrEmpty(AppSettings.Pageant) ||
            !AppSettings.AutoStartPageant ||
            !GitSshHelpers.IsPlink)
        {
            return "";
        }

        return GetSetting($"remote.{remote}.puttykeyfile");
    }

    ArgumentString IGitModule.FetchCmd(string? remote, string? remoteBranch, string? localBranch,
        bool? fetchTags = false, bool isUnshallow = false, bool pruneRemoteBranches = false,
        bool pruneRemoteBranchesAndTags = false)
    {
        return new GitArgumentBuilder("fetch", gitOptions: GetFetchOptions())
        {
            "--progress",
            {
                !string.IsNullOrEmpty(remote) || !string.IsNullOrEmpty(remoteBranch) ||
                !string.IsNullOrEmpty(localBranch),
                GetFetchArgs(remote, remoteBranch, localBranch, fetchTags, isUnshallow, pruneRemoteBranches,
                    pruneRemoteBranchesAndTags)
            }
        };
    }

    ArgumentString IGitModule.PullCmd(string remote, string? remoteBranch, bool rebase, bool? fetchTags = false,
        bool isUnshallow = false)
    {
        return new GitArgumentBuilder("pull", gitOptions: GetFetchOptions())
        {
            { rebase, "--rebase" }, "--progress", GetFetchArgs(remote, remoteBranch, null, fetchTags, isUnshallow)
        };
    }

    string IGitModule.GetRebaseDir()
    {
        var gitDirectory = _executor.GetGitDirectory();

        var rebaseMergeDir = gitDirectory + "rebase-merge" + Path.DirectorySeparatorChar;
        if (Directory.Exists(rebaseMergeDir))
        {
            return rebaseMergeDir;
        }

        var rebaseApplyDir = gitDirectory + "rebase-apply" + Path.DirectorySeparatorChar;
        if (Directory.Exists(rebaseApplyDir))
        {
            return rebaseApplyDir;
        }

        var rebaseDir = gitDirectory + "rebase" + Path.DirectorySeparatorChar;
        if (Directory.Exists(rebaseDir))
        {
            return rebaseDir;
        }

        return "";
    }

    string IGitModule.ApplyPatch(string dir, ArgumentString arguments)
    {
        using var process = GitExecutable.Start(arguments, false, true, true, SystemEncoding);
        var files = Directory.GetFiles(dir);

        if (files.Length == 0)
        {
            return "";
        }

        foreach (var file in files)
        {
            using FileStream fs = new(file, FileMode.Open);
            fs.CopyTo(process.StandardInput.BaseStream);
        }

        process.StandardInput.Close();
        process.WaitForExit();

        return process.StandardOutput.ReadToEnd().Trim();
    }

    bool IGitModule.InTheMiddleOfBisect()
    {
        return File.Exists(Path.Join(_executor.GetGitDirectory(), "BISECT_START"));
    }

    bool IGitModule.InTheMiddleOfRebase()
    {
        return InTheMiddleOfGitOperation("applying");
    }

    bool IGitModule.InTheMiddleOfPatch()
    {
        return InTheMiddleOfGitOperation("rebasing");
    }

    bool IGitModule.InTheMiddleOfMerge()
    {
        return File.Exists(Path.Join(_executor.GetGitDirectory(), "MERGE_HEAD"));
    }

    bool IGitModule.InTheMiddleOfAction()
    {
        return InTheMiddleOfConflictedMerge() || InTheMiddleOfRebase();
    }

    void IGitModule.RemoveConfigSection(string section, string? subsection)
    {
        GitExecutable.RemoveConfigSection(GitSettingLevel.Local, section, subsection);
        InvalidateGitSettings();
    }

    string IGitModule.RemoveRemote(string remoteName)
    {
        GitArgumentBuilder args = new("remote") { "rm", remoteName.QuoteNe() };
        var output = GitExecutable.GetOutput(args);
        InvalidateGitSettings();
        return output;
    }

    string IGitModule.RenameRemote(string remoteName, string newName)
    {
        GitArgumentBuilder args = new("remote") { "rename", remoteName.QuoteNe(), newName.QuoteNe() };
        var output = GitExecutable.GetOutput(args);
        InvalidateGitSettings();
        return output;
    }

    string IGitModule.AddRemote(string? name, string? path)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Please enter a name.";
        }

        GitArgumentBuilder args = new("remote") { "add", name.Quote(), GetPathForGitExecution(path).QuoteNe() };
        var output = GitExecutable.GetOutput(args);
        InvalidateGitSettings();
        return output;
    }

    IReadOnlyList<string> IGitModule.GetRemoteNames()
    {
        return GitExecutable
            .Execute("remote")
            .StandardOutput
            .LazySplit('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    async Task<IReadOnlyList<Remote>> IGitModule.GetRemotesAsync()
    {
        var result =
            await GitExecutable.ExecuteAsync(new GitArgumentBuilder("remote") { "-v" }, throwOnErrorExit: false);
        ////TODO: Handle non-empty result.StandardError if not result.ExitedSuccessfully
        return result.ExitedSuccessfully
            ? ParseRemotes(result)
            : [];

        IReadOnlyList<Remote> ParseRemotes(ExecutionResult result)
        {
            IEnumerable<string> lines = result.StandardOutput.LazySplit('\n', StringSplitOptions.RemoveEmptyEntries);
            List<Remote> remotes = [];

            // See tests for explanation of the format

            using var enumerator = lines.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var remoteLine = enumerator.Current;
                var remoteMatch = RemoteVerboseLineRegex.Match(remoteLine);
                if (!remoteMatch.Success
                    || (remoteMatch.Groups["direction"].ValueSpan is not "fetch"
                        && remoteMatch.Groups["direction"].ValueSpan is not "push"))
                {
                    // Ignore malformed and unknown entries
                    continue;
                }

                var name = remoteMatch.Groups["name"].Value;
                var remoteUrl = remoteMatch.Groups["url"].Value;
                if (PathUtil.IsLocalFile(remoteUrl))
                {
                    remoteUrl = GetWindowsPath(remoteUrl).ToPosixPath();
                }

                if (remoteMatch.Groups["direction"].ValueSpan is "push")
                {
                    if (remotes.Count <= 0 || name != remotes[^1].Name)
                    {
                        throw new Exception("Unable to update remote pushurl for command output: " + remoteLine);
                    }

                    remotes[^1].PushUrls.Add(remoteUrl);
                    continue;
                }

                if (!enumerator.MoveNext())
                {
                    throw new Exception("Remote URLs should appear in pairs, no pushurl for fetch: " + remoteLine);
                }

                var pushLine = enumerator.Current;
                var pushMatch = RemoteVerboseLineRegex.Match(pushLine);
                if (!pushMatch.Success || pushMatch.Groups["direction"].ValueSpan is not "push")
                {
                    throw new Exception("Unable to parse git remote push URL line: " + pushLine);
                }

                var pushUrl = pushMatch.Groups["url"].Value;
                if (PathUtil.IsLocalFile(pushUrl))
                {
                    pushUrl = GetWindowsPath(pushUrl).ToPosixPath();
                }

                if (name != pushMatch.Groups["name"].Value)
                {
                    throw new Exception("Fetch and push remote names must match: " +
                                        remoteLine + ", " + pushLine);
                }

                remotes.Add(new Remote(name, remoteUrl, pushUrl));
            }

            return remotes;
        }
    }

    FrozenDictionary<string, Color> IGitModule.GetRemoteColors()
    {
        if (_remoteColors is null)
        {
            lock (_lock)
            {
                _remoteColors ??= new ConfigFileRemoteSettingsManager(() => this)
                    .LoadRemotes(loadDisabled: false)
                    .Where(r => !string.IsNullOrEmpty(r.Color) && !string.IsNullOrEmpty(r.Name))
                    .ToFrozenDictionary(r => r.Name!, r => ColorTranslator.FromHtml(r.Color!), StringComparer.Ordinal);
            }
        }

        return _remoteColors;
    }

    void IGitModule.ResetRemoteColors()
    {
        lock (_lock)
        {
            _remoteColors = null;
        }
    }

    IEnumerable<string> IGitModule.GetSettings(string setting)
    {
        return LocalGitConfigSettings.GetValues(setting);
    }

    string IGitModule.GetSetting(string setting)
    {
        return GetEffectiveSetting(setting);
    }

    string IGitModule.GetEffectiveSetting(string setting, string defaultValue = "")
    {
        return EffectiveGitConfigSettings.GetValue(setting) ?? defaultValue;
    }

    T? IGitModule.GetEffectiveSetting<T>(string setting)
    {
        return EffectiveGitConfigSettings.GetValue<T>(setting);
    }

    void IGitModule.UnsetSetting(string setting)
    {
        SetGitSetting(GitSettingLevel.Local, setting, null);
    }

    void IGitModule.SetSetting(string setting, string value, bool append = false)
    {
        SetGitSetting(GitSettingLevel.Local, setting, value, append);
    }

    IReadOnlyList<GitStash> IGitModule.GetStashes(bool noLocks = false)
    {
        var args = GetStashesCmd(noLocks);
        var lines = GitExecutable.GetOutput(args).Split(Delimiters.LineFeed);

        List<GitStash> stashes = new(lines.Length);

        foreach (var line in lines)
        {
            if (GitStash.TryParse(line, out var stash))
            {
                stashes.Add(stash);
            }
        }

        return stashes;
    }

    /// <summary>
    ///     Returns the list of git worktrees for this repository by parsing
    ///     the output of <c>git worktree list --porcelain -z</c>.
    /// </summary>
    IReadOnlyList<GitWorktree> IGitModule.GetWorktrees()
    {
        GitArgumentBuilder args = new("worktree") { "list", "--porcelain", { GitVersion.SupportWorktreeZ, "-z" } };

        var output = GitExecutable.GetOutput(args);
        List<GitWorktree> worktrees = [];

        string? path = null;
        string? sha1 = null;
        string? branch = null;
        var headType = GitWorktreeHeadType.Branch;

        // Note: -z for separator is not configurable before Git 2.36 so paths with \n will fail
        // Just ignore this case, require Git update (no user feedback other than NBug).
        var sep = GitVersion.SupportWorktreeZ ? '\0' : '\n';

        foreach (var field in output.LazySplit(sep))
        {
            // Double \0 (empty field) marks the boundary between worktree records
            if (field.Length == 0)
            {
                if (path is not null)
                {
                    var fullPath = GetWindowsPath(path);
                    var isDeleted = !Directory.Exists(fullPath);
                    worktrees.Add(
                        new GitWorktree(fullPath, headType, sha1, branch, isDeleted) { IsMain = worktrees.Count == 0 });
                }

                path = null;
                sha1 = null;
                branch = null;
                headType = GitWorktreeHeadType.Branch;
                continue;
            }

            var spaceIndex = field.IndexOf(' ');
            var key = spaceIndex >= 0 ? field.AsSpan(0, spaceIndex) : field;
            var value = spaceIndex >= 0 ? field[(spaceIndex + 1)..] : "";
            switch (key)
            {
                case "worktree":
                    path = value;
                    break;
                case "HEAD":
                    sha1 = value;
                    break;
                case "branch":
                    branch = value.RemovePrefix(GitRefName.RefsHeadsPrefix);
                    break;
                case "bare":
                    headType = GitWorktreeHeadType.Bare;
                    break;
                case "detached":
                    headType = GitWorktreeHeadType.Detached;
                    break;
            }
        }

        if (path is not null)
        {
            var fullPath = GetWindowsPath(path);
            var isDeleted = !Directory.Exists(fullPath);
            worktrees.Add(
                new GitWorktree(fullPath, headType, sha1, branch, isDeleted) { IsMain = worktrees.Count == 0 });
        }

        return worktrees;
    }

    async Task<ExecutionResult> IGitModule.GetSingleDifftoolAsync(
        ObjectId firstId,
        ObjectId secondId,
        string? fileName,
        string? oldFileName,
        ArgumentString extraDiffArguments,
        bool cacheResult,
        string extraCacheKey,
        bool isTracked,
        bool useGitColoring,
        CancellationToken cancellationToken)
    {
        // fix refs slashes
        fileName = fileName.ToPosixPath();
        oldFileName = oldFileName.ToPosixPath();
        var firstRevision = firstId.IsZero ? null : firstId.ToString();
        var secondRevision = secondId.IsZero ? null : secondId.ToString();

        string? diffOptions =
            _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked);

        GitArgumentBuilder args = new("difftool", null, "--no-pager")
        {
            "--find-renames",
            "--find-copies",
            "-y",
            extraDiffArguments,
            diffOptions
        };

        var cache = cacheResult
                    && !string.IsNullOrEmpty(secondRevision)
                    && !string.IsNullOrEmpty(firstRevision)
                    && !secondRevision.IsArtificial()
                    && !firstRevision.IsArtificial()
            ? GitCommandCache
            : null;

        var result = await GitExecutable.ExecuteAsync(
            args,
            null,
            LosslessEncoding,
            cache,
            extraCacheKey,
            !useGitColoring,
            false,
            cancellationToken);

        return result;
    }

    async Task<(Patch? Patch, string? ErrorMessage)> IGitModule.GetSingleDiffAsync(
        ObjectId firstId,
        ObjectId secondId,
        string? fileName,
        string? oldFileName,
        string extraDiffArguments,
        Encoding encoding,
        bool cacheResult,
        bool isTracked,
        bool useGitColoring,
        IGitCommandConfiguration commandConfiguration,
        CancellationToken cancellationToken)
    {
        // fix refs slashes
        fileName = fileName.ToPosixPath();
        oldFileName = oldFileName.ToPosixPath();
        var firstRevision = firstId.IsZero ? null : firstId.ToString();
        var secondRevision = secondId.IsZero ? null : secondId.ToString();

        string? diffOptions =
            _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked);

        GitArgumentBuilder args = new("diff", commandConfiguration)
        {
            "--no-ext-diff",
            "--find-renames",
            "--find-copies",
            { useGitColoring, "--color=always" },
            { AppSettings.UseHistogramDiffAlgorithm, "--histogram" },
            extraDiffArguments,
            diffOptions
        };

        var cache = cacheResult &&
                    !string.IsNullOrEmpty(secondRevision) &&
                    !string.IsNullOrEmpty(firstRevision) &&
                    !secondRevision.IsArtificial() &&
                    !firstRevision.IsArtificial()
            ? GitCommandCache
            : null;

        var result = await GitExecutable.ExecuteAsync(
            args,
            cache: cache,
            outputEncoding: LosslessEncoding,
            stripAnsiEscapeCodes: !useGitColoring,
            throwOnErrorExit: false,
            cancellationToken: cancellationToken);
        if (!result.ExitedSuccessfully && (isTracked || result.ExitCode != 1))
        {
            return (Patch: null,
                ErrorMessage:
                $"{result.StandardError}{Environment.NewLine}Git command (exit code: {result.ExitCodeDisplay}): {args}{Environment.NewLine}");
        }

        // git diff --no-index returns 1 when it successfully finds differences.
        var patch = result.StandardOutput;
        IReadOnlyList<Patch> patches =
            PatchProcessor.CreatePatchesFromString(patch, new Lazy<Encoding>(() => encoding)).ToList();

        return (Patch: GetPatch(patches, fileName, oldFileName), ErrorMessage: null);
    }

    async Task<ExecutionResult> IGitModule.GetRangeDiffAsync(
        ObjectId firstId,
        ObjectId secondId,
        ObjectId firstBase,
        ObjectId secondBase,
        string extraDiffArguments,
        string? pathFilter,
        bool useGitColoring,
        IGitCommandConfiguration commandConfiguration,
        CancellationToken cancellationToken)
    {
        // range-diff is not possible for artificial commits, use HEAD
        var first = firstId.IsArtificial ? "HEAD" : firstId.ToString();
        var second = secondId.IsArtificial ? "HEAD" : secondId.ToString();

        if (firstBase.IsArtificial || secondBase.IsArtificial)
        {
            throw new ArgumentException(
                $"Cannot get range diff for artificial commit base of A: {firstBase} or base of B: {secondBase}.");
        }

        // Supported since Git 2.19 (checks when adding the command)
        GitArgumentBuilder args = new("range-diff", commandConfiguration)
        {
            "--find-renames",
            "--find-copies",
            { AppSettings.UseHistogramDiffAlgorithm, "--histogram" },
            { useGitColoring, "--color=always" },
            extraDiffArguments,
            {
                firstBase.IsZero || secondBase.IsZero, $"{first}...{second}",
                $"{firstBase}..{first} {secondBase}..{second}"
            },
            { GitVersion.SupportRangeDiffPath && !string.IsNullOrWhiteSpace(pathFilter), "--" },
            { GitVersion.SupportRangeDiffPath && !string.IsNullOrWhiteSpace(pathFilter), pathFilter }
        };

        var result = await GitExecutable.ExecuteAsync(
            args,
            cache: GitCommandCache,
            outputEncoding: LosslessEncoding,
            stripAnsiEscapeCodes: !useGitColoring,
            throwOnErrorExit: false,
            cancellationToken: cancellationToken);

        return result;
    }

    string IGitModule.GetStatusText(bool untracked)
    {
        return GitExecutable.GetOutput(new GitArgumentBuilder("status") { "-s", { untracked, "-u" } });
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetGrepFilesStatus(ObjectId objectId, string grepString,
        bool applyAppSettings, CancellationToken cancellationToken)
    {
        List<GitItemStatus> result = [];
        var exec = GetGrepFiles(objectId, grepString, applyAppSettings, cancellationToken);
        if (!exec.ExitedSuccessfully)
        {
            // Cannot see difference from error and no matches
            return [];
        }

        foreach (var file in exec.StandardOutput.LazySplit('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var startIndex = file.IndexOf(':') + 1;
            result.Add(new GitItemStatus(file[startIndex..])
            {
                GrepString = grepString,

                // Assume this file is handled by Git, may not be entirely correct for worktree
                IsTracked = true
            });
        }

        return result;
    }

    async Task<ExecutionResult> IGitModule.GetGrepFileAsync(
        ObjectId objectId,
        string fileName,
        ArgumentString extraArgs,
        string grepString,
        bool useGitColoring,
        bool showFunctionName,
        IGitCommandConfiguration commandConfiguration,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        var noCache = objectId.IsArtificial;

        GitArgumentBuilder args = new("grep", commandConfiguration)
        {
            "--line-number",
            { showFunctionName, "--show-function" },
            { !useGitColoring, "--column" },
            { useGitColoring, "--color=always" },
            extraArgs,
            AppSettings.GitGrepUserArguments.Value,
            { AppSettings.GitGrepIgnoreCase.Value, "--ignore-case" },
            { AppSettings.GitGrepMatchWholeWord.Value, "--word-regexp" },
            grepString,
            !objectId.IsArtificial ? objectId.ToString() : objectId == ObjectId.IndexId ? "--cached" : "",
            "--",
            fileName.Quote()
        };

        return await GitExecutable.ExecuteAsync(
            args,
            outputEncoding: encoding,
            cache: noCache ? null : GitCommandCache,
            throwOnErrorExit: false,
            stripAnsiEscapeCodes: !useGitColoring,
            cancellationToken: cancellationToken);
    }

    ExecutionResult IGitModule.GetDiffFiles(string? firstRevision, string? secondRevision, bool noCache = false,
        bool rawParsable = false, CancellationToken cancellationToken = default)
    {
        noCache = noCache || firstRevision.IsArtificial() || secondRevision.IsArtificial();

        // It is possible that a commit does not exist, should not raise an error to the user:
        // * Create a commit in a submodule, do not push
        // * Commit submodule changes
        // * Open the repo in a worktree clone
        // * Checkout the commit
        // * Select the submodule in the diff tab
        // This should not raise a popup to the user, but describe the error message
        return GitExecutable.Execute(
            new GitArgumentBuilder("diff")
            {
                "--no-ext-diff",
                "--find-renames",
                "--find-copies",
                { rawParsable, "--raw", "--name-status" },
                { rawParsable, "-z" },
                _revisionDiffProvider.Get(firstRevision, secondRevision)
            },
            cache: noCache ? null : GitCommandCache,
            throwOnErrorExit: false,
            cancellationToken: cancellationToken);
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetDiffFilesWithSubmodulesStatus(ObjectId firstId,
        ObjectId secondId,
        ObjectId parentToSecond,
        bool excludeSkipWorktreeFiles,
        UntrackedFilesMode untrackedFilesMode,
        CancellationToken cancellationToken)
    {
        var stagedStatus = GetStagedStatus(firstId, secondId, parentToSecond);
        var status = GetDiffFilesWithUntracked(firstId.IsZero ? null : firstId.ToString(),
            secondId.IsZero ? null : secondId.ToString(), stagedStatus, excludeSkipWorktreeFiles, untrackedFilesMode,
            cancellationToken: cancellationToken);
        GetSubmoduleDiffStatus(status, firstId, secondId, cancellationToken);
        return status;
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetDiffFilesWithUntracked(string? firstRevision,
        string? secondRevision,
        StagedStatus stagedStatus,
        bool excludeSkipWorktreeFiles = true,
        UntrackedFilesMode untrackedFilesMode = UntrackedFilesMode.Default,
        bool noCache = false,
        CancellationToken cancellationToken = default)
    {
        if (stagedStatus is StagedStatus.WorkTree or StagedStatus.Index)
        {
            var status = GetAllChangedFilesWithSubmodulesStatus(excludeIgnoredFiles: true,
                excludeAssumeUnchangedFiles: true, excludeSkipWorktreeFiles, untrackedFilesMode, cancellationToken);
            return status.Where(x => x.Staged == stagedStatus || x.IsStatusOnly).ToList();
        }

        var exec = GetDiffFiles(firstRevision, secondRevision, noCache: noCache, rawParsable: true,
            cancellationToken);
        var result = GetDiffChangedFilesFromString(exec.StandardOutput, stagedStatus);
        if (!exec.ExitedSuccessfully)
        {
            result.Add(CreateErrorGitItemStatus(exec.StandardError));
        }

        if (firstRevision == GitRevision.WorkTreeGuid || secondRevision == GitRevision.WorkTreeGuid)
        {
            // For worktree the untracked must be added too
            // Note that this may add a second GitError, this is a separate Git command
            GitItemStatus[] files =
            [
                .. GetAllChangedFilesWithSubmodulesStatus(cancellationToken: cancellationToken).Where(x =>
                    (x.Staged == StagedStatus.WorkTree && x.IsNew) || x.IsStatusOnly)
            ];
            if (firstRevision == GitRevision.WorkTreeGuid)
            {
                // The file is seen as "deleted" in 'to' objectId
                foreach (var item in files)
                {
                    item.IsNew = false;
                    item.IsDeleted = true;
                    result.Add(item);
                }
            }
            else
            {
                result.AddRange(files);
            }
        }

        return result;
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetStashDiffFiles(string stashName)
    {
        List<GitItemStatus> resultCollection =
            [.. GetDiffFilesWithUntracked(stashName + "^", stashName, StagedStatus.None, noCache: true)];

        // add - optionally stashed - untracked files
        GitArgumentBuilder args = new("log") { $"{stashName}^3".Quote(), "--pretty=format:\"%T\"", "--max-count=1" };
        var executionResult = GitExecutable.Execute(args, throwOnErrorExit: false);
        if (executionResult.ExitedSuccessfully &&
            ObjectId.TryParse(executionResult.StandardOutput, out var treeId))
        {
            var files = GetTreeFiles(treeId, full: true)
                .Select(i =>
                {
                    i.IsNew = true;
                    return i;
                });

            resultCollection.AddRange(files);
        }

        return resultCollection;
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetTreeFiles(ObjectId commitId, bool full,
        CancellationToken cancellationToken = default)
    {
        return GetTree(commitId, full, cancellationToken: cancellationToken)
            .Select(file => new GitItemStatus(file.Name)
            {
                // IsTracked is always true, only tracked are reported
                // (all with TreeId are tracked)
                // TreeId for worktree reflects Index, just for reference
                // New/Changed/Deleted are are just set
                IsTracked = true,
                IsNew = false,
                IsChanged = false,
                IsDeleted = false,
                Staged = StagedStatus.Unset,
                TreeId = file.ObjectId,
                IsSubmodule = file.ObjectType == GitObjectType.Commit
            })
            .ToList();
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetAllChangedFiles(bool excludeIgnoredFiles = true,
        bool excludeAssumeUnchangedFiles = true, bool excludeSkipWorktreeFiles = true,
        UntrackedFilesMode untrackedFiles = UntrackedFilesMode.Default, CancellationToken cancellationToken = default)
    {
        var exec = GitExecutable.Execute(Commands.GetAllChangedFiles(excludeIgnoredFiles, untrackedFiles),
            throwOnErrorExit: false, cancellationToken: cancellationToken);
        List<GitItemStatus> result = [.. _getAllChangedFilesOutputParser.Parse(exec.StandardOutput)];
        if (!exec.ExitedSuccessfully)
        {
            // No simple way to pass the error message, create fake file
            result.Add(CreateErrorGitItemStatus(exec.StandardError));
        }

        if (!excludeAssumeUnchangedFiles || !excludeSkipWorktreeFiles)
        {
            GitArgumentBuilder args = new("ls-files") { "-v" };
            var lsOutput = GitExecutable.GetOutput(args);

            if (!excludeAssumeUnchangedFiles)
            {
                result.AddRange(GetAssumeUnchangedFilesFromString(lsOutput));
            }

            if (!excludeSkipWorktreeFiles)
            {
                result.AddRange(GetSkipWorktreeFilesFromString(lsOutput, excludeAssumeUnchangedFiles));
            }
        }

        return result;

        static IReadOnlyList<GitItemStatus> GetAssumeUnchangedFilesFromString(string lsString)
        {
            List<GitItemStatus> result = [];

            foreach (var line in lsString.LazySplit('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var statusCharacter = line[0];
                if (char.IsUpper(statusCharacter))
                {
                    // git-ls-files -v will return lowercase status characters for assume unchanged files
                    continue;
                }

                // Get a default status object, then set AssumeUnchanged
                var fileName = line.SubstringAfter(' ');
                var gitItemStatus = GitItemStatus.GetDefaultStatus(fileName);
                gitItemStatus.IsAssumeUnchanged = true;
                result.Add(gitItemStatus);
            }

            return result;
        }

        static IReadOnlyList<GitItemStatus> GetSkipWorktreeFilesFromString(string lsString,
            bool excludeAssumeUnchangedFiles)
        {
            List<GitItemStatus> result = [];

            foreach (var line in lsString.LazySplit('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // If both AssumeUnchange and SkipWorktree is set, status will be 's',
                // already handled in GetAssumeUnchangedFilesFromString()
                var statusCharacter = line[0];
                const char skippedStatus = 'S';
                const char skippedStatusAssumeUnchanged = 's';
                if (statusCharacter is not skippedStatus && (!excludeAssumeUnchangedFiles ||
                                                             statusCharacter is not skippedStatusAssumeUnchanged))
                {
                    continue;
                }

                // Get a default status object, then set SkipWorktree
                var fileName = line.SubstringAfter(' ');
                var gitItemStatus = GitItemStatus.GetDefaultStatus(fileName);
                gitItemStatus.IsSkipWorktree = true;
                result.Add(gitItemStatus);
            }

            return result;
        }
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetAllChangedFilesWithSubmodulesStatus(
        CancellationToken cancellationToken = default)
    {
        return GetAllChangedFilesWithSubmodulesStatus(excludeIgnoredFiles: true, excludeAssumeUnchangedFiles: true,
            excludeSkipWorktreeFiles: true, untrackedFiles: UntrackedFilesMode.Default, cancellationToken);
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetAllChangedFilesWithSubmodulesStatus(bool excludeIgnoredFiles,
        bool excludeAssumeUnchangedFiles, bool excludeSkipWorktreeFiles,
        UntrackedFilesMode untrackedFiles, CancellationToken cancellationToken)
    {
        var status = GetAllChangedFiles(excludeIgnoredFiles, excludeAssumeUnchangedFiles,
            excludeSkipWorktreeFiles, untrackedFiles, cancellationToken);
        GetSubmoduleCurrentStatus(status);
        return status;
    }

    void IGitModule.GetSubmoduleCurrentStatus(IReadOnlyList<GitItemStatus> status)
    {
        foreach (var item in status)
        {
            if (item.IsSubmodule)
            {
                var localItem = item;
                localItem.SetSubmoduleStatus(ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    return await SubmoduleHelpers.GetSubmoduleCurrentChangesAsync(this, localItem.Name,
                            localItem.OldName, localItem.Staged == StagedStatus.Index)
                        .ConfigureAwait(false);
                }));
            }
        }
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetIndexFilesWithSubmodulesStatus()
    {
        var status = GetIndexFiles();
        GetSubmoduleCurrentStatus(status);
        return status;
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetWorkTreeFiles()
    {
        return GetAllChangedFiles().Where(x => x.Staged == StagedStatus.WorkTree).ToArray();
    }

    IReadOnlyList<GitItemStatus> IGitModule.GitStatus(UntrackedFilesMode untrackedFilesMode,
        IgnoreSubmodulesMode ignoreSubmodulesMode = IgnoreSubmodulesMode.None)
    {
        var args = Commands.GetAllChangedFiles(true, untrackedFilesMode, ignoreSubmodulesMode);
        var exec = GitExecutable.Execute(args, throwOnErrorExit: false);
        List<GitItemStatus> result = [.. _getAllChangedFilesOutputParser.Parse(exec.StandardOutput)];
        if (!exec.ExitedSuccessfully)
        {
            // No simple way to pass the error message, create fake file
            result.Add(CreateErrorGitItemStatus(exec.StandardError));
        }

        return result;
    }

    /// <summary>
    ///     Indicates whether there are any changes to the repository,
    ///     including any untracked files or directories; excluding submodules.
    /// </summary>
    bool IGitModule.IsDirtyDir()
    {
        return GitStatus(UntrackedFilesMode.All, IgnoreSubmodulesMode.All).Count > 0;
    }

    async Task<Patch?> IGitModule.GetCurrentChangesAsync(string? fileName, string? oldFileName, bool staged,
        string extraDiffArguments, Encoding? encoding = null, bool noLocks = false)
    {
        var result = await GitExecutable.ExecuteAsync(
            Commands.GetCurrentChanges(fileName, oldFileName, staged, extraDiffArguments, noLocks),
            outputEncoding: LosslessEncoding, throwOnErrorExit: false).ConfigureAwait(false);
        if (!result.ExitedSuccessfully)
        {
            // occurs if a submodule in a submodule does not exist
            Trace.WriteLine($"Failure to get current changes: {result.AllOutput}");
            return null;
        }

        IReadOnlyList<Patch> patches = PatchProcessor
            .CreatePatchesFromString(result.StandardOutput, new Lazy<Encoding>(() => encoding ?? FilesEncoding))
            .ToList();

        return GetPatch(patches, fileName, oldFileName);
    }

    /// <summary>
    ///     Get the file contents for the HEAD commit
    /// </summary>
    /// <param name="file">The Git status item</param>
    /// <returns>An awaitable task with the requested file contents.</returns>
    async Task<string?> IGitModule.GetFileContentsAsync(GitItemStatus file)
    {
        if (!file.IsTracked || (file.Staged == StagedStatus.Index && file.IsNew))
        {
            // File is not in Git
            return null;
        }

        StringBuilder contents = new();
        var currentContents = await GetFileContentsAsync(file.Name).ConfigureAwaitRunInline();
        if (currentContents is not null)
        {
            contents.Append(currentContents);
        }

        if (file.OldName is not null)
        {
            var oldContents = await GetFileContentsAsync(file.OldName).ConfigureAwaitRunInline();
            if (oldContents is not null)
            {
                contents.Append(oldContents);
            }
        }

        return contents.Length > 0 ? contents.ToString() : null;
    }

    void IGitModule.UnstageFile(string file)
    {
        GitArgumentBuilder args = new("rm") { "--cached", file.ToPosixPath().QuoteNe() };
        GitExecutable.RunCommand(args);
    }

    bool IGitModule.IsDetachedHead()
    {
        return DetachedHeadParser.IsDetachedHead(GetSelectedBranch());
    }

    string IGitModule.GetCurrentRemote()
    {
        var remote = GetSetting(string.Format(SettingKeyString.BranchRemote, GetSelectedBranch()));
        return remote;
    }

    string IGitModule.GetRemoteBranch(string branch)
    {
        var remote = GetSetting(string.Format(SettingKeyString.BranchRemote, branch));
        var merge = GetSetting(string.Format(SettingKeyString.BranchMerge, branch));

        if (string.IsNullOrEmpty(remote) || string.IsNullOrEmpty(merge))
        {
            return "";
        }

        return $"{remote}/{merge.RemovePrefix(GitRefName.RefsHeadsPrefix)}";
    }

    IEnumerable<IGitRef> IGitModule.GetRemoteBranches()
    {
        return GetRefs(RefsFilter.Remotes);
    }

    IReadOnlyList<IGitRef> IGitModule.GetRemoteServerRefs(string remote, bool tags, bool branches,
        out string? errorOutput, CancellationToken cancellationToken)
    {
        var executionResult = !tags && !branches
            ? new ExecutionResult() // TODO is this an error?
            : GitExecutable.Execute(
                new GitArgumentBuilder("ls-remote")
                {
                    { tags, "--tags" }, { branches, "--heads" }, remote.ToPosixPath().QuoteNe()
                },
                throwOnErrorExit: false,
                cancellationToken: cancellationToken);

        // TODO AllOutput is parsed at errors, can this be detected better?
        var output = executionResult.AllOutput;

        if (executionResult.ExitedSuccessfully)
        {
            errorOutput = null;
            return ParseRefs(executionResult.StandardOutput);
        }

        errorOutput = output;
        return [];
    }

    /// <summary>
    ///     Get the Git refs.
    /// </summary>
    /// <param name="getRef">Combined refs to search for.</param>
    /// <returns>All Git refs.</returns>
    IReadOnlyList<IGitRef> IGitModule.GetRefs(RefsFilter getRef)
    {
        // We do not want to lock the repo for background operations.
        // The primary use of 'noLocks' is to run git-status the commit count as a background operation,
        // but to run the same in a foreground for FormCommit.
        //
        // Assume that all GetRefs() are done in the background, which may not be correct in the future.
        const bool noLocks = true;

        var cmd = Commands.GetRefs(getRef, noLocks, AppSettings.RefsSortBy, AppSettings.RefsSortOrder);
        var result = GitExecutable.Execute(cmd, throwOnErrorExit: false);
        return result.ExitedSuccessfully
            ? ParseRefs(result.StandardOutput)
            : [];
    }

    async Task<string[]> IGitModule.GetMergedBranchesAsync(bool includeRemote, bool fullRefname, string? commit,
        CancellationToken cancellationToken)
    {
        var result = await GitExecutable
            .ExecuteAsync(Commands.MergedBranches(includeRemote, fullRefname, commit), throwOnErrorExit: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ////TODO: Handle non-empty result.StandardError
        return result.StandardOutput.Split(Delimiters.LineFeed, StringSplitOptions.RemoveEmptyEntries);
    }

    IEnumerable<string> IGitModule.GetMergedBranches(bool includeRemote = false)
    {
        return GitExecutable
            .GetOutput(Commands.MergedBranches(includeRemote))
            .LazySplit('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    IReadOnlyList<string> IGitModule.GetMergedRemoteBranches()
    {
        const string remoteBranchPrefixForMergedBranches = "remotes/";
        const string refsPrefix = "refs/";

        var remotes = GetRemoteNames();
        var result = GitExecutable.Execute(Commands.MergedBranches(true));
        var lines = result.StandardOutput.LazySplit('\n');

        return lines
            .Select(b => b.Trim())
            .Where(b => b.StartsWith(remoteBranchPrefixForMergedBranches))
            .Select(b => string.Concat(refsPrefix, b))
            .Where(b => !string.IsNullOrEmpty(GitRefName.GetRemoteName(b, remotes)))
            .ToList();
    }

    IReadOnlyList<IGitRef> IGitModule.ParseRefs(string refList)
    {
        var matches = RefRegex.Matches(refList);

        List<IGitRef> gitRefs = [];
        Dictionary<string, GitRef> headByRemote = [];

        foreach (Match match in matches)
        {
            var refName = match.Groups["refname"].Value;
            var objectId = ObjectId.Parse(refList, match.Groups["objectid"]);
            var remoteName = GitRefName.GetRemoteName(refName);
            GitRef head = new(this, objectId, refName, remoteName);

            if (GitRefName.IsRemoteHead(refName))
            {
                headByRemote[remoteName] = head;
            }
            else
            {
                gitRefs.Add(head);
            }
        }

        // do not show default head if remote has a branch on the same commit
        foreach (var gitRef in gitRefs)
        {
            if (headByRemote.TryGetValue(gitRef.Remote, out var defaultHead) &&
                gitRef.ObjectId == defaultHead.ObjectId)
            {
                headByRemote.Remove(gitRef.Remote);
            }
        }

        gitRefs.AddRange(headByRemote.Values);

        return gitRefs;
    }

    IReadOnlyList<string> IGitModule.GetAllBranchesWhichContainGivenCommit(ObjectId objectId, bool getLocal,
        bool getRemote, CancellationToken cancellationToken)
    {
        if (!getLocal && !getRemote)
        {
            return [];
        }

        GitArgumentBuilder args = new("branch")
        {
            { getRemote && getLocal, "-a" }, { getRemote && !getLocal, "-r" }, "--contains", objectId
        };
        var exec =
            GitExecutable.Execute(args, throwOnErrorExit: false, cancellationToken: cancellationToken);
        if (!exec.ExitedSuccessfully)
        {
            // Error occurred, no matches (no error presented to the user)
            return [];
        }

        var result =
            exec.StandardOutput.Split(Delimiters.LineFeedAndCarriageReturn, StringSplitOptions.RemoveEmptyEntries);

        // Remove symlink targets as in "origin/HEAD -> origin/master"
        for (var i = 0; i < result.Length; i++)
        {
            var item = result[i];

            // remove prepended branch state "* ", "+ ", "  "
            if (item.Length >= 2 && item[1] == ' ' && item[0] is ' ' or '*' or '+')
            {
                item = item[2..];
            }

            if (getRemote)
            {
                var idx = item.IndexOf(" ->", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    item = item[..idx];
                }
            }

            result[i] = item;
        }

        return result;
    }

    IReadOnlyList<string> IGitModule.GetAllTagsWhichContainGivenCommit(ObjectId objectId,
        CancellationToken cancellationToken)
    {
        var exec = GitExecutable.Execute($"tag --contains {objectId}", throwOnErrorExit: false,
            cancellationToken: cancellationToken);
        if (!exec.ExitedSuccessfully)
        {
            // Error occurred, no matches (no error presented to the user)
            return [];
        }

        return exec.StandardOutput.Split(Delimiters.GitOutput, StringSplitOptions.RemoveEmptyEntries);
    }

    string? IGitModule.GetTagMessage(string? tag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        tag = tag.Trim();

        var exec = GitExecutable.Execute($"cat-file -p {tag}", throwOnErrorExit: false,
            cancellationToken: cancellationToken);
        if (!exec.ExitedSuccessfully)
        {
            // Error occurred, no message (no error presented to the user)
            return null;
        }

        var output = exec.StandardOutput;
        var messageLines = output.Split(Delimiters.NewLines, StringSplitOptions.None);

        return messageLines.Length <= _standardCatFileTagHeaderLength
            ? null
            : messageLines[_standardCatFileTagHeaderLength..]
                .Join(Environment.NewLine)
                .TrimEnd();
    }

    /// <summary>
    ///     Returns list of file names which would be ignored.
    /// </summary>
    /// <param name="ignorePatterns">Patterns to ignore (.gitignore syntax).</param>
    IReadOnlyList<string> IGitModule.GetIgnoredFiles(IEnumerable<string> ignorePatterns)
    {
        List<string> notEmptyPatterns = [.. ignorePatterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern))];

        if (notEmptyPatterns.Count != 0)
        {
            var excludeParams =
                notEmptyPatterns
                    .Select(pattern => "-x " + pattern.Quote())
                    .Join(" ");

            // filter duplicates out of the result because options -c and -m may return
            // same files at times
            return GitExecutable.GetOutput($"ls-files -z -o -m -c -i {excludeParams}")
                .Split(Delimiters.NullAndLineFeed, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();
        }

        return [];
    }

    IReadOnlyList<string> IGitModule.GetFullTree(string id)
    {
        return GitExecutable.GetOutput(
                $"ls-tree -z -r --name-only {id.Quote()}",
                cache: GitCommandCache)
            .Split(Delimiters.NullAndLineFeed);
    }

    IEnumerable<IObjectGitItem> IGitModule.GetTree(ObjectId commitId, bool full, string fileName = "",
        CancellationToken cancellationToken = default)
    {
        var isArtificial = commitId.IsArtificial;
        if (isArtificial && !full)
        {
            throw new ArgumentOutOfRangeException(nameof(full), "Artificial commit requires 'full'.");
        }

        if (commitId == ObjectId.CombinedDiffId)
        {
            throw new ArgumentOutOfRangeException(nameof(commitId), "Cannot use CombinedDiffId.");
        }

        var args = isArtificial
            ? new GitArgumentBuilder("ls-files")
            {
                // ls-files with same format as ls-tree
                "-z",
                { commitId == ObjectId.IndexId, "--cached", "--no-cached" },
                { GitVersion.SupportLsFilesFormat, @$"--format=""{_gitTreeParser.GitTreeFormat}""", "--stage" },
                "--",
                fileName.QuoteNe()
            }
            : new GitArgumentBuilder("ls-tree")
            {
                // optimized codepath, default is "--format={_gitTreeParser.GitTreeFormat}"
                "-z",
                { full, "-r" },
                commitId.IsZero ? "HEAD" : commitId.ToString(),
                "--",
                fileName.QuoteNe()
            };

        var result = GitExecutable.Execute(args, cache: isArtificial ? null : GitCommandCache,
            cancellationToken: cancellationToken);

        if (isArtificial && !GitVersion.SupportLsFilesFormat)
        {
            return _gitTreeParser.ParseLsFiles(result.StandardOutput);
        }

        return _gitTreeParser.Parse(result.StandardOutput);
    }

    GitBlame IGitModule.Blame(string? fileName, string from, Encoding encoding, string? lines,
        CancellationToken cancellationToken)
    {
        GitArgumentBuilder args = new("blame")
        {
            "--porcelain",
            { AppSettings.DetectCopyInFileOnBlame, "-M" }, // as git-diff --find-renames
            { AppSettings.DetectCopyInAllOnBlame, "-C" }, // as git-diff --find-copies
            { AppSettings.IgnoreWhitespaceOnBlame, "-w" }, // as git-diff --ignore-all-space
            "-l",
            { lines is not null, $"-L {lines}" },
            from.ToPosixPath().Quote(),
            "--",
            fileName.ToPosixPath().Quote()
        };

        var result = GitExecutable.Execute(
            args,
            cache: GitCommandCache,
            outputEncoding: LosslessEncoding,
            cancellationToken: cancellationToken);

        var output = result.StandardOutput;

        try
        {
            return ParseGitBlame(output, encoding);
        }
        catch
        {
            // Catch all parser errors, and ignore them!
            // We should never get here...
            Debug.WriteLine("Error parsing output from command: {0}\n\nPlease report a bug!", args);

            return new GitBlame([]);
        }
    }

    string? IGitModule.GetFileText(ObjectId id, Encoding encoding, bool stripAnsiEscapeCodes)
    {
        GitArgumentBuilder args = new("cat-file") { "blob", id.ToString().QuoteNe() };

        var exec = GitExecutable.Execute(args, outputEncoding: encoding,
            stripAnsiEscapeCodes: stripAnsiEscapeCodes, throwOnErrorExit: false, cache: GitCommandCache);
        if (!exec.ExitedSuccessfully)
        {
            // blob did not exist, this could be a submodule that is removed
            return null;
        }

        return exec.StandardOutput;
    }

    ObjectId IGitModule.GetFileBlobHash(string fileName, ObjectId objectId)
    {
        IObjectGitItem[] items = [.. GetTree(objectId, full: true, fileName)];
        return items.Length == 1 && items[0].ObjectType is GitObjectType.Blob
            ? items[0].ObjectId
            : default;
    }

    Task<MemoryStream?> IGitModule.GetFileStreamAsync(string blob, CancellationToken cancellationToken)
    {
        return GitFileStreamGetter.GetFileStreamAsync(blob, GitCommandRunner, cancellationToken);
    }

    IEnumerable<string?> IGitModule.GetPreviousCommitMessages(int count, string revision, string authorPattern)
    {
        GitArgumentBuilder args = new("log")
        {
            "-z",
            $"-n {count}",
            revision.Quote(),
            @"--pretty=""format:%B""",
            { !string.IsNullOrEmpty(authorPattern), string.Concat("--author=\"", authorPattern, "\"") }
        };

        var result = GitExecutable.Execute(args, outputEncoding: LosslessEncoding, throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            return new[] { string.Empty };
        }

        var messages = result.StandardOutput.Split(Delimiters.Null, StringSplitOptions.RemoveEmptyEntries);
        return messages.Length == 0
            ? new[] { string.Empty }
            : messages.Select(ReEncodeCommitMessage);
    }

    string IGitModule.GetCustomDiffMergeTools(bool isDiff, CancellationToken cancellationToken)
    {
        // Use a global list of custom tools, always use Windows tools (native paths for the app).
        // Note that --gui has no effect here
        GitArgumentBuilder args = new(isDiff ? "difftool" : "mergetool") { "--tool-help" };
        var result = _executor.GitWindowsExecutable.Execute(args, cancellationToken: cancellationToken);
        return result.StandardOutput;
    }

    void IGitModule.OpenWithDifftoolDirDiff(string? firstRevision, string? secondRevision, string? customTool = null)
    {
        OpenWithDifftool(filename: null, firstRevision: firstRevision, secondRevision: secondRevision,
            extraDiffArguments: "--dir-diff", customTool: customTool);
    }

    void IGitModule.OpenWithDifftool(string? filename, string? oldFileName = "",
        string? firstRevision = GitRevision.IndexGuid, string? secondRevision = GitRevision.WorkTreeGuid,
        string? extraDiffArguments = null, bool isTracked = true, string? customTool = null)
    {
        // Use Windows Git if custom tool is selected as the list is native to the application.
        (string.IsNullOrWhiteSpace(customTool) ? GitCommandRunner : _executor.GitWindowsCommandRunner)
            .RunDetached(new GitArgumentBuilder("difftool")
            {
                { string.IsNullOrWhiteSpace(customTool), "--gui", $"--tool={customTool}" },
                "--find-renames",
                "--find-copies",
                "--no-prompt",
                extraDiffArguments,
                _revisionDiffProvider.Get(firstRevision, secondRevision, filename, oldFileName, isTracked)
            });
    }

    /// <summary>
    ///     Compare two Git commitish; blob or rev:path.
    ///     Does nothing if either input is null or empty.
    /// </summary>
    /// <param name="firstGitCommit">commitish.</param>
    /// <param name="secondGitCommit">commitish.</param>
    void IGitModule.OpenFilesWithDifftool(string? firstGitCommit, string? secondGitCommit, string? customTool = null)
    {
        if (string.IsNullOrWhiteSpace(firstGitCommit) || string.IsNullOrWhiteSpace(secondGitCommit))
        {
            return;
        }

        // Use Windows Git if custom tool is selected as the list is native to the application.
        (string.IsNullOrWhiteSpace(customTool) ? GitCommandRunner : _executor.GitWindowsCommandRunner)
            .RunDetached(new GitArgumentBuilder("difftool")
            {
                { string.IsNullOrWhiteSpace(customTool), "--gui", $"--tool={customTool}" },
                "--find-renames",
                "--find-copies",
                "--no-prompt",
                firstGitCommit.QuoteNe(),
                secondGitCommit.QuoteNe()
            });
    }

    ObjectId IGitModule.RevParse(string? revisionExpression)
    {
        if (string.IsNullOrWhiteSpace(revisionExpression) || revisionExpression.Length > 260)
        {
            return default;
        }

        if (ObjectId.TryParse(revisionExpression, out var objectId))
        {
            return objectId;
        }

        GitArgumentBuilder args = new("rev-parse") { "--quiet", "--verify", $"{revisionExpression}~0".Quote() };
        var result = GitExecutable.Execute(args, throwOnErrorExit: false);

        return result.ExitedSuccessfully && ObjectId.TryParse(result.StandardOutput, 0, out objectId)
            ? objectId
            : default;
    }

    ObjectId IGitModule.GetMergeBase(ObjectId a, ObjectId b)
    {
        if (a == b)
        {
            return a;
        }

        GitArgumentBuilder args = new("merge-base") { a, b };
        var result = GitExecutable.Execute(args, cache: GitCommandCache, throwOnErrorExit: false);
        var output = result.StandardOutput;

        return ObjectId.TryParse(output, 0, out var objectId)
            ? objectId
            : default;
    }

    bool IGitModule.CheckBranchFormat(string branchName)
    {
        ArgumentNullException.ThrowIfNull(branchName);

        if (string.IsNullOrWhiteSpace(branchName))
        {
            return false;
        }

        GitArgumentBuilder args = new("check-ref-format") { "--branch", branchName.QuoteNe() };
        return GitExecutable.Execute(args, throwOnErrorExit: false).ExitedSuccessfully;
    }

    string IGitModule.FormatBranchName(string branchName)
    {
        ArgumentNullException.ThrowIfNull(branchName);

        var fullBranchName = GitRefName.GetFullBranchName(branchName);

        if (RevParse(fullBranchName).IsZero)
        {
            return branchName;
        }

        return fullBranchName;
    }

    bool IGitModule.IsRunningGitProcess()
    {
        if (_indexLockManager.IsIndexLocked())
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            var processes = Process.GetProcessesByName("git");
            var running = processes.Length > 0;
            foreach (var p in processes)
            {
                p.Dispose();
            }

            return running;
        }

        // Get processes by "ps" command.
        var cmd = Path.Join(AppSettings.LinuxToolsDir, "ps");
        var lines = new Executable(cmd).GetOutput("x").Split(Delimiters.LineFeed);

        if (lines.Length <= 2)
        {
            return false;
        }

        var headers = lines[0].LazySplit(' ', StringSplitOptions.RemoveEmptyEntries);
        var commandIndex = headers.IndexOf(header => header == "COMMAND");
        for (var i = 1; i < lines.Length; i++)
        {
            var columns = lines[i].Split(Delimiters.Space, StringSplitOptions.RemoveEmptyEntries);
            if (commandIndex < columns.Length)
            {
                var command = columns[commandIndex];
                if (command.EndsWith("/git"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [return: NotNullIfNotNull(nameof(s))]
    string? IGitModule.ReEncodeStringFromLossless(string? s)
    {
        return ReEncodeStringFromLossless(s, LogOutputEncoding);
    }

    [return: NotNullIfNotNull(nameof(s))]
    string? IGitModule.ReEncodeCommitMessage(string? s)
    {
        return ReEncodeStringFromLossless(s, LogOutputEncoding)?.Trim();
    }

    string? IGitModule.GetLocalTrackingBranchName(string remoteName, string branch)
    {
        var branchName = remoteName.Length > 0 ? branch[(remoteName.Length + 1)..] : branch;
        var localGitConfigSettings = LocalGitConfigSettings;
        foreach ((var setting, var value) in localGitConfigSettings.GetAllValues())
        {
            (var section, var subsection, var name) = IGitConfigSettingsGetter.SplitSetting(setting);
            if (section == "branch" && name == "remote" && value == remoteName)
            {
                var remoteBranch = localGitConfigSettings
                    .GetValue(string.Format(SettingKeyString.BranchMerge, subsection))
                    ?.Replace("refs/heads/", string.Empty);
                if (remoteBranch == branchName)
                {
                    return subsection;
                }
            }
        }

        return branchName;
    }

    IReadOnlyList<GitItemStatus> IGitModule.GetCombinedDiffFileList(ObjectId mergeCommitObjectId)
    {
        GitArgumentBuilder args = new("diff-tree")
        {
            "--name-only",
            "-z",
            "--cc",
            "--no-commit-id",
            mergeCommitObjectId
        };

        var fileList = GitExecutable.GetOutput(args);

        if (string.IsNullOrWhiteSpace(fileList))
        {
            return [];
        }

        var files = fileList.LazySplit('\0', StringSplitOptions.RemoveEmptyEntries);

        return files.Select(file => new GitItemStatus(file)
        {
            IsChanged = true,
            IsTracked = true,
            IsDeleted = false,
            IsNew = false,
            Staged = StagedStatus.None
        }).ToList();
    }

    bool IGitModule.GetCombinedDiffContent(
        ObjectId objectIdOfMergeCommit,
        string filePath,
        string extraArgs,
        Encoding encoding,
        out string diffOfConflict,
        bool useGitColoring,
        IGitCommandConfiguration commandConfiguration,
        CancellationToken cancellationToken)
    {
        GitArgumentBuilder args = new("diff-tree", commandConfiguration)
        {
            { AppSettings.OmitUninterestingDiff, "--cc", "-c -p" },
            "--no-commit-id",
            { AppSettings.UseHistogramDiffAlgorithm, "--histogram" },
            { useGitColoring, "--color=always" },
            extraArgs,
            objectIdOfMergeCommit,
            "--",
            filePath.ToPosixPath().Quote()
        };

        var result = GitExecutable.Execute(
            args,
            cache: GitCommandCache,
            outputEncoding: LosslessEncoding,
            stripAnsiEscapeCodes: !useGitColoring,
            throwOnErrorExit: false,
            cancellationToken: cancellationToken);

        if (!result.ExitedSuccessfully)
        {
            diffOfConflict = result.AllOutput;
            return false;
        }

        if (string.IsNullOrEmpty(result.StandardOutput))
        {
            diffOfConflict = "";
            return true;
        }

        List<Patch> patches =
            [.. PatchProcessor.CreatePatchesFromString(result.StandardOutput, new Lazy<Encoding>(() => encoding))];

        var patch = GetPatch(patches, filePath, filePath);
        if (patch is null)
        {
            diffOfConflict = "";
            return false;
        }

        diffOfConflict = patch.Text ?? "";
        return true;
    }

    bool IGitModule.StopTrackingFile(string filename)
    {
        GitArgumentBuilder args = new("rm") { "--cached", filename.ToPosixPath().Quote() };
        return GitExecutable.Execute(args, throwOnErrorExit: false).ExitedSuccessfully;
    }

    /// <summary>
    ///     Finds the most recent tag that is reachable from a commit
    /// </summary>
    /// <param name="commitId">The commit where to start searching</param>
    /// <returns>Tag name if it exists, otherwise null</returns>
    string? IGitModule.GetDescribe(ObjectId commitId, CancellationToken cancellationToken)
    {
        GitArgumentBuilder args = new("describe") { "--tags", "--first-parent", "--abbrev=40", commitId };

        var exec =
            GitExecutable.Execute(args, throwOnErrorExit: false, cancellationToken: cancellationToken);
        return exec.ExitedSuccessfully
            ? exec.StandardOutput.TrimEnd()
            : null;
    }

    (int TotalCount, Dictionary<string, int> CountByName) IGitModule.GetCommitsByContributor(DateTime? since = null,
        DateTime? until = null)
    {
        Dictionary<string, int> countByName = [];
        var totalCommits = 0;

        GitArgumentBuilder args = new("shortlog")
        {
            "--all",
            "-s",
            "-n",
            "--no-merges",
            GetDateParameter("--since", since),
            GetDateParameter("--until", until)
        };

        var result = GitExecutable.Execute(args);
        var lines = result.StandardOutput.LazySplit('\n');

        foreach (var line in lines)
        {
            var match = ShortlogRegex.Match(line);

            if (!match.Success)
            {
                continue;
            }

            var count = int.Parse(match.Groups["count"].ValueSpan);
            var name = match.Groups["name"].Value;

            totalCommits += count;

            if (!countByName.TryGetValue(name, out var oldCount))
            {
                countByName[name] = count;
            }
            else
            {
                // Sometimes this happen because of wrong encoding
                countByName[name] = oldCount + count;
            }
        }

        return (totalCommits, countByName);

        static string GetDateParameter(string param, DateTime? date)
        {
            return date is not null
                ? $"{param}=\"{date:yyyy-MM-dd hh:mm:ss}\""
                : "";
        }
    }

    /// <summary>
    ///     Indicates whether the specified directory contains a git repository.
    /// </summary>
    public static bool IsValidGitWorkingDir(string? dir)
    {
        if (string.IsNullOrEmpty(dir))
        {
            return false;
        }

        var dirPath = dir.EnsureTrailingPathSeparator();
        var path = dirPath + ".git";

        if (Directory.Exists(path) || File.Exists(path))
        {
            return true;
        }

        return Directory.Exists(dirPath + "info") &&
               Directory.Exists(dirPath + "objects") &&
               Directory.Exists(dirPath + "refs");
    }

    public static bool IsBareRepository(string repositoryPath)
    {
        return repositoryPath == new GitDirectoryResolver().Resolve(repositoryPath);
    }

    /// <summary>
    ///     Searches from <paramref name="startDir" /> and up through the directory
    ///     hierarchy for a valid git working directory. If found, the path is returned,
    ///     otherwise <c>null</c>.
    /// </summary>
    public static string? TryFindGitWorkingDir(string? startDir)
    {
        var dir = startDir?.Trim();

        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (IsValidGitWorkingDir(dir))
            {
                return dir.EnsureTrailingPathSeparator();
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    public ExecutionResult Clean(CleanMode mode, bool dryRun = false, bool directories = false, string? paths = null)
    {
        return GitExecutable.Execute(
            Commands.Clean(mode, dryRun, directories, paths));
    }

    private static string GetSide(string side)
    {
        if (side.Equals("REMOTE", StringComparison.CurrentCultureIgnoreCase))
        {
            side = "3";
        }

        if (side.Equals("LOCAL", StringComparison.CurrentCultureIgnoreCase))
        {
            side = "2";
        }

        if (side.Equals("BASE", StringComparison.CurrentCultureIgnoreCase))
        {
            side = "1";
        }

        return side;
    }

    /// <summary>
    ///     Gets the name of the temporary file holding one side of a conflicted file.
    /// </summary>
    /// <remarks>
    ///     Uses the same scheme as git-mergetool ("folder/file_LOCAL_1234.ext"):
    ///     the extension is kept at the end so that merge tools recognize the file type.
    /// </remarks>
    private static string GetConflictPartFileName(string fileName, string side, int processId)
    {
        var extension = Path.GetExtension(fileName);
        return $"{fileName[..^extension.Length]}_{side}_{processId}{extension}";
    }

    /// <summary>
    ///     If necessary, inserts an index before the extension of the path until the file does not exist.
    /// </summary>
    private static string GetAvailableFileName(string basePath, Func<string, bool> fileExists)
    {
        var extension = Path.GetExtension(basePath);
        var stem = basePath[..^extension.Length];
        var test = basePath;

        for (var index = 1; fileExists(test) && index < 50; index++)
        {
            test = $"{stem}_{index}{extension}";
        }

        return test;
    }

    private IGitItem? GetSubmoduleCommitHash(string? filename, string refName)
    {
        GitArgumentBuilder args = new("ls-tree")
        {
            // optimized codepath, default is "--format={_gitTreeParser.GitTreeFormat}"
            refName.Quote(), "--", filename.QuoteNe()
        };
        var output = GitExecutable.GetOutput(args);

        return _gitTreeParser.ParseSingle(output);
    }

    private (int? left, int? right) GetRevListLeftRightCount(ObjectId firstId, string secondRef, bool cache,
        bool throwOnErrorExit = true)
    {
        var firstRef = firstId.IsArtificial ? "HEAD" : firstId.ToString();
        if (firstRef == secondRef)
        {
            return (0, 0);
        }

        GitArgumentBuilder args = new("rev-list") { $"{firstRef}...{secondRef}".Quote(), "--count", "--left-right" };

        var result = GitExecutable.Execute(args, cache: cache ? GitCommandCache : null,
            throwOnErrorExit: throwOnErrorExit);
        if (!result.ExitedSuccessfully)
        {
            // this is likely one of the commits in a submodule no longer existing
            return (null, null);
        }

        var counts = result.StandardOutput.Split(Delimiters.Tab);
        if (counts.Length == 2 && int.TryParse(counts[0], out var first) && int.TryParse(counts[1], out var second))
        {
            return (first, second);
        }

        return (null, null);
    }

    // TODO: Replace RunMergeTool with RunMergeToolAsync in all usages.
    private async Task RunMergeToolAsync(string? fileName, string? customTool)
    {
        await TaskScheduler.Default;

        // Use Windows Git if custom tool is selected as the list is native to the application.
        var isWindowsGit = !string.IsNullOrWhiteSpace(customTool);
        var gui = (isWindowsGit ? GitVersion.Current : GitVersion).SupportGuiMergeTool ? "--gui" : string.Empty;
        GitArgumentBuilder args = new("mergetool")
        {
            { string.IsNullOrWhiteSpace(customTool), gui, $"--tool={customTool}" },
            "--",
            fileName.ToPosixPath().QuoteNe()
        };

        using var process =
            (isWindowsGit ? _executor.GitWindowsExecutable : GitExecutable).Start(args, true, throwOnErrorExit: false);
        await process.WaitForExitAsync();
    }

    public string Init(bool bare, bool shared)
    {
        GitArgumentBuilder args = new("init") { { bare, "--bare" }, { shared, "--shared=all" } };

        // Note that the output contains the path to the repo for the Git executable.
        // This means that the WSL path is presented in WSL repos, not the Windows path (native to the app).
        var output = GitExecutable.GetOutput(args);

        WorkingDirGitDir = _executor.GetGitDirectory();
        return output;
    }

    /// <summary>
    ///     <para>
    ///         Gets the current branch name. This is the branch whose ref will be updated to the next commit. When the
    ///         repository is
    ///         newly-initialized, this is a nonexistent ref whose name is set to the effective value of init.defaultbranch at
    ///         the time
    ///         of init (unless overridden with --initial-branch).
    ///     </para>
    ///     <para>
    ///         If there is no current branch name (e.g. detached HEAD), this method returns an empty
    ///         <see cref="string" />.
    ///     </para>
    /// </summary>
    public string GetCurrentBranchName()
    {
        GitArgumentBuilder args = new("branch") { "--show-current" };
        var result = GitExecutable.Execute(args, throwOnErrorExit: false);

        result.ThrowIfErrorExit("Error retrieving current branch name");

        return result.StandardOutput.TrimEnd();
    }

    public IGitModule GetSubmodule(string? localPath)
    {
        return new GitModule(_executorProvider, GetSubmoduleFullPath(localPath));
    }

    public string GetSubmoduleSummary(string submodule)
    {
        GitArgumentBuilder args = new("submodule") { "summary", submodule };
        return GitExecutable.GetOutput(args);
    }

    /// <summary>
    ///     Executes <c>checkout-index</c> ("reset") command which copies files from the index overwriting the working tree.
    /// </summary>
    /// <param name="files">List with (relative path) filenames.</param>
    /// <returns>stdout from Git.</returns>
    public string CheckoutIndexFiles(IReadOnlyList<string> files)
    {
        if (files?.Count is not > 0)
        {
            return string.Empty;
        }

        return GitExecutable.GetBatchOutput(new GitArgumentBuilder("checkout-index") { "--index", "--force", "--" }
            .BuildBatchArgumentsForFiles(files));
    }

    public string RemoveFiles(IReadOnlyList<string> files, bool force)
    {
        if (files.Count == 0)
        {
            return "";
        }

        return GitExecutable.GetBatchOutput(
            new GitArgumentBuilder("rm") { { force, "--force" }, "--" }
                .BuildBatchArgumentsForFiles(files));
    }

    private ArgumentString GetFetchArgs(string? remote, string? remoteBranch, string? localBranch, bool? fetchTags,
        bool isUnshallow, bool pruneRemoteBranches = false, bool pruneRemoteBranchesAndTags = false)
    {
        // Remove spaces...
        remoteBranch = remoteBranch?.Replace(" ", "");
        localBranch = localBranch?.Replace(" ", "");

        var branchArguments = "";

        if (!string.IsNullOrEmpty(remoteBranch))
        {
            if (remoteBranch.StartsWith('+'))
            {
                remoteBranch = remoteBranch[1..];
            }

            branchArguments = "+" + FormatBranchName(remoteBranch);

            if (!string.IsNullOrEmpty(localBranch))
            {
                branchArguments += ":" + GitRefName.GetFullBranchName(localBranch);
            }
        }

        // TODO return ArgumentBuilder and add special case ArgumentBuilder.Add(ArgumentBuilder childBuilder)
        return new ArgumentBuilder
        {
            remote.ToPosixPath()?.Trim().Quote(),
            branchArguments,
            { fetchTags == true, "--tags" },
            { fetchTags == false, "--no-tags" },
            { isUnshallow, "--unshallow" },
            { pruneRemoteBranches || pruneRemoteBranchesAndTags, "--prune --force" },
            { pruneRemoteBranchesAndTags, "--prune-tags" }
        };
    }

    private ArgumentString GetFetchOptions()
    {
        return new ArgumentBuilder
        {
            { string.IsNullOrWhiteSpace(GetEffectiveSetting("fetch.parallel")), "-c fetch.parallel=0" },
            { string.IsNullOrWhiteSpace(GetEffectiveSetting("submodule.fetchjobs")), "-c submodule.fetchjobs=0" }
        };
    }

    private bool InTheMiddleOfGitOperation(string filename)
    {
        var rebaseDir = GetRebaseDir();
        return !File.Exists(rebaseDir + filename) && Directory.Exists(rebaseDir);
    }

    internal GitArgumentBuilder GetStashesCmd(bool noLocks)
    {
        return new GitArgumentBuilder("stash", gitOptions: noLocks ? (ArgumentString)"--no-optional-locks" : default)
        {
            "list"
        };
    }

    private static Patch? GetPatch(IReadOnlyList<Patch> patches, string? fileName, string? oldFileName)
    {
        foreach (var patch in patches)
        {
            if (fileName == patch.FileNameB && (fileName == patch.FileNameA || oldFileName == patch.FileNameA))
            {
                return patch;
            }
        }

        return patches.Count != 0
            ? patches[^1]
            : null;
    }

    private ExecutionResult GetGrepFiles(ObjectId objectId, string grepString, bool applyAppSettings,
        CancellationToken cancellationToken = default)
    {
        var noCache = objectId.IsArtificial;

        return GitExecutable.Execute(
            new GitArgumentBuilder("grep")
            {
                "--files-with-matches",
                "-z",
                { applyAppSettings, AppSettings.GitGrepUserArguments.Value },
                { applyAppSettings && AppSettings.GitGrepIgnoreCase.Value, "--ignore-case" },
                { applyAppSettings && AppSettings.GitGrepMatchWholeWord.Value, "--word-regexp" },
                grepString,
                !objectId.IsArtificial ? objectId.ToString() : objectId == ObjectId.IndexId ? "--cached" : "",
                "--"
            },
            cache: noCache ? null : GitCommandCache,
            throwOnErrorExit: false,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     If possible, find if files in a diff are index or worktree.
    /// </summary>
    /// <param name="firstId">from objectId string.</param>
    /// <param name="secondId">to objectId.</param>
    /// <param name="parentToSecond">The parent for the second objectId.</param>
    /// <remarks>Git revisions are required to determine if <see cref="StagedStatus" /> allows stage/unstage.</remarks>
    public static StagedStatus GetStagedStatus(ObjectId firstId, ObjectId secondId, ObjectId parentToSecond)
    {
        StagedStatus staged;
        if (firstId == ObjectId.IndexId && secondId == ObjectId.WorkTreeId)
        {
            staged = StagedStatus.WorkTree;
        }
        else if (firstId == parentToSecond && secondId == ObjectId.IndexId)
        {
            staged = StagedStatus.Index;
        }
        else if (!firstId.IsZeroOrArtificial && !secondId.IsZeroOrArtificial)
        {
            // This cannot be a worktree/index file
            staged = StagedStatus.None;
        }
        else
        {
            staged = StagedStatus.Unknown;
        }

        return staged;
    }

    private void GetSubmoduleDiffStatus(IReadOnlyList<GitItemStatus> status, ObjectId firstId, ObjectId secondId,
        CancellationToken cancellationToken)
    {
        foreach (var item in status.Where(i => i.IsSubmodule))
        {
            item.SetSubmoduleStatus(
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default.SwitchTo(true);
                    return await SubmoduleHelpers.GetSubmoduleDiffChangesAsync(this, item.Name, item.OldName, firstId,
                            secondId, cancellationToken)
                        .ConfigureAwait(false);
                }));
        }
    }

    public IReadOnlyList<GitItemStatus> GetIndexFiles()
    {
        GitArgumentBuilder args = new("diff")
        {
            "--no-ext-diff",
            "--find-renames",
            "--find-copies",
            "-z",
            "--raw",
            "--cached"
        };
        var exec = GitExecutable.Execute(args, throwOnErrorExit: false);
        if (exec.ExitedSuccessfully)
        {
            return GetDiffChangedFilesFromString(exec.StandardOutput, StagedStatus.Index);
        }

        // This command is a little more expensive because it will return both staged and unstaged files
        var command = Commands.GetAllChangedFiles(true, UntrackedFilesMode.No);
        exec = GitExecutable.Execute(command, throwOnErrorExit: false);
        List<GitItemStatus> res =
        [
            .. _getAllChangedFilesOutputParser.Parse(exec.StandardOutput)
                .Where(item => item.Staged == StagedStatus.Index || item.IsStatusOnly)
        ];
        if (!exec.ExitedSuccessfully)
        {
            // No simple way to pass the error message, create fake file
            res.Add(CreateErrorGitItemStatus(exec.StandardError));
        }

        return res;
    }

    /// <summary>
    ///     Parse the output from git-diff --raw.
    /// </summary>
    /// <param name="statusString">output from the git command.</param>
    /// <param name="staged">required to determine if <see cref="StagedStatus" /> allows stage/unstage.</param>
    /// <returns>list with the parsed GitItemStatus.</returns>
    /// <seealso href="https://git-scm.com/docs/git-diff" />
    private List<GitItemStatus> GetDiffChangedFilesFromString(string statusString, StagedStatus staged)
    {
        return _getAllChangedFilesOutputParser.GetDiffChangedFilesFromString(statusString, staged);
    }

    private async Task<string?> GetFileContentsAsync(string? path)
    {
        GitArgumentBuilder args = new("show") { $"HEAD:{path.ToPosixPath().Quote()}" };
        var result =
            await GitExecutable.ExecuteAsync(args, throwOnErrorExit: false).ConfigureAwaitRunInline();

        return result.ExitedSuccessfully
            ? result.StandardOutput
            : null;
    }

    public void UnstageFileToRemove(string file)
    {
        var args = Commands.Reset(ResetMode.ResetIndex, "HEAD", file);
        GitExecutable.RunCommand(args);
    }

    internal GitBlame ParseGitBlame(string output, Encoding encoding)
    {
        // "git blame --porcelain" produces a stable, machine-readable format which we parse here.
        // Each line in the file is associated with a commit, and one commit may originate many lines
        // in the file, both in contiguous chunks and disparate ranges.
        //
        // The command output walks the file from top to bottom, line by line.
        // Each time a new commit is encountered, its 'header' is presented.
        // Later chunks from the same commit will refer back to this header by
        // the object ID (SHA-1).
        //
        // After this header, the lines from the file are presented, indented.
        //
        // Here is an excerpt from the middle of GE's README.md file:

        // e3268019c66da7534414e9562ececdee5d455b1b 6 6 1
        // author RussKie
        // author-mail <russkie@gmail.com>
        // author-time 1510573702
        // author-tz +1100
        // committer RussKie
        // committer-mail <russkie@gmail.com>
        // committer-time 1510573702
        // committer-tz +1100
        // summary chore: Reformat readme
        // previous cd032fd48a6693b080f38d87388bc0f601db4e02 README.md
        // filename README.md
        //         ## Overview
        // 957ff3ce9193fec3bd2578378e71676841804935 4 7 1
        //
        // e3268019c66da7534414e9562ececdee5d455b1b 8 8 1
        //         Git Extensions is a standalone UI tool for managing git repositories.<br />

        // Where one commit is responsible for multiple contiguous lines, they appear as follows:

        // e3268019c66da7534414e9562ececdee5d455b1b 11 13 9
        //         <table>
        // e3268019c66da7534414e9562ececdee5d455b1b 12 14
        //           <tr>
        // e3268019c66da7534414e9562ececdee5d455b1b 13 15
        //             <th>&nbsp;</th>
        // e3268019c66da7534414e9562ececdee5d455b1b 14 16
        //             <th>Windows</th>
        // e3268019c66da7534414e9562ececdee5d455b1b 15 17
        //             <th>Linux/Mac</th>
        // e3268019c66da7534414e9562ececdee5d455b1b 16 18
        //           </tr>
        // e3268019c66da7534414e9562ececdee5d455b1b 17 19
        //           <tr>
        // e3268019c66da7534414e9562ececdee5d455b1b 18 20
        //             <td>Requirement</td>
        // e3268019c66da7534414e9562ececdee5d455b1b 19 21

        // On the first line here, there are three trailing numbers. The third is the number of
        // lines from the one commit.

        // There are three 'chunks' here. The first includes a commit header.
        // The second and third refer back to a header that would have been presented earlier.
        // We see the content of the chunks, where the first is a markdown header, the second
        // is a blank line, and third is an introductory paragraph about the project.

        Dictionary<ObjectId, GitBlameCommit> commitByObjectId = [];

        // Pre-allocate the list with a capacity estimated from the approximate git blame length to describe a file line
        const int gitBlameLengthPerLineHeuristicValue = 120;
        List<GitBlameLine> lines =
            new(Math.Min(Math.Max(256, output.Length / gitBlameLengthPerLineHeuristicValue), 5000));

        bool hasCommitHeader;
        ObjectId objectId;
        int finalLineNumber;
        int originLineNumber;
        string? author;
        string? authorMail;
        DateTime authorTime;
        string? authorTimeZone;
        string? committer;
        string? committerMail;
        DateTime committerTime;
        string? committerTimeZone;
        string? summary;
        string? filename;

        Reset();

        foreach (var line in output.LazySplit('\n').Select(l => l.TrimEnd('\r')))
        {
            var match = HeaderRegex.Match(line);

            if (match.Success)
            {
                objectId = ObjectId.Parse(line, match.Groups["objectid"]);
                finalLineNumber = int.Parse(match.Groups["finallinenum"].ValueSpan);
                originLineNumber = int.Parse(match.Groups["origlinenum"].ValueSpan);
            }
            else if (line.StartsWith('\t'))
            {
                // The contents of the actual line is output after the above header, prefixed by a TAB. This is to allow adding more header elements later.
                var text = ReEncodeStringFromLossless(line[1..], encoding);

                // objectId is guaranteed to be set by the preceding git blame header line
                var oid = objectId.IsZero
                    ? throw new InvalidOperationException(
                        "Invalid git blame output: missing object ID header before content line.")
                    : objectId;

                GitBlameCommit commit;
                if (hasCommitHeader)
                {
                    // TODO quite a few nullable suppressions here (via ! character) which should be addressed as they hint at a design flaw

                    if (!commitByObjectId.TryGetValue(oid, out var commitData))
                    {
                        commit = new GitBlameCommit(
                            oid,
                            author!,
                            authorMail!,
                            authorTime,
                            authorTimeZone!,
                            committer!,
                            committerMail!,
                            committerTime,
                            committerTimeZone!,
                            summary!,
                            filename!);
                        commitByObjectId[oid] = commit;
                    }
                    else
                    {
                        if (filename == commitData.FileName)
                        {
                            commit = commitData;
                        }
                        else
                        {
                            commit = new GitBlameCommit(
                                commitData.ObjectId,
                                commitData.Author,
                                commitData.AuthorMail,
                                commitData.AuthorTime,
                                commitData.AuthorTimeZone,
                                commitData.Committer,
                                commitData.CommitterMail,
                                commitData.CommitterTime,
                                commitData.CommitterTimeZone,
                                commitData.Summary,
                                filename!);
                        }
                    }
                }
                else
                {
                    commit = commitByObjectId[oid];
                }

                lines.Add(new GitBlameLine(commit, finalLineNumber, originLineNumber, text));

                // Start a new header
                Reset();
            }
            else if (line.StartsWith("author "))
            {
                author = ReEncodeStringFromLossless(line["author ".Length..]);
                hasCommitHeader = true;
            }
            else if (line.StartsWith("author-mail "))
            {
                authorMail = ReEncodeStringFromLossless(line["author-mail ".Length..]);
                hasCommitHeader = true;
            }
            else if (line.StartsWith("author-time "))
            {
                authorTime = DateTimeUtils.ParseUnixTime(line.AsSpan("author-time ".Length));
                hasCommitHeader = true;
            }
            else if (line.StartsWith("author-tz "))
            {
                authorTimeZone = line["author-tz ".Length..];
                hasCommitHeader = true;
            }
            else if (line.StartsWith("committer "))
            {
                committer = ReEncodeStringFromLossless(line["committer ".Length..]);
                hasCommitHeader = true;
            }
            else if (line.StartsWith("committer-mail "))
            {
                committerMail = line["committer-mail ".Length..];
                hasCommitHeader = true;
            }
            else if (line.StartsWith("committer-time "))
            {
                committerTime = DateTimeUtils.ParseUnixTime(line.AsSpan("committer-time ".Length..));
                hasCommitHeader = true;
            }
            else if (line.StartsWith("committer-tz "))
            {
                committerTimeZone = line["committer-tz ".Length..];
                hasCommitHeader = true;
            }
            else if (line.StartsWith("summary "))
            {
                summary = ReEncodeStringFromLossless(line["summary ".Length..]);
                hasCommitHeader = true;
            }
            else if (line.StartsWith("filename "))
            {
                filename = ReEncodeFileNameFromLossless(line["filename ".Length..]);
                hasCommitHeader = true;
            }
        }

        return new GitBlame(lines);

        void Reset()
        {
            hasCommitHeader = false;
            objectId = default;
            finalLineNumber = -1;
            originLineNumber = -1;
            author = null;
            authorMail = null;
            authorTime = DateTime.MinValue;
            authorTimeZone = null;
            committer = null;
            committerMail = null;
            committerTime = DateTime.MinValue;
            committerTimeZone = null;
            summary = null;
            filename = null;
        }
    }

    /// <summary>
    ///     Un-escapes any octal code points embedded within <paramref name="s" />.
    /// </summary>
    /// <remarks>
    ///     If no portions of <paramref name="s" /> contain escaped data, then <paramref name="s" /> is returned.
    ///     <para />
    ///     If <paramref name="s" /> is <c>null</c> then an empty string is returned.
    /// </remarks>
    /// <example>
    ///     <code>UnescapeOctalCodePoints(@"\353\221\220\353\213\244") == "두다"</code>
    /// </example>
    /// <param name="s">The string to unescape.</param>
    /// <returns>
    ///     The unescaped string, or <paramref name="s" /> if no escaped values were present, or <c>""</c> if
    ///     <paramref name="s" /> is <c>null</c>.
    /// </returns>
    [return: NotNullIfNotNull(nameof(s))]
    public static string? UnescapeOctalCodePoints(string? s)
    {
        if (s is null)
        {
            return null;
        }

        return EscapedOctalCodePointRegex.Replace(
            s,
            match =>
            {
                try
                {
                    return SystemEncoding.GetString(
                    [
                        .. match.Groups["octal"]
                            .Captures
                            .Select(c => Convert.ToByte(c.Value, 8))
                    ]);
                }
                catch (OverflowException)
                {
                    // Octal values greater than 377 overflow a single byte.
                    // These should not be present in the input string.
                    return match.Value;
                }
            });
    }

    [return: NotNullIfNotNull(nameof(fileName))]
    public static string? ReEncodeFileNameFromLossless(string? fileName)
    {
        fileName = ReEncodeStringFromLossless(fileName, SystemEncoding);
        return UnescapeOctalCodePoints(fileName);
    }

    [return: NotNullIfNotNull(nameof(s))]
    public static string? ReEncodeString(string? s, Encoding fromEncoding, Encoding toEncoding)
    {
        if (s is null || fromEncoding.WebName == toEncoding.WebName)
        {
            return s;
        }

        var bytes = fromEncoding.GetBytes(s);
        return toEncoding.GetString(bytes);
    }

    /// <summary>
    ///     Re-encodes string from Commands.LosslessEncoding to toEncoding.
    /// </summary>
    [return: NotNullIfNotNull(nameof(s))]
    public static string? ReEncodeStringFromLossless(string? s, Encoding? toEncoding)
    {
        if (toEncoding is null)
        {
            return s;
        }

        return ReEncodeString(s, LosslessEncoding, toEncoding);
    }

    /// <summary>
    ///     header part of show result is encoded in logoutputencoding (including re-encoded commit message).
    ///     diff part is raw data in file's original encoding.
    ///     s should be encoded in LosslessEncoding.
    /// </summary>
    [return: NotNullIfNotNull(nameof(s))]
    public string? ReEncodeShowString(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }

        var p = s.IndexOf("diff --git");
        string header;
        string diffHeader;
        string diffContent;
        string diff;
        if (p > 0)
        {
            header = s[..p];
            diff = s[p..];
        }
        else
        {
            header = string.Empty;
            diff = s;
        }

        p = diff.IndexOf("@@");
        if (p > 0)
        {
            diffHeader = diff[..p];
            diffContent = diff[p..];
        }
        else
        {
            diffHeader = string.Empty;
            diffContent = diff;
        }

        header = ReEncodeString(header, LosslessEncoding, LogOutputEncoding);
        diffHeader = ReEncodeFileNameFromLossless(diffHeader);
        diffContent = ReEncodeString(diffContent, LosslessEncoding, FilesEncoding);
        return header + diffHeader + diffContent;
    }

    public override string ToString()
    {
        return WorkingDir;
    }

    private static GitItemStatus CreateErrorGitItemStatus(string gitOutput)
    {
        return new GitItemStatus(_gitError) { IsStatusOnly = true, ErrorMessage = gitOutput.Replace('\0', '\t') };
    }

    private void SetGitSetting(GitSettingLevel settingLevel, string setting, string? value, bool append = false)
    {
        GitExecutable.SetGitSetting(settingLevel, setting, value, append);

        EffectiveGitConfigSettings.Invalidate();
        if (settingLevel == GitSettingLevel.Local)
        {
            LocalGitConfigSettings.Invalidate();
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(GitModule gitModule)
    {
        public GitExecutor.TestAccessor Executor
        {
            get => ((GitExecutor)gitModule._executor).GetTestAccessor();
        }

        public DistributedSettings? EffectiveSettings
        {
            get => gitModule._effectiveSettings;
        }

        public FrozenDictionary<string, Color>? RemoteColors
        {
            get => gitModule._remoteColors;
        }

        public GitArgumentBuilder UpdateIndexCmd(bool showErrorsWhenStagingFiles)
        {
            return GitModule.UpdateIndexCmd(showErrorsWhenStagingFiles);
        }

        public List<GitItemStatus> GetDiffChangedFilesFromString(string statusString, StagedStatus staged)
        {
            return gitModule.GetDiffChangedFilesFromString(statusString, staged);
        }

        public StagedStatus GetStagedStatus(ObjectId firstId, ObjectId secondId, ObjectId parentToSecond)
        {
            return GitModule.GetStagedStatus(firstId, secondId, parentToSecond);
        }

        public static string GetConflictPartFileName(string fileName, string side, int processId)
        {
            return GitModule.GetConflictPartFileName(fileName, side, processId);
        }

        public static string GetAvailableFileName(string basePath, Func<string, bool> fileExists)
        {
            return GitModule.GetAvailableFileName(basePath, fileExists);
        }
    }

    #region Stage/Unstage

    bool IGitModule.AssumeUnchangedFiles(IReadOnlyList<GitItemStatus> files, bool assumeUnchanged, out string allOutput)
    {
        files = files.Where(file => file.IsAssumeUnchanged != assumeUnchanged).ToList();

        if (files.Count == 0)
        {
            allOutput = "";
            return true;
        }

        var execution = GitExecutable.Execute(
            new GitArgumentBuilder("update-index")
            {
                assumeUnchanged ? "--assume-unchanged" : "--no-assume-unchanged", "--stdin"
            },
            inputWriter =>
            {
                foreach (var file in files)
                {
                    UpdateIndex(inputWriter, file.Name);
                }
            },
            SystemEncoding,
            throwOnErrorExit: false);

        allOutput = execution.AllOutput;
        return execution.ExitedSuccessfully;
    }

    bool IGitModule.SkipWorktreeFiles(IReadOnlyList<GitItemStatus> files, bool skipWorktree, out string allOutput)
    {
        files = files.Where(file => file.IsSkipWorktree != skipWorktree).ToList();

        if (files.Count == 0)
        {
            allOutput = "";
            return true;
        }

        var execution = GitExecutable.Execute(
            new GitArgumentBuilder("update-index")
            {
                skipWorktree ? "--skip-worktree" : "--no-skip-worktree", "--stdin"
            },
            inputWriter =>
            {
                foreach (var file in files)
                {
                    UpdateIndex(inputWriter, file.Name);
                }
            },
            SystemEncoding);

        allOutput = execution.AllOutput;
        return execution.ExitedSuccessfully;
    }

    /// <summary>
    ///     Stage files from worktree to index.
    /// </summary>
    /// <param name="files">The files to set the status for.</param>
    /// <param name="allOutput">stdout and stderr output.</param>
    /// <returns><see langword="true" /> if no errors occurred; otherwise, <see langword="false" />.</returns>
    bool IGitModule.StageFiles(IReadOnlyList<GitItemStatus> files, out string allOutput)
    {
        var wereErrors = false;

        if (files.Count == 0)
        {
            allOutput = "";
            return !wereErrors;
        }

        StringBuilder output = new();
        List<GitItemStatus> nonDeletedFiles = [.. files.Where(file => !file.IsDeleted)];
        List<GitItemStatus> deletedFiles = [.. files.Where(file => file.IsDeleted)];

        if (nonDeletedFiles.Count != 0)
        {
            var execution = GitExecutable.Execute(
                UpdateIndexCmd(AppSettings.ShowErrorsWhenStagingFiles),
                inputWriter =>
                {
                    foreach (var file in nonDeletedFiles)
                    {
                        UpdateIndex(inputWriter, file.Name.TrimEnd('/'));
                    }
                },
                SystemEncoding,
                throwOnErrorExit: false);

            wereErrors |= !execution.ExitedSuccessfully;
            output.AppendLine(execution.AllOutput);
        }

        if (deletedFiles.Count != 0)
        {
            var execution = GitExecutable.Execute(
                new GitArgumentBuilder("update-index") { "--remove", "--stdin" },
                inputWriter =>
                {
                    foreach (var file in deletedFiles)
                    {
                        UpdateIndex(inputWriter, file.Name);
                    }
                },
                SystemEncoding,
                throwOnErrorExit: false);

            wereErrors |= !execution.ExitedSuccessfully;
            output.Append(execution.AllOutput);
        }

        allOutput = output.ToString();
        return !wereErrors;
    }

    void IGitModule.StageFile(string file)
    {
        GitExecutable.RunCommand(
            new GitArgumentBuilder("update-index") { "--add", file.ToPosixPath().Quote() });
    }

    /// <summary>
    ///     Unstage files from index to worktree.
    /// </summary>
    /// <param name="files">The files to set the status for.</param>
    /// <param name="allOutput">stdout and stderr output.</param>
    /// <returns><see langword="true" /> if no errors occurred; otherwise, <see langword="false" />.</returns>
    bool IGitModule.UnstageFiles(IReadOnlyList<GitItemStatus> files, out string allOutput)
    {
        var wereErrors = false;

        if (files.Count == 0)
        {
            allOutput = "";
            return !wereErrors;
        }

        StringBuilder output = new();
        List<GitItemStatus> nonNewFiles = [.. files.Where(file => !file.IsDeleted)];
        List<GitItemStatus> newFiles = [.. files.Where(file => file.IsDeleted)];

        if (nonNewFiles.Count != 0)
        {
            GitArgumentBuilder sb = new("reset") { "--" };
            foreach (var file in nonNewFiles)
            {
                sb.Add(file.Name.ToPosixPath().QuoteNe());
            }

            var execution = GitExecutable.Execute(sb);

            output.AppendLine(execution.AllOutput);
        }

        if (newFiles.Count != 0)
        {
            var execution = GitExecutable.Execute(
                new GitArgumentBuilder("update-index") { "--force-remove", "--stdin" },
                inputWriter =>
                {
                    foreach (var file in newFiles)
                    {
                        UpdateIndex(inputWriter, file.Name);
                    }
                },
                SystemEncoding);

            output.Append(execution.AllOutput);
        }

        allOutput = output.ToString();
        return !wereErrors;
    }

    bool IGitModule.BatchUnstageFiles(IEnumerable<GitItemStatus> files,
        Action<BatchProgressEventArgs>? progressCallback = null)
    {
        List<GitItemStatus> filesToUnstage = [];
        List<string> filesToRemove = [];
        var shouldRescanChanges = false;
        foreach (var item in files)
        {
            if (!item.IsNew)
            {
                Assumes.True(item.Name is not null, "Item must have a name");

                filesToRemove.Add(item.Name);

                if (item.IsRenamed)
                {
                    Assumes.True(item.OldName is not null, "Renamed item must have an old name");
                    filesToRemove.Add(item.OldName);
                }

                if (item.IsDeleted)
                {
                    shouldRescanChanges = true;
                }
            }
            else
            {
                filesToUnstage.Add(item);
            }
        }

        if (filesToRemove.Count > 0)
        {
            var args = Commands.Reset(ResetMode.ResetIndex, "HEAD", quiet: progressCallback is null);
            GitExecutable.RunBatchCommand(new ArgumentBuilder { args }
                    .BuildBatchArgumentsForFiles(filesToRemove),
                progressCallback);
        }

        UnstageFiles(filesToUnstage, out _);

        return shouldRescanChanges;
    }

    async Task<bool> IGitModule.AddInteractiveAsync(GitItemStatus file)
    {
        GitArgumentBuilder args = new("add") { "--patch", file.Name.Quote() };

        using var process = GitExecutable.Start(args, true);
        return await process.WaitForExitAsync() == 0;
    }

    async Task<bool> IGitModule.ResetInteractiveAsync(GitItemStatus file)
    {
        GitArgumentBuilder args = new("checkout") { "-p", file.Name.Quote() };

        using var process = GitExecutable.Start(args, true);
        return await process.WaitForExitAsync() == 0;
    }

    private static void UpdateIndex(StreamWriter inputWriter, string? filename)
    {
        var bytes = EncodingHelper.ConvertTo(
            SystemEncoding,
            $"\"{filename.ToPosixPath()}\"{inputWriter.NewLine}");

        inputWriter.BaseStream.Write(bytes, 0, bytes.Length);
    }

    private static GitArgumentBuilder UpdateIndexCmd(bool showErrorsWhenStagingFiles)
    {
        return new GitArgumentBuilder("update-index", gitOptions:
            showErrorsWhenStagingFiles
                ? default
                : (ArgumentString)"-c core.safecrlf=false") { "--add", "--stdin" };
    }

    #endregion
}
