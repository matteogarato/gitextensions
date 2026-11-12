using GitCommands.Settings;
using GitExtUtils.GitUI;
using Microsoft.VisualStudio.Threading;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitCommands.Git.Gpg;

/// <summary>
///     Provides methods to load GPG information for a given Git revision.
/// </summary>
public interface IGpgInfoProvider
{
    /// <summary>
    ///     Asynchronously loads GPG information for the specified Git revision.
    /// </summary>
    /// <param name="revision">The Git revision to load GPG information for.</param>
    /// <param name="cancellationToken">A token used to cancel a stale, no-longer-relevant request.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the GPG information, or
    ///     <c>null</c> if no information is available.
    /// </returns>
    Task<GpgInfo?> LoadGpgInfoAsync(GitRevision? revision, CancellationToken cancellationToken);
}

public class GpgInfoProvider(IGitGpgController gitGpgController) : IGpgInfoProvider
{
    private readonly IGitGpgController _gitGpgController = gitGpgController;

    async Task<GpgInfo?> IGpgInfoProvider.LoadGpgInfoAsync(GitRevision? revision, CancellationToken cancellationToken)
    {
        if (!AppSettings.ShowGpgInformation.Value || revision?.ObjectId is null)
        {
            return null;
        }

        await TaskScheduler.Default;
        cancellationToken.ThrowIfCancellationRequested();
        var getCommitSignature = _gitGpgController.GetRevisionCommitSignatureStatusAsync(revision);
        var getTagSignature = _gitGpgController.GetRevisionTagSignatureStatusAsync(revision);
        await Task.WhenAll(getCommitSignature, getTagSignature);
        cancellationToken.ThrowIfCancellationRequested();

        var commitStatus = getCommitSignature.CompletedResult();
        var tagStatus = getTagSignature.CompletedResult();

        // Absence of Commit sign and Tag sign
        if (commitStatus == CommitStatus.NoSignature && tagStatus == TagStatus.NoTag)
        {
            return null;
        }

        return new GpgInfo(commitStatus,
            _gitGpgController.GetCommitVerificationMessage(revision),
            tagStatus,
            _gitGpgController.GetTagVerifyMessage(revision));
    }
}
