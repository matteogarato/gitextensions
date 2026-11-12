using System.Text.RegularExpressions;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitCommands.Git.Gpg;

public interface IGitGpgController
{
    /// <summary>
    ///     Obtain the commit signature status on current revision.
    /// </summary>
    /// <returns>Enum value that indicate the gpg status for current git revision.</returns>
    Task<CommitStatus> GetRevisionCommitSignatureStatusAsync(GitRevision revision);

    /// <summary>
    ///     Obtain the commit verification message, coming from <c>--pretty="format:%GG"</c>
    /// </summary>
    /// <returns>Full string coming from GPG analysis on current revision.</returns>
    string GetCommitVerificationMessage(GitRevision revision);

    /// <summary>
    ///     Obtain the tag status on current revision.
    /// </summary>
    /// <returns>
    ///     Enum value that indicate if current git revision has one tag with good signature, one tag with bad signature
    ///     or more than one tag.
    /// </returns>
    Task<TagStatus> GetRevisionTagSignatureStatusAsync(GitRevision revision);

    /// <summary>
    ///     Obtain the tag verification message for all the tag in current git revision
    /// </summary>
    /// <returns>Full concatenated string coming from GPG analysis on all tags on current git revision.</returns>
    string? GetTagVerifyMessage(GitRevision revision);
}

public partial class GitGpgController : IGitGpgController
{
    /* Commit GPG status */
    private const string _goodSign = "G";
    private const string _badSign = "B";
    private const string _unkSignValidity = "U";
    private const string _expiredSign = "X";
    private const string _expiredSignKey = "Y";
    private const string _revokedKey = "R";
    private const string _missingPubKey = "E";
    private const string _noSign = "N";

    /* Tag GPG status */
    private const string _goodSignature = "GOODSIG";
    private const string _validTagSign = "VALIDSIG";
    private const string _noTagPubKey = "NO_PUBKEY";
    private const string _noSignatureFound = "error: no signature found";
    private readonly Func<IGitModule> _getModule;

    /// <summary>
    ///     Obtain the tag verification message for all the tag in current git revision
    /// </summary>
    /// <returns>Full concatenated string coming from GPG analysis on all tags on current git revision.</returns>
    public GitGpgController(Func<IGitModule> getModule)
    {
        _getModule = getModule;
    }

    [GeneratedRegex(_validTagSign, RegexOptions.ExplicitCapture)]
    private static partial Regex ValidSignatureTagRegex { get; }

    [GeneratedRegex(_goodSignature, RegexOptions.ExplicitCapture)]
    private static partial Regex GoodSignatureTagRegex { get; }

    [GeneratedRegex(_noTagPubKey, RegexOptions.ExplicitCapture)]
    private static partial Regex NoPubKeyTagRegex { get; }

    [GeneratedRegex(_noSignatureFound, RegexOptions.ExplicitCapture)]
    private static partial Regex NoSignatureFoundTagRegex { get; }

    /// <summary>
    ///     Obtain the commit signature status on current revision.
    /// </summary>
    /// <returns>Enum value that indicate the gpg status for current git revision.</returns>
    async Task<CommitStatus> IGitGpgController.GetRevisionCommitSignatureStatusAsync(GitRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var module = GetModule();

        return await Task.Run(() =>
        {
            CommitStatus cmtStatus;
            GitArgumentBuilder args = new("log") { "--pretty=\"format:%G?\"", "-1", revision.Guid };
            var gpg = module.GitExecutable.GetOutput(args);

#pragma warning disable SA1025 // Code should not contain multiple whitespace in a row
            switch (gpg)
            {
                case _goodSign: // "G" for a good (valid) signature
                    cmtStatus = CommitStatus.GoodSignature;
                    break;
                case _badSign: // "B" for a bad signature
                case _unkSignValidity: // "U" for a good signature with unknown validity
                case _expiredSign: // "X" for a good signature that has expired
                case _expiredSignKey: // "Y" for a good signature made by an expired key
                case _revokedKey: // "R" for a good signature made by a revoked key
                    cmtStatus = CommitStatus.SignatureError;
                    break;
                case _missingPubKey: // "E" if the signature cannot be checked (e.g.missing key)
                    cmtStatus = CommitStatus.MissingPublicKey;
                    break;
                case _noSign: // "N" for no signature
                default:
                    cmtStatus = CommitStatus.NoSignature;
                    break;
            }
#pragma warning restore SA1025 // Code should not contain multiple whitespace in a row

            return cmtStatus;
        });
    }

    /// <summary>
    ///     Obtain the tag status on current revision.
    /// </summary>
    /// <returns>
    ///     Enum value that indicate if current git revision has one tag with good signature, one tag with bad signature
    ///     or more than one tag.
    /// </returns>
    async Task<TagStatus> IGitGpgController.GetRevisionTagSignatureStatusAsync(GitRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        /* No Tag present, exit */
        List<IGitRef> usefulTagRefs = [.. revision.Refs.Where(x => x.IsTag && x.IsDereference)];

        if (usefulTagRefs.Count == 0)
        {
            return TagStatus.NoTag;
        }

        return await Task.Run(() =>
        {
            /* More than one tag on the revision */
            if (usefulTagRefs.Count > 1)
            {
                return TagStatus.Many;
            }

            /* Raw message to be checked */
            var rawGpgMessage = GetTagVerificationMessage(usefulTagRefs[0]);

            /* Look for icon to be shown */
            if (GoodSignatureTagRegex.IsMatch(rawGpgMessage!) && ValidSignatureTagRegex.IsMatch(rawGpgMessage!))
            {
                /* It's only one good tag */
                return TagStatus.OneGood;
            }

            if (NoSignatureFoundTagRegex.IsMatch(rawGpgMessage!))
            {
                /* One tag, but not signed */
                return TagStatus.TagNotSigned;
            }

            if (NoPubKeyTagRegex.IsMatch(rawGpgMessage!))
            {
                /* One tag, signed, but user has not the public key */
                return TagStatus.NoPubKey;
            }

            /* One tag, signed, any other error */
            return TagStatus.OneBad;
        });
    }

    /// <summary>
    ///     Obtain the commit verification message, coming from --pretty="format:%GG"
    /// </summary>
    /// <returns>Full string coming from GPG analysis on current revision.</returns>
    string IGitGpgController.GetCommitVerificationMessage(GitRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var module = GetModule();
        GitArgumentBuilder args = new("log") { "--pretty=\"format:%GG\"", "-1", revision.Guid };
        return module.GitExecutable.GetOutput(args);
    }

    /// <summary>
    ///     Obtain the tag verification message for all the tag on the revision
    /// </summary>
    /// <returns>Full string coming from GPG analysis on current revision.</returns>
    string? IGitGpgController.GetTagVerifyMessage(GitRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        List<IGitRef> usefulTagRefs = [.. revision.Refs.Where(x => x.IsTag && x.IsDereference)];
        return EvaluateTagVerifyMessage(usefulTagRefs);
    }

    private string? GetTagVerificationMessage(IGitRef tagRef, bool raw = true)
    {
        var tagName = tagRef.LocalName;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        GitArgumentBuilder args = new("verify-tag") { { raw, "--raw" }, tagName };

        // 'verify-tag' returns info in stderr
        return GetModule().GitExecutable.Execute(args, throwOnErrorExit: false).StandardError;
    }

    private string? EvaluateTagVerifyMessage(IReadOnlyList<IGitRef> usefulTagRefs)
    {
        if (usefulTagRefs.Count == 0)
        {
            return "";
        }

        if (usefulTagRefs.Count == 1)
        {
            return GetTagVerificationMessage(usefulTagRefs[0], false);
        }

        /* When _usefulTagRefs.Count > 1 */
        var tagVerifyMessage = "";

        /* Only to populate TagVerifyMessage */
        foreach (var tagRef in usefulTagRefs)
        {
            /* String printed in dialog box */
            tagVerifyMessage =
                $"{tagVerifyMessage}{tagRef.LocalName}\r\n{GetTagVerificationMessage(tagRef, false)}\r\n\r\n";
        }

        return tagVerifyMessage;
    }

    private IGitModule GetModule()
    {
        return _getModule() ?? throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");
    }
}
