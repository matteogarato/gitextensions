using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace GitCommands.Git;

public static partial class DetachedHeadParser
{
    internal const string DetachedBranch = "(no branch)";
    internal const string UnknownBranchName = "???";

    private static readonly string[] _detachedPrefixes = ["(no branch", "(detached from ", "(HEAD detached at "];

    [GeneratedRegex(@"^\(.* (?<sha1>.*)\)$", RegexOptions.ExplicitCapture)]
    private static partial Regex ShaRegex { get; }

    public static bool IsDetachedHead([NotNullWhen(true)] string? branch)
    {
        return branch is not null && _detachedPrefixes.Any(a => branch.StartsWith(a, StringComparison.Ordinal));
    }

    public static bool TryParse(string text, [NotNullWhen(true)] out string? sha1)
    {
        sha1 = null;
        if (!IsDetachedHead(text))
        {
            return false;
        }

        var sha1Match = ShaRegex.Match(text);
        if (!sha1Match.Success)
        {
            return false;
        }

        sha1 = sha1Match.Groups["sha1"].Value;
        return true;
    }
}
