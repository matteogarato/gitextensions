using System.ComponentModel.Composition;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Plugins.GitHubActionsIntegration;

[Export(typeof(IBuildServerAutoDetector))]
internal sealed class GitHubActionsAutoDetector : IBuildServerAutoDetector
{
    string IBuildServerAutoDetector.BuildServerType
    {
        get => GitHubActionsAdapter.PluginName;
    }

    bool IBuildServerAutoDetector.TryDetect(IReadOnlyList<string> remoteUrls, SettingsSource? settingsSource)
    {
        GitHubRemoteParser parser = new();

        foreach (var remoteUrl in remoteUrls)
        {
            if (parser.TryExtractGitHubDataFromRemoteUrl(remoteUrl, out var owner, out var repository))
            {
                if (settingsSource is not null)
                {
                    if (string.IsNullOrWhiteSpace(settingsSource.GetString(GitHubActionsAdapter.SettingOwner, null)))
                    {
                        settingsSource.SetString(GitHubActionsAdapter.SettingOwner, owner);
                    }

                    if (string.IsNullOrWhiteSpace(
                            settingsSource.GetString(GitHubActionsAdapter.SettingRepository, null)))
                    {
                        settingsSource.SetString(GitHubActionsAdapter.SettingRepository, repository);
                    }
                }

                return true;
            }
        }

        return false;
    }
}
