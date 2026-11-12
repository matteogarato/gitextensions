using System.Text.RegularExpressions;
using GitCommands.Git;
using GitExtensions.Extensibility;

namespace GitUI.CommandsDialogs;

/// <summary>
///     The options chosen by the user for a commit.
/// </summary>
public sealed record CommitRequest(
    string Message,
    bool Amend,
    bool SignOff,
    bool NoVerify,
    string Author = "",
    bool? GpgSign = null,
    string GpgKeyId = "",
    bool ResetAuthor = false,
    bool UsingCommitTemplate = false,
    string TemplateText = "",
    bool PushAfterCommit = false,
    bool UseExplicitCommitMessage = true);

/// <summary>
///     A previously used commit message and its one-line display text.
/// </summary>
public sealed record CommitMessageHistoryItem(string Message, string Preview);

/// <summary>
///     Validates a <see cref="CommitRequest" /> and maps it onto the shared <see cref="Commands.Commit" /> builder,
///     so the Avalonia UI uses the same git semantics as the WinForms commit dialog.
/// </summary>
public static class CommitWorkflow
{
    private const string _amendCommitPrefix = "amend!";
    private const string _fixupCommitPrefix = "fixup!";
    private const string _squashCommitPrefix = "squash!";

    /// <summary>
    ///     Returns a user-facing error if the commit cannot be created, otherwise <see langword="null" />.
    /// </summary>
    public static string? Validate(CommitRequest request, int stagedCount, bool isMergeInProgress,
        bool hasUnresolvedConflicts)
    {
        if (hasUnresolvedConflicts)
        {
            return TranslatedStrings.UnresolvedConflicts;
        }

        if (request.UseExplicitCommitMessage
            && (string.IsNullOrWhiteSpace(request.Message)
                || (request.UsingCommitTemplate && request.Message == request.TemplateText)))
        {
            return TranslatedStrings.EnterCommitMessageError;
        }

        // An amend may only change the message and a merge commit may legitimately have nothing staged.
        if (stagedCount == 0 && !request.Amend && !isMergeInProgress)
        {
            return TranslatedStrings.NoStagedChangesError;
        }

        return null;
    }

    public static ArgumentString BuildCommand(
        CommitRequest request,
        int stagedCount,
        string commitMessagePath,
        Func<string?, string?> getPathForGitExecution)
    {
        return Commands.Commit(
            request.Amend,
            request.SignOff,
            request.Author,
            request.UseExplicitCommitMessage,
            request.UseExplicitCommitMessage ? commitMessagePath : null,
            getPathForGitExecution,
            request.NoVerify,
            request.GpgSign,
            request.GpgKeyId,
            stagedCount == 0,
            request.ResetAuthor);
    }

    /// <summary>
    ///     Returns warnings for configured commit-message validation rules.
    /// </summary>
    /// <param name="message">The commit message to validate.</param>
    /// <returns>The validation warnings that require confirmation.</returns>
    public static IReadOnlyList<string> GetMessageValidationWarnings(string message)
    {
        List<string> warnings = [];
        var lines = message.Split(Delimiters.NewLines, StringSplitOptions.RemoveEmptyEntries);

        if (AppSettings.CommitValidationMaxCntCharsFirstLine > 0
            && lines.Length > 0
            && lines[0].Length > AppSettings.CommitValidationMaxCntCharsFirstLine)
        {
            warnings.Add(TranslatedStrings.CommitMsgFirstLineInvalid);
        }

        if (AppSettings.CommitValidationMaxCntCharsPerLine > 0)
        {
            foreach (var line in lines)
            {
                if (line.Length > AppSettings.CommitValidationMaxCntCharsPerLine)
                {
                    warnings.Add(string.Format(TranslatedStrings.CommitMsgLineInvalid, line));
                }
            }
        }

        if (AppSettings.CommitValidationSecondLineMustBeEmpty)
        {
            var allLines = message.Split(Delimiters.NewLines, StringSplitOptions.None);
            if (allLines.Length > 2 && allLines[1].Length > 0)
            {
                warnings.Add(TranslatedStrings.CommitMsgSecondLineNotEmpty);
            }
        }

        if (!string.IsNullOrEmpty(AppSettings.CommitValidationRegEx)
            && !message.StartsWith(_fixupCommitPrefix, StringComparison.Ordinal)
            && !message.StartsWith(_squashCommitPrefix, StringComparison.Ordinal)
            && !Regex.IsMatch(GetTextToValidate(message), AppSettings.CommitValidationRegEx))
        {
            warnings.Add(TranslatedStrings.CommitMsgRegExNotMatched);
        }

        return warnings;

        static string GetTextToValidate(string text)
        {
            if (!text.StartsWith(_amendCommitPrefix, StringComparison.Ordinal)
                || !text.ContainsAny(Delimiters.LineFeedAndCarriageReturnSearchValues))
            {
                return text;
            }

            var lines = text.Split(Delimiters.NewLines, StringSplitOptions.None);
            if (lines.Length > 2 && lines[1].Length == 0)
            {
                return string.Join(Environment.NewLine, lines.AsSpan(2));
            }

            return text;
        }
    }

    /// <summary>
    ///     Builds a distinct, bounded history list with the last saved message first.
    /// </summary>
    /// <param name="previousMessages">Messages read from repository history.</param>
    /// <param name="lastMessage">The last message saved by the application.</param>
    /// <param name="maxCount">The maximum number of messages to return.</param>
    /// <returns>The messages and their one-line previews.</returns>
    public static IReadOnlyList<CommitMessageHistoryItem> BuildMessageHistory(
        IEnumerable<string?> previousMessages,
        string lastMessage,
        int maxCount)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        List<string> messages =
        [
            .. previousMessages
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message?.TrimEnd('\r', '\n') ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .Take(maxCount)
        ];

        if (!string.IsNullOrWhiteSpace(lastMessage))
        {
            var normalizedLastMessage = lastMessage.TrimEnd('\r', '\n');
            messages.RemoveAll(message => message == normalizedLastMessage);
            messages.Insert(0, normalizedLastMessage);
            if (messages.Count > maxCount)
            {
                messages.RemoveAt(maxCount);
            }
        }

        return messages.Select(message => new CommitMessageHistoryItem(
                message,
                message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty))
            .ToArray();
    }
}
