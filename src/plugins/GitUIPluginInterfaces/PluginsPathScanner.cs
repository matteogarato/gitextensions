namespace GitUIPluginInterfaces;

/// <summary>
///     A helper class for scanning for plugin files.
/// </summary>
public static class PluginsPathScanner
{
    /// <summary>
    ///     Searches for plugin files in the <paramref name="pluginsPaths" />.
    ///     Returns dll files
    ///     - located in the folder for backward compatibility,
    ///     - located in the first level of sub directories for the new plugin structure.
    /// </summary>
    /// <example>
    ///     Plugins
    ///     \OldPlugin1.dll
    ///     \OldPlugin2.dll
    ///     \OldPlugin2.config
    ///     \NewPlugin1
    ///     \Plugin1.dll
    ///     \Plugin1.config
    ///     \NewPlugin2
    ///     \Plugin2.dll
    ///     \Plugin2.config
    /// </example>
    /// <param name="pluginsPaths">An array of filesystem paths to search for plugins in.</param>
    /// <returns>An enumeration of found dll files.</returns>
    public static IEnumerable<FileInfo> GetFiles(params string?[] pluginsPaths)
    {
        IEnumerable<FileInfo> result = [];

        foreach (var pluginsPath in pluginsPaths)
        {
            if (!string.IsNullOrEmpty(pluginsPath) && Directory.Exists(pluginsPath))
            {
                DirectoryInfo directory = new(pluginsPath);

                result = result.Concat(directory.GetFiles("*.dll"));

                foreach (var childDirectory in directory.GetDirectories())
                {
                    result = result.Concat(childDirectory.GetFiles("*.dll"));
                }
            }
        }

        return result;
    }
}
