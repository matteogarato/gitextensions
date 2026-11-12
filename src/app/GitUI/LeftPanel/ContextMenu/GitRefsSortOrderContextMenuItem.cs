using GitCommands.Settings;
using GitCommands.Utils;
using GitUI.Properties;
using GitUIPluginInterfaces;

namespace GitUI.LeftPanel.ContextMenu;

internal sealed class GitRefsSortOrderContextMenuItem : ToolStripMenuItem
{
    internal const string MenuItemName = "GitRefsSortOrderContextMenuItem";
    private readonly Action _onSortOrderChanged;

    public GitRefsSortOrderContextMenuItem(Action onSortOrderChanged)
    {
        _onSortOrderChanged = onSortOrderChanged;

        Image = Images.SortBy;
        Text = TranslatedStrings.SortOrder;
        Name = MenuItemName;

        foreach (var option in EnumHelper.GetValues<GitRefsSortOrder>()
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
        var currentSort = AppSettings.RefsSortOrder;
        foreach (ToolStripMenuItem item in DropDownItems)
        {
            item.Checked = currentSort.Equals(item.Tag);
        }
    }

    private void Item_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item)
        {
            var sortingType = (GitRefsSortOrder)item.Tag!;
            AppSettings.RefsSortOrder = sortingType;

            _onSortOrderChanged?.Invoke();
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(GitRefsSortOrderContextMenuItem menuitem)
    {
        public readonly void RaiseDropDownOpening()
        {
            menuitem.RequerySortingMethod();
        }
    }
}
