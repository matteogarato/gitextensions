using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitCommands;

public interface ICommitDataManager
{
    event EventHandler<GitRevision> RevisionDetailsLoaded;

    /// <summary>
    ///     Converts a <see cref="GitRevision" /> object into a <see cref="CommitData" />.
    /// </summary>
    /// <remarks>
    ///     The <see cref="GitRevision" /> object contains all required fields, so no additional
    ///     data lookup is required to populate the returned <see cref="CommitData" /> object.
    /// </remarks>
    /// <param name="revision">The <see cref="GitRevision" /> to convert from.</param>
    /// <param name="children">The list of children to add to the returned object.</param>
    CommitData CreateFromRevision(GitRevision revision, IReadOnlyList<ObjectId>? children);

    /// <summary>
    ///     Gets <see cref="CommitData" /> for the specified <paramref name="commitId" />.
    /// </summary>
    /// <param name="commitId">The sha or Git reference.</param>
    /// <param name="includeNotes">
    ///     Include Notes with the commit info. This also means that the Git command is not cached.
    ///     Note that Notes are only needed if the full Body with Notes is to be used, regardless of Settings.
    /// </param>
    CommitData? GetCommitData(string commitId, bool includeNotes = false);

    /// <summary>
    ///     Requests background loading of <see cref="GitRevision.Body" /> (commit message) and
    ///     <see cref="GitRevision.Notes" /> properties of <paramref name="revision" />.
    ///     <br />The last request wins. The execution is delayed in order to avoid loading data for a revision which has been
    ///     scrolled out of view.
    ///     <br />Emits <see cref="RevisionDetailsLoaded" /> when finished.
    /// </summary>
    void InitiateDelayedLoadingOfDetails(GitRevision revision);

    /// <summary>
    ///     Updates the <see cref="GitRevision.Body" /> (commit message) and <see cref="GitRevision.Notes" /> properties of
    ///     <paramref name="revision" />.
    /// </summary>
    void UpdateBodyAndNotes(GitRevision revision);
}

public sealed class CommitDataManager(Func<IGitModule> getModule) : ICommitDataManager
{
    private readonly CancellationTokenSequence _cancellationTokenSequence = new();

    event EventHandler<GitRevision>? ICommitDataManager.RevisionDetailsLoaded
    {
        add => RevisionDetailsLoaded += value;
        remove => RevisionDetailsLoaded -= value;
    }

    void ICommitDataManager.InitiateDelayedLoadingOfDetails(GitRevision revision)
    {
        var cancellationToken = _cancellationTokenSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            const int millisecondsDelayBetweenSubsequentDetailLoading = 100;
            await Task.Delay(millisecondsDelayBetweenSubsequentDetailLoading, cancellationToken);
            if (revision.Notes is null || revision.Body is null)
            {
                UpdateBodyAndNotes(revision);
            }
        });
    }

    void ICommitDataManager.UpdateBodyAndNotes(GitRevision revision)
    {
        var appendNotesOnly = revision.Body is not null;
        const string bodyAndNotesFormat = $"%B{RevisionReader.NotesFormat}";
        const string notesFormat = "%N";

        if (!TryGetCommitLog(revision.ObjectId.ToString(), appendNotesOnly ? notesFormat : bodyAndNotesFormat,
                out var error, out var data, false))
        {
            Trace.WriteLine($"Exception in {nameof(UpdateBodyAndNotes)}: {error}", "git");
            return;
        }

        // Commit message is not re-encoded by Git when format is given
        data = GetModule().ReEncodeCommitMessage(data.Replace('\v', '\n'));

        try
        {
            if (appendNotesOnly)
            {
                revision.Notes = data;
                return;
            }

            var splitPos = data.LastIndexOf(RevisionReader.NotesMarkerWithoutTrailingLf);
            revision.Body = data[..splitPos].TrimEnd();
            splitPos += RevisionReader.NotesMarkerWithoutTrailingLf.Length + /*LF*/ 1;
            revision.Notes = splitPos >= data.Length ? "" : data[splitPos..];
        }
        finally
        {
            RevisionDetailsLoaded?.Invoke(this, revision);
        }
    }

    CommitData? ICommitDataManager.GetCommitData(string commitId, bool includeNotes = false)
    {
        var revision =
            new RevisionReader(GetModule(), true).GetRevision(commitId, includeNotes, false, default);
        return revision is not null
            ? CreateFromRevision(revision, null)
            : null;
    }

    CommitData ICommitDataManager.CreateFromRevision(GitRevision revision, IReadOnlyList<ObjectId>? children)
    {
        ArgumentNullException.ThrowIfNull(revision);

        if (revision.ObjectId.IsZero)
        {
            throw new ArgumentException($"Cannot have a zero {nameof(GitRevision.ObjectId)}.", nameof(revision));
        }

        return new CommitData(revision.ObjectId, revision.ParentIds,
            FormatUser(revision.Author, revision.AuthorEmail), revision.AuthorDate,
            FormatUser(revision.Committer, revision.CommitterEmail), revision.CommitDate,
            revision.Body ?? revision.Subject) { ChildIds = children, Notes = revision.Notes };

        static string? FormatUser(string? user, string? email)
        {
            if (user is null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(email) ? user : $"{user} <{email}>";
        }
    }

    private event EventHandler<GitRevision>? RevisionDetailsLoaded;

    private IGitModule GetModule()
    {
        return getModule() ?? throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");
    }

    private bool TryGetCommitLog(string commitId, string format, [NotNullWhen(false)] out string? error,
        [NotNullWhen(true)] out string? data, bool cache)
    {
        if (commitId.IsArtificial())
        {
            data = null;
            error = "No log information for artificial commits";
            return false;
        }

        GitArgumentBuilder arguments = new("log") { "-1", $"--pretty=\"format:{format}\"", commitId.Quote() };

        // This command can be cached if commitId is a git sha and Notes are ignored
        DebugHelpers.Assert(!cache || ObjectId.TryParse(commitId, out _),
            $"git-log cache should be used only for sha ({commitId})");

        var exec = GetModule().GitExecutable.Execute(arguments,
            outputEncoding: GitModule.LosslessEncoding,
            cache: cache ? GitModule.GitCommandCache : null, throwOnErrorExit: false);

        if (!exec.ExitedSuccessfully)
        {
            data = null;
            error = "Cannot find commit " + commitId;
            return false;
        }

        data = exec.StandardOutput;
        error = null;
        return true;
    }
}
