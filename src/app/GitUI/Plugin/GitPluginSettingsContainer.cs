using GitCommands.Settings;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;

namespace GitUI.Plugin;

public class GitPluginSettingsContainer(Guid pluginId, string pluginName) : SettingsSource, IGitPluginSettingsContainer
{
    private readonly string _pluginPrefix = pluginId == Guid.Empty ? pluginName : $"{pluginId}.";
    private SettingsSource? _settingsSource;

    private SettingsSource ExternalSettings
    {
        get => _settingsSource ?? AppSettings.SettingsContainer;
    }

    public override SettingLevel SettingLevel
    {
        get => ExternalSettings.SettingLevel;
        init => throw new InvalidOperationException(nameof(SettingLevel));
    }

    SettingsSource IGitPluginSettingsContainer.GetSettingsSource()
    {
        return this;
    }

    void IGitPluginSettingsContainer.SetSettingsSource(SettingsSource? settingsSource)
    {
        _settingsSource = settingsSource;
    }

    public override string? GetValue(string name)
    {
        return ExternalSettings.GetValue($"{_pluginPrefix}{name}")
               ?? ExternalSettings.GetValue($"{pluginName}{name}");
    }

    public override void SetValue(string name, string? value)
    {
        ExternalSettings.SetValue($"{_pluginPrefix}{name}", value);
    }
}
