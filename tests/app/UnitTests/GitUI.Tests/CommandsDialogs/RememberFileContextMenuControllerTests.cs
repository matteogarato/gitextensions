using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using GitUI.UserControls;
using GitUIPluginInterfaces;

namespace GitUITests.CommandsDialogs;
public class RememberFileContextMenuControllerTests
{
    private RememberFileContextMenuController _rememberFileContextMenuController = null!;

    /// <summary>
    /// Mock of GitModule.GetFileBlobHash
    /// </summary>
    /// <param name="name">The git blob name</param>
    /// <param name="id">The commit id</param>
    /// <returns>The Git commit id for the item, to get a predictable output</returns>
    private static ObjectId GetFileBlobHash(string name, ObjectId id)
    {
        return id;
    }

    public RememberFileContextMenuControllerTests()
    {
        _rememberFileContextMenuController = new RememberFileContextMenuController();
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void RememberFile_ShouldEnableFirstRemember_WorkTree(bool isSubmodule, bool isDeleted, bool isSecondRev, bool result)
    {
        GitRevision rev = new(ObjectId.Random());
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: new GitRevision(ObjectId.WorkTreeId),
            item: new GitItemStatus("file1")
            {
                IsSubmodule = isSubmodule,
                IsDeleted = isDeleted
            });
        _rememberFileContextMenuController.ShouldEnableFirstItemDiff(item, isSecondRev).Should().Be(result);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void RememberFile_ShouldEnableFirstRemember_Commit(bool isSubmodule, bool isDeleted, bool isSecondRev, bool result)
    {
        GitRevision rev = new(ObjectId.Random());
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: rev,
            item: new GitItemStatus(name: "file1")
            {
                IsSubmodule = isSubmodule,
                IsDeleted = isDeleted
            });
        _rememberFileContextMenuController.ShouldEnableFirstItemDiff(item, isSecondRev).Should().Be(result);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void RememberFile_ShouldEnableSecondRemember_WorkTree(bool isSubmodule, bool isDeleted, bool isSecondRev, bool result)
    {
        GitRevision rev = new(ObjectId.Random());
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: new GitRevision(ObjectId.WorkTreeId),
            item: new GitItemStatus("file1")
            {
                IsSubmodule = isSubmodule,
                IsDeleted = isDeleted
            });
        _rememberFileContextMenuController.ShouldEnableSecondItemDiff(item, isSecondRev).Should().Be(result);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void RememberFile_ShouldEnableSecondRemember_Commit(bool isSubmodule, bool isDeleted, bool isSecondRev, bool result)
    {
        GitRevision rev = new(ObjectId.Random());
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: rev,
            item: new GitItemStatus("file1")
            {
                IsSubmodule = isSubmodule,
                IsDeleted = isDeleted
            });
        _rememberFileContextMenuController.ShouldEnableSecondItemDiff(item, isSecondRev).Should().Be(result);
    }

    [Fact]
    public void RememberFile_GetGitCommit_null()
    {
        GitRevision rev = new(ObjectId.Random());
        GitRevision workTree = new(ObjectId.WorkTreeId);

        _rememberFileContextMenuController.GetGitCommit(null, null, false).Should().BeNull();

        FileStatusItem item = new(
            firstRev: null,
            secondRev: rev,
            item: new GitItemStatus("file"));
        _rememberFileContextMenuController.GetGitCommit(null, item, false).Should().BeNull();
    }

    [Fact]
    public void RememberFile_GetGitCommit_FirstWorkTree()
    {
        GitRevision rev = new(ObjectId.Random());
        GitRevision workTree = new(ObjectId.WorkTreeId);
        string name = "WorkTreeFile";
        FileStatusItem item = new(
            firstRev: workTree,
            secondRev: rev,
            item: new GitItemStatus(name));
        _rememberFileContextMenuController.GetGitCommit(null, item, false).Should().Be(name);
    }

    [Fact]
    public void RememberFile_GetGitCommit_SecondWorkTree()
    {
        GitRevision rev = new(ObjectId.Random());
        GitRevision workTree = new(ObjectId.WorkTreeId);
        string name = "WorkTreeFile";
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: workTree,
            item: new GitItemStatus(name));
        _rememberFileContextMenuController.GetGitCommit(null, item, true).Should().Be(name);
    }

    [Fact]
    public void RememberFile_GetGitCommit_Index_Tree()
    {
        GitRevision rev = new(ObjectId.Random());
        GitRevision index = new(ObjectId.IndexId);
        const string name = "File";
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: index,
            item: new GitItemStatus(name) { TreeId = ObjectId.Random() });
        _rememberFileContextMenuController.GetGitCommit(GetFileBlobHash, item, true).Should().Be(ObjectId.IndexId.ToString());
    }

    [Fact]
    public void RememberFile_GetGitCommit_Index_GetBlob()
    {
        GitRevision rev = new(ObjectId.Random());
        GitRevision index = new(ObjectId.IndexId);
        const string name = "File";
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: index,
            item: new GitItemStatus(name) { TreeId = default });
        _rememberFileContextMenuController.GetGitCommit(GetFileBlobHash, item, true).Should().Be(ObjectId.IndexId.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RememberFile_GetGitCommit_Commit(bool isSecondRev)
    {
        ObjectId id = ObjectId.Random();
        GitRevision rev = new(id);
        const string newName = "newName";
        const string oldName = "oldName";
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: rev,
            item: new GitItemStatus(name: newName)
            {
                OldName = oldName
            });
        string expected = $"{id}:{(isSecondRev ? newName : oldName)}";
        _rememberFileContextMenuController.GetGitCommit(null, item, isSecondRev).Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RememberFile_GetGitCommit_CommitNoOld(bool isSecondRev)
    {
        ObjectId id = ObjectId.Random();
        GitRevision rev = new(id);
        const string newName = "newName";
        FileStatusItem item = new(
            firstRev: rev,
            secondRev: rev,
            item: new GitItemStatus(name: newName));
        string expected = $"{id}:{newName}";
        _rememberFileContextMenuController.GetGitCommit(null, item, isSecondRev).Should().Be(expected);
    }
}
