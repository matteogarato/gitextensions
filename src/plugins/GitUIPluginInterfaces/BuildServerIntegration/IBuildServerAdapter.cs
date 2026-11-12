using System.Reactive.Concurrency;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;

namespace GitUIPluginInterfaces.BuildServerIntegration;

public interface IBuildServerAdapter : IDisposable
{
    /// <summary>
    ///     Gets a unique key which identifies this build server.
    /// </summary>
    string UniqueKey { get; }

    void Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config, Action openSettings,
        Func<ObjectId, bool>? isCommitInRevisionGrid = null);

    IObservable<BuildInfo> GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null);

    IObservable<BuildInfo> GetRunningBuilds(IScheduler scheduler);

    /// <summary>
    ///     Provides an extension point for handling the switch of repositories.
    ///     For example, it could be used to clear build changes.
    /// </summary>
    void OnRepositoryChanged()
    {
        // Default implementation: we do nothing
    }
}
