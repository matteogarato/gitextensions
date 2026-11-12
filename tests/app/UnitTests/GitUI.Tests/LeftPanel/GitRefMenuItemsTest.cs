using System.Runtime.CompilerServices;
using GitUI.LeftPanel.ContextMenu;
using GitUI.LeftPanel.Interfaces;
using NSubstitute;
using ResourceManager;

namespace GitUITests.LeftPanel;

public class GitRefMenuItemsTest : IDisposable
{
    private const int _expectedMenuItems = 7;
    private const int _expectedTotal = _expectedMenuItems + 1; // + end separator
    private readonly IMenuItemFactory _factory = null!;
    private readonly Queue<ToolStripMenuItem> _factoryQueue = new();
    private readonly TestBranchNode _testNode = new();

    public GitRefMenuItemsTest()
    {
        _factory = Substitute.For<IMenuItemFactory>();
        _factory.CreateMenuItem<ToolStripMenuItem, TestBranchNode>(
                Arg.Do<Action<TestBranchNode>>(arg => _factoryQueue.Peek().Click += (sender, e) => arg(_testNode)),
                Arg.Any<TranslationString>(),
                Arg.Any<TranslationString>(),
                Arg.Any<Bitmap>())
            .Returns(_ => _factoryQueue.Dequeue());

        Enumerable.Range(0, _expectedMenuItems).ForEach(_ => _factoryQueue.Enqueue(new ToolStripMenuItem()));
    }

    void IDisposable.Dispose()
    {
        while (_factoryQueue.Count > 0)
        {
            _factoryQueue.Dequeue().Dispose();
        }
    }

    [StaFact]
    public void WithInactiveLocalBranch_HasAllMenuItems()
    {
        // Arrange
        var group = CreateGenerator(_factory);
        new LocalBranchMenuItemsStrings().ApplyTo(group.Strings);
        WithRefMenu_HasAllItems(group);
    }

    [StaFact]
    public void WithRemoteBranch_HasAllMenuItems()
    {
        // Arrange
        var group = CreateGenerator(_factory);
        new RemoteBranchMenuItemsStrings().ApplyTo(group.Strings);
        WithRefMenu_HasAllItems(group);
    }

    [StaFact]
    public void WithTagNode_HasAllMenuItems()
    {
        // Arrange
        var group = CreateGenerator(_factory);
        new TagMenuItemsStrings().ApplyTo(group.Strings);

        // mock rename to keep the test simple
        group.Strings.Tooltips[MenuItemKey.Rename] = new TranslationString("Rename");
        WithRefMenu_HasAllItems(group);
    }

    private void WithRefMenu_HasAllItems(MenuItemsGenerator<TestBranchNode> group)
    {
        // Act
        ToolStripItemWithKey[] menuItems = [.. group];
        _factoryQueue.Should().BeEmpty();
        _expectedTotal.Should().Be(menuItems.Length);
        var testIndex = 0;
        AssertItem(menuItems[testIndex++], nameof(TestBranchNode.Checkout));
        AssertItem(menuItems[testIndex++], nameof(TestBranchNode.Merge));
        AssertItem(menuItems[testIndex++], nameof(TestBranchNode.Rebase));
        AssertItem(menuItems[testIndex++], nameof(TestBranchNode.CreateBranch));
        AssertItem(menuItems[testIndex++], nameof(TestBranchNode.Reset));
        menuItems[testIndex++].Item.Should().BeOfType<ToolStripSeparator>();
        AssertItem(menuItems[testIndex++], nameof(TestBranchNode.Rename));
        AssertItem(menuItems[testIndex++], nameof(TestBranchNode.Delete));
    }

    [StaFact]
    public void WithCurrentLocalBranch_HavingDisabledItems()
    {
        // Arrange
        LocalBranchMenuItems<TestBranchNode> generator = new(_factory);

        // Act
        const int expectedEnabled = 2; // create branch, rename
        var expectedDisabled = _expectedTotal - expectedEnabled;
        ToolStripItemWithKey[] disabledItems =
            [.. generator.Where(t => !LocalBranchMenuItems<TestBranchNode>.CurrentBranchItemKeys.Contains(t.Key))];
        expectedDisabled.Should().Be(disabledItems.Length);
        var testIndex = 0;
        AssertItem(disabledItems[testIndex++], nameof(TestBranchNode.Checkout));
        AssertItem(disabledItems[testIndex++], nameof(TestBranchNode.Merge));
        AssertItem(disabledItems[testIndex++], nameof(TestBranchNode.Rebase));
        AssertItem(disabledItems[testIndex++], nameof(TestBranchNode.Reset));
        disabledItems[testIndex++].Item.Should().BeOfType<ToolStripSeparator>();
        AssertItem(disabledItems[testIndex++], nameof(TestBranchNode.Delete));
    }

    private void AssertItem(ToolStripItemWithKey menuItem, string caption)
    {
        var item = menuItem.Item as ToolStripMenuItem;
        item!.PerformClick();
        _testNode.CallStatck.Pop().Should().Be(caption);
    }

    private static MenuItemsGenerator<TestBranchNode> CreateGenerator(IMenuItemFactory factory)
    {
        return new MenuItemsGenerator<TestBranchNode>(factory);
    }

    // can't use a substitute here because of class constraint on INode
    public class TestBranchNode : INode, IGitRefActions, ICanDelete, ICanRename
    {
        public Stack<string> CallStatck { get; } = new();

        bool ICanDelete.Delete()
        {
            return Trace();
        }

        bool ICanRename.Rename()
        {
            return Trace();
        }

        string IGitRefActions.FullPath
        {
            get => throw new NotImplementedException();
        }

        bool IGitRefActions.Checkout()
        {
            return Trace();
        }

        bool IGitRefActions.CreateBranch()
        {
            return Trace();
        }

        bool IGitRefActions.Merge()
        {
            return Trace();
        }

        bool IGitRefActions.Rebase()
        {
            return Trace();
        }

        bool IGitRefActions.Reset()
        {
            return Trace();
        }

        private bool Trace([CallerMemberName] string name = "")
        {
            CallStatck.Push(name);
            return true;
        }
    }
}
