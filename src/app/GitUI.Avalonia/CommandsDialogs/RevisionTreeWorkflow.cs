using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitUI.CommandsDialogs;

/// <summary>
///  One row of the revision file tree: a directory, a file or a submodule directly below the browsed directory.
/// </summary>
public sealed record RevisionTreeEntry(string DisplayName, string Path, GitObjectType ObjectType, ObjectId ObjectId)
{
    public bool IsDirectory => ObjectType == GitObjectType.Tree;

    public bool IsSubmodule => ObjectType == GitObjectType.Commit;

    public override string ToString() => IsDirectory ? DisplayName + "/" : IsSubmodule ? DisplayName + " (submodule)" : DisplayName;
}

/// <summary>
///  Lists and navigates the content of a revision's tree, one directory level at a time.
/// </summary>
public static class RevisionTreeWorkflow
{
    /// <summary>
    ///  Returns the entries of <paramref name="directory"/> at <paramref name="revision"/>, directories first.
    /// </summary>
    /// <param name="module">The repository.</param>
    /// <param name="revision">The revision whose tree is browsed.</param>
    /// <param name="directory">The directory path without trailing slash, or empty for the root.</param>
    /// <param name="cancellationToken">A token to cancel the listing.</param>
    public static IReadOnlyList<RevisionTreeEntry> ListDirectory(IGitModule module, ObjectId revision, string directory, CancellationToken cancellationToken = default)
    {
        string pathSpec = directory.Length == 0 ? string.Empty : directory + "/";
        return Sort(module.GetTree(revision, full: false, pathSpec, cancellationToken), directory);
    }

    /// <summary>
    ///  Converts git tree items into display entries, directories first and then alphabetically.
    /// </summary>
    internal static IReadOnlyList<RevisionTreeEntry> Sort(IEnumerable<IObjectGitItem> items, string directory)
    {
        string prefix = directory.Length == 0 ? string.Empty : directory + "/";
        return [.. items
            .Select(item => new RevisionTreeEntry(
                item.Name.StartsWith(prefix, StringComparison.Ordinal) ? item.Name[prefix.Length..] : item.Name,
                item.Name,
                item.ObjectType,
                item.ObjectId))
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    ///  Returns the parent of <paramref name="directory"/>, or <see langword="null"/> when already at the root.
    /// </summary>
    public static string? GetParentDirectory(string directory)
    {
        if (directory.Length == 0)
        {
            return null;
        }

        int index = directory.LastIndexOf('/');
        return index < 0 ? string.Empty : directory[..index];
    }
}
