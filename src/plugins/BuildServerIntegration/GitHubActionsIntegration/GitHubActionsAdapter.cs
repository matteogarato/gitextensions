using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.GitHubActionsIntegration.ApiClient;
using GitExtensions.Plugins.GitHubActionsIntegration.ApiClient.Models;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Plugins.GitHubActionsIntegration;

[Export(typeof(IBuildServerAdapter))]
[GitHubActionsIntegrationMetadata(PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public sealed class GitHubActionsAdapter(IGitHubActionsApiClientFactory apiClientFactory) : IBuildServerAdapter
{
    public const string PluginName = "GitHub Actions";

    internal const string SettingApiUrl = "GitHubActionsApiUrl";
    internal const string SettingApiToken = "GitHubActionsApiToken";
    internal const string SettingOwner = "GitHubActionsOwner";
    internal const string SettingRepository = "GitHubActionsRepository";

    private const string _defaultApiUrl = "https://api.github.com";
    private const int _pageSize = 100;

    private readonly ConcurrentDictionary<string, DateTime> _loadedItems = new();

    private IGitHubActionsApiClient? _apiClient;

    public GitHubActionsAdapter()
        : this(new GitHubActionsApiClientFactory())
    {
    }

    void IBuildServerAdapter.Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config,
        Action openSettings, Func<ObjectId, bool>? isCommitInRevisionGrid = null)
    {
        var apiUrl = config.GetString(SettingApiUrl, null) ?? _defaultApiUrl;
        var apiToken = config.GetString(SettingApiToken, null);
        var owner = config.GetString(SettingOwner, null);
        var repository = config.GetString(SettingRepository, null);

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
        {
            return;
        }

        _apiClient = apiClientFactory.CreateApiClient(apiUrl, owner, repository, apiToken);
    }

    string IBuildServerAdapter.UniqueKey
    {
        get
        {
            Validates.NotNull(_apiClient);
            return _apiClient.BaseUrl;
        }
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
    {
        return GetBuilds(scheduler, sinceDate, false);
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetRunningBuilds(IScheduler scheduler)
    {
        return GetBuilds(scheduler, null, true);
    }

    void IDisposable.Dispose()
    {
        _apiClient?.Dispose();
    }

    private IObservable<BuildInfo> GetBuilds(IScheduler scheduler, DateTime? sinceDate, bool running)
    {
        return Observable.Create<BuildInfo>(ObserveBuildsAsync)
            .SubscribeOn(scheduler);

        async Task ObserveBuildsAsync(IObserver<BuildInfo> observer, CancellationToken cancellationToken)
        {
            if (_apiClient is null)
            {
                observer.OnCompleted();
                return;
            }

            try
            {
                var page = 1;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var response = await _apiClient.GetWorkflowRunsAsync(
                        running,
                        sinceDate,
                        page,
                        _pageSize,
                        cancellationToken);

                    foreach (var run in response.WorkflowRuns)
                    {
                        if (!_loadedItems.TryGetValue(run.HeadSha, out var lastUpdated) ||
                            lastUpdated < run.UpdatedAt)
                        {
                            _loadedItems[run.HeadSha] = run.UpdatedAt;
                            observer.OnNext(run.ToBuildInfo());
                        }
                    }

                    if (response.WorkflowRuns.Count < _pageSize)
                    {
                        break;
                    }

                    page++;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
                return;
            }

            observer.OnCompleted();
        }
    }
}
