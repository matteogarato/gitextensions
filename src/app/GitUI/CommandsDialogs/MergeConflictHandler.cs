using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

public static class MergeConflictHandler
{
    public static bool HandleMergeConflicts(IGitUiCommands commands, IWin32Window? owner, bool offerCommit = true,
        bool offerUpdateSubmodules = true)
    {
        if (commands.Module.InTheMiddleOfConflictedMerge())
        {
            if (MessageBoxes.ConfirmResolveMergeConflicts(owner))
            {
                SolveMergeConflicts(commands, owner, offerCommit);
            }

            return true;
        }

        if (offerUpdateSubmodules)
        {
            commands.UpdateSubmodules(owner);
        }

        return false;
    }

    private static void SolveMergeConflicts(IGitUiCommands commands, IWin32Window? owner, bool offerCommit)
    {
        if (commands.Module.InTheMiddleOfConflictedMerge())
        {
            commands.StartResolveConflictsDialog(owner, offerCommit);
        }

        if (commands.Module.InTheMiddleOfPatch())
        {
            if (MessageBoxes.MiddleOfPatchApply(owner))
            {
                commands.StartApplyPatchDialog(owner);
            }
        }
        else if (commands.Module.InTheMiddleOfRebase())
        {
            if (MessageBoxes.MiddleOfRebase(owner))
            {
                commands.StartTheContinueRebaseDialog(owner);
            }
        }
    }
}
