using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Options for applying a patch file.
/// </summary>
/// <param name="IgnoreWhitespace">Whether whitespace differences are ignored.</param>
/// <param name="SignOff">Whether a Signed-off-by line is added to applied mailbox patches.</param>
public sealed record PatchApplyOptions(bool IgnoreWhitespace = false, bool SignOff = false);

/// <summary>
///     Builds Git arguments for applying and creating patch files in the Avalonia browser.
/// </summary>
public static class PatchWorkflow
{
    /// <summary>
    ///     Reads a patch file without losing its original byte values.
    /// </summary>
    /// <param name="patchFile">The patch file path.</param>
    /// <returns>The patch text.</returns>
    public static string ReadPatchFile(string patchFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchFile);

        return File.ReadAllText(patchFile, GitModule.LosslessEncoding);
    }

    /// <summary>
    ///     Determines whether the file is a plain diff (applied with <c>git apply</c>) rather than a mailbox patch.
    /// </summary>
    /// <param name="patchFile">The patch file path.</param>
    /// <returns><see langword="true" /> for a plain diff.</returns>
    public static bool IsDiffPatch(string patchFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchFile);

        using StreamReader reader = new(patchFile);
        var firstLine = reader.ReadLine();

        return firstLine is not null
               && (firstLine.StartsWith("diff ", StringComparison.Ordinal)
                   || firstLine.StartsWith("Index: ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Builds the Git command matching the selected patch file format.
    /// </summary>
    /// <param name="module">The repository receiving the patch.</param>
    /// <param name="patchFile">The patch file path.</param>
    /// <param name="options">The whitespace and sign-off options; sign-off only applies to mailbox patches.</param>
    /// <returns>The Git arguments to apply the patch.</returns>
    public static ArgumentString BuildApply(IGitModule module, string patchFile, PatchApplyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(patchFile);

        options ??= new PatchApplyOptions();

        return IsDiffPatch(patchFile)
            ? Commands.ApplyDiffPatch(options.IgnoreWhitespace, patchFile, module.GetPathForGitExecution)
            : Commands.ApplyMailboxPatch(options.SignOff, options.IgnoreWhitespace, patchFile,
                module.GetPathForGitExecution);
    }

    /// <summary>
    ///     Builds the Git arguments to create a patch for one revision.
    /// </summary>
    /// <param name="module">The repository containing the revision.</param>
    /// <param name="from">The parent revision, or an empty string for a root commit.</param>
    /// <param name="to">The revision to format.</param>
    /// <param name="outputDirectory">The directory receiving the patch file.</param>
    /// <param name="startNumber">The initial patch sequence number, or <see langword="null" /> to use Git's default.</param>
    /// <returns>The Git arguments to create the patch.</returns>
    public static ArgumentString BuildFormatPatch(
        IGitModule module,
        string from,
        string to,
        string outputDirectory,
        int? startNumber = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var outputPath = module.GetPathForGitExecution(outputDirectory);

        return new GitArgumentBuilder("format-patch")
        {
            "--find-renames",
            "--find-copies",
            "--break-rewrites",
            { startNumber is not null, $"--start-number {startNumber}" },
            { !string.IsNullOrEmpty(from), $"{from.Quote()}..{to.Quote()}", $"--root {to.Quote()}" },
            $"-o {outputPath.Quote()}"
        };
    }

    /// <summary>
    ///     Builds format-patch commands for selected revisions in chronological order.
    /// </summary>
    /// <param name="module">The repository containing the revisions.</param>
    /// <param name="revisions">The selected revisions in chronological (oldest-first) order.</param>
    /// <param name="outputDirectory">The directory receiving the patch files.</param>
    /// <returns>The Git commands that create the selected patch files.</returns>
    public static IReadOnlyList<ArgumentString> BuildFormatPatches(
        IGitModule module,
        IReadOnlyList<GitRevision> revisions,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(revisions);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentOutOfRangeException.ThrowIfZero(revisions.Count);

        if (revisions.Any(revision => revision.ObjectId.IsArtificial))
        {
            throw new ArgumentException("Selected revisions must be real commits.", nameof(revisions));
        }

        if (revisions.Count == 2)
        {
            return
            [
                BuildFormatPatch(
                    module,
                    GetFirstParent(revisions[0]),
                    revisions[1].Guid,
                    outputDirectory)
            ];
        }

        if (revisions.Count == 1)
        {
            return [BuildFormatPatch(module, GetFirstParent(revisions[0]), revisions[0].Guid, outputDirectory)];
        }

        return revisions
            .Select((revision, index) => BuildFormatPatch(
                module,
                GetFirstParent(revision),
                revision.Guid,
                outputDirectory,
                index + 1))
            .ToArray();

        static string GetFirstParent(GitRevision revision)
        {
            return revision.ParentIds is { Count: > 0 } parents ? parents[0].ToString() : string.Empty;
        }
    }
}
