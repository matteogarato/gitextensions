using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GitExtensions.Plugins.GitHubActionsIntegration.ApiClient.Models;

namespace GitExtensions.Plugins.GitHubActionsIntegration.ApiClient;

public sealed class GitHubActionsApiClient : IGitHubActionsApiClient
{
    private const int _defaultPerPage = 100;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _repository;

    public GitHubActionsApiClient(string apiUrl, string owner, string repository, string? apiToken)
    {
        _repository = repository;

        var baseUrl = apiUrl.TrimEnd('/');

        BaseUrl = $"{baseUrl}/repos/{owner}/{_repository}";

        _httpClient = CreateHttpClient(apiToken);
    }

    string IGitHubActionsApiClient.BaseUrl { get; }

    async Task<GitHubActionsWorkflowRunsResponse> IGitHubActionsApiClient.GetWorkflowRunsAsync(
        bool running,
        DateTime? sinceDate,
        int page,
        int perPage,
        CancellationToken cancellationToken)
    {
        List<string> queryParams =
        [
            $"page={page}",
            $"per_page={Math.Min(perPage, _defaultPerPage)}"
        ];

        if (running)
        {
            queryParams.Add("status=in_progress");
        }
        else
        {
            queryParams.Add("status=completed");
        }

        if (sinceDate.HasValue)
        {
            var createdFilter = $">={sinceDate.Value:yyyy-MM-ddTHH:mm:ssZ}";
            queryParams.Add($"created={Uri.EscapeDataString(createdFilter)}");
        }

        var query = string.Join("&", queryParams);
        var url = $"{BaseUrl}/actions/runs?{query}";

        return await GetAsync<GitHubActionsWorkflowRunsResponse>(url, cancellationToken);
    }

    void IDisposable.Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException(
                    $"GitHub API returned {response.StatusCode}. Check your API token.");
            case HttpStatusCode.NotFound:
                throw new InvalidOperationException($"Repository not found at {url}. Check owner/repository settings.");
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
        Validates.NotNull(result);

        return result;
    }

    private static HttpClient CreateHttpClient(string? apiToken)
    {
        HttpClient client = new() { Timeout = TimeSpan.FromMinutes(2) };

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("User-Agent", "GitExtensions");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }

        return client;
    }
}
