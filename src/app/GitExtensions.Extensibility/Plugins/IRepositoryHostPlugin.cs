namespace GitExtensions.Extensibility.Plugins;

/// <summary>
///     Define that the plugin provides features (clone, ...) related to an online git hosting service.
///     A plugin implementing this interface **must** have the `Export` attribute declared this way:
///     [Export(typeof(IRepositoryHostPlugin))]
/// </summary>
public interface IRepositoryHostPlugin : IGitPlugin
{
    bool ConfigurationOk { get; }
    string? OwnerLogin { get; }
    IReadOnlyList<IHostedRepository> SearchForRepository(string search);
    IReadOnlyList<IHostedRepository> GetRepositoriesOfUser(string user);
    IHostedRepository GetRepository(string user, string repositoryName);

    IReadOnlyList<IHostedRepository> GetMyRepos();

    void ConfigureContextMenu(ContextMenuStrip contextMenu);

    bool GitModuleIsRelevantToMe();
    IReadOnlyList<IHostedRemote> GetHostedRemotesForModule();

    Task<string?> AddUpstreamRemoteAsync();
}
