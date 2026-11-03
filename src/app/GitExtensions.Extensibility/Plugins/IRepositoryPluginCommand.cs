using GitExtensions.Extensibility.Git;

namespace GitExtensions.Extensibility.Plugins;

/// <summary>
///  Describes a repository action that a UI host can present using its native controls.
/// </summary>
public interface IRepositoryPluginCommand
{
    /// <summary>
    ///  Gets the stable identifier for this command within its plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    ///  Gets the display name for this command.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///  Gets a value indicating whether running this command may change repository state.
    /// </summary>
    bool ChangesRepository { get; }

    /// <summary>
    ///  Determines whether the command can run for the specified repository.
    /// </summary>
    /// <param name="module">The active repository module.</param>
    /// <returns><see langword="true"/> when the command is available.</returns>
    bool CanExecute(IGitModule module);

    /// <summary>
    ///  Runs this command for the specified repository.
    /// </summary>
    /// <param name="module">The active repository module.</param>
    /// <param name="cancellationToken">A token that requests cancellation.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    Task ExecuteAsync(IGitModule module, CancellationToken cancellationToken);
}
