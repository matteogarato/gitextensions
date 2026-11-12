using GitExtensions.Extensibility.Settings;

namespace GitUI.CommandsDialogs.SettingsDialog;

public interface ISettingsPageHost
{
    CheckSettingsLogic CheckSettingsLogic { get; }
    void GotoPage(SettingsPageReference settingsPageReference);

    /// <summary>
    ///     needed by ChecklistSettingsPage (TODO: needed here?).
    /// </summary>
    void SaveAll();

    /// <summary>
    ///     needed by ChecklistSettingsPage (TODO: needed here?).
    /// </summary>
    void LoadAll();
}

public class SettingsPageHostMock(CheckSettingsLogic checkSettingsLogic) : ISettingsPageHost
{
    void ISettingsPageHost.GotoPage(SettingsPageReference settingsPageReference)
    {
    }

    void ISettingsPageHost.SaveAll()
    {
    }

    void ISettingsPageHost.LoadAll()
    {
    }

    CheckSettingsLogic ISettingsPageHost.CheckSettingsLogic { get; } = checkSettingsLogic;
}
