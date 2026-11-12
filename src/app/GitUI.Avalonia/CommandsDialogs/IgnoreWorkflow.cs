using System.Text;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Adds files to the repository <c>.gitignore</c>.
/// </summary>
public static class IgnoreWorkflow
{
    private const string _gitIgnoreFileName = ".gitignore";

    /// <summary>
    ///     Appends root-anchored patterns for <paramref name="paths" /> to the <c>.gitignore</c> in
    ///     <paramref name="workingDir" />, skipping patterns that are already present.
    /// </summary>
    /// <returns>The patterns that were added.</returns>
    public static IReadOnlyList<string> AddToGitIgnore(string workingDir, IEnumerable<string> paths)
    {
        var ignoreFile = Path.Combine(workingDir, _gitIgnoreFileName);
        var existing = File.Exists(ignoreFile) ? File.ReadAllText(ignoreFile) : string.Empty;
        HashSet<string> knownPatterns = [.. existing.Split('\n').Select(line => line.TrimEnd('\r'))];

        List<string> added = [];
        foreach (var path in paths)
        {
            var pattern = BuildPattern(path);
            if (pattern.Length > 1 && knownPatterns.Add(pattern))
            {
                added.Add(pattern);
            }
        }

        if (added.Count == 0)
        {
            return added;
        }

        StringBuilder content = new(existing);
        if (content.Length > 0 && content[^1] != '\n')
        {
            content.Append(Environment.NewLine);
        }

        foreach (var pattern in added)
        {
            content.Append(pattern).Append(Environment.NewLine);
        }

        File.WriteAllText(ignoreFile, content.ToString(), new UTF8Encoding(false));

        return added;
    }

    /// <summary>
    ///     Builds a root-anchored ignore pattern for a repository-relative path.
    /// </summary>
    public static string BuildPattern(string path)
    {
        StringBuilder pattern = new("/");
        foreach (var character in path.Replace('\\', '/').Trim('/'))
        {
            if (character is '*' or '?' or '[' or ']' or ' ' or '#' or '!')
            {
                pattern.Append('\\');
            }

            pattern.Append(character);
        }

        return pattern.ToString();
    }
}
