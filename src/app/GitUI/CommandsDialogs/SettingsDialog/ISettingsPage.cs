using GitExtensions.Extensibility.Settings;

namespace GitUI.CommandsDialogs.SettingsDialog;

public interface ISettingsPage
{
    Control? GuiControl { get; }

    /// <summary>
    ///     true if the page cannot properly react to cancel or discard.
    /// </summary>
    bool IsInstantSavePage { get; }

    SettingsPageReference PageReference { get; }
    string GetTitle();

    void OnPageShown();

    void LoadSettings();

    void SaveSettings();

    IEnumerable<string> GetSearchKeywords();
}
