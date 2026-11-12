using System.Buffers;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs.WorktreeDialog;
using Microsoft.VisualStudio.Threading;

namespace GitUI.LeftPanel;

internal sealed class WorktreeTree(TreeNode treeNode, IGitUiCommandsSource uiCommands) : Tree(treeNode, uiCommands)
{
    private static readonly SearchValues<char> _directorySeparatorChars =
        SearchValues.Create(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static readonly SearchValues<char> _wordBoundaryChars = SearchValues.Create('_', '-', '.', ' ');

    internal void Refresh()
    {
        if (!IsAttached)
        {
            return;
        }

        ReloadNodesDetached((_, cancellationToken) => LoadNodesAsync(cancellationToken), null!);
    }

    private async Task<Nodes> LoadNodesAsync(CancellationToken token)
    {
        await TaskScheduler.Default;
        token.ThrowIfCancellationRequested();

        return FillWorktreeTree(token);
    }

    private Nodes FillWorktreeTree(CancellationToken token)
    {
        Nodes nodes = new(this);
        var currentWorkingDir = Module.WorkingDir.TrimEnd(Path.DirectorySeparatorChar);

        var worktrees = Module.GetWorktrees();

        // Use the main worktree's parent as the reference for relative paths.
        // Git always lists the main worktree first. Using the main worktree's parent
        // ensures linked worktrees in a sibling directory produce paths like
        // "repo.worktrees/feature-a" rather than flat names with no directory component.
        var mainWorktreePath = worktrees.Count > 0
            ? worktrees[0].Path.TrimEnd(Path.DirectorySeparatorChar)
            : currentWorkingDir;
        var parentDir = Path.GetDirectoryName(mainWorktreePath) ?? mainWorktreePath;

        List<(GitWorktree Worktree, bool IsCurrent, string RelativePath)> worktreeInfos = [];

        foreach (var worktree in worktrees)
        {
            token.ThrowIfCancellationRequested();

            var isCurrent = string.Equals(
                worktree.Path.TrimEnd(Path.DirectorySeparatorChar),
                currentWorkingDir,
                StringComparison.OrdinalIgnoreCase);

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(parentDir, worktree.Path);
            }
            catch
            {
                relativePath = worktree.Path;
            }

            worktreeInfos.Add((worktree, isCurrent, relativePath));
        }

        // Strip the common directory prefix from non-main worktrees to reduce visual noise.
        // The first worktree is always the main worktree and is excluded from prefix calculation.
        var commonPrefix = GetCommonPrefix(
            worktreeInfos.Skip(1).Select(w => w.RelativePath));

        for (var i = 0; i < worktreeInfos.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            (var worktree, var isCurrent, var relativePath) = worktreeInfos[i];

            var displayPath = i > 0
                              && commonPrefix.Length > 0
                              && relativePath.StartsWith(commonPrefix, StringComparison.OrdinalIgnoreCase)
                ? relativePath[commonPrefix.Length..]
                : relativePath;

            WorktreeNode node = new(this, worktree, isCurrent, displayPath);
            nodes.AddNode(node);
        }

        return nodes;
    }

    /// <summary>
    ///     Finds the longest common prefix among the given relative paths, snapping to a word boundary.
    /// </summary>
    /// <remarks>
    ///     Word boundaries are directory separators and the characters <c>_</c>, <c>-</c>, <c>.</c>, and space.
    ///     This ensures names like "apricot" and "apple" are not truncated to "pricot" and "pple".
    /// </remarks>
    /// <returns>
    ///     The common prefix including its trailing delimiter, or an empty string if there is no common prefix.
    /// </returns>
    internal static string GetCommonPrefix(IEnumerable<string> paths)
    {
        ReadOnlySpan<char> prefix = default;
        var hasDirectorySeparator = false;
        var count = 0;

        foreach (var path in paths)
        {
            var span = path.AsSpan();
            count++;

            if (count == 1)
            {
                prefix = span;
                hasDirectorySeparator = span.ContainsAny(_directorySeparatorChars);
                continue;
            }

            hasDirectorySeparator = hasDirectorySeparator || span.ContainsAny(_directorySeparatorChars);

            // Find the longest character-for-character match.
            var limit = Math.Min(prefix.Length, span.Length);
            var matchLength = 0;
            for (var i = 0; i < limit; i++)
            {
                if (char.ToUpperInvariant(prefix[i]) != char.ToUpperInvariant(span[i]))
                {
                    break;
                }

                matchLength = i + 1;
            }

            prefix = prefix[..matchLength];
        }

        if (count < 2 || prefix.Length == 0)
        {
            return "";
        }

        // Snap to the last directory separator in the common prefix.
        var dirSepIndex = prefix.LastIndexOfAny(_directorySeparatorChars);
        if (dirSepIndex >= 0)
        {
            return prefix[..(dirSepIndex + 1)].ToString();
        }

        // Fall back to word-boundary characters only when none of the paths contain
        // directory separators. This handles the case where all worktrees are flat
        // siblings in the same directory (e.g. "repo_dev", "repo_test").
        if (hasDirectorySeparator)
        {
            return "";
        }

        var boundaryIndex = prefix.LastIndexOfAny(_wordBoundaryChars);
        if (boundaryIndex < 0)
        {
            return "";
        }

        return prefix[..(boundaryIndex + 1)].ToString();
    }

    protected override void PostFillTreeViewNode(bool firstTime)
    {
        if (firstTime && TreeViewNode.Nodes.Count > 1)
        {
            TreeViewNode.Expand();
        }
    }

    public void CreateWorktree(IWin32Window owner)
    {
        var mainWorktreePath = GetMainWorktreePath();
        if (UiCommands.WorktreeCreate(owner, mainWorktreePath))
        {
            Refresh();
        }
    }

    public void PruneWorktrees(IWin32Window owner)
    {
        if (UiCommands.StartCommandLineProcessDialog(owner, null, "worktree prune"))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    public void ManageWorktrees(IWin32Window owner)
    {
        using FormManageWorktree form = new(UiCommands);
        form.ShowDialog(owner);
        if (form.ShouldRefreshRevisionGrid)
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private string GetMainWorktreePath()
    {
        foreach (var node in Nodes)
        {
            if (node is WorktreeNode wt)
            {
                return wt.Worktree.Path;
            }
        }

        return Module.WorkingDir;
    }
}
