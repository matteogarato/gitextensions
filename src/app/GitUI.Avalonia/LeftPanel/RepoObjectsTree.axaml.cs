using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitUI.Compat;

namespace GitUI.LeftPanel;

// TODO(avalonia-port): milestone M1.4b — a read-only branches/remotes/tags panel.
// The WinForms RepoObjectsTree's nested branch folders, submodules, stashes, favorites,
// filtering, and context menus arrive in later milestones.
public partial class RepoObjectsTree : GitExtensionsControl
{
    public RepoObjectsTree()
    {
        InitializeComponent();

        treeMain.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);

        InitializeComplete();
    }

    /// <summary>
    ///     Gets the ref of the selected node, or <see langword="null" /> for group nodes.
    /// </summary>
    public IGitRef? SelectedRef
    {
        get => (treeMain.SelectedItem as TreeViewItem)?.Tag as IGitRef;
    }

    /// <summary>
    ///     Gets the selected stash, or <see langword="null" />.
    /// </summary>
    public GitStash? SelectedStash
    {
        get => (treeMain.SelectedItem as TreeViewItem)?.Tag as GitStash;
    }

    /// <summary>
    ///     Occurs when the selected node changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    ///     Fills the tree from the repository refs and stashes.
    /// </summary>
    public void SetRefs(IReadOnlyList<IGitRef> refs, IReadOnlyList<GitStash>? stashes = null)
    {
        stashes ??= [];
        TreeViewItem stashesNode = new()
        {
            Header = $"{TranslatedStrings.Stashes} ({stashes.Count})",
            ItemsSource = stashes.Select(stash => new TreeViewItem { Header = stash.Summary, Tag = stash })
                .ToArray()
        };
        var branchesNode = CreateGroupNode(TranslatedStrings.Branches,
            refs.Where(gitRef => gitRef.IsHead && !gitRef.IsTag));
        var remotesNode = CreateGroupNode(TranslatedStrings.Remotes, refs.Where(gitRef => gitRef.IsRemote));
        var tagsNode = CreateGroupNode(TranslatedStrings.Tags, refs.Where(gitRef => gitRef.IsTag));
        branchesNode.IsExpanded = true;

        treeMain.ItemsSource = new[] { branchesNode, remotesNode, tagsNode, stashesNode };

        return;

        static TreeViewItem CreateGroupNode(string caption, IEnumerable<IGitRef> groupRefs)
        {
            TreeViewItem[] children =
            [
                .. groupRefs
                    .OrderBy(gitRef => gitRef.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(gitRef => new TreeViewItem
                    {
                        Header = gitRef.IsSelected ? $"* {gitRef.Name}" : gitRef.Name, Tag = gitRef
                    })
            ];
            return new TreeViewItem { Header = $"{caption} ({children.Length})", ItemsSource = children };
        }
    }
}
