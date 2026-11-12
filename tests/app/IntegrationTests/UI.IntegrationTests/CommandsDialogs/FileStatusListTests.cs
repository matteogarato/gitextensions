using System.ComponentModel.Design;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.UserControls;
using NSubstitute;
using FileStatusList = GitUI.UserControls.FileStatusList;

namespace UITests.CommandsDialogs;

public class FileStatusListTests : IDisposable
{
    private readonly FileStatusList _fileStatusList = null!;

    // Created once for each test
    private readonly Form _form = null!;

    public FileStatusListTests()
    {
        var serviceContainer = GlobalServiceContainer.CreateDefaultMockServiceContainer();
        var module = Substitute.For<IGitModule>();
        GitUiCommands commands = new(serviceContainer, module);
        var uiCommandsSource = Substitute.For<IGitUiCommandsSource>();
        uiCommandsSource.UiCommands.Returns(x => commands);

        _form = new Form();
        _fileStatusList = new FileStatusList { Parent = _form, UiCommandsSource = uiCommandsSource };
        _form.Show(); // must be visible to be able to change the focus
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _fileStatusList.Dispose();
        _form.Dispose();
    }

    [WinFormsFact]
    public void SelectAll_should_expand_folders_and_focus_the_first_node()
    {
        const int folderCount = 4;
        const int filesPerFolder = 50;
        var items = Enumerable.Range(0, folderCount * filesPerFolder)
            .Select(index => new GitItemStatus($"folder{index / filesPerFolder}/subfolder/file{index:D3}"))
            .ToArray();
        _fileStatusList.SetDiffs(null, new GitRevision(ObjectId.Random()), items);
        var tree = _fileStatusList.GetTestAccessor().FileStatusListView;
        tree.CollapseAll();

        _fileStatusList.SelectAll();

        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(items);
        tree.Items().Where(node => node.Nodes.Count > 0).Should().OnlyContain(node => node.IsExpanded);
        tree.FocusedNode.Should().BeSameAs(tree.Nodes[0]);
        tree.Nodes[0].IsVisible.Should().BeTrue();
        tree.UpdateSuspended.Should().BeFalse();
    }

    [WinFormsFact]
    public void ItemSelections()
    {
        var accessor = _fileStatusList.GetTestAccessor();

        GitItemStatus itemNotInList = new("not in list");
        GitItemStatus item0 = new("z.0");
        GitItemStatus item1 = new("x.1");
        GitItemStatus item2 = new("y.2");
        List<GitItemStatus> items = [item0, item1, item2];

        // alphabetical order
        var itemAt0 = item1;
        var itemAt1 = item2;
        var itemAt2 = item0;
        GitRevision firstRev = new(ObjectId.Random());
        GitRevision secondRev = new(ObjectId.Random());
        _fileStatusList.SetDiffs(firstRev, secondRev, items);

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(0);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt0);
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt0);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt0 });

        _fileStatusList.SelectedGitItem.Should().BeSameAs(_fileStatusList.SelectedItem!.Item);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(_fileStatusList.SelectedItems.Items());

        // SelectedIndex

        _fileStatusList.SelectedGitItems = [itemAt1];

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt1);
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt1);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1 });

        _fileStatusList.SelectedItems = [];

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1); // unchanged
        _fileStatusList.SelectedItem.Should().BeNull(); // empty
        _fileStatusList.SelectedGitItem.Should().BeNull(); // empty
        _fileStatusList.SelectedItems.Items().Should().BeEmpty();

        _fileStatusList.SelectedGitItems = [itemAt2];
        _fileStatusList.SelectedGitItems = [itemNotInList]; // clears the selection

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(2); // unchanged
        _fileStatusList.SelectedItem.Should().BeNull(); // empty
        _fileStatusList.SelectedGitItem.Should().BeNull(); // empty
        _fileStatusList.SelectedItems.Items().Should().BeEmpty();

        _fileStatusList.SelectedGitItems = [itemAt1];

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt1);
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt1);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1 });

        // SelectedGitItem

        _fileStatusList.SelectedGitItem = itemAt1;

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt1);
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt1);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1 });

        _fileStatusList.SelectedGitItem = null;

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1); // unchanged
        _fileStatusList.SelectedItem.Should().BeNull(); // empty
        _fileStatusList.SelectedGitItem.Should().BeNull(); // empty
        _fileStatusList.SelectedItems.Items().Should().BeEmpty();

        _fileStatusList.SelectedGitItem = itemAt2;
        _fileStatusList.SelectedGitItem = itemNotInList; // clears the selection

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(2); // unchanged
        _fileStatusList.SelectedItem.Should().BeNull(); // empty
        _fileStatusList.SelectedGitItem.Should().BeNull(); // empty
        _fileStatusList.SelectedItems.Items().Should().BeEmpty();

        _fileStatusList.SelectedGitItem = itemAt1;

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt1);
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt1);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1 });

        // SelectedItems.Items() (up to one item)

        _fileStatusList.SelectedGitItems = new List<GitItemStatus> { itemAt1 };

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt1);
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt1);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1 });

        _fileStatusList.SelectedItems = null!;

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1); // unchanged
        _fileStatusList.SelectedItem.Should().BeNull(); // empty
        _fileStatusList.SelectedGitItem.Should().BeNull(); // empty
        _fileStatusList.SelectedItems!.Items().Should().BeEmpty();

        _fileStatusList.SelectedGitItems = new List<GitItemStatus>();

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1); // unchanged
        _fileStatusList.SelectedItem.Should().BeNull(); // empty
        _fileStatusList.SelectedGitItem.Should().BeNull(); // empty
        _fileStatusList.SelectedItems.Items().Should().BeEmpty();

        _fileStatusList.SelectedGitItems = new List<GitItemStatus> { itemAt2 };
        _fileStatusList.SelectedGitItems = new List<GitItemStatus> { itemNotInList }; // clears the selection

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(2); // unchanged
        _fileStatusList.SelectedItem.Should().BeNull(); // empty
        _fileStatusList.SelectedGitItem.Should().BeNull(); // empty
        _fileStatusList.SelectedItems.Items().Should().BeEmpty();

        _fileStatusList.SelectedGitItems = new List<GitItemStatus> { itemAt1 };

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt1);
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt1);
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1 });

        // SelectedItems.Items() (multiple items)

        _fileStatusList.SelectedGitItems = [itemAt2];
        _fileStatusList.SelectedGitItems = new List<GitItemStatus> { itemAt2, itemAt0, itemNotInList };

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(0);
        _fileStatusList.SelectedItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedGitItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt0, itemAt2 });

        accessor.FileStatusListView.FocusedNode = accessor.FileStatusListView.Nodes[1];

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedGitItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt0, itemAt2 });

        _fileStatusList.SelectedGitItems = [itemAt2];
        _fileStatusList.SelectedGitItems = new List<GitItemStatus> { itemAt2, itemAt1, itemNotInList };

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(1);
        _fileStatusList.SelectedItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedGitItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1, itemAt2 });

        accessor.FileStatusListView.FocusedNode = accessor.FileStatusListView.Nodes[0];

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(0);
        _fileStatusList.SelectedItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedGitItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt1, itemAt2 });

        // SelectAll

        _fileStatusList.SelectedGitItems = [itemAt2];
        _fileStatusList.SelectAll();

        foreach (var item in accessor.FileStatusListView.Items())
        {
            accessor.FileStatusListView.SelectedNodes.Contains(item).Should().BeTrue();
        }

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(0);
        _fileStatusList.SelectedItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedGitItem.Should().BeNull(); // due to multi-selection
        _fileStatusList.SelectedItems.Items().Should()
            .BeEquivalentTo(new List<GitItemStatus> { itemAt0, itemAt1, itemAt2 });

        // SelectFirstVisibleItem

        _fileStatusList.SelectedGitItems = [itemAt2];
        _fileStatusList.SelectFirstVisibleItem();

        accessor.FileStatusListView.FocusedNode!.Index.Should().Be(0);
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt0); // first item
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt0); // first item
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt0 });

        // no focus
        accessor.FileStatusListView.FocusedNode = null;

        accessor.FileStatusListView.FocusedNode.Should().BeNull();
        _fileStatusList.SelectedItem!.Item.Should().Be(itemAt0); // unchanged
        _fileStatusList.SelectedGitItem.Should().BeSameAs(itemAt0); // unchanged
        _fileStatusList.SelectedItems.Items().Should().BeEquivalentTo(new List<GitItemStatus> { itemAt0 });
    }

    [WinFormsTheory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("x", true)]
    [InlineData("x", false)]
    public void Test_FilterWatermarkLabelVisibility_on_FilterVisibleChange(
        string? filterText,
        bool filterFocused)
    {
        var accessor = _fileStatusList.GetTestAccessor();

        accessor.FilterComboBox.Text =
            filterText; // must be set first because it does not need to update the visibility
        accessor.SetFileStatusListVisibility(false);

        if (filterFocused)
        {
            accessor.FilterComboBox.Focus(); // must be done after FilterVisible = true
        }

        accessor.FilterWatermarkLabelVisible.Should().Be(!filterFocused && string.IsNullOrEmpty(filterText));
    }

    [WinFormsFact]
    public void Test_FilterWatermarkLabelVisibility_on_Focus()
    {
        var accessor = _fileStatusList.GetTestAccessor();

        accessor.FilterComboBox.Text = "";
        accessor.SetFileStatusListVisibility(false);
        accessor.FilterWatermarkLabelVisible.Should().BeTrue();

        accessor.FilterComboBox.Focus();
        accessor.SetFileStatusListVisibility(false);
        accessor.FilterWatermarkLabelVisible.Should().BeFalse();

        accessor.FileStatusListView.Focus();
        accessor.SetFileStatusListVisibility(false);
        accessor.FilterWatermarkLabelVisible.Should().BeTrue();
    }

    [WinFormsTheory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("\\.cs", true)]
    public void Test_StoreFilter_valid(string? regex, bool active)
    {
        var accessor = _fileStatusList.GetTestAccessor();

        var expectedColor = active ? accessor.ActiveInputColor : SystemColors.Window;
        var expectedRegex = string.IsNullOrEmpty(regex) ? null : regex;

        _fileStatusList.SetFilter(regex!);
        accessor.SetFileStatusListVisibility(false);

        CheckStoreFilter(expectedColor, expectedRegex!, accessor);

        accessor.DeleteFilterButton.Visible.Should().Be(active);
    }

    [WinFormsFact]
    public void Test_StoreFilter_invalid()
    {
        const string validRegex = "\\.cs";
        const string invalidRegex = "(";

        var accessor = _fileStatusList.GetTestAccessor();

        // set a valid Filter that must not change
        accessor.StoreFilter(validRegex);

        ((Action)(() => accessor.StoreFilter(invalidRegex))).Should().Throw<ArgumentException>();

        CheckStoreFilter(accessor.InvalidInputColor, validRegex, accessor);
    }

    private static void CheckStoreFilter(Color expectedColor, string expectedRegex,
        FileStatusList.TestAccessor accessor)
    {
        accessor.FilterComboBox.BackColor.Should().Be(expectedColor);
        accessor.Filter?.ToString().Should().Be(expectedRegex);
    }
}
