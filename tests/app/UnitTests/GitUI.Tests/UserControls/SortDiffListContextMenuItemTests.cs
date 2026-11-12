using GitCommands;
using GitUI.UserControls;
using NSubstitute;

namespace GitUITests.UserControls;

[Culture("en-US")]
public class SortDiffListContextMenuItemTests : IDisposable
{
    private readonly SortDiffListContextMenuItem _itemUnderTest = null!;
    private readonly IDiffListSortService _testingSortService = null!;

    public SortDiffListContextMenuItemTests()
    {
        _testingSortService = Substitute.For<IDiffListSortService>();
        _testingSortService.DiffListSorting.Returns(DiffListSortType.FilePath);
        _itemUnderTest = new SortDiffListContextMenuItem(_testingSortService);
    }

    void IDisposable.Dispose()
    {
        _itemUnderTest.Dispose();
    }

    [Fact]
    public void Should_show_all_sort_options()
    {
        _itemUnderTest.HasDropDownItems.Should().BeTrue();
        _itemUnderTest.DropDownItems.Count.Should().Be(6);

        AssertMenuItemTextAndLinkedSortType(_itemUnderTest.DropDownItems[0], "File &path - tree",
            DiffListSortType.FilePath);
        AssertMenuItemTextAndLinkedSortType(_itemUnderTest.DropDownItems[1], "&File path - flat",
            DiffListSortType.FilePathFlat);
        AssertMenuItemTextAndLinkedSortType(_itemUnderTest.DropDownItems[2], "File &extension - tree",
            DiffListSortType.FileExtension);
        AssertMenuItemTextAndLinkedSortType(_itemUnderTest.DropDownItems[3], "File e&xtension - flat",
            DiffListSortType.FileExtensionFlat);
        AssertMenuItemTextAndLinkedSortType(_itemUnderTest.DropDownItems[4], "File &status - tree",
            DiffListSortType.FileStatus);
        AssertMenuItemTextAndLinkedSortType(_itemUnderTest.DropDownItems[5], "File s&tatus - flat",
            DiffListSortType.FileStatusFlat);
    }

    [Theory]
    [InlineData(DiffListSortType.FilePath)]
    [InlineData(DiffListSortType.FileExtension)]
    [InlineData(DiffListSortType.FileStatus)]
    public void Only_the_current_sort_option_is_selected(DiffListSortType sortType)
    {
        // assert the default setup is selected and then change it.
        AssertOnlyCheckedItemIs(DiffListSortType.FilePath);

        _testingSortService.DiffListSorting.Returns(sortType);

        // invoke the requery method to reselect the proper sub item
        _itemUnderTest.GetTestAccessor().RaiseDropDownOpening();

        AssertOnlyCheckedItemIs(sortType);
    }

    [Fact]
    public void Clicking_an_item_sets_sort_in_service()
    {
        foreach (var item in _itemUnderTest.DropDownItems.Cast<ToolStripMenuItem>())
        {
            item.PerformClick();
            _testingSortService.Received(1).DiffListSorting = (DiffListSortType)item.Tag!;
        }
    }

    private void AssertOnlyCheckedItemIs(DiffListSortType sortType)
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

    private static void AssertMenuItemTextAndLinkedSortType(ToolStripItem menuItem, string expectedText,
        DiffListSortType expectedSortType)
    {
        menuItem.Text.Should().Be(expectedText);
        menuItem.Tag.Should().Be(expectedSortType);
    }
}
