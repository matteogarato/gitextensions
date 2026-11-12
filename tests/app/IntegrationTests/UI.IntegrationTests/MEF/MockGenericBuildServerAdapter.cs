using System.ComponentModel.Composition;
using System.Reactive.Concurrency;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace UITests.MEF;

[PartNotDiscoverable]
[MockGenericBuildServerIntegrationMetadata("GenericBuildServerMock")]
[Export(typeof(IBuildServerAdapter))]
[PartCreationPolicy(CreationPolicy.NonShared)]
internal class MockGenericBuildServerAdapter : IBuildServerAdapter
{
    string IBuildServerAdapter.UniqueKey { get; } = null!;

    void IDisposable.Dispose()
    {
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
    {
        throw new NotImplementedException();
    }

    IObservable<BuildInfo> IBuildServerAdapter.GetRunningBuilds(IScheduler scheduler)
    {
        throw new NotImplementedException();
    }

    void IBuildServerAdapter.Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config,
        Action openSettings, Func<ObjectId, bool>? isCommitInRevisionGrid = null)
    {
    }
}
