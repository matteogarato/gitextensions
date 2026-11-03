using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitUI.CommandsDialogs;

internal enum ArchiveFormat
{
    Zip,
    Tar,
}

internal static class ArchiveWorkflow
{
    internal static ArgumentString BuildArchive(
        IGitModule module,
        GitRevision revision,
        string outputPath,
        ArchiveFormat format,
        IReadOnlyList<string>? paths = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (revision.ObjectId.IsArtificial)
        {
            throw new ArgumentException("The selected revision must be a real commit.", nameof(revision));
        }

        if (paths is { Count: 0 })
        {
            throw new ArgumentException("At least one path must be selected for a filtered archive.", nameof(paths));
        }

        string? gitOutputPath = module.GetPathForGitExecution(outputPath);
        if (string.IsNullOrWhiteSpace(gitOutputPath))
        {
            throw new ArgumentException("The archive output path is invalid.", nameof(outputPath));
        }

        GitExtUtils.GitArgumentBuilder arguments = new("archive")
        {
            $"--format={format.ToString().ToLowerInvariant()}",
            $"--output {gitOutputPath.Quote()}",
            revision.Guid.Quote(),
            { paths is not null, "--" },
        };

        if (paths is not null)
        {
            foreach (string path in paths)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(path);
                arguments.Add(path.QuoteNE());
            }
        }

        return arguments;
    }

    internal static IReadOnlyList<string> GetChangedPaths(
        IGitModule module,
        GitRevision fromRevision,
        GitRevision toRevision)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(fromRevision);
        ArgumentNullException.ThrowIfNull(toRevision);

        return module.GetDiffFilesWithUntracked(
                fromRevision.Guid,
                toRevision.Guid,
                StagedStatus.None)
            .Where(file => file.ErrorMessage is null && !file.IsDeleted)
            .Select(file => file.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
