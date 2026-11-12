using System.Diagnostics;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls;

namespace GitUI.CommandsDialogs;

internal interface IRevisionDiffController
{
    void SaveFiles(List<FileStatusItem> files, Func<string, string?> userSelection);
    bool ShouldShowMenuBlame(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuCherryPick(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuEditWorkingDirectoryFile(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuOpenRevision(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuDeleteFile(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuMove(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowResetFileMenus(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuFileHistory(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuSaveAs(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuCopyFileName(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuShowInFolder(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuShowInFileTree(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuStage(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowMenuUnstage(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowSubmoduleMenus(ContextMenuSelectionInfo selectionInfo);
    bool ShouldShowDifftoolMenus(ContextMenuSelectionInfo selectionInfo);
}

internal sealed class RevisionDiffController(Func<IGitModule> getModule, IFullPathResolver fullPathResolver)
    : IRevisionDiffController
{
    private readonly IFullPathResolver _fullPathResolver = fullPathResolver;
    private readonly Func<IGitModule> _getModule = getModule;

    void IRevisionDiffController.SaveFiles(List<FileStatusItem> files, Func<string, string?> userSelection)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(userSelection);

        if (files.Count > 1)
        {
            SaveMultipleFiles(files);
            return;
        }

        SaveSingleFile(files[0]);
        return;

        void SaveMultipleFiles(List<FileStatusItem> selectedFiles)
        {
            var longestCommonPath = GetLongestCommonPath(selectedFiles);
            var baseSourceDirectory = _fullPathResolver
                .Resolve(longestCommonPath.Length == 0 ? "." : longestCommonPath).EnsureTrailingPathSeparator();
            if (baseSourceDirectory is null)
            {
                throw new ExternalOperationException(innerException: new DirectoryNotFoundException(
                    @$"Could not resolve common relative path ""{longestCommonPath}"" in ""{_getModule().WorkingDir}""."));
            }

            var selectedPath = userSelection(baseSourceDirectory);
            if (selectedPath is null)
            {
                // User has cancelled the selection
                return;
            }

            Uri baseSourceDirectoryUri = new(baseSourceDirectory!);

            foreach (var item in selectedFiles)
            {
                var selectedItemFullName = _fullPathResolver.Resolve(item.Item.Name);
                var selectedItemSourceDirectory =
                    Path.GetDirectoryName(selectedItemFullName).EnsureTrailingPathSeparator();

                string targetDirectory;
                if (selectedItemSourceDirectory == baseSourceDirectory)
                {
                    targetDirectory = selectedPath;
                }
                else
                {
                    Uri selectedItemUri = new(selectedItemSourceDirectory!);
                    targetDirectory = Path.Combine(selectedPath,
                        baseSourceDirectoryUri.MakeRelativeUri(selectedItemUri).OriginalString);
                }

                // TODO: check target file exists.
                // TODO: allow cancel the whole sequence

                Directory.CreateDirectory(targetDirectory);
                var targetFileName =
                    Path.Join(targetDirectory, Path.GetFileName(selectedItemFullName)!).ToNativePath();
                Debug.WriteLine($"Saving {selectedItemFullName} --> {targetFileName}");

                GetModule().SaveBlobAs(targetFileName, $"{item.SecondRevision.Guid}:\"{item.Item.Name}\"");
            }

            return;

            static string GetLongestCommonPath(List<FileStatusItem> files)
            {
                var firstFile = files[0].Item.Name;
                for (var length = files.Min(f => f.Item.Path.Length) + 1; length > 0; --length)
                {
                    var possibleMatch = firstFile[..length];
                    if (files.All(f => f.Item.Name.StartsWith(possibleMatch)))
                    {
                        return Path.GetDirectoryName(possibleMatch)!;
                    }
                }

                return string.Empty;
            }
        }

        void SaveSingleFile(FileStatusItem item)
        {
            var fullName = _fullPathResolver.Resolve(item.Item.Name);
            var selectedFileName = userSelection(fullName!);
            if (selectedFileName is null)
            {
                // User has cancelled the selection
                return;
            }

            GetModule().SaveBlobAs(selectedFileName, $"{item.SecondRevision.Guid}:\"{item.Item.Name}\"");
        }
    }

    // The enabling of menu items is related to how the actions have been implemented

    bool IRevisionDiffController.ShouldShowDifftoolMenus(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SelectedGitItemCount > 0 && !selectionInfo.IsDisplayOnlyDiff;
    }

    bool IRevisionDiffController.ShouldShowResetFileMenus(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SelectedGitItemCount > 0
               && !selectionInfo.IsBareRepository
               && selectionInfo.IsAnyTracked
               && !selectionInfo.IsDisplayOnlyDiff
               && !(selectionInfo.IsAnySubmodule && selectionInfo.SelectedGitItemCount == 1 &&
                    selectionInfo.IsAnyItemWorkTree);
    }

    private IGitModule GetModule()
    {
        return _getModule() ?? throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");
    }

    #region Main menu items

    bool IRevisionDiffController.ShouldShowMenuSaveAs(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SelectedGitItemCount > 0 && !selectionInfo.IsAnySubmodule
                                                      && !(selectionInfo.SelectedRevision?.IsArtificial ?? false)
                                                      && !selectionInfo.IsDisplayOnlyDiff;
    }

    bool IRevisionDiffController.ShouldShowMenuCherryPick(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SupportPatches && !selectionInfo.IsAnyItemWorkTree;
    }

    // Stage/unstage must limit the selected items, IsStaged is not reflecting Staged status
    bool IRevisionDiffController.ShouldShowMenuStage(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.IsAnyItemWorkTree;
    }

    bool IRevisionDiffController.ShouldShowMenuUnstage(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.IsAnyItemIndex;
    }

    bool IRevisionDiffController.ShouldShowSubmoduleMenus(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.IsAnySubmodule && selectionInfo.SelectedRevision?.ObjectId == ObjectId.WorkTreeId &&
               selectionInfo.AllDirectoriesExist;
    }

    bool IRevisionDiffController.ShouldShowMenuEditWorkingDirectoryFile(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SelectedGitItemCount == 1 && selectionInfo.AllFilesExist;
    }

    bool IRevisionDiffController.ShouldShowMenuDeleteFile(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.AllFilesOrUntrackedDirectoriesExist &&
               (selectionInfo.SelectedRevision?.IsArtificial ?? false);
    }

    bool IRevisionDiffController.ShouldShowMenuMove(ContextMenuSelectionInfo selectionInfo)
    {
        return (selectionInfo.SelectedGitItemCount == 1 && selectionInfo.IsAnyTracked && !selectionInfo.IsAnySubmodule)
               || selectionInfo.SelectedFolder is not null;
    }

    bool IRevisionDiffController.ShouldShowMenuOpenRevision(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SelectedGitItemCount == 1
               && !selectionInfo.IsAnySubmodule
               && !selectionInfo.IsDisplayOnlyDiff
               && !(selectionInfo.SelectedRevision?.IsArtificial ?? false);
    }

    bool IRevisionDiffController.ShouldShowMenuCopyFileName(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SelectedGitItemCount > 0 && !selectionInfo.IsStatusOnly;
    }

    bool IRevisionDiffController.ShouldShowMenuShowInFolder(ContextMenuSelectionInfo selectionInfo)
    {
        return selectionInfo.SelectedGitItemCount > 0 && !selectionInfo.IsStatusOnly;
    }

    bool IRevisionDiffController.ShouldShowMenuShowInFileTree(ContextMenuSelectionInfo selectionInfo)
    {
        return (selectionInfo.SelectedGitItemCount == 1 && selectionInfo.IsAnyTracked && !selectionInfo.IsDeleted)
               || selectionInfo.SelectedFolder is not null;
    }

    bool IRevisionDiffController.ShouldShowMenuFileHistory(ContextMenuSelectionInfo selectionInfo)
    {
        return (selectionInfo.SelectedGitItemCount == 1 ||
                !string.IsNullOrEmpty(selectionInfo.SelectedFolder?.Value)) && selectionInfo.IsAnyTracked;
    }

    bool IRevisionDiffController.ShouldShowMenuBlame(ContextMenuSelectionInfo selectionInfo)
    {
        return ShouldShowMenuFileHistory(selectionInfo) && !selectionInfo.IsAnySubmodule &&
               selectionInfo.SelectedFolder is null;
    }

    #endregion
}
