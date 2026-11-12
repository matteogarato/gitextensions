namespace GitExtensions.Plugins.GitlabIntegration.ApiClient;

public interface IGitlabApiClientFactory
{
    IGitlabApiClient CreateGitlabApiClient(string instanceUrl, string apiToken, int projectId);
}

internal sealed class GitlabApiClientFactory : IGitlabApiClientFactory
{
    IGitlabApiClient IGitlabApiClientFactory.CreateGitlabApiClient(string instanceUrl, string apiToken, int projectId)
    {
        return new GitlabApiClient(instanceUrl, apiToken, projectId);
    }
}
