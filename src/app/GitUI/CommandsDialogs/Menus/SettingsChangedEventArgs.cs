using GitCommands.Settings;

namespace GitUI.CommandsDialogs.Menus;

public class SettingsChangedEventArgs(string oldTranslation, CommitInfoPosition oldCommitInfoPosition)
    : EventArgs
{
    public CommitInfoPosition OldCommitInfoPosition { get; } = oldCommitInfoPosition;
    public string OldTranslation { get; } = oldTranslation;
}
