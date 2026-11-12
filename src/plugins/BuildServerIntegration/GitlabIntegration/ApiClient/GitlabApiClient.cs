using System.Collections.Specialized;
using System.Web;
using GitExtensions.Plugins.GitlabIntegration.ApiClient.Models;

namespace GitExtensions.Plugins.GitlabIntegration.ApiClient;

public interface IGitlabApiClient : IDisposable
{
    string InstanceUrl { get; }

    Task<PagedResponse<GitlabPipeline>> GetPipelinesAsync(DateTime? sinceDate, bool running, int pageNumber,
        CancellationToken cancellationToken);
}

public class GitlabApiClient(string instanceUrl, string apiToken, int projectId = 0)
    : GitlabApiClientBase(instanceUrl, apiToken), IGitlabApiClient
{
    private const int _pageSize = 100;

    async Task<PagedResponse<GitlabPipeline>> IGitlabApiClient.GetPipelinesAsync(DateTime? sinceDate, bool running,
        int pageNumber, CancellationToken cancellationToken)
    {
        UriBuilder pipelinesUriBuilder = new($"{InstanceUrl}/api/v4/projects/{projectId}/pipelines");
        var query = HttpUtility.ParseQueryString(pipelinesUriBuilder.Query);

        if (sinceDate is not null)
        {
            query["updated_after"] = sinceDate.Value.ToString("u");
        }

        if (running)
        {
            query["scope"] = "running";
        }
        else
        {
            query["scope"] = "finished";
        }

        query["page"] = pageNumber.ToString();
        query["per_page"] = _pageSize.ToString();

        pipelinesUriBuilder.Query = query.ToString() ?? string.Empty;

        return await LoadListAsync<GitlabPipeline>(pipelinesUriBuilder.Uri, cancellationToken);
    }

    public async Task<GitlabProject?> GetProjectAsync(string projectNamespace, string projectName)
    {
        UriBuilder projectUriBuilder =
            new($"{InstanceUrl}/api/v4/projects/{Uri.EscapeDataString($"{projectNamespace}/{projectName}")}");

        return await LoadItemAsync<GitlabProject?>(projectUriBuilder.Uri);
    }
}
