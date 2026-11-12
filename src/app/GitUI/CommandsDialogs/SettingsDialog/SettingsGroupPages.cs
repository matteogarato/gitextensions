using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitUI.CommandsDialogs.SettingsDialog;

public class GitSettingsGroup() : GroupSettingsPage("Git")
{
    public static SettingsPageReference GetPageReference()
    {
        return new SettingsPageReferenceByType(typeof(GitSettingsGroup));
    }
}

public class GitExtensionsSettingsGroup() : GroupSettingsPage(AppSettings.ApplicationName)
{
    public static SettingsPageReference GetPageReference()
    {
        return new SettingsPageReferenceByType(typeof(GitExtensionsSettingsGroup));
    }
}

public class PluginsSettingsGroup() : GroupSettingsPage("Plugins")
{
    public static SettingsPageReference GetPageReference()
    {
        return new SettingsPageReferenceByType(typeof(PluginsSettingsGroup));
    }
}
