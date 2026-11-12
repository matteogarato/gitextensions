using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Extensibility.Plugins;

public interface IGitPlugin
{
    Guid Id { get; }

    string? Name { get; }

    string? Description { get; }

    Image? Icon { get; }

    IGitPluginSettingsContainer? SettingsContainer { get; set; }

    bool HasSettings { get; }

    IEnumerable<ISetting> GetSettings();

    void Register(IGitUiCommands gitUiCommands);

    void Unregister(IGitUiCommands gitUiCommands);

    /// <summary>
    ///     Runs the plugin and returns whether the RevisionGrid should be refreshed.
    /// </summary>
    bool Execute(GitUiEventArgs args);
}
