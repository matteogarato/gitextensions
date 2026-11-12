using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Path helpers behind the changed-file list actions (copy path, open file, show in folder).
/// </summary>
public static class FilePathWorkflow
{
    /// <summary>
    ///     Returns the repository-relative paths of <paramref name="files" />, one per line, using
    ///     forward slashes like git; with <paramref name="fullPath" /> they are rooted at <paramref name="workingDir" />.
    /// </summary>
    public static string FormatPaths(IEnumerable<GitItemStatus> files, string workingDir, bool fullPath)
    {
        return string.Join(Environment.NewLine,
            files.Select(file => fullPath ? GetFullPath(workingDir, file.Name) : file.Name));
    }

    /// <summary>
    ///     Combines the working directory with a repository-relative git path.
    /// </summary>
    public static string GetFullPath(string workingDir, string gitPath)
    {
        return Path.GetFullPath(Path.Combine(workingDir, gitPath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
