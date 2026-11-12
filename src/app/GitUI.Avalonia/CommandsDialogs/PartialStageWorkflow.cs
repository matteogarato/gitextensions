using GitCommands;
using GitCommands.Patches;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Builds and applies selected-line patches for supported working-tree changes.
/// </summary>
public static class PartialStageWorkflow
{
    /// <summary>
    ///     Determines whether the selected working-tree item supports partial staging.
    /// </summary>
    public static bool CanStage(GitItemStatus? item)
    {
        return IsPlainTrackedTextChange(item, StagedStatus.WorkTree);
    }

    /// <summary>
    ///     Determines whether the selected staged item supports partial unstaging.
    /// </summary>
    public static bool CanUnstage(GitItemStatus? item)
    {
        return IsPlainTrackedTextChange(item, StagedStatus.Index);
    }

    private static bool IsPlainTrackedTextChange(GitItemStatus? item, StagedStatus staged)
    {
        return item is { Staged: var itemStaged } && itemStaged == staged && item is
        {
            IsTracked: true,
            IsChanged: true,
            IsNew: false,
            IsDeleted: false,
            IsCopied: false,
            IsUnmerged: false,
            IsSubmodule: false,
            IsStatusOnly: false
        };
    }

    /// <summary>
    ///     Creates a patch for selected changed lines of an unstaged (to stage) or staged (to unstage) item, or
    ///     <see langword="null" /> when the selection contains no changes.
    /// </summary>
    public static byte[]? CreatePatch(IGitModule module, GitItemStatus item, string diff, int selectionPosition,
        int selectionLength)
    {
        var unstage = CanUnstage(item);
        if (!unstage && !CanStage(item))
        {
            return null;
        }

        return PatchManager.GetSelectedLinesAsPatch(
            diff,
            selectionPosition,
            selectionLength,
            unstage,
            module.FilesEncoding,
            false,
            false,
            false);
    }

    /// <summary>
    ///     Creates a patch that discards selected changed lines of an item from the working tree, or from the index and
    ///     working tree when the item is staged.
    /// </summary>
    public static byte[]? CreateDiscardPatch(IGitModule module, GitItemStatus item, string diff, int selectionPosition,
        int selectionLength)
    {
        if (CanUnstage(item))
        {
            return PatchManager.GetSelectedLinesAsPatch(
                diff,
                selectionPosition,
                selectionLength,
                true,
                module.FilesEncoding,
                true,
                false,
                item.IsRenamed);
        }

        return CanStage(item)
            ? PatchManager.GetResetWorkTreeLinesAsPatch(diff, selectionPosition, selectionLength, module.FilesEncoding)
            : null;
    }

    /// <summary>
    ///     Applies a patch created by <see cref="CreateDiscardPatch" /> to the working tree (and the index for staged items).
    /// </summary>
    public static ExecutionResult ApplyDiscardPatch(IGitModule module, byte[] patch, bool staged)
    {
        GitArgumentBuilder arguments = new("apply") { "--whitespace=nowarn", { staged, "--reverse --index" } };

        return module.GitExecutable.Execute(
            arguments,
            inputWriter => inputWriter.BaseStream.Write(patch),
            throwOnErrorExit: false);
    }

    /// <summary>
    ///     Applies a selected-line patch to the index and returns Git's result.
    /// </summary>
    /// <param name="module">The repository.</param>
    /// <param name="patch">The patch created by <see cref="CreatePatch" />.</param>
    /// <param name="unstage">Whether the patch is applied in reverse to take the lines out of the index.</param>
    public static ExecutionResult ApplyPatch(IGitModule module, byte[] patch, bool unstage = false)
    {
        GitArgumentBuilder arguments = new("apply")
        {
            "--cached", "--index", "--whitespace=nowarn", { unstage, "--reverse" }
        };

        return module.GitExecutable.Execute(
            arguments,
            inputWriter => inputWriter.BaseStream.Write(patch),
            throwOnErrorExit: false);
    }
}
