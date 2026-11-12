using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;

namespace GitUI.Plugin;

internal static class RepositoryPluginCommandRunner
{
    internal static async Task ExecuteAsync(IGitUiCommands uiCommands, IRepositoryPluginCommand command)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);
        ArgumentNullException.ThrowIfNull(command);

        var module = uiCommands.Module;
        if (!command.CanExecute(module))
        {
            return;
        }

        try
        {
            await command.ExecuteAsync(module, CancellationToken.None);
        }
        finally
        {
            if (command.ChangesRepository)
            {
                uiCommands.RepoChangedNotifier.Notify();
            }
        }
    }
}
