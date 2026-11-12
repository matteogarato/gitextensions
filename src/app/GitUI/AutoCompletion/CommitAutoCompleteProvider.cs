using System.Text.RegularExpressions;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AutoCompletion;

public class CommitAutoCompleteProvider(Func<IGitModule> getModule) : IAutoCompleteProvider
{
    private static readonly Lazy<Dictionary<string, Regex>> _regexes = new(ParseRegexes);
    private readonly GetAllChangedFilesOutputParser _getAllChangedFilesOutputParser = new(getModule);

    async Task<IEnumerable<AutoCompleteWord>> IAutoCompleteProvider.GetAutoCompleteWordsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<string> autoCompleteWords = [];

        var module = GetModule();
        var cmd = Commands.GetAllChangedFiles(true, UntrackedFilesMode.Default, noLocks: true);
        var result =
            await module.GitExecutable.ExecuteAsync(cmd, throwOnErrorExit: false, cancellationToken: cancellationToken);
        if (!result.ExitedSuccessfully)
        {
            // Try again
            result = await module.GitExecutable.ExecuteAsync(cmd, throwOnErrorExit: false,
                cancellationToken: cancellationToken);
        }

        if (!result.ExitedSuccessfully)
        {
            // Failed after retry, do not bother
            return [];
        }

        var output = result.StandardOutput;
        var changedFiles = _getAllChangedFilesOutputParser.Parse(output);
        foreach (var file in changedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            autoCompleteWords.Add(Path.GetFileName(file.Name));

            if (file.IsSubmodule)
            {
                continue;
            }

            var regex = GetRegexForExtension(Path.GetExtension(file.Name) ?? "");

            if (regex is not null)
            {
                // HACK: need to expose require methods at IGitModule level
                var text = await GetChangedFileTextAsync((GitModule)module, file);
                var matches = regex.Matches(text!);
                foreach (Match match in matches)
                {
                    // Skip first group since it always contains the entire matched string (regardless of capture groups)
                    foreach (var group in match.Groups.OfType<Group>().Skip(1))
                    {
                        foreach (Capture capture in group.Captures)
                        {
                            autoCompleteWords.Add(capture.Value);
                        }
                    }
                }
            }

            autoCompleteWords.Add(Path.GetFileNameWithoutExtension(file.Name));
            if (!string.IsNullOrWhiteSpace(file.OldName))
            {
                autoCompleteWords.Add(Path.GetFileNameWithoutExtension(file.OldName));
                autoCompleteWords.Add(Path.GetFileName(file.OldName));
            }
        }

        return autoCompleteWords.Select(w => new AutoCompleteWord(w));
    }

    private IGitModule GetModule()
    {
        return getModule() ?? throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");
    }

    private static Regex? GetRegexForExtension(string extension)
    {
        return _regexes.Value.TryGetValue(extension, out var value) ? value : null;
    }

    private static IEnumerable<string> ReadOrInitializeAutoCompleteRegexes()
    {
        var appDataPath = AppSettings.ApplicationDataPath.Value;

        Validates.NotNull(appDataPath);

        var path = Path.Join(appDataPath, "AutoCompleteRegexes.txt");

        if (File.Exists(path))
        {
            return File.ReadLines(path);
        }

        return AutoCompleteRegexes.Default.Split(Delimiters.LineFeedAndCarriageReturn,
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static Dictionary<string, Regex> ParseRegexes()
    {
        var autoCompleteRegexes = ReadOrInitializeAutoCompleteRegexes();

        Dictionary<string, Regex> regexes = [];

        foreach (var line in autoCompleteRegexes)
        {
            var i = line.IndexOf('=');
            var extensionStr = line[..i];
            var regexStr = line[(i + 1)..].Trim();

            var extensions = extensionStr.LazySplit(',').Select(s => s.Trim()).Distinct();
            Regex regex = new(regexStr, RegexOptions.Compiled);

            foreach (var extension in extensions)
            {
                regexes.Add(extension, regex);
            }
        }

        return regexes;
    }

    private static async Task<string?> GetChangedFileTextAsync(IGitModule module, GitItemStatus file)
    {
        if (file.IsTracked)
        {
            Patch? changes = null;
            try
            {
                changes = await module.GetCurrentChangesAsync(file.Name, file.OldName,
                        file.Staged == StagedStatus.Index, "-U5", noLocks: true)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors, this can be very repetitive
            }

            if (changes is not null)
            {
                return changes.Text;
            }

            var content = await module.GetFileContentsAsync(file).ConfigureAwaitRunInline();
            if (content is not null)
            {
                return content;
            }
        }

        // Try to read the contents of the file: if it cannot be read, skip the operation silently.
        try
        {
            using var reader = File.OpenText(Path.Combine(module.WorkingDir, file.Name));
            return await reader.ReadToEndAsync();
        }
        catch
        {
            return "";
        }
    }
}
