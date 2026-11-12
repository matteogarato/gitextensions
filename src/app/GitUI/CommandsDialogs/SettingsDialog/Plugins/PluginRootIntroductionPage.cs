using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitUI.CommandsDialogs.SettingsDialog.Plugins;

public partial class PluginRootIntroductionPage : SettingsPageBase
{
    public PluginRootIntroductionPage(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        InitializeComponent();
        InitializeComplete();
    }

    protected override SettingsSource GetCurrentSettings()
    {
        return AppSettings.SettingsContainer;
    }
}
