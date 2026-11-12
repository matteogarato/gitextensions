using GitExtensions.Extensibility.Settings;

namespace GitCommands.Settings;

public static class SettingsSourceExtension
{
    public static IDetachedSettings Detached(this SettingsSource settingsSource)
    {
        return new DetachedSettings(settingsSource);
    }
}
