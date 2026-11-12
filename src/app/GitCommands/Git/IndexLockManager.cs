using System.IO.Abstractions;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

public interface IIndexLockManager
{
    /// <summary>
    ///     Determines whether the given repository has index.lock file.
    /// </summary>
    /// <returns><see langword="true" /> if index is locked; otherwise <see langword="false" />.</returns>
    bool IsIndexLocked();

    /// <summary>
    ///     Delete index.lock in the current working folder.
    /// </summary>
    /// <param name="includeSubmodules">
    ///     If <see langword="true" /> all submodules will be scanned for index.lock files and have them delete, if found.
    /// </param>
    /// <exception cref="FileDeleteException">Unable to delete specific index.lock.</exception>
    void UnlockIndex(bool includeSubmodules = true);
}

/// <summary>
///     Facilitates detection and deletion of index.lock files.
/// </summary>
public sealed class IndexLockManager(
    IGitModule module,
    IGitDirectoryResolver gitDirectoryResolver,
    IFileSystem fileSystem)
    : IIndexLockManager
{
    private const string _indexLock = "index.lock";

    public IndexLockManager(IGitModule module)
        : this(module, new GitDirectoryResolver(), new FileSystem())
    {
    }

    /// <summary>
    ///     Determines whether the given repository has index.lock file.
    /// </summary>
    /// <returns><see langword="true" /> if index is locked; otherwise <see langword="false" />.</returns>
    bool IIndexLockManager.IsIndexLocked()
    {
        var indexLockFile = Path.Join(gitDirectoryResolver.Resolve(module.WorkingDir), _indexLock);
        return fileSystem.File.Exists(indexLockFile);
    }

    /// <summary>
    ///     Delete index.lock in the current working folder.
    /// </summary>
    /// <param name="includeSubmodules">
    ///     If <see langword="true" /> all submodules will be scanned for index.lock files and have them delete, if found.
    /// </param>
    /// <exception cref="FileDeleteException">Unable to delete specific index.lock.</exception>
    void IIndexLockManager.UnlockIndex(bool includeSubmodules = true)
    {
        var workingFolderIndexLock = Path.Join(gitDirectoryResolver.Resolve(module.WorkingDir), _indexLock);
        if (!includeSubmodules)
        {
            DeleteIndexLock(workingFolderIndexLock);
            return;
        }

        // get the list of files to delete
        var submodules = module.GetSubmodulesLocalPaths();
        var list = submodules.Select(sm =>
        {
            var submodulePath = module.GetSubmoduleFullPath(sm);
            var submoduleIndexLock = Path.Join(gitDirectoryResolver.Resolve(submodulePath), _indexLock);
            return submoduleIndexLock;
        }).Union(new[] { workingFolderIndexLock });

        foreach (var indexLock in list)
        {
            DeleteIndexLock(indexLock);
        }
    }

    private void DeleteIndexLock(string fileName)
    {
        if (!fileSystem.File.Exists(fileName))
        {
            return;
        }

        try
        {
            fileSystem.File.Delete(fileName);
        }
        catch (Exception ex)
        {
            throw new FileDeleteException(fileName, ex);
        }
    }
}
