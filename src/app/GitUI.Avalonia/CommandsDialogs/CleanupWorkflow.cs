using GitCommands;
using GitExtensions.Extensibility;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

internal static class CleanupWorkflow
{
    internal static IReadOnlyList<string> ParsePathLines(string? text)
    {
        return (text ?? string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Replace('\\', '/'))
            .ToArray();
    }

    internal static ArgumentString BuildClean(
        CleanMode mode,
        bool dryRun,
        bool directories,
        IReadOnlyList<string>? paths = null,
        IReadOnlyList<string>? excludes = null)
    {
        ValidatePaths(paths, nameof(paths));
        ValidatePaths(excludes, nameof(excludes));

        GitArgumentBuilder arguments = new("clean")
        {
            mode,
            { directories, "-d" },
            { dryRun, "--dry-run", "-f" },
        };

        if (excludes is not null)
        {
            foreach (string exclude in excludes)
            {
                arguments.Add($"--exclude={exclude}".QuoteNE());
            }
        }

        AddPathspecs(arguments, paths);
        return arguments;
    }

    internal static ArgumentString BuildSubmoduleClean(
        CleanMode mode,
        bool dryRun,
        bool directories,
        IReadOnlyList<string>? paths = null)
    {
        ValidatePaths(paths, nameof(paths));

        GitArgumentBuilder arguments = new("submodule")
        {
            "foreach --recursive git clean",
            mode,
            { directories, "-d" },
            { dryRun, "--dry-run", "-f" },
        };

        AddPathspecs(arguments, paths);
        return arguments;
    }

    internal static string ToRepositoryPath(string workingDirectory, string selectedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);

        string repositoryRoot = Path.GetFullPath(workingDirectory);
        string fullPath = Path.GetFullPath(selectedPath);
        string relativePath = Path.GetRelativePath(repositoryRoot, fullPath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", comparison)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", comparison)
            || relativePath.Equals(".", comparison))
        {
            throw new ArgumentException(GitUI.TranslatedStrings.CleanupPathOutsideRepository, nameof(selectedPath));
        }

        return relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static void AddPathspecs(GitArgumentBuilder arguments, IReadOnlyList<string>? paths)
    {
        if (paths is null)
        {
            return;
        }

        arguments.Add("--");
        foreach (string path in paths)
        {
            arguments.Add(path.QuoteNE());
        }
    }

    private static void ValidatePaths(IReadOnlyList<string>? paths, string parameterName)
    {
        if (paths is { Count: 0 })
        {
            throw new ArgumentException("At least one path is required when a path filter is enabled.", parameterName);
        }

        if (paths is not null && paths.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Path filters cannot contain empty paths.", parameterName);
        }
    }
}
