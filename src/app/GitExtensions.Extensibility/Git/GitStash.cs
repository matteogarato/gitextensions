using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace GitExtensions.Extensibility.Git;

/// <summary>Stored local modifications.</summary>
public sealed partial class GitStash(int index, string message)
{
    [GeneratedRegex(@"^stash@\{(?<index>\d+)\}: (?<message>.+)$", RegexOptions.ExplicitCapture)]
    private static partial Regex StashRegex { get; }

    /// <summary>Short description of the commit the stash was based on.</summary>
    public string Message { get; } = message;

    /// <summary>Gets the index of the stash in the list.</summary>
    public int Index { get; } = index;

    /// <summary>Name of the stash.</summary>
    /// <remarks>"stash@{n}"</remarks>
    public string Name
    {
        get => $"stash@{{{Index}}}";
    }

    public string Summary
    {
        get => Index == -1 ? Message : $"@{{{Index}}}: {Message}";
    }

    public static bool TryParse(string s, [NotNullWhen(true)] out GitStash? stash)
    {
        // "stash@{i}: WIP on {branch}: {PreviousCommitMiniSHA} {PreviousCommitMessage}"
        // "stash@{i}: On {branch}: {Message}"
        // "stash@{i}: autostash"

        var match = StashRegex.Match(s);

        if (!match.Success)
        {
            stash = default;
            return false;
        }

        stash = new GitStash(
            int.Parse(match.Groups["index"].ValueSpan),
            match.Groups["message"].Value);

        return true;
    }

    public override string ToString()
    {
        return Message;
    }
}
