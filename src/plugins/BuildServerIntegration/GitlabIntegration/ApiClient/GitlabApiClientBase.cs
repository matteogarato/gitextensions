using System.Net;
using System.Text.Json;
using GitExtensions.Plugins.GitlabIntegration.ApiClient.Models;

namespace GitExtensions.Plugins.GitlabIntegration.ApiClient;

public abstract class GitlabApiClientBase(string instanceUrl, string apiToken) : IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient = InitClient(instanceUrl, apiToken);

    public string InstanceUrl { get; } = instanceUrl;

    void IDisposable.Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private static HttpClient InitClient(string instanceUrl, string apiToken)
    {
        HttpClient? client = new() { BaseAddress = new Uri(instanceUrl, UriKind.RelativeOrAbsolute) };
        client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", apiToken);

        return client;
    }

    private async Task<HttpResponseMessage> HttpGetAsync(Uri url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.NotFound:
                throw new UnauthorizedAccessException();
            default:
                response.EnsureSuccessStatusCode();
                return response;
        }
    }

    private static int? GetIntHeader(HttpResponseMessage response, string key)
    {
        if (response.Headers.TryGetValues(key, out var values))
        {
            if (int.TryParse(values.First(), out var result))
            {
                return result;
            }
        }

        return null;
    }

    protected async Task<PagedResponse<TItem>> LoadListAsync<TItem>(Uri url, CancellationToken cancellationToken)
    {
        using var response = await HttpGetAsync(url, cancellationToken);
        Validates.NotNull(response);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var list = JsonSerializer.Deserialize<IEnumerable<TItem>>(json, _jsonOptions);

        PagedResponse<TItem> result = new()
        {
            Total = GetIntHeader(response, "X-Total"),
            TotalPages = GetIntHeader(response, "X-Total-Pages"),
            PageNumber = GetIntHeader(response, "X-Page"),
            PageSize = GetIntHeader(response, "X-Per-Page"),
            NextPage = GetIntHeader(response, "X-Next-Page"),
            Items = list ?? []
        };

        return result;
    }

    protected async Task<TItem?> LoadItemAsync<TItem>(Uri url)
    {
        using var response = await HttpGetAsync(url, CancellationToken.None);
        Validates.NotNull(response);

        var json = await response.Content.ReadAsStringAsync();

        var item = JsonSerializer.Deserialize<TItem>(json, _jsonOptions);

        return item;
    }
}
