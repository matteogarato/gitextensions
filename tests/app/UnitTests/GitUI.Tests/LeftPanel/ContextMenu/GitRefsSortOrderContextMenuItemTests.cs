using GitCommands.Settings;
using GitCommands.Utils;
using GitUI.LeftPanel.ContextMenu;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitUITests.LeftPanel.ContextMenu;

[Culture("en-US")]
public class GitRefsSortOrderContextMenuItemTests : IDisposable
{
    private readonly GitRefsSortOrderContextMenuItem _itemUnderTest = null!;
    private readonly Action _onSortOrderChanged = null!;

    public GitRefsSortOrderContextMenuItemTests()
    {
        _onSortOrderChanged = Substitute.For<Action>();
        _itemUnderTest = new GitRefsSortOrderContextMenuItem(_onSortOrderChanged);
    }

    public static IEnumerable<object[]> SortOrderOptions
    {
        get
        {
            foreach (var order in EnumHelper.GetValues<GitRefsSortOrder>())
            {
                yield return new object[] { order };
            }
        }
    }

    void IDisposable.Dispose()
    {
        _itemUnderTest.Dispose();
    }

    [Fact]
    public void Should_show_all_sort_options()
    {
        _itemUnderTest.HasDropDownItems.Should().BeTrue();
        _itemUnderTest.DropDownItems.Count.Should().Be(EnumHelper.GetValues<GitRefsSortOrder>().Length);
    }

    [Theory]
    [MemberData(nameof(SortOrderOptions))]
    public void Only_the_current_sort_option_is_selected(GitRefsSortOrder order)
    {
        var original = AppSettings.RefsSortOrder;
        try
        {
            AppSettings.RefsSortOrder = order;

            // invoke the requery method to reselect the proper sub item
            _itemUnderTest.GetTestAccessor().RaiseDropDownOpening();

            AssertOnlyCheckedItemIs(order);
        }
        finally
        {
            AppSettings.RefsSortOrder = original;
        }
    }

    [Fact]
    public void Clicking_an_item_sets_sort_in_service()
    {
        var original = AppSettings.RefsSortOrder;
        try
        {
            // Reset to the default
            AppSettings.RefsSortOrder = GitRefsSortOrder.Descending;

            foreach (var item in _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>())
            {
                item.PerformClick();
                _onSortOrderChanged.Received(1).Invoke();
                _onSortOrderChanged.ClearReceivedCalls();
            }
        }
        finally
        {
            AppSettings.RefsSortOrder = original;
        }
    }

    private void AssertOnlyCheckedItemIs(GitRefsSortOrder sortType)
    {
        var matchingSubItem =
            _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i!.Tag!.Equals(sortType));
        matchingSubItem.Checked.Should().BeTrue();

        foreach (var otherItem in _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>()
                     .Except(new[] { matchingSubItem }))
        {
            otherItem.Checked.Should().BeFalse();
        }
    }
}
