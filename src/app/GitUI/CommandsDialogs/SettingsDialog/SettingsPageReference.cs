using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;

namespace GitUI.CommandsDialogs.SettingsDialog;

/// <summary>
///     Type may be a SettingsPage type or a IGitPlugin subclass type.
/// </summary>
public class SettingsPageReferenceByType(Type settingsPageType) : SettingsPageReference
{
    public Type SettingsPageType { get; } = settingsPageType;

    public override bool Equals(object? obj)
    {
        return obj is SettingsPageReferenceByType type && type.SettingsPageType == SettingsPageType;
    }

    public override int GetHashCode()
    {
        return SettingsPageType.GetHashCode();
    }

    public override string ToString()
    {
        return SettingsPageType.ToString();
    }
}

public class SettingsPageReferenceByPlugin(IGitPlugin gitPlugin) : SettingsPageReferenceByType(gitPlugin.GetType());
