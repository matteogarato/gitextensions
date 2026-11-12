using System.ComponentModel.Composition;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json.Nodes;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.AzureDevOpsIntegration.Settings;
using GitExtUtils.GitUI;
using GitUIPluginInterfaces.BuildServerIntegration;
using Microsoft.VisualStudio.Threading;
using ResourceManager;

namespace GitExtensions.Plugins.AzureDevOpsIntegration;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
public class AzureDevOpsIntegrationMetadata(string buildServerType)
    : BuildServerAdapterMetadataAttribute(buildServerType);

/// <summary>
///     Provides build server integration for Azure DevOps (or TFS>=2015) into GitExtensions
/// </summary>
[Export(typeof(IBuildServerAdapter))]
[AzureDevOpsIntegrationMetadata(PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
internal sealed class AzureDevOpsAdapter : IBuildServerAdapter
{
    public const string PluginName = "Azure DevOps and Team Foundation Server (since TFS2015)";
    private static readonly IBuildDurationFormatter _buildDurationFormatter = new BuildDurationFormatter();

    // Static variable used to convey the data between the different instances of the class but that doesn't necessarily require synchronization because:
    // * there is only one instance at every given time (link to the revision grid and recreated on revision grid refresh)
    // * data is created by the first instance and used in readonly by the later instances
    private static BuildsCache? _buildsCache;
    private static string? _projectOnErrorKey;

    private readonly TranslationString _badTokenErrorMessage = new(
        @"The personal access token is invalid or has expired. Update it in the 'Build server integration' settings.

The build server integration has been disabled for this session.");

    private readonly TranslationString _buildIntegrationErrorCaption = new("Azure DevOps error");

    private readonly TranslationString _genericErrorMessage = new(
        @"An error occurred when requesting build server results.

As a consequence, the build server integration has been disabled for this session.

Detail of the error:");

    private readonly TranslationString _ignoreButton = new("Ignore");
    private readonly TranslationString _openSettingsButton = new("Open settings");
    private ApiClient? _apiClient;
    private string? _buildDefinitions;
    private JoinableTask<string?>? _buildDefinitionsTask;
    private IBuildServerWatcher? _buildServerWatcher;

    private bool _firstCallForFinishedBuildsWasIgnored;

    private Action? _openSettings;
    private string? _projectUrl;
    private IntegrationSettings? _settings;

    private string CacheKey
    {
        get
        {
            Validates.NotNull(_settings);
            return _projectUrl + "|" + _settings.BuildDefinitionFilter;
        }
    }

    void IBuildServerAdapter.Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config,
        Action openSettings, Func<ObjectId, bool>? isCommitInRevisionGrid = null)
    {
        if (_buildServerWatcher is not null)
        {
            throw new InvalidOperationException("Already initialized");
        }

        _buildServerWatcher = buildServerWatcher;
        _openSettings = openSettings;
        _settings = IntegrationSettings.ReadFrom(config);

        if (!_settings.IsValid())
        {
            throw new InvalidOperationException(
                "Azure DevOps integration settings are incomplete (project URL missing).");
        }

        _projectUrl = _buildServerWatcher.ReplaceVariables(_settings.ProjectUrl);

        if (!Uri.IsWellFormedUriString(_projectUrl, UriKind.Absolute))
        {
            throw new InvalidOperationException("Azure DevOps project URL is not a valid absolute URI.");
        }

        _apiClient = new ApiClient(_projectUrl, _settings.ApiToken);
        if (_buildsCache is null || _buildsCache.Id != CacheKey)
        {
            _buildsCache = null;
            _buildDefinitionsTask = ThreadHelper.JoinableTaskFactory.RunAsync(() => _apiClient.GetBuildDefinitionsAsync(
                _settings.BuildDefinitionFilter,
                string.IsNullOrWhiteSpace(_settings.RepositoryName) ? null : _settings.RepositoryName));
        }
        else
        {
            _buildDefinitions = _buildsCache.BuildDefinitions;
        }
    }

    /// <summary>
    ///     Gets a unique key which identifies this build server.
    /// </summary>
    string IBuildServerAdapter.UniqueKey
    {
        get => _settings?.ProjectUrl ??
               throw new InvalidOperationException($"{nameof(AzureDevOpsAdapter)} is not yet initialized");
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
    {
        if (!_firstCallForFinishedBuildsWasIgnored)
        {
            _firstCallForFinishedBuildsWasIgnored = true;
            return Observable.Empty<BuildInfo>();
        }

        return GetBuilds(sinceDate);
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetRunningBuilds(IScheduler scheduler)
    {
        return GetBuilds(null, true);
    }

    void IBuildServerAdapter.OnRepositoryChanged()
    {
        _buildsCache = null;
    }

    void IDisposable.Dispose()
    {
        _apiClient?.Dispose();
    }

    private IObservable<BuildInfo> GetBuilds(DateTime? sinceDate = null, bool running = false)
    {
        return Observable.Create<BuildInfo>((observer, cancellationToken) =>
            ObserveBuildsAsync(sinceDate, running, observer));
    }

    private async Task ObserveBuildsAsync(DateTime? sinceDate, bool running, IObserver<BuildInfo> observer)
    {
        if (_apiClient is null)
        {
            observer.OnCompleted();
            return;
        }

        try
        {
            var isBuildDefinitionsInitializedSuccessfully = await EnsureBuildDefinitionsIsInitializedAsync(observer);
            if (!isBuildDefinitionsInitializedSuccessfully)
            {
                return;
            }

            if (running)
            {
                var builds =
                    FilterRunningBuilds(await _apiClient.QueryRunningBuildsAsync(_buildDefinitions!));
                foreach (var build in builds)
                {
                    observer.OnNext(CreateBuildInfo(build));
                }
            }
            else
            {
                // Display cached builds results
                if (!sinceDate.HasValue && _buildsCache is not null && _buildsCache.FinishedBuilds.Count != 0)
                {
                    foreach (var buildInfo in _buildsCache.FinishedBuilds)
                    {
                        observer.OnNext(buildInfo);
                    }

                    // Reduce the scope to query only the builds finished after the last one in the cache
                    sinceDate = _buildsCache.LastCall;
                }

                IEnumerable<Build> builds = await _apiClient.QueryFinishedBuildsAsync(_buildDefinitions!, sinceDate);
                var buildsByCommit = builds.GroupBy(b => b.SourceVersion);

                foreach (var buildsForACommit in buildsByCommit)
                {
                    var buildToDisplay = buildsForACommit.OrderByDescending(b => b.FinishTime).First();
                    var buildInfo = CreateBuildInfo(buildToDisplay);
                    observer.OnNext(buildInfo);
                    _buildsCache!.FinishedBuilds.Add(buildInfo);
                    if (buildToDisplay.FinishTime.HasValue && buildToDisplay.FinishTime.Value >= _buildsCache.LastCall)
                    {
                        _buildsCache.LastCall = buildToDisplay.FinishTime.Value.AddSeconds(1);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Do nothing, the observer is already stopped
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
        }
    }

    private async Task<bool> EnsureBuildDefinitionsIsInitializedAsync(IObserver<BuildInfo> observer)
    {
        if (_buildDefinitions is not null)
        {
            return true;
        }

        try
        {
            Validates.NotNull(_buildDefinitionsTask);

            _buildDefinitions = await _buildDefinitionsTask.JoinAsync();

            if (_buildDefinitions is null)
            {
                observer.OnCompleted();
                return false;
            }

            _buildsCache = new BuildsCache { Id = CacheKey, BuildDefinitions = _buildDefinitions };
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            var errorMessage = _badTokenErrorMessage.Text;
            DisplayInitializationErrorInRevisionGrid(errorMessage);

            if (_projectOnErrorKey is null || _projectOnErrorKey != CacheKey)
            {
                _projectOnErrorKey = CacheKey;

                TaskDialogButton btnOpenSettings = new(_openSettingsButton.Text);
                TaskDialogButton btnIgnore = new(_ignoreButton.Text);
                TaskDialogPage page = new()
                {
                    Heading = errorMessage,
                    Icon = TaskDialogIcon.Error,
                    AllowCancel = true,
                    Caption = _buildIntegrationErrorCaption.Text,
                    Buttons = { btnOpenSettings, btnIgnore }
                };

                var result = await TaskDialog.ShowDialogAsync(page);
                if (result == btnOpenSettings)
                {
                    _projectOnErrorKey = null;
                    Validates.NotNull(_openSettings);
                    _openSettings();
                }
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"{_genericErrorMessage.Text}{Environment.NewLine}{ex.Message}";
            DisplayInitializationErrorInRevisionGrid(errorMessage);

            if (_projectOnErrorKey is null || _projectOnErrorKey != CacheKey)
            {
                _projectOnErrorKey = CacheKey;
                MessageBoxes.ShowError(null, errorMessage, _buildIntegrationErrorCaption.Text);
            }
        }

        return false;

        void DisplayInitializationErrorInRevisionGrid(string errorMessage)
        {
            _apiClient = null;
            observer.OnNext(new BuildInfo
            {
                CommitHashList = new[] { ObjectId.WorkTreeId },
                Description = errorMessage,
                Status = BuildStatus.Failure
            });
            observer.OnCompleted();
        }
    }

    private static IEnumerable<Build> FilterRunningBuilds(IList<Build> runningBuilds)
    {
        if (runningBuilds.Count < 2)
        {
            return runningBuilds;
        }

        var byCommitBuilds = runningBuilds.GroupBy(b => b.SourceVersion);
        runningBuilds = [];

        // Filter running builds to display the best build as we can only display one build for a commit
        // by selecting the first started or if none, one that is waiting to start
        foreach (var commitBuilds in byCommitBuilds)
        {
            var buildSelected = commitBuilds.Where(b => b.StartTime.HasValue).MinBy(b => b.StartTime)
                                ?? commitBuilds.First();
            runningBuilds.Add(buildSelected);
        }

        return runningBuilds;
    }

    private static (string duration, string tooltip) CreateBuildTooltip(Build buildDetail)
    {
        var duration = string.Empty;

        if (buildDetail.Status is not null
            && buildDetail.Status != "none"
            && buildDetail.Status != "notStarted"
            && buildDetail.Status != "postponed"
            && buildDetail.StartTime.HasValue)
        {
            duration = buildDetail.Status == "inProgress"
                ? _buildDurationFormatter.Format(
                    (long)(DateTime.UtcNow - buildDetail.StartTime.Value).TotalMilliseconds)
                : buildDetail.FinishTime.HasValue
                    ? _buildDurationFormatter.Format((long)(buildDetail.FinishTime.Value - buildDetail.StartTime.Value)
                        .TotalMilliseconds)
                    : "???";
        }

        return (duration,
            $"{buildDetail.BuildNumber} {ConvertResult(buildDetail.IsInProgress ? buildDetail.Status : buildDetail.Result)} - {duration} [{buildDetail.Definition?.Name}]");
    }

    private static BuildInfo CreateBuildInfo(Build buildDetail)
    {
        (var duration, var tooltip) = CreateBuildTooltip(buildDetail);
        Validates.NotNull(buildDetail.SourceVersion);

        var pullRequestTooltip = string.Empty;
        string? pullRequestUrl = null;
        if (buildDetail.IsPullRequest)
        {
            // It's a PR and we need to dive into "Parameters" json to get the real commit hash
            var pullRequestNode =
                buildDetail.Parameters is not null ? JsonNode.Parse(buildDetail.Parameters) : null;
            var commitHash = GetNodeValue(pullRequestNode, "system.pullRequest.sourceCommitId");
            if (!string.IsNullOrEmpty(commitHash))
            {
                buildDetail.SourceVersion = commitHash;
            }

            var pullRequestId = GetNodeValue(pullRequestNode, "system.pullRequest.pullRequestId");
            if (!string.IsNullOrWhiteSpace(pullRequestId))
            {
                pullRequestTooltip = $"{Environment.NewLine}PR #{pullRequestId}";
                pullRequestUrl = $"{buildDetail.Repository?.Url}/pullrequest/{pullRequestId}";
            }
        }

        BuildInfo buildInfo = new()
        {
            Id = buildDetail.BuildNumber,
            StartDate = buildDetail.StartTime ?? DateTime.MinValue,
            Status = buildDetail.IsInProgress ? BuildStatus.InProgress : MapResult(buildDetail.Result),
            Description = duration + " " + buildDetail.BuildNumber,
            Tooltip = tooltip + pullRequestTooltip,
            CommitHashList = [ObjectId.Parse(buildDetail.SourceVersion)],
            Url = buildDetail.Links?.Web?.Href,
            ShowInBuildReportTab = false,
            PullRequestUrl = pullRequestUrl
        };

        return buildInfo;
    }

    private static string? GetNodeValue(JsonNode? node, string key)
    {
        if (node is null)
        {
            return null;
        }

        try
        {
            return node[key]?.GetValue<string>();
        }
        catch (Exception)
        {
            // Ignore if not the expected format.
        }

        return null;
    }

    private static string ConvertResult(string? result)
    {
        return result switch
        {
            "failed" => "❌",
            "canceled" => "⏹️",
            "succeeded" => "✔",
            "partiallySucceeded" => "❗",
            Build.StatusCancelling => "⏹️",
            Build.StatusInProgress => "▶️",
            Build.StatusNotStarted => "⏸",
            Build.StatusPostponed => "⏱",
            _ => "❓"
        };
    }

    private static BuildStatus MapResult(string? status)
    {
        return status switch
        {
            "failed" => BuildStatus.Failure,
            "canceled" => BuildStatus.Stopped,
            "succeeded" => BuildStatus.Success,
            "partiallySucceeded" => BuildStatus.Unstable,
            _ => BuildStatus.Unknown
        };
    }

    #region TestAccessor

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(AzureDevOpsAdapter azureDevOpsAdapter)
    {
        public AzureDevOpsAdapter AzureDevOpsAdapter { get; } = azureDevOpsAdapter;

        public IEnumerable<Build> FilterRunningBuilds(IList<Build> runningBuilds)
        {
            return AzureDevOpsAdapter.FilterRunningBuilds(runningBuilds);
        }
    }

    #endregion
}
