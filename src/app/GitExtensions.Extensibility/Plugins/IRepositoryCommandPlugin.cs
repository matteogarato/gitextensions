namespace GitExtensions.Extensibility.Plugins;

/// <summary>
///     Provides UI-neutral commands that operate on the active repository.
/// </summary>
public interface IRepositoryCommandPlugin
{
    /// <summary>
    ///     Gets the stable identifier for this plugin.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    ///     Gets the display name for this plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the repository commands exposed by this plugin.
    /// </summary>
    IReadOnlyList<IRepositoryPluginCommand> Commands { get; }
}
