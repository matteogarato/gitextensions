using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.GitlabIntegration.ApiClient;
using GitExtensions.Plugins.GitlabIntegration.ApiClient.Models;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Plugins.GitlabIntegration;

[Export(typeof(IBuildServerAdapter))]
[GitlabIntegrationMetadata(PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public sealed class GitlabAdapter(IGitlabApiClientFactory apiClientFactory) : IBuildServerAdapter
{
    public const string PluginName = "Gitlab";
    private readonly ConcurrentDictionary<string, DateTime> _loadedItems = new();

    private IGitlabApiClient? _apiClient;
    private int? _pagesLimit;

    public GitlabAdapter()
        : this(new GitlabApiClientFactory())
    {
    }

    void IBuildServerAdapter.Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config,
        Action openSettings, Func<ObjectId, bool>? isCommitInRevisionGrid = null)
    {
        _loadedItems.Clear();

        _apiClient = apiClientFactory.CreateGitlabApiClient(
            config.GetString("InstanceUrl", string.Empty),
            config.GetString("ApiToken", string.Empty),
            config.GetInt("ProjectId", 0));

        _pagesLimit = config.GetInt("PagesLimit");
        if (_pagesLimit is 0)
        {
            _pagesLimit = null;
        }
    }

    string IBuildServerAdapter.UniqueKey
    {
        get
        {
            Validates.NotNull(_apiClient);
            return _apiClient.InstanceUrl;
        }
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
    {
        return GetBuilds(sinceDate);
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetRunningBuilds(IScheduler scheduler)
    {
        return GetBuilds(null, true);
    }

    void IDisposable.Dispose()
    {
        _apiClient?.Dispose();
    }

    private IObservable<BuildInfo> GetBuilds(DateTime? sinceDate = null, bool running = false)
    {
        return Observable.Create<BuildInfo>((observer, cancellationToken) =>
            ObserveBuildsAsync(sinceDate, running, observer, cancellationToken));
    }

    private async Task ObserveBuildsAsync(DateTime? sinceDate, bool running, IObserver<BuildInfo> observer,
        CancellationToken cancellationToken)
    {
        Validates.NotNull(_apiClient);

        try
        {
            var firstPage =
                await _apiClient.GetPipelinesAsync(sinceDate, running, 1, cancellationToken);
            ProcessLoadedBuilds(firstPage.Items, observer);

            if (firstPage.TotalPages is > 1)
            {
                var totalPages = firstPage.TotalPages.Value;
                if (_pagesLimit < totalPages)
                {
                    totalPages = _pagesLimit.Value;
                }

                var pagesTasks =
                    new Task<PagedResponse<GitlabPipeline>>[totalPages - 1];
                for (var i = 2; i <= totalPages; i++)
                {
                    pagesTasks[i - 2] = _apiClient.GetPipelinesAsync(sinceDate, running, i, cancellationToken);
                }

                var pages = await Task.WhenAll(pagesTasks);
                foreach (var page in pages)
                {
                    ProcessLoadedBuilds(page.Items, observer);
                }
            }
            else
            {
                var currentPage = firstPage;

                while (currentPage.NextPage.HasValue
                       && (!_pagesLimit.HasValue || currentPage.NextPage.Value <= _pagesLimit.Value)
                       && !cancellationToken.IsCancellationRequested)
                {
                    currentPage = await _apiClient.GetPipelinesAsync(sinceDate, running, currentPage.NextPage.Value,
                        cancellationToken);
                    ProcessLoadedBuilds(currentPage.Items, observer);
                }
            }

            observer.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            observer.OnCompleted();
        }
    }

    private void ProcessLoadedBuilds(IEnumerable<GitlabPipeline> items, IObserver<BuildInfo> observer)
    {
        foreach (var item in items)
        {
            if (!_loadedItems.TryGetValue(item.Sha, out var dateTime) || dateTime < item.UpdatedAt)
            {
                _loadedItems[item.Sha] = item.UpdatedAt;
                observer.OnNext(item.ToBuildInfo());
            }
        }
    }
}
