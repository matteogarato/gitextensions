using System.ComponentModel.Composition;
using GitExtensions.Extensibility.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;
using ResourceManager;

namespace GitExtensions.Plugins.JenkinsIntegration.Settings;

[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(JenkinsAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class JenkinsSettingsUserControl : GitExtensionsControl, IBuildServerSettingsUserControl
{
    private string? _defaultProjectName;

    public JenkinsSettingsUserControl()
    {
        InitializeComponent();
        InitializeComplete();

        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
    }

    void IBuildServerSettingsUserControl.Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        _defaultProjectName = defaultProjectName;
    }

    void IBuildServerSettingsUserControl.LoadSettings(SettingsSource buildServerConfig)
    {
        JenkinsServerUrl.Text = buildServerConfig.GetString("BuildServerUrl", null);
        JenkinsProjectName.Text = buildServerConfig.GetString("ProjectName", _defaultProjectName);
        IgnoreBuildBranch.Text = buildServerConfig.GetString("IgnoreBuildBranch", null);
    }

    void IBuildServerSettingsUserControl.SaveSettings(SettingsSource buildServerConfig)
    {
        buildServerConfig.SetString("BuildServerUrl", JenkinsServerUrl.Text.NullIfEmpty());
        buildServerConfig.SetString("ProjectName", JenkinsProjectName.Text.NullIfEmpty());

        // While an empty value is valid as as override for lower level settings,
        // the behaviour requiring that the "effective" value is set is considered a worse limitation.
        buildServerConfig.SetString("IgnoreBuildBranch", IgnoreBuildBranch.Text.NullIfEmpty());
    }
}
