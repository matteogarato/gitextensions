using System.ComponentModel.Composition;
using GitCommands.Git;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;
using ResourceManager;

namespace GitExtensions.Plugins.GitHubActionsIntegration.Settings;

[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(GitHubActionsAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class GitHubActionsSettingsUserControl : GitExtensionsControl, IBuildServerSettingsUserControl
{
    private const string _defaultApiUrl = "https://api.github.com";

    private readonly GitHubRemoteParser _remoteParser = new();

    public GitHubActionsSettingsUserControl()
    {
        InitializeComponent();
        InitializeComplete();
    }

    void IBuildServerSettingsUserControl.Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        foreach (var remote in remotes)
        {
            if (remote is not null
                && _remoteParser.TryExtractGitHubDataFromRemoteUrl(remote, out var owner, out var repository))
            {
                txtOwner.Text = owner;
                txtRepository.Text = repository;
                break;
            }
        }
    }

    void IBuildServerSettingsUserControl.LoadSettings(SettingsSource buildServerConfig)
    {
        var apiUrl = buildServerConfig.GetString(GitHubActionsAdapter.SettingApiUrl, null);
        var owner = buildServerConfig.GetString(GitHubActionsAdapter.SettingOwner, null);
        var repository = buildServerConfig.GetString(GitHubActionsAdapter.SettingRepository, null);
        var apiToken = buildServerConfig.GetString(GitHubActionsAdapter.SettingApiToken, null);

        txtApiUrl.Text = apiUrl ?? _defaultApiUrl;

        if (!string.IsNullOrWhiteSpace(owner))
        {
            txtOwner.Text = owner;
        }

        if (!string.IsNullOrWhiteSpace(repository))
        {
            txtRepository.Text = repository;
        }

        txtApiToken.Text = apiToken;
    }

    void IBuildServerSettingsUserControl.SaveSettings(SettingsSource buildServerConfig)
    {
        var apiUrl = txtApiUrl.Text.Trim().TrimEnd('/').NullIfEmpty();
        if (string.Equals(apiUrl, _defaultApiUrl, StringComparison.OrdinalIgnoreCase))
        {
            apiUrl = null;
        }

        buildServerConfig.SetString(GitHubActionsAdapter.SettingApiUrl, apiUrl);
        buildServerConfig.SetString(GitHubActionsAdapter.SettingOwner, txtOwner.Text.NullIfEmpty());
        buildServerConfig.SetString(GitHubActionsAdapter.SettingRepository, txtRepository.Text.NullIfEmpty());
        buildServerConfig.SetString(GitHubActionsAdapter.SettingApiToken, txtApiToken.Text.NullIfEmpty());
    }

    private void lnkTokenManagement_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        OsShellUtil.OpenUrlInDefaultBrowser("https://github.com/settings/personal-access-tokens/new");
    }
}
