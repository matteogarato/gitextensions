using System.Text;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

/// <summary>
///     Provides a minimal git execution context for lightweight repository operations such as reading the current branch.
/// </summary>
internal sealed class GitExecutor : IGitExecutor
{
    private static Encoding? _systemEncoding;

    private readonly IGitDirectoryResolver _gitDirectoryResolverInstance;

    public GitExecutor(IGitDirectoryResolver gitDirectoryResolver, string? workingDir)
    {
        _gitDirectoryResolverInstance = gitDirectoryResolver;

        WorkingDir = (workingDir ?? "").NormalizePath().NormalizeWslPath().EnsureTrailingPathSeparator();
        GitWindowsExecutable = new Executable(() => AppSettings.GitCommand, WorkingDir);
        GitWindowsCommandRunner = new GitCommandRunner(GitWindowsExecutable, () => SystemEncoding);

        WslDistro = AppSettings.WslGitEnabled ? PathUtil.GetWslDistro(WorkingDir) : "";
        if (!string.IsNullOrEmpty(WslDistro))
        {
            // In some WSL environments the current working directory is not passed along to the git command without using the `--cd` argument. Adding it to
            // the command line is required for these environments. For those that do not need it using the argument is just redundant.
            // `--exec` bypasses the distro's default login shell. Without it, wsl.exe re-parses the git command line through that shell
            // (e.g. zsh), which glob-expands patterns like "--exclude=refs/sessions/**" before git ever sees them. zsh's default
            // "nomatch" behaviour then aborts the whole command with a non-zero exit and no output when the pattern matches nothing,
            // which silently empties revision/diff output for any repo whose distro defaults to zsh (or any shell) with that setting.
            GitExecutable = new Executable(() => AppSettings.WslCommand, WorkingDir,
                $"-d {WslDistro} --cd {WorkingDir.RemoveTrailingPathSeparator().Quote()} --exec {AppSettings.WslGitCommand} ");
            GitCommandRunner = new GitCommandRunner(GitExecutable, () => SystemEncoding);
        }
        else
        {
            GitExecutable = GitWindowsExecutable;
            GitCommandRunner = GitWindowsCommandRunner;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Setter is needed for tests that need to replace the GitCommandRunner with a mock.
    /// </remarks>
    private IGitCommandRunner GitCommandRunner { get; set; }

    /// <inheritdoc />
    /// <remarks>
    ///     Setter is needed for tests that need to replace the GitExecutable with a mock.
    /// </remarks>
    private IExecutable GitExecutable { get; set; }

    /// <summary>
    ///     Gets the system encoding.
    /// </summary>
    public static Encoding SystemEncoding
    {
        get => _systemEncoding ??= new SystemEncodingReader().Read();
    }

    private string WorkingDir { get; }

    /// <summary>
    ///     Gets the  Windows Git executable associated with this executor.
    /// </summary>
    /// <remarks>
    ///     Setter is needed for tests that need to replace the GitWindowsExecutable with a mock.
    /// </remarks>
    private IExecutable GitWindowsExecutable { get; set; }

    bool IGitExecutor.IsReftableRepo { get; set; }

    /// <inheritdoc />
    /// <remarks>
    ///     Setter is needed for tests that need to replace the GitCommandRunner with a mock.
    /// </remarks>
    IGitCommandRunner IGitExecutor.GitCommandRunner
    {
        get => GitCommandRunner;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Setter is needed for tests that need to replace the GitExecutable with a mock.
    /// </remarks>
    IExecutable IGitExecutor.GitExecutable
    {
        get => GitExecutable;
    }

    string IGitExecutor.WorkingDir
    {
        get => WorkingDir;
    }

    /// <summary>
    ///     Gets the  Windows Git executable associated with this executor.
    /// </summary>
    /// <remarks>
    ///     Setter is needed for tests that need to replace the GitWindowsExecutable with a mock.
    /// </remarks>
    IExecutable IGitExecutor.GitWindowsExecutable
    {
        get => GitWindowsExecutable;
    }

    IGitCommandRunner IGitExecutor.GitWindowsCommandRunner { get; }

    string IGitExecutor.WslDistro { get; }

    /// <summary>
    ///     Gets the ".git" directory path.
    /// </summary>
    string IGitExecutor.GetGitDirectory()
    {
        return GetGitDirectory(WorkingDir);
    }

    /// <summary>
    ///     Gets the path to the Git directory associated with the specified repository path.
    /// </summary>
    /// <param name="repositoryPath">
    ///     The file system path to the root of the repository. This path must refer to an existing
    ///     Git repository.
    /// </param>
    /// <returns>
    ///     The path to the Git directory for the specified repository, or null if the path does not correspond to a
    ///     valid Git repository.
    /// </returns>
    internal string GetGitDirectory(string repositoryPath)
    {
        return _gitDirectoryResolverInstance.Resolve(repositoryPath);
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(GitExecutor executor)
    {
        public IExecutable GitExecutable
        {
            get => executor.GitExecutable;
            set => executor.GitExecutable = value;
        }

        public IExecutable GitWindowsExecutable
        {
            get => executor.GitWindowsExecutable;
            set => executor.GitWindowsExecutable = value;
        }

        public IGitCommandRunner GitCommandRunner
        {
            get => executor.GitCommandRunner;
            set => executor.GitCommandRunner = value;
        }
    }
}
