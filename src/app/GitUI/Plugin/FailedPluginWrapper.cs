using System.Diagnostics;
using System.Text.RegularExpressions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitUI.Properties;

namespace GitUI.Plugin;

internal partial class FailedPluginWrapper : IGitPlugin
{
    private readonly string _exception;
    private readonly string _pluginName = null!;

    public FailedPluginWrapper(Exception loadingException)
    {
        ArgumentNullException.ThrowIfNull(loadingException);

        _exception = loadingException.Demystify().ToString();
        try
        {
            // Try to extract plugin name from exception
            var match = PluginNameRegex.Match(_exception);
            if (match.Success)
            {
                _pluginName = match.Value;
                Name = $"{TranslatedStrings.FailedToLoadPlugin}: {_pluginName[..Math.Min(50, match.ValueSpan.Length)]}";
            }
        }
        catch (Exception)
        {
            // no-op: best effort
        }
    }

    [GeneratedRegex(@"""GitExtensions.([^""]+)""", RegexOptions.ExplicitCapture)]
    private static partial Regex PluginNameRegex { get; }

    private string? Name { get; } = TranslatedStrings.FailedToLoadPlugin;

    Guid IGitPlugin.Id { get; } = Guid.NewGuid();

    string? IGitPlugin.Name
    {
        get => Name;
    }

    string? IGitPlugin.Description { get; } = TranslatedStrings.FailedToLoadPlugin;
    Image? IGitPlugin.Icon { get; } = Resources.bug;
    IGitPluginSettingsContainer? IGitPlugin.SettingsContainer { get; set; }
    bool IGitPlugin.HasSettings { get; } = false;

    bool IGitPlugin.Execute(GitUiEventArgs args)
    {
        var result = MessageBoxes.Show(
            string.Format(TranslatedStrings.FailedToLoadPluginPopupText, _exception),
            TranslatedStrings.FailedToLoadPlugin, MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
        if (result == DialogResult.OK)
        {
            Clipboard.SetText(_exception);
        }

        return false;
    }

    IEnumerable<ISetting> IGitPlugin.GetSettings()
    {
        return [];
    }

    void IGitPlugin.Register(IGitUiCommands gitUiCommands)
    {
    }

    void IGitPlugin.Unregister(IGitUiCommands gitUiCommands)
    {
    }
}
