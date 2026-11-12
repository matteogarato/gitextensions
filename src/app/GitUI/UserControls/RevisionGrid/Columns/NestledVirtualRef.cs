using GitCommands.Git;
using GitExtensions.Extensibility.Git;

namespace GitUI.UserControls.RevisionGrid.Columns;

public sealed class NestledVirtualRef(IGitRef gitRef, string completeName, bool trackingBranchIsGone) : IGitRef
{
    public bool TrackingBranchIsGone
    {
        get => trackingBranchIsGone;
    }

    string INamedGitItem.Name { get; } = GitRef.ParseName(completeName);

    /// <summary>
    ///     <see cref="ObjectId" /> of a nestled ref is always default/zero.
    /// </summary>
    ObjectId IGitItem.ObjectId
    {
        get => default;
    }

    string? IGitItem.Guid
    {
        get => null;
    }

    string IGitRef.CompleteName
    {
        get => completeName;
    }

    string IGitRef.LocalName
    {
        get => GitRef.ComputeLocalName(IsRemote, Remote, Name);
    }

    /// <summary>
    ///     The tracked local branch name as on the remote for <see cref="IsHead" />,
    ///     or the tracking local branch for <see cref="IsRemote" />.
    /// </summary>
    string IGitRef.MergeWith
    {
        get => gitRef.LocalName;
        set => throw new NotSupportedException();
    }

    string IGitRef.Remote
    {
        get => gitRef.TrackingRemote;
    }

    string IGitRef.TrackingRemote
    {
        get => gitRef.Remote;
        set => throw new NotSupportedException();
    }

    bool IGitRef.IsHead
    {
        get => gitRef.IsRemote;
    }

    bool IGitRef.IsRemote
    {
        get => gitRef.IsHead;
    }

    bool IGitRef.IsTag
    {
        get => false;
    }

    bool IGitRef.IsStash
    {
        get => false;
    }

    bool IGitRef.IsDereference
    {
        get => false;
    }

    bool IGitRef.IsSelected { get; set; }
    bool IGitRef.IsSelectedHeadMergeSource { get; set; }

    bool IGitRef.IsBisect
    {
        get => false;
    }

    bool IGitRef.IsBisectGood
    {
        get => false;
    }

    bool IGitRef.IsBisectBad
    {
        get => false;
    }

    IGitModule IGitRef.Module
    {
        get => gitRef.Module;
    }

    bool IGitRef.IsTrackingRemote(IGitRef? remote)
    {
        return remote is not null && IsHead && remote.IsRemote
               && MergeWith == remote.LocalName && TrackingRemote == remote.Remote;
    }

    public override bool Equals(object? obj)
    {
        return obj is NestledVirtualRef other && CompleteName == other.CompleteName;
    }

    public override int GetHashCode()
    {
        return completeName.GetHashCode();
    }

    public override string ToString()
    {
        return $"NestledVirtualRef: {CompleteName}";
    }
}
