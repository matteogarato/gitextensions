using GitCommands;
using GitCommands.Utils;
using GitUI.LeftPanel.ContextMenu;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitUITests.UserControls;

[Culture("en-US")]
public class GitRefsSortByContextMenuItemTests : IDisposable
{
    private Action _onSortOrderChanged = null!;
    private GitRefsSortByContextMenuItem _itemUnderTest = null!;

    public GitRefsSortByContextMenuItemTests()
    {
        _onSortOrderChanged = Substitute.For<Action>();
        _itemUnderTest = new GitRefsSortByContextMenuItem(_onSortOrderChanged);
    }

    public void Dispose()
    {
        _itemUnderTest.Dispose();
    }

    [Fact]
    public void Should_show_all_sort_options()
    {
        _itemUnderTest.HasDropDownItems.Should().BeTrue();
        _itemUnderTest.DropDownItems.Count.Should().Be(EnumHelper.GetValues<GitRefsSortBy>().Length);
    }

    public static IEnumerable<object[]> SortOrderOptions
    {
        get
        {
            foreach (GitRefsSortBy order in EnumHelper.GetValues<GitRefsSortBy>())
            {
                yield return new object[] { order };
            }
        }
    }

    [Theory]
    [MemberData(nameof(SortOrderOptions))]
    public void Only_the_current_sort_option_is_selected(GitRefsSortBy order)
    {
        GitRefsSortBy original = AppSettings.RefsSortBy;
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
        GitRefsSortBy original = AppSettings.RefsSortBy;
        try
        {
            // Reset to the default
            AppSettings.RefsSortBy = GitRefsSortBy.Default;

            foreach (ToolStripMenuItem item in _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>())
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
        ToolStripMenuItem matchingSubItem = _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i!.Tag!.Equals(sortType));
        matchingSubItem.Checked.Should().BeTrue();

        foreach (ToolStripMenuItem otherItem in _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>().Except(new[] { matchingSubItem }))
        {
            otherItem.Checked.Should().BeFalse();
        }
    }
}
