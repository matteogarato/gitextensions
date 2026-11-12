using System.IO.Abstractions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitCommands.Git;

public interface IGitRevisionTester
{
    /// <summary>
    ///     Finds if all of the first selected are parents to the selected revision
    /// </summary>
    /// <param name="firstSelected">The first selected revisions (A)</param>
    /// <param name="selectedRevision">The currently (last) selected revision (B)</param>
    /// <returns>
    ///     True if one of the first selected is parent
    /// </returns>
    bool AllFirstAreParentsToSelected(IEnumerable<ObjectId>? firstSelected, GitRevision? selectedRevision);

    /// <summary>
    ///     Finds if any of the git items exists as a file.
    /// </summary>
    /// <param name="selectedItemsWithParent">List of items to resolve and check</param>
    /// <returns>
    ///     True if at least one file exists.
    /// </returns>
    bool AnyLocalFileExists(IEnumerable<GitItemStatus>? selectedItemsWithParent);

    bool Matches(GitRevision? revision, string criteria);
}

public class GitRevisionTester(IFullPathResolver fullPathResolver, IFileSystem? fileSystem = null)
    : IGitRevisionTester
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();

    /// <inheritdoc />
    bool IGitRevisionTester.AllFirstAreParentsToSelected(IEnumerable<ObjectId>? firstSelected,
        GitRevision? selectedRevision)
    {
        if (selectedRevision?.ParentIds is null || firstSelected is null)
        {
            return false;
        }

        foreach (var item in firstSelected)
        {
            if (!selectedRevision.ParentIds.Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    bool IGitRevisionTester.AnyLocalFileExists(IEnumerable<GitItemStatus>? selectedItemsWithParent)
    {
        if (selectedItemsWithParent is null)
        {
            return false;
        }

        var items = selectedItemsWithParent as List<GitItemStatus> ?? [.. selectedItemsWithParent];
        var localExists = items.Any(item => !item.IsTracked);
        if (localExists)
        {
            return true;
        }

        foreach (var item in items)
        {
            var filePath = fullPathResolver.Resolve(item.Name);
            if (_fileSystem.File.Exists(filePath))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    bool IGitRevisionTester.Matches(GitRevision? revision, string criteria)
    {
        if (revision is null || string.IsNullOrWhiteSpace(criteria))
        {
            // don't throw exception for performance reasons
            return false;
        }

        if (revision.Refs.Where(gitHead => !string.IsNullOrWhiteSpace(gitHead.Name))
            .Any(gitHead => gitHead.Name.Contains(criteria, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (criteria.Length > 2 &&
            revision.Guid?.StartsWith(criteria, StringComparison.CurrentCultureIgnoreCase) == true)
        {
            return true;
        }

        return revision.Author?.IndexOf(criteria, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
               (revision.Body ?? revision.Subject)?.IndexOf(criteria, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
