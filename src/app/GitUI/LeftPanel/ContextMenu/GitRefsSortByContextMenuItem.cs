using GitCommands.Settings;
using GitCommands.Utils;
using GitUI.Properties;
using GitUIPluginInterfaces;

namespace GitUI.LeftPanel.ContextMenu;

internal sealed class GitRefsSortByContextMenuItem : ToolStripMenuItem
{
    private readonly Action _onSortByChanged;

    public GitRefsSortByContextMenuItem(Action onSortByChanged)
    {
        _onSortByChanged = onSortByChanged;

        Image = Images.SortBy;
        Text = TranslatedStrings.SortBy;

        foreach (var option in EnumHelper.GetValues<GitRefsSortBy>()
                     .Select(e => (Text: e.GetDescription(), Value: e)))
        {
            ToolStripMenuItem item = new() { Text = option.text, Image = null, Tag = option.value };

            item.Click += Item_Click;
            DropDownItems.Add(item);
        }

        DropDownOpening += (s, e) => RequerySortingMethod();
        RequerySortingMethod();
    }

    private void RequerySortingMethod()
    {
        var currentSort = AppSettings.RefsSortBy;
        foreach (ToolStripMenuItem item in DropDownItems)
        {
            item.Checked = currentSort.Equals(item.Tag);
        }
    }

    private void Item_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item)
        {
            var sortingType = (GitRefsSortBy)item.Tag!;
            AppSettings.RefsSortBy = sortingType;

            _onSortByChanged?.Invoke();
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(GitRefsSortByContextMenuItem menuitem)
    {
        public readonly void RaiseDropDownOpening()
        {
            menuitem.RequerySortingMethod();
        }
    }
}
