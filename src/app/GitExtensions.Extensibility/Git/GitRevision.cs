using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GitExtensions.Extensibility.BuildServerIntegration;

namespace GitExtensions.Extensibility.Git;

public sealed partial class GitRevision : IGitItem, INotifyPropertyChanged
{
    /// <summary>40 characters of 1's</summary>
    public const string WorkTreeGuid = "1111111111111111111111111111111111111111";

    /// <summary>40 characters of 2's</summary>
    public const string IndexGuid = "2222222222222222222222222222222222222222";

    /// <summary>
    ///     40 characters of 2's
    ///     Artificial commit for the combined diff
    /// </summary>
    public const string CombinedDiffGuid = "3333333333333333333333333333333333333333";

    private string? _body;

    private BuildInfo? _buildStatus;

    public GitRevision(ObjectId objectId)
    {
        if (objectId.IsZero)
        {
            throw new ArgumentException("ObjectId must not be the default (zero) value.", nameof(objectId));
        }

        ObjectId = objectId;
    }

    [GeneratedRegex(@"^[a-f\d]{40}$", RegexOptions.ExplicitCapture)]
    public static partial Regex Sha1HashRegex { get; }

    [GeneratedRegex(@"\b[a-f\d]{7,40}\b(?![^@\s]*@)", RegexOptions.ExplicitCapture)]
    public static partial Regex Sha1HashShortRegex { get; }

    // TODO this should probably be null when not yet populated, similar to how ParentIds works
    public IReadOnlyList<IGitRef> Refs { get; set; } = [];

    /// <summary>
    ///     Gets the revision's parent IDs.
    /// </summary>
    /// <remarks>
    ///     Can return <c>null</c> in cases where the data has not been populated
    ///     for whatever reason.
    /// </remarks>
    public IReadOnlyList<ObjectId>? ParentIds { get; set; }

    public ObjectId TreeId { get; set; }

    public string? Author { get; set; }
    public string? AuthorEmail { get; set; }

    // Git native datetime format
    public long AuthorUnixTime { get; set; }

    public DateTime AuthorDate
    {
        get => FromUnixTimeSeconds(AuthorUnixTime);
    }

    public string? Committer { get; set; }
    public string? CommitterEmail { get; set; }
    public long CommitUnixTime { get; set; }

    public DateTime CommitDate
    {
        get => FromUnixTimeSeconds(CommitUnixTime);
    }

    public BuildInfo? BuildStatus
    {
        get => _buildStatus;
        set
        {
            if (Equals(value, _buildStatus))
            {
                return;
            }

            _buildStatus = value;
            OnPropertyChanged();
        }
    }

    public string Subject { get; set; } = "";

    /// <summary>
    ///     Full commit message (all lines), but without <see cref="Notes" />.
    /// </summary>
    public string? Body
    {
        // Body is not stored by default for older commits to reduce memory usage
        // Body do not have to be stored explicitly if same as subject and not multiline
        get => _body ?? (!HasMultiLineMessage ? Subject : null);
        set => _body = value;
    }

    public bool HasMultiLineMessage { get; set; }

    /// <summary>
    ///     Git <see href="https://git-scm.com/docs/git-notes">Notes</see>
    /// </summary>
    /// <remarks>
    ///     <see langword="null" /> means "not loaded yet", <see cref="string.Empty" /> means "empty".
    /// </remarks>
    public string? Notes { get; set; }

    /// <summary>
    ///     Indicates whether the commit is an artificial commit.
    /// </summary>
    public bool IsArtificial
    {
        get => ObjectId.IsArtificial;
    }

    /// <summary>
    ///     Indicates whether the commit is an autostash commit.
    /// </summary>
    public bool IsAutostash { get; set; }

    /// <summary>
    ///     Indicates whether the commit is a main stash commit.
    /// </summary>
    public bool IsStash
    {
        get => ReflogSelector is not null;
    }

    /// <summary>
    ///     The reflog selector, contains the stash name like "stash{0}"
    /// </summary>
    public string? ReflogSelector { get; set; }

    public bool HasParent
    {
        get => ParentIds?.Count > 0;
    }

    public ObjectId FirstParentId
    {
        get => HasParent ? ParentIds![0] : default;
    }

    ObjectId IGitItem.ObjectId { get; }

    string IGitItem.Guid
    {
        get => ObjectId.ToString();
    }

    /// <summary>
    ///     Make a shallow clone of the object.
    /// </summary>
    /// <returns>A shallow copy.</returns>
    public GitRevision Clone()
    {
        return (GitRevision)MemberwiseClone();
    }

    private static DateTime FromUnixTimeSeconds(long unixTime)
    {
        return unixTime == 0 ? DateTime.MaxValue : DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
    }

    public override string ToString()
    {
        return $"{ObjectId.ToShortString()}:{Subject}";
    }

    /// <summary>
    ///     Returns a value indicating whether <paramref name="id" /> is a valid SHA-1 hash.
    /// </summary>
    /// <remarks>
    ///     To be valid the string must contain exactly 40 lower-case hexadecimal characters.
    /// </remarks>
    /// <param name="id">The string to validate.</param>
    /// <returns><c>true</c> if <paramref name="id" /> is a valid SHA-1 hash, otherwise <c>false</c>.</returns>
    public static bool IsFullSha1Hash(string id)
    {
        return Sha1HashRegex.IsMatch(id);
    }

    #region INotifyPropertyChanged

    private event PropertyChangedEventHandler? PropertyChanged;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
