using GitExtensions.Extensibility.Git;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.CommandsDialogs;

public interface IFileStatusListContextMenuController
{
    bool ShouldHideToLocal(ContextMenuDiffToolInfo selectionInfo);
    bool ShouldShowMenuFirstToSelected(ContextMenuDiffToolInfo selectionInfo);
    bool ShouldShowMenuFirstToLocal(ContextMenuDiffToolInfo selectionInfo);
    bool ShouldShowMenuSelectedToLocal(ContextMenuDiffToolInfo selectionInfo);
}

public sealed class ContextMenuDiffToolInfo(
    GitRevision? selectedRevision = null,
    IReadOnlyList<ObjectId>? selectedItemParentRevs = null,
    bool allAreNew = false,
    bool allAreDeleted = false,
    bool firstIsParent = false,
    bool localExists = true)
{
    public GitRevision? SelectedRevision { get; } = selectedRevision;
    public IEnumerable<ObjectId>? SelectedItemParentRevs { get; } = selectedItemParentRevs;
    public bool AllAreNew { get; } = allAreNew;
    public bool AllAreDeleted { get; } = allAreDeleted;
    public bool FirstIsParent { get; } = firstIsParent;
    public bool LocalExists { get; } = localExists;
}

public class FileStatusListContextMenuController : IFileStatusListContextMenuController
{
    bool IFileStatusListContextMenuController.ShouldHideToLocal(ContextMenuDiffToolInfo selectionInfo)
    {
        return (selectionInfo.SelectedRevision?.ObjectId == ObjectId.WorkTreeId &&
                selectionInfo.SelectedItemParentRevs?.All(parentId => parentId == ObjectId.IndexId) is true)
               || (selectionInfo.SelectedRevision?.ObjectId == ObjectId.IndexId &&
                   selectionInfo.SelectedItemParentRevs?.All(parentId => parentId == ObjectId.WorkTreeId) is true);
    }

    bool IFileStatusListContextMenuController.ShouldShowMenuFirstToSelected(ContextMenuDiffToolInfo selectionInfo)
    {
        return selectionInfo.SelectedRevision is not null;
    }

    bool IFileStatusListContextMenuController.ShouldShowMenuFirstToLocal(ContextMenuDiffToolInfo selectionInfo)
    {
        return selectionInfo.SelectedRevision is not null && selectionInfo.LocalExists

                                                          // First (A) exists (Can only determine that A does not exist if A is parent and B is new)
                                                          && (!selectionInfo.FirstIsParent || !selectionInfo.AllAreNew)

                                                          // First (A) is not local
                                                          && (selectionInfo.SelectedItemParentRevs is null ||
                                                              !selectionInfo.SelectedItemParentRevs.Contains(
                                                                  ObjectId.WorkTreeId));
    }

    bool IFileStatusListContextMenuController.ShouldShowMenuSelectedToLocal(ContextMenuDiffToolInfo selectionInfo)
    {
        return selectionInfo.SelectedRevision is not null && selectionInfo.LocalExists

                                                          // Selected (B) exists
                                                          && !selectionInfo.AllAreDeleted

                                                          // Selected (B) is not local
                                                          && selectionInfo.SelectedRevision.ObjectId !=
                                                          ObjectId.WorkTreeId;
    }
}
