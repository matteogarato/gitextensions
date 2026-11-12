using System.ComponentModel.Composition;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Plugins.AzureDevOpsIntegration;

[Export(typeof(IBuildServerAutoDetector))]
internal sealed class AzureDevOpsAutoDetector : IBuildServerAutoDetector
{
    string IBuildServerAutoDetector.BuildServerType
    {
        get => AzureDevOpsAdapter.PluginName;
    }

    bool IBuildServerAutoDetector.TryDetect(IReadOnlyList<string> remoteUrls, SettingsSource? settingsSource)
    {
        AzureDevOpsRemoteParser parser = new();

        foreach (var remoteUrl in remoteUrls)
        {
            if (parser.TryExtractAzureDevopsDataFromRemoteUrl(remoteUrl, out var owner, out var project,
                    out var repository))
            {
                if (settingsSource is not null)
                {
                    if (string.IsNullOrWhiteSpace(settingsSource.GetString("ProjectUrl", null)))
                    {
                        var projectUrl = AzureDevOpsRemoteParser.BuildProjectUrl(remoteUrl, owner, project);
                        if (projectUrl is not null)
                        {
                            settingsSource.SetString("ProjectUrl", projectUrl);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(settingsSource.GetString("RepositoryName", null)))
                    {
                        settingsSource.SetString("RepositoryName", repository);
                    }
                }

                return true;
            }
        }

        return false;
    }
}
