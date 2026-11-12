using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Decides which revisions an external diff tool compares and whether a tool is configured to do it.
/// </summary>
public static class ExternalToolWorkflow
{
    private const string _guiToolKey = "diff.guitool";
    private const string _toolKey = "diff.tool";

    /// <summary>
    ///     Gets the diff tool Git would use for <c>difftool --gui</c>.
    /// </summary>
    /// <returns>The tool name, or <see langword="null" /> when none is configured.</returns>
    public static string? GetConfiguredDiffTool(IGitModule module)
    {
        var tool = module.GetEffectiveSetting(_guiToolKey);
        if (string.IsNullOrWhiteSpace(tool))
        {
            tool = module.GetEffectiveSetting(_toolKey);
        }

        return string.IsNullOrWhiteSpace(tool) ? null : tool.Trim();
    }

    /// <summary>
    ///     Resolves the pair of revisions to compare for the current selection.
    /// </summary>
    /// <param name="selection">The selected revisions in revision-grid order (newest first).</param>
    /// <returns>
    ///     The older and newer revision expressions: the two selected commits, or a single commit against its first parent;
    ///     <see langword="null" /> for artificial revisions, a root commit or any other selection size.
    /// </returns>
    public static (string First, string Second)? ResolveComparison(IReadOnlyList<GitRevision> selection)
    {
        if (selection.Any(revision => revision.IsArtificial))
        {
            return null;
        }

        return selection.Count switch
        {
            1 when selection[0].ParentIds is { Count: > 0 } parents => (parents[0].ToString(),
                selection[0].ObjectId.ToString()),
            2 => (selection[1].ObjectId.ToString(), selection[0].ObjectId.ToString()),
            _ => null
        };
    }
}
