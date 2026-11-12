using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Maps stash operations onto the shared git argument builders, so the Avalonia UI uses the same
///     git semantics as the WinForms stash dialog and <c>GitUICommands</c>.
/// </summary>
public static class StashWorkflow
{
    public static ArgumentString BuildSave(bool includeUntrackedFiles, bool keepIndex, string? message,
        IReadOnlyList<string>? selectedFiles = null)
    {
        return Commands.StashSave(includeUntrackedFiles, keepIndex, message?.Trim() ?? string.Empty, selectedFiles);
    }

    public static ArgumentString BuildSaveStaged(string? message)
    {
        return new GitArgumentBuilder("stash")
        {
            "push", "--staged", string.IsNullOrWhiteSpace(message) ? null : $"-m {message.Trim().Quote()}"
        };
    }

    public static ArgumentString BuildApply(GitStash stash)
    {
        return new GitArgumentBuilder("stash") { "apply", stash.Name.Quote() };
    }

    public static ArgumentString BuildApplyLatest()
    {
        return new GitArgumentBuilder("stash") { "apply", "stash@{0}".Quote() };
    }

    public static ArgumentString BuildPop(GitStash? stash)
    {
        return new GitArgumentBuilder("stash") { "pop", stash?.Name.QuoteNe() };
    }

    public static ArgumentString BuildDrop(GitStash stash)
    {
        return new GitArgumentBuilder("stash") { "drop", stash.Name.Quote() };
    }

    public static ArgumentString BuildClear()
    {
        return new GitArgumentBuilder("stash") { "clear" };
    }

    /// <summary>
    ///     Returns a user-facing error if there is nothing to stash, otherwise <see langword="null" />.
    /// </summary>
    public static string? ValidateSave(int changedFileCount)
    {
        return changedFileCount > 0 ? null : TranslatedStrings.NothingToStashError;
    }
}
