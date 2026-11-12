using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Validates branch operations and maps them onto the shared <see cref="Commands" /> builders,
///     so the Avalonia UI uses the same git semantics as the WinForms branch dialogs.
/// </summary>
public static class BranchWorkflow
{
    public static bool CanCheckout(IGitRef? gitRef)
    {
        return gitRef is { IsTag: false, IsSelected: false } && (gitRef.IsHead || gitRef.IsRemote);
    }

    public static bool CanCheckoutRevision(GitRevision? revision)
    {
        return revision is not null && !revision.IsArtificial;
    }

    public static string GetSuggestedBranchName(IGitRef? selectedRef, GitRevision? selectedRevision)
    {
        IEnumerable<IGitRef> references = selectedRef is not null
            ? [selectedRef]
            : selectedRevision?.Refs ?? [];
        var preferredRef = references.FirstOrDefault(gitRef => !gitRef.IsTag) ?? references.FirstOrDefault();
        return preferredRef?.LocalName ?? string.Empty;
    }

    public static bool CanDelete(IGitRef? gitRef)
    {
        return gitRef is { IsTag: false, IsSelected: false } && (gitRef.IsHead || gitRef.IsRemote);
    }

    public static bool CanDeleteRemoteBranch(IGitRef? gitRef)
    {
        return gitRef is { IsRemote: true, IsTag: false }
               && !string.IsNullOrWhiteSpace(gitRef.Remote)
               && !string.Equals(gitRef.LocalName, "HEAD", StringComparison.Ordinal);
    }

    public static IGitRef? FindTrackingBranch(IGitRef remoteBranch, IEnumerable<IGitRef> localBranches)
    {
        ArgumentNullException.ThrowIfNull(remoteBranch);
        ArgumentNullException.ThrowIfNull(localBranches);

        return localBranches.FirstOrDefault(branch => branch.IsTrackingRemote(remoteBranch));
    }

    public static bool CanForceDelete(IGitRef? gitRef)
    {
        return gitRef is { IsHead: true, IsRemote: false, IsTag: false, IsSelected: false };
    }

    /// <summary>
    ///     Finds a worktree that currently has the specified local branch checked out.
    /// </summary>
    /// <param name="gitRef">The branch reference to find.</param>
    /// <param name="worktrees">The repository's known worktrees.</param>
    /// <returns>The matching worktree, or <see langword="null" /> when the branch is not checked out.</returns>
    public static GitWorktree? FindWorktreeUsingBranch(IGitRef gitRef, IReadOnlyList<GitWorktree> worktrees)
    {
        return gitRef.IsHead
            ? worktrees.FirstOrDefault(worktree =>
                worktree.HeadType is GitWorktreeHeadType.Branch
                && !worktree.IsDeleted
                && string.Equals(worktree.Branch, gitRef.Name, StringComparison.Ordinal))
            : null;
    }

    /// <summary>
    ///     Indicates whether <paramref name="gitRef" /> is a local branch that can be renamed.
    /// </summary>
    /// <param name="gitRef">The selected reference.</param>
    /// <returns><see langword="true" /> if the reference is a local branch; otherwise, <see langword="false" />.</returns>
    public static bool CanRename(IGitRef? gitRef)
    {
        return gitRef is { IsHead: true, IsRemote: false, IsTag: false };
    }

    /// <summary>
    ///     Returns a user-facing error if <paramref name="branchName" /> cannot be created, otherwise <see langword="null" />.
    /// </summary>
    public static string? ValidateNewBranchName(IGitModule module, string? branchName)
    {
        return string.IsNullOrWhiteSpace(branchName) ? TranslatedStrings.EnterBranchNameError :
            module.CheckBranchFormat(branchName.Trim()) ? null : TranslatedStrings.InvalidBranchNameError;
    }

    public static ArgumentString BuildCreate(string branchName, ObjectId objectId, bool checkout)
    {
        return Commands.Branch(branchName, objectId, checkout);
    }

    public static ArgumentString BuildCreateOrphan(string branchName, ObjectId startPoint)
    {
        return Commands.CreateOrphan(branchName, startPoint);
    }

    /// <summary>
    ///     Local branches are checked out directly; a remote branch gets a tracking local branch of the same name.
    /// </summary>
    public static IGitCommand BuildCheckout(IGitRef gitRef,
        LocalChangesAction localChanges = LocalChangesAction.DontChange)
    {
        return gitRef.IsRemote
            ? Commands.CheckoutBranch(gitRef.Name, remote: true, localChanges, CheckoutNewBranchMode.Create,
                gitRef.LocalName)
            : Commands.CheckoutBranch(gitRef.Name, remote: false, localChanges);
    }

    public static IGitCommand BuildCheckoutRevision(ObjectId objectId)
    {
        return Commands.CheckoutBranch(objectId.ToString(), false);
    }

    public static IGitCommand BuildDelete(IGitRef gitRef, bool force)
    {
        return Commands.DeleteBranch([gitRef], force);
    }

    public static IGitCommand BuildDeleteRemoteBranch(IGitRef gitRef)
    {
        if (!CanDeleteRemoteBranch(gitRef))
        {
            throw new ArgumentException("A remote branch is required.", nameof(gitRef));
        }

        return Commands.DeleteRemoteBranches(gitRef.Remote, [gitRef.LocalName]);
    }

    /// <summary>
    ///     Builds the Git command arguments to rename a branch.
    /// </summary>
    /// <param name="branchName">The existing branch name.</param>
    /// <param name="newBranchName">The replacement branch name.</param>
    /// <returns>The command arguments.</returns>
    public static ArgumentString BuildRename(string branchName, string newBranchName)
    {
        return Commands.RenameBranch(branchName, newBranchName);
    }

    internal static string NormaliseBranchName(
        string branchName,
        IGitBranchNameNormaliser branchNameNormaliser,
        GitBranchNameOptions options)
    {
        return branchName.Any(PathUtil.IsValidPathChar)
            ? branchNameNormaliser.Normalise(branchName, options)
            : branchName;
    }
}
