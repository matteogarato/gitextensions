using System.ComponentModel.Composition;
using System.Net;
using System.Net.Http.Headers;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI;
using GitUIPluginInterfaces.BuildServerIntegration;
using Microsoft.VisualStudio.Threading;

namespace GitExtensions.Plugins.JenkinsIntegration;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
public sealed class JenkinsIntegrationMetadata(string buildServerType)
    : BuildServerAdapterMetadataAttribute(buildServerType)
{
    public override string? CanBeLoaded
    {
        get => null;
    }
}

[Export(typeof(IBuildServerAdapter))]
[JenkinsIntegrationMetadata(PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
internal sealed class JenkinsAdapter : IBuildServerAdapter
{
    public const string PluginName = "Jenkins";

    private const string _jenkinsTreeBuildInfo =
        "number,result,timestamp,url,actions[lastBuiltRevision[SHA1,branch[name]],totalCount,failCount,skipCount],building,duration";

    private static readonly IBuildDurationFormatter _buildDurationFormatter = new BuildDurationFormatter();
    private static readonly char[] _separator = ['|'];

    // last known build per project
    private readonly Dictionary<string, long> _lastProjectBuildTime = [];
    private IBuildServerWatcher? _buildServerWatcher;

    private HttpClient? _httpClient;
    private Regex? _ignoreBuilds;

    void IBuildServerAdapter.Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config,
        Action openSettings, Func<ObjectId, bool>? isCommitInRevisionGrid = null)
    {
        if (_buildServerWatcher is not null)
        {
            throw new InvalidOperationException("Already initialized");
        }

        _buildServerWatcher = buildServerWatcher;

        var projectName = config.GetString("ProjectName", null);
        var hostName = config.GetString("BuildServerUrl", null);

        if (!string.IsNullOrEmpty(hostName) && !string.IsNullOrEmpty(projectName))
        {
            var baseAddress = hostName.Contains("://")
                ? new Uri(hostName, UriKind.Absolute)
                : new Uri($"{Uri.UriSchemeHttp}://{hostName}:8080", UriKind.Absolute);

            _httpClient = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true })
            {
                Timeout = TimeSpan.FromMinutes(2), BaseAddress = baseAddress
            };

            var buildServerCredentials = buildServerWatcher.GetBuildServerCredentials(this, true);

            UpdateHttpClientOptions(buildServerCredentials);

            var projectUrls = _buildServerWatcher.ReplaceVariables(projectName)
                .Split(_separator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var projectUrl in projectUrls.Select(s => baseAddress + "job/" + s.Trim() + "/"))
            {
                _lastProjectBuildTime[projectUrl] = -1;
            }
        }

        var ignoreBuilds = config.GetString("IgnoreBuildBranch", string.Empty);
        _ignoreBuilds = !string.IsNullOrWhiteSpace(ignoreBuilds) ? new Regex(ignoreBuilds) : null;
    }

    /// <summary>
    ///     Gets a unique key which identifies this build server.
    /// </summary>
    string IBuildServerAdapter.UniqueKey
    {
        get
        {
            Validates.NotNull(_httpClient);
            return _httpClient.BaseAddress!.Host;
        }
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
    {
        // GetBuilds() will return the same builds as for GetRunningBuilds().
        // Multiple calls will fetch same info multiple times and make debugging very confusing
        // Similar as for AppVeyor
        // 'sinceDate' is not supported in Jenkins API
        return Observable.Empty<BuildInfo>();
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetRunningBuilds(IScheduler scheduler)
    {
        return GetBuilds(scheduler);
    }

    void IDisposable.Dispose()
    {
        _httpClient?.Dispose();
    }

    private async Task<ResponseInfo> GetBuildInfoTaskAsync(string projectUrl, bool fullInfo,
        CancellationToken cancellationToken)
    {
        string? t;
        long timestamp = 0;
        IEnumerable<JsonNode> s = [];

        try
        {
            t = await GetResponseAsync(FormatToGetJson(projectUrl, fullInfo), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Could be cancelled or failed. Explicitly assign 't' to reveal the intended behavior of following code
            // for this case.
            t = null;
        }

        if (!string.IsNullOrWhiteSpace(t) && !cancellationToken.IsCancellationRequested)
        {
            var jobDescription = JsonNode.Parse(t)!;
            if (jobDescription["builds"] is not null)
            {
                // Freestyle jobs
                s = jobDescription["builds"]!.AsArray().OfType<JsonNode>();
            }
            else if (jobDescription["jobs"] is not null)
            {
                // Multi-branch pipeline
                s = jobDescription["jobs"]!.AsArray()
                    .OfType<JsonNode>()
                    .SelectMany(j => j["builds"]!.AsArray().OfType<JsonNode>());
                foreach (var j in jobDescription["jobs"]!.AsArray())
                {
                    try
                    {
                        if (j?["lastBuild"]?["timestamp"] is null)
                        {
                            continue;
                        }

                        var ts = j!["lastBuild"]!["timestamp"]!.GetValue<long>();
                        timestamp = Math.Max(timestamp, ts);
                    }
                    catch
                    {
                        // Ignore malformed build ids
                    }
                }
            }

            // else: The server had no response (overloaded?) or a multi-branch pipeline is not configured
            if (timestamp == 0 && jobDescription["lastBuild"]?["timestamp"] is not null)
            {
                timestamp = jobDescription["lastBuild"]!["timestamp"]!.GetValue<long>();
            }
        }

        return new ResponseInfo { Url = projectUrl, Timestamp = timestamp, JobDescription = s };
    }

    private IObservable<BuildInfo> GetBuilds(IScheduler scheduler)
    {
        return Observable.Create<BuildInfo>((observer, cancellationToken) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                return scheduler.Schedule(() => ObserveBuilds(observer, cancellationToken));
            }).Task);
    }

    private void ObserveBuilds(IObserver<BuildInfo> observer, CancellationToken cancellationToken)
    {
        try
        {
            List<JoinableTask<ResponseInfo>> allBuildInfos = [];
            List<JoinableTask<ResponseInfo>> latestBuildInfos = [];

            foreach (var projectUrl in _lastProjectBuildTime.Keys)
            {
                if (_lastProjectBuildTime[projectUrl] <= 0)
                {
                    // This project must be updated, no need to to check the latest builds
                    allBuildInfos.Add(ThreadHelper.JoinableTaskFactory.RunAsync(() =>
                        GetBuildInfoTaskAsync(projectUrl, true, cancellationToken)));
                }
                else
                {
                    latestBuildInfos.Add(ThreadHelper.JoinableTaskFactory.RunAsync(() =>
                        GetBuildInfoTaskAsync(projectUrl, false, cancellationToken)));
                }
            }

            // Check the latest build on the server to the existing build timestamp
            // The simple request will limit the load on the Jenkins server
            // To fetch just new builds is possible too, but it will make the solution more complicated
            // Similar, the build results could be cached so they are available when switching repos
            foreach (var info in latestBuildInfos.Where(info => !info.Task.IsFaulted))
            {
                var responseInfo = info.Join(cancellationToken);

                Validates.NotNull(responseInfo.Url);

                if (responseInfo.Timestamp > _lastProjectBuildTime[responseInfo.Url])
                {
                    // The info has at least one newer job, query the status
                    allBuildInfos.Add(ThreadHelper.JoinableTaskFactory.RunAsync(() =>
                        GetBuildInfoTaskAsync(responseInfo.Url, true, cancellationToken)));
                }
            }

            if (allBuildInfos.All(t => t.Task.IsCanceled))
            {
                observer.OnCompleted();
                return;
            }

            Dictionary<ObjectId, BuildStatus> builds = [];
            foreach (var build in allBuildInfos)
            {
                if (build.Task.IsFaulted)
                {
                    DebugHelpers.Assert(build.Task.Exception is not null, "build.Task.Exception is not null");

                    observer.OnError(build.Task.Exception);
                    continue;
                }

                var buildResponse = build.Join(cancellationToken);
                if (build.Task.IsCanceled || buildResponse.Timestamp <= 0)
                {
                    // No valid information received for the build
                    continue;
                }

                Validates.NotNull(buildResponse.Url);
                Validates.NotNull(buildResponse.JobDescription);

                _lastProjectBuildTime[buildResponse.Url] = buildResponse.Timestamp;

                foreach (var buildDetails in buildResponse.JobDescription)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        var buildInfo = CreateBuildInfo(buildDetails);
                        if (buildInfo is null)
                        {
                            continue;
                        }

                        if (buildInfo.Status == BuildStatus.InProgress)
                        {
                            // Need to make a full request next time
                            _lastProjectBuildTime[buildResponse.Url] = 0;
                        }

                        // Normally, there is only one commit per Jenkins build
                        // builds listed with latest first, likely most interesting
                        // Update the observer if at least one of them is better (cannot be selective)
                        var isBetter = false;
                        foreach (var commit in buildInfo.CommitHashList)
                        {
                            if (!StatusIsBetter(buildInfo.Status, commit, builds))
                            {
                                continue;
                            }

                            isBetter = true;
                            builds[commit] = buildInfo.Status;
                        }

                        if (isBetter)
                        {
                            observer.OnNext(buildInfo);
                        }
                    }
                    catch
                    {
                        // Ignore unexpected responses
                    }
                }
            }

            // Complete the job, it will be run again with Observe.Retry() (every 10th sec)
            observer.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            // Do nothing, the observer is already stopped
        }
        catch (Exception ex)
        {
            // Cancelling a sub-task is similar to cancelling this task
            if (ex.InnerException is not OperationCanceledException)
            {
                observer.OnError(ex);
            }
        }

        return;

        // Priority: Completed > InProgress > Aborted/Stopped > None
        static bool StatusIsBetter(BuildStatus newStatus, ObjectId commit, Dictionary<ObjectId, BuildStatus> builds)
        {
            // No existing status
            if (!builds.TryGetValue(commit, out var existingStatus))
            {
                return true;
            }

            // Completed status is never replaced
            if (IsBuildCompleted(existingStatus))
            {
                return false;
            }

            // Existing status is lower than Completed, new complete replaces
            if (IsBuildCompleted(newStatus))
            {
                return true;
            }

            // Replace existing aborted/stopped if new is InProgress
            return existingStatus != BuildStatus.InProgress
                   && newStatus == BuildStatus.InProgress;
        }

        static bool IsBuildCompleted(BuildStatus status)
        {
            return status == BuildStatus.Success
                   || status == BuildStatus.Failure
                   || status == BuildStatus.Unstable;
        }
    }

    internal BuildInfo? CreateBuildInfo(JsonNode buildDescription)
    {
        var idValue = GetNodeString(buildDescription["number"]);
        var statusValue = GetNodeString(buildDescription["result"]);
        var startDateTicks = buildDescription["timestamp"]!.GetValue<long>();
        var webUrl = GetNodeString(buildDescription["url"]);

        var action = buildDescription["actions"]?.AsArray();
        List<ObjectId> commitHashList = [];
        var testResults = string.Empty;
        foreach (var element in action ?? [])
        {
            if (element is null)
            {
                continue;
            }

            if (element["lastBuiltRevision"] is not null)
            {
                commitHashList.Add(ObjectId.Parse(GetNodeString(element["lastBuiltRevision"]!["SHA1"])!));
                var branches = element["lastBuiltRevision"]!["branch"]?.AsArray();
                if (_ignoreBuilds is not null && branches is not null)
                {
                    // Ignore build events for specified branches
                    foreach (var branch in branches)
                    {
                        if (branch is null)
                        {
                            continue;
                        }

                        var name = GetNodeString(branch["name"]);
                        if (name is null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(name) && _ignoreBuilds.IsMatch(name))
                        {
                            return null;
                        }
                    }
                }
            }

            if (element["totalCount"] is null)
            {
                continue;
            }

            var testCount = element["totalCount"]!.GetValue<int>();
            if (testCount != 0)
            {
                var failedTestCount = element["failCount"]!.GetValue<int>();
                var skippedTestCount = element["skipCount"]!.GetValue<int>();
                testResults = $"{testCount} tests ({failedTestCount} failed, {skippedTestCount} skipped)";
            }
        }

        var isRunning = buildDescription["building"]!.GetValue<bool>();
        long? buildDuration;
        if (isRunning)
        {
            buildDuration = null;
        }
        else
        {
            buildDuration = buildDescription["duration"]!.GetValue<long>();
        }

        var status = isRunning ? BuildStatus.InProgress : ParseBuildStatus(statusValue ?? string.Empty);
        var statusText = status.ToString("G");
        BuildInfo buildInfo = new()
        {
            Id = idValue,
            StartDate = TimestampToDateTime(startDateTicks),
            Duration = buildDuration,
            Status = status,
            CommitHashList = commitHashList.ToArray(),
            Url = webUrl
        };
        var durationText = _buildDurationFormatter.Format(buildInfo.Duration);
        buildInfo.Description = $"#{idValue} {durationText} {testResults} {statusText}";
        return buildInfo;
    }

    private static DateTime TimestampToDateTime(long timestamp)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTime.Now.Kind).AddMilliseconds(timestamp);
    }

    private static AuthenticationHeaderValue CreateBasicHeader(string username, string password)
    {
        var byteArray = Encoding.UTF8.GetBytes($"{username}:{password}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
    }

    // Converts non-string JSON values (numbers, booleans) to their string representations,
    // aligned with Newtonsoft's ToObject<string>() behavior.
    private static string? GetNodeString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out string? stringValue))
        {
            return stringValue;
        }

        return node.GetValue<JsonElement>().ToString();
    }

    private static BuildStatus ParseBuildStatus(string statusValue)
    {
        return statusValue switch
        {
            "SUCCESS" => BuildStatus.Success,
            "FAILURE" => BuildStatus.Failure,
            "UNSTABLE" => BuildStatus.Unstable,
            "ABORTED" => BuildStatus.Stopped,

            // Jenkins status "NOT_BUILT"
            _ => BuildStatus.Unknown
        };
    }

    private async Task<Stream?> GetStreamAsync(string restServicePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Validates.NotNull(_httpClient);

        var response = await _httpClient
            .GetAsync(restServicePath, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        return await GetStreamFromHttpResponseAsync(response);

        async Task<Stream?> GetStreamFromHttpResponseAsync(HttpResponseMessage resp)
        {
            var unauthorized = resp.StatusCode == HttpStatusCode.Unauthorized;

            if (resp.IsSuccessStatusCode)
            {
                var httpContent = resp.Content;

                if (httpContent.Headers.ContentType?.MediaType == "text/html")
                {
                    // Jenkins responds with an HTML login page when guest access is denied.
                    unauthorized = true;
                }
                else
                {
                    return await httpContent.ReadAsStreamAsync(cancellationToken);
                }
            }
            else if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                // The url does not exist, no jobs to retrieve
                return null;
            }
            else if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                unauthorized = true;
            }

            if (!unauthorized)
            {
                throw new HttpRequestException(resp.ReasonPhrase);
            }

            Validates.NotNull(_buildServerWatcher);

            var buildServerCredentials =
                _buildServerWatcher.GetBuildServerCredentials(this, false) ??
                throw new OperationCanceledException(resp.ReasonPhrase);
            UpdateHttpClientOptions(buildServerCredentials);

            return await GetStreamAsync(restServicePath, cancellationToken);
        }
    }

    private void UpdateHttpClientOptions(IBuildServerCredentials? buildServerCredentials)
    {
        Validates.NotNull(_httpClient);

        if (buildServerCredentials is null ||
            buildServerCredentials.BuildServerCredentialsType == BuildServerCredentialsType.Guest)
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else if (buildServerCredentials.BuildServerCredentialsType == BuildServerCredentialsType.UsernameAndPassword)
        {
            Validates.NotNull(buildServerCredentials.Username);
            Validates.NotNull(buildServerCredentials.Password);
            _httpClient.DefaultRequestHeaders.Authorization =
                CreateBasicHeader(buildServerCredentials.Username, buildServerCredentials.Password);
        }
    }

    private async Task<string> GetResponseAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var responseStream = await GetStreamAsync(relativePath, cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(responseStream ?? Stream.Null);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string FormatToGetJson(string restServicePath, bool buildsInfo = false)
    {
        var buildTree = "lastBuild[timestamp]";
        var depth = 1;
        var postIndex = restServicePath.IndexOf('?');
        if (postIndex >= 0)
        {
            var endLen = restServicePath.Length - postIndex;
            if (restServicePath.EndsWith('/'))
            {
                endLen--;
            }

            var post = restServicePath.Substring(postIndex, endLen);
            if (post == "?m")
            {
                // Multi pipeline project
                buildTree = "jobs[" + buildTree;
                if (buildsInfo)
                {
                    depth = 2;
                    buildTree += ",builds[" + _jenkinsTreeBuildInfo + "]";
                }

                buildTree += "]";
            }
            else
            {
                // user defined format (will likely require changes in the code)
                buildTree = post;
            }

            restServicePath = restServicePath[..postIndex];
        }
        else
        {
            // Freestyle project
            if (buildsInfo)
            {
                buildTree += ",builds[" + _jenkinsTreeBuildInfo + "]";
            }
        }

        if (!restServicePath.EndsWith('/'))
        {
            restServicePath += "/";
        }

        restServicePath += "api/json?depth=" + depth + "&tree=" + buildTree;
        return restServicePath;
    }

    private sealed class ResponseInfo
    {
        public string? Url { get; set; }
        public long Timestamp { get; set; }
        public IEnumerable<JsonNode>? JobDescription { get; set; }
    }
}
