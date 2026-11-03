using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitUIPluginInterfaces;

namespace GitUI;

internal static class PluginRegistry
{
    private static readonly HashSet<IGitPlugin> _registeredPlugins = [];
    private static readonly List<IRepositoryCommandPlugin> _repositoryCommandPlugins = [];
    private static bool _isLoaded;
    private static bool _repositoryCommandPluginsLoaded;

    internal static List<IGitPlugin> Plugins { get; } = [];

    internal static IReadOnlyList<IRepositoryCommandPlugin> RepositoryCommandPlugins => _repositoryCommandPlugins;

    internal static bool IsInitialized => _isLoaded;

    internal static void InitializeRepositoryCommandPlugins(IWin32Window owner)
    {
        if (_repositoryCommandPluginsLoaded)
        {
            return;
        }

        if (ManagedExtensibility.UserPluginsPath is null)
        {
            throw new InvalidOperationException("Managed extensibility must be initialized before loading plugins.");
        }

        foreach (Lazy<IRepositoryCommandPlugin> export in ManagedExtensibility.GetExports<IRepositoryCommandPlugin>())
        {
            try
            {
                IRepositoryCommandPlugin plugin = export.Value;
                Validate(plugin);
                if (_repositoryCommandPlugins.Any(existing => existing.Id == plugin.Id))
                {
                    throw new InvalidOperationException($"A repository command plugin with ID '{plugin.Id}' is already loaded.");
                }

                _repositoryCommandPlugins.Add(plugin);
            }
            catch (Exception exception)
            {
                MessageBoxes.ShowError(owner, exception.ToString());
            }
        }

        _repositoryCommandPluginsLoaded = true;

        return;

        static void Validate(IRepositoryCommandPlugin plugin)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            if (plugin.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Repository command plugin IDs must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(plugin.Name))
            {
                throw new InvalidOperationException($"Repository command plugin '{plugin.Id}' must have a name.");
            }

            HashSet<string> commandIds = new(StringComparer.Ordinal);
            foreach (IRepositoryPluginCommand command in plugin.Commands)
            {
                if (command is null)
                {
                    throw new InvalidOperationException($"Repository command plugin '{plugin.Id}' contains a null command.");
                }

                if (string.IsNullOrWhiteSpace(command.Id) || string.IsNullOrWhiteSpace(command.Name))
                {
                    throw new InvalidOperationException($"Repository command plugin '{plugin.Id}' contains a command without an ID or name.");
                }

                if (!commandIds.Add(command.Id))
                {
                    throw new InvalidOperationException($"Repository command plugin '{plugin.Id}' contains duplicate command ID '{command.Id}'.");
                }
            }
        }
    }

    internal static void InitializeAll(IWin32Window owner)
    {
        if (_isLoaded)
        {
            return;
        }

        if (ManagedExtensibility.UserPluginsPath is null)
        {
            throw new InvalidOperationException("Managed extensibility must be initialized before loading plugins.");
        }

        foreach (Lazy<IGitPlugin> export in ManagedExtensibility.GetExports<IGitPlugin>())
        {
            try
            {
                IGitPlugin plugin = export.Value;
                string pluginName = plugin.Description ?? plugin.Name ?? plugin.Id.ToString();
                plugin.SettingsContainer = new GitPluginSettingsContainer(plugin.Id, pluginName);

                lock (Plugins)
                {
                    if (!Plugins.Contains(plugin))
                    {
                        Plugins.Add(plugin);
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBoxes.ShowError(owner, exception.ToString());
            }
        }

        _isLoaded = true;
    }

    internal static void Register(IGitUICommands uiCommands, IWin32Window owner)
    {
        lock (Plugins)
        {
            foreach (IGitPlugin plugin in Plugins)
            {
                if (!_registeredPlugins.Add(plugin))
                {
                    continue;
                }

                try
                {
                    plugin.Register(uiCommands);
                }
                catch (Exception exception)
                {
                    _registeredPlugins.Remove(plugin);
                    MessageBoxes.ShowError(owner, exception.ToString());
                }
            }
        }
    }

    internal static void Unregister(IGitUICommands uiCommands, IWin32Window owner)
    {
        lock (Plugins)
        {
            foreach (IGitPlugin plugin in Plugins.Where(_registeredPlugins.Contains).ToArray())
            {
                try
                {
                    plugin.Unregister(uiCommands);
                }
                catch (Exception exception)
                {
                    MessageBoxes.ShowError(owner, exception.ToString());
                }
                finally
                {
                    _registeredPlugins.Remove(plugin);
                }
            }
        }
    }
}
