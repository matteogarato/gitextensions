using GitCommands.Settings;
using GitCommands.Utils;
using GitUI.LeftPanel.ContextMenu;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitUITests.LeftPanel.ContextMenu;

[Culture("en-US")]
public class GitRefsSortByContextMenuItemTests : IDisposable
{
    private readonly GitRefsSortByContextMenuItem _itemUnderTest = null!;
    private readonly Action _onSortOrderChanged = null!;

    public GitRefsSortByContextMenuItemTests()
    {
        _onSortOrderChanged = Substitute.For<Action>();
        _itemUnderTest = new GitRefsSortByContextMenuItem(_onSortOrderChanged);
    }

    public static IEnumerable<object[]> SortOrderOptions
    {
        get
        {
            foreach (var order in EnumHelper.GetValues<GitRefsSortBy>())
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
        _itemUnderTest.DropDownItems.Count.Should().Be(EnumHelper.GetValues<GitRefsSortBy>().Length);
    }

    [Theory]
    [MemberData(nameof(SortOrderOptions))]
    public void Only_the_current_sort_option_is_selected(GitRefsSortBy order)
    {
        var original = AppSettings.RefsSortBy;
        try
        {
            AppSettings.RefsSortBy = order;

            // invoke the requery method to reselect the proper sub item
            _itemUnderTest.GetTestAccessor().RaiseDropDownOpening();

            AssertOnlyCheckedItemIs(order);
        }
        finally
        {
            AppSettings.RefsSortBy = original;
        }
    }

    [Fact]
    public void Clicking_an_item_sets_sort_in_service()
    {
        var original = AppSettings.RefsSortBy;
        try
        {
            // Reset to the default
            AppSettings.RefsSortBy = GitRefsSortBy.Default;

            foreach (var item in _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>())
            {
                item.PerformClick();
                _onSortOrderChanged.Received(1).Invoke();
                _onSortOrderChanged.ClearReceivedCalls();
            }
        }
        finally
        {
            AppSettings.RefsSortBy = original;
        }
    }

    private void AssertOnlyCheckedItemIs(GitRefsSortBy sortType)
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
