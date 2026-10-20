using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using GitUI.UserControls;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitUITests.CommandsDialogs;

[Culture("en-US")]
public class RevisionDiffControllerTests
{
    private IGitModule _module = null!;
    private IFullPathResolver _fullPathResolver = null!;
    private RevisionDiffController _controller = null!;

    public RevisionDiffControllerTests()
    {
        _module = Substitute.For<IGitModule>();
        _fullPathResolver = Substitute.For<IFullPathResolver>();
        _controller = new(() => _module, _fullPathResolver);
    }

    [Fact]
    public void SaveFiles_should_throw_if_files_null()
    {
        ((Action)(() => _controller.SaveFiles(files: null!, userSelection: null!))).Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'files')");
    }

    [Fact]
    public void SaveFiles_should_not_throw_if_userSelection_null_when_files_empty()
    {
        List<FileStatusItem> files = [];

        ((Action)(() => _controller.SaveFiles(files, userSelection: null!))).Should().NotThrow();
    }

    [Fact]
    public void SaveFiles_should_throw_if_userSelection_null_when_files_notnull()
    {
        List<FileStatusItem> files =
        [
            new(default, new(ObjectId.Random()), new(""))
        ];

        ((Action)(() => _controller.SaveFiles(files, userSelection: null!))).Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'userSelection')");
    }

    [Fact]
    public void SaveFiles_should_not_save_single_file_if_selection_cancelled()
    {
        FileStatusItem item = new(default, new(ObjectId.Random()), new(""));
        List<FileStatusItem> files =
        [
            item
        ];

        // User cancelled the dialog
        Func<string, string?> userSelection = (_) => null;

        _controller.SaveFiles(files, userSelection);

        _fullPathResolver.Received(1).Resolve(item.Item.Name);
        _module.Received(0).SaveBlobAs(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveFiles_should_save_single_file()
    {
        FileStatusItem item = new(default, new(ObjectId.Random()), new(""));
        List<FileStatusItem> files =
        [
            item
        ];

        Func<string, string?> userSelection = (_) => "c:\\temp\\file.txt";

        _controller.SaveFiles(files, userSelection);

        _fullPathResolver.Received(1).Resolve(item.Item.Name);
        _module.Received(1).SaveBlobAs("c:\\temp\\file.txt", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveFiles_should_not_save_multi_files_if_selection_cancelled()
    {
        FileStatusItem item1 = new(default, new(ObjectId.Random()), new("item1"));
        FileStatusItem item2 = new(default, new(ObjectId.Random()), new("item2"));
        List<FileStatusItem> files =
        [
            item1,
            item2
        ];

        // User cancelled the dialog
        Func<string, string?> userSelection = (_) => null;

        _controller.SaveFiles(files, userSelection);

        _fullPathResolver.Received(1).Resolve(".");
        _fullPathResolver.Received(0).Resolve(item1.Item.Name);
        _fullPathResolver.Received(0).Resolve(item2.Item.Name);
        _module.Received(0).SaveBlobAs(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("c:\\temp")]
    [InlineData("c:\\temp\\")]
    public void SaveFiles_should_save_multi_files_same_folder(string targetFolder)
    {
        FileStatusItem item1 = new(default, new(ObjectId.Random()), new("item1"));
        FileStatusItem item2 = new(default, new(ObjectId.Random()), new("item2"));
        FileStatusItem item3 = new(default, new(ObjectId.Random()), new("item3"));
        List<FileStatusItem> files =
        [
            item3,
            item1,
            item2,
        ];

        _fullPathResolver.Resolve(".").Returns(x => "c:\\temp\\");
        _fullPathResolver.Resolve(item1.Item.Name).Returns(x => "c:\\temp\\item1.txt");
        _fullPathResolver.Resolve(item2.Item.Name).Returns(x => "c:\\temp\\folder1\\item2.txt");
        _fullPathResolver.Resolve(item3.Item.Name).Returns(x => "c:\\temp\\folder1\\folder2\\item3.txt");

        Func<string, string?> userSelection = (_) => targetFolder;

        _controller.SaveFiles(files, userSelection);

        _fullPathResolver.Received(1).Resolve(".");
        _fullPathResolver.Received(1).Resolve(item1.Item.Name);
        _fullPathResolver.Received(1).Resolve(item2.Item.Name);
        _fullPathResolver.Received(1).Resolve(item3.Item.Name);
        _module.ReceivedWithAnyArgs(3).SaveBlobAs(default!, default!, Arg.Any<CancellationToken>());
        _module.Received(1).SaveBlobAs("c:\\temp\\item1.txt", Arg.Any<string>(), Arg.Any<CancellationToken>());
        _module.Received(1).SaveBlobAs("c:\\temp\\folder1\\item2.txt", Arg.Any<string>(), Arg.Any<CancellationToken>());
        _module.Received(1).SaveBlobAs("c:\\temp\\folder1\\folder2\\item3.txt", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("c:\\myproject\\src")]
    [InlineData("c:\\myproject\\src\\")]
    public void SaveFiles_should_save_multi_files_different_folder(string targetFolder)
    {
        FileStatusItem item1 = new(default, new(ObjectId.Random()), new("item1"));
        FileStatusItem item2 = new(default, new(ObjectId.Random()), new("item2"));
        FileStatusItem item3 = new(default, new(ObjectId.Random()), new("item3"));
        List<FileStatusItem> files =
        [
            item1,
            item2,
            item3,
        ];

        _fullPathResolver.Resolve(".").Returns(x => "c:\\temp\\");
        _fullPathResolver.Resolve(item1.Item.Name).Returns(x => "c:\\temp\\item1.txt");
        _fullPathResolver.Resolve(item2.Item.Name).Returns(x => "c:\\temp\\folder1\\item2.txt");
        _fullPathResolver.Resolve(item3.Item.Name).Returns(x => "c:\\temp\\folder1\\folder2\\item3.txt");

        Func<string, string?> userSelection = (_) => targetFolder;

        _controller.SaveFiles(files, userSelection);

        _fullPathResolver.Received(1).Resolve(".");
        _fullPathResolver.Received(1).Resolve(item1.Item.Name);
        _fullPathResolver.Received(1).Resolve(item2.Item.Name);
        _fullPathResolver.Received(1).Resolve(item3.Item.Name);
        _module.ReceivedWithAnyArgs(3).SaveBlobAs(default!, default!, Arg.Any<CancellationToken>());
        _module.Received(1).SaveBlobAs("c:\\myproject\\src\\item1.txt", Arg.Any<string>(), Arg.Any<CancellationToken>());
        _module.Received(1).SaveBlobAs("c:\\myproject\\src\\folder1\\item2.txt", Arg.Any<string>(), Arg.Any<CancellationToken>());
        _module.Received(1).SaveBlobAs("c:\\myproject\\src\\folder1\\folder2\\item3.txt", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static ContextMenuSelectionInfo CreateContextMenuSelectionInfo(
        GitRevision? selectedRevision = null,
        RelativePath? relativePath = null,
        bool isDisplayOnlyDiff = false,
        bool isStatusOnly = false,
        int selectedGitItemCount = 1,
        bool isAnyItemIndex = false,
        bool isAnyItemWorkTree = false,
        bool isBareRepository = false,
        bool allFilesExist = true,
        bool allDirectoriesExist = false,
        bool allFilesOrUntrackedDirectoriesExist = false,
        bool isAnyTracked = true,
        bool supportPatches = true,
        bool isDeleted = false,
        bool isAnySubmodule = false)
    {
        return new ContextMenuSelectionInfo(
            selectedRevision,
            relativePath,
            isDisplayOnlyDiff,
            isStatusOnly,
            selectedGitItemCount,
            isAnyItemIndex,
            isAnyItemWorkTree,
            isBareRepository,
            allFilesExist,
            allDirectoriesExist,
            allFilesOrUntrackedDirectoriesExist,
            isAnyTracked,
            supportPatches,
            isDeleted,
            isAnySubmodule);
    }

    #region difftool menu

    [Fact]
    public void BrowseDiff_DifftoolMenu_Default()
    {
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo();
        _controller.ShouldShowDifftoolMenus(selectionInfo).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BrowseDiff_DifftoolMenu_Selected(int t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, selectedGitItemCount: t);
        _controller.ShouldShowDifftoolMenus(selectionInfo).Should().Be(t > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_DifftoolMenu_BareRepo(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isBareRepository: t);
        _controller.ShouldShowDifftoolMenus(selectionInfo).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_DifftoolMenu_DisplayOnly(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isDisplayOnlyDiff: t);
        _controller.ShouldShowDifftoolMenus(selectionInfo).Should().Be(!t);
    }

    #endregion

    #region reset menu

    [Fact]
    public void BrowseDiff_ResetMenu_Default()
    {
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo();
        _controller.ShouldShowResetFileMenus(selectionInfo).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BrowseDiff_ResetMenu_Selected(int t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, selectedGitItemCount: t);
        _controller.ShouldShowResetFileMenus(selectionInfo).Should().Be(t > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_ResetMenu_Tracked(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isAnyTracked: t);
        _controller.ShouldShowResetFileMenus(selectionInfo).Should().Be(t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_ResetMenu_BareRepo(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isBareRepository: t);
        _controller.ShouldShowResetFileMenus(selectionInfo).Should().Be(!t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_ResetMenu_DisplayOnly(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isDisplayOnlyDiff: t);
        _controller.ShouldShowResetFileMenus(selectionInfo).Should().Be(!t);
    }

    #endregion

    #region main menu

    [Fact]
    public void BrowseDiff_MainMenus_NoSelection()
    {
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(selectedGitItemCount: 0, allFilesExist: false, isAnyTracked: false, supportPatches: false);
        _controller.ShouldShowMenuSaveAs(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuCherryPick(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuStage(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuUnstage(selectionInfo).Should().BeFalse();
        _controller.ShouldShowSubmoduleMenus(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuEditWorkingDirectoryFile(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuDeleteFile(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuCopyFileName(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuShowInFolder(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuShowInFileTree(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuFileHistory(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuBlame(selectionInfo).Should().BeFalse();
    }

    [Fact]
    public void BrowseDiff_MainMenus_Default()
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(selectedRevision: rev);
        _controller.ShouldShowMenuSaveAs(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuCherryPick(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuStage(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuUnstage(selectionInfo).Should().BeFalse();
        _controller.ShouldShowSubmoduleMenus(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuEditWorkingDirectoryFile(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuDeleteFile(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuCopyFileName(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuShowInFolder(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuShowInFileTree(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuFileHistory(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuBlame(selectionInfo).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BrowseDiff_MainMenus_SingleSelected(int t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(selectedRevision: rev, selectedGitItemCount: t);
        _controller.ShouldShowMenuSaveAs(selectionInfo).Should().Be(t != 0);
        _controller.ShouldShowMenuStage(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuUnstage(selectionInfo).Should().BeFalse();
        _controller.ShouldShowSubmoduleMenus(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuEditWorkingDirectoryFile(selectionInfo).Should().Be(t != 0);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().Be(t != 0);
        _controller.ShouldShowMenuDeleteFile(selectionInfo).Should().BeFalse();
        _controller.ShouldShowMenuCopyFileName(selectionInfo).Should().Be(t != 0);
        _controller.ShouldShowMenuShowInFolder(selectionInfo).Should().Be(t != 0);
        _controller.ShouldShowMenuShowInFileTree(selectionInfo).Should().Be(t != 0);
        _controller.ShouldShowMenuFileHistory(selectionInfo).Should().Be(t != 0);
        _controller.ShouldShowMenuBlame(selectionInfo).Should().Be(t != 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_StageMenus_WorkTree(bool t)
    {
        GitRevision rev = new(ObjectId.WorkTreeId);
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isAnyItemIndex: t);
        _controller.ShouldShowMenuUnstage(selectionInfo).Should().Be(t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_StageMenus_Index(bool t)
    {
        GitRevision rev = new(ObjectId.WorkTreeId);
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isAnyItemWorkTree: t);
        _controller.ShouldShowMenuStage(selectionInfo).Should().Be(t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_EditOpen_IsAnySubmodule(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(selectedRevision: rev, isAnySubmodule: t);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().Be(!t);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BrowseDiff_OpenRevisionFile_Commit(int t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, selectedGitItemCount: t);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().Be(t == 1);
    }

    [Fact]
    public void BrowseDiff_OpenRevisionFile_WorkTree()
    {
        GitRevision rev = new(ObjectId.WorkTreeId);
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().BeFalse();
    }

    [Fact]
    public void BrowseDiff_OpenRevisionFile_Index()
    {
        GitRevision rev = new(ObjectId.IndexId);
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().BeFalse();
    }

    [Fact]
    public void BrowseDiff_OpenRevisionFile_DisplayOnly()
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isDisplayOnlyDiff: true);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_SupportLinePatches(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, supportPatches: t);
        _controller.ShouldShowMenuSaveAs(selectionInfo).Should().BeTrue();
        _controller.ShouldShowMenuCherryPick(selectionInfo).Should().Be(t);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_DisplayOnlyDiff(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isDisplayOnlyDiff: t);
        _controller.ShouldShowMenuSaveAs(selectionInfo).Should().Be(!t);
        _controller.ShouldShowMenuOpenRevision(selectionInfo).Should().Be(!t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_StatusOnlyDiff(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isStatusOnly: t, isAnyTracked: !t);
        _controller.ShouldShowMenuCopyFileName(selectionInfo).Should().Be(!t);
        _controller.ShouldShowMenuShowInFolder(selectionInfo).Should().Be(!t);
        _controller.ShouldShowMenuShowInFileTree(selectionInfo).Should().Be(!t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_ShowInFileTree(bool t)
    {
        GitRevision rev = new(ObjectId.Random());
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isDeleted: t);
        _controller.ShouldShowMenuCopyFileName(selectionInfo).Should().Be(true);
        _controller.ShouldShowMenuShowInFolder(selectionInfo).Should().Be(true);
        _controller.ShouldShowMenuShowInFileTree(selectionInfo).Should().Be(!t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_DeleteFile_WorkTree(bool t)
    {
        GitRevision rev = new(ObjectId.WorkTreeId);
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, allFilesOrUntrackedDirectoriesExist: t);
        _controller.ShouldShowMenuDeleteFile(selectionInfo).Should().Be(t);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrowseDiff_DeleteFile_Index(bool t)
    {
        GitRevision rev = new(ObjectId.IndexId);
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, allFilesOrUntrackedDirectoriesExist: t);
        _controller.ShouldShowMenuDeleteFile(selectionInfo).Should().Be(t);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void BrowseDiff_Submodules_WorkTree(bool isAnySubmodule, bool submodulesExist, bool expected)
    {
        GitRevision rev = new(ObjectId.WorkTreeId);
        ContextMenuSelectionInfo selectionInfo = CreateContextMenuSelectionInfo(rev, isAnySubmodule: isAnySubmodule, allDirectoriesExist: submodulesExist);
        _controller.ShouldShowSubmoduleMenus(selectionInfo).Should().Be(expected);
    }

    #endregion
}
