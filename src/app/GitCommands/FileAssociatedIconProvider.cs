using System.Collections.Concurrent;
using System.IO.Abstractions;

namespace GitCommands;

/// <summary>
///     Provides the ability to extract icons associated with file types.
/// </summary>
public interface IFileAssociatedIconProvider
{
    /// <summary>
    ///     Retrieves the icon associated with the given file type.
    /// </summary>
    /// <param name="workingDirectory">The git repository working directory.</param>
    /// <param name="relativeFilePath">The relative path to the file.</param>
    /// <returns>The icon associated with the given file type or <see langword="null" />.</returns>
    Icon? Get(string workingDirectory, string relativeFilePath);
}

public sealed class FileAssociatedIconProvider(IFileSystem fileSystem) : IFileAssociatedIconProvider
{
    private static readonly ConcurrentDictionary<string, Icon?>
        _loadedFileIcons = new(StringComparer.OrdinalIgnoreCase);

    public FileAssociatedIconProvider()
        : this(new FileSystem())
    {
    }

    // unit tests only
    internal int CacheCount
    {
        get => _loadedFileIcons.Count;
    }

    /// <summary>
    ///     Retrieves the icon associated with the given file type.
    ///     The retrieved icons are cached by extensions.
    /// </summary>
    /// <param name="workingDirectory">The git repository working directory.</param>
    /// <param name="relativeFilePath">The relative path to the file.</param>
    /// <returns>The icon associated with the given file type or <see langword="null" />.</returns>
    /// <remarks>
    ///     The method takes two parameters to performance reasons - the full path is established
    ///     only if the file type has not been processed already and the extensions is not cached.
    /// </remarks>
    Icon? IFileAssociatedIconProvider.Get(string workingDirectory, string relativeFilePath)
    {
        var extension = Path.GetExtension(relativeFilePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var icon = _loadedFileIcons.GetOrAdd(extension, ext =>
        {
            string? tempFile = null;
            try
            {
                // if the file doesn't exist - create a blank temp file with the required extension
                // so we can call Icon.ExtractAssociatedIcon on it
                // this may have a slight overhead, however an alternative would be extracting
                // extensions from the registry and using p/invokes and WinAPI, which have
                // significantly higher maintenance overhead.

                var fullPath = Path.Combine(workingDirectory, relativeFilePath);
                if (!fileSystem.File.Exists(fullPath))
                {
                    tempFile = CreateTempFile(Path.GetFileName(fullPath));
                    fullPath = tempFile;
                }

                return Icon.ExtractAssociatedIcon(fullPath);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFile))
                {
                    DeleteFile(tempFile);
                }
            }
        });
        return icon;
    }

    private string CreateTempFile(string fileName)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), fileName);
        fileSystem.File.WriteAllText(tempFile, string.Empty);
        return tempFile;
    }

    private void DeleteFile(string path)
    {
        try
        {
            fileSystem.File.Delete(path);
        }
        catch
        {
            // do nothing
        }
    }

    // unit tests only
    internal void ResetCache()
    {
        _loadedFileIcons.Clear();
    }
}
