using System.Diagnostics.CodeAnalysis;
using GitCommands.Config;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

internal enum GitRefType
{
    Other,
    Head,
    Remote,
    Tag,
    Bisect,
    BisectGood,
    BisectBad,
    Stash
}

public sealed class GitRef : IGitRef
{
    private readonly GitRefType _type;
    private string? _localName;
    private string? _mergeWith;
    private string? _trackingRemote;

    public GitRef(IGitModule module, ObjectId objectId, string completeName, string remote = "")
    {
        Module = module;
        ObjectId = objectId;
        CompleteName = completeName;
        Remote = remote;
        IsDereference = CompleteName.EndsWith(GitRefName.TagDereferenceSuffix);
        _type = DetermineType(completeName);
        Name = ParseName(completeName, _type, IsDereference);
    }

    IGitModule IGitRef.Module { get; }

    string IGitRef.CompleteName { get; }

    string INamedGitItem.Name { get; }

    string IGitRef.LocalName
    {
        get => _localName ??= ComputeLocalName(IsRemote, Remote, Name);
    }

    [AllowNull]
    string IGitRef.MergeWith
    {
        get => _mergeWith ??= IsHead
            ? Module.GetEffectiveSetting(string.Format(SettingKeyString.BranchMerge, LocalName))
                .RemovePrefix(GitRefName.RefsHeadsPrefix)
            : "";
        set
        {
            if (!IsHead)
            {
                throw new InvalidOperationException("MergeWith can only be set for local branches.");
            }

            var settingName = string.Format(SettingKeyString.BranchMerge, LocalName);
            if (string.IsNullOrEmpty(value))
            {
                Module.UnsetSetting(settingName);
                _mergeWith = "";
            }
            else
            {
                Module.SetSetting(settingName, GitRefName.GetFullBranchName(value));
                _mergeWith = value;
            }
        }
    }

    string IGitRef.Remote { get; }

    [AllowNull]
    string IGitRef.TrackingRemote
    {
        get => _trackingRemote ??=
            IsHead ? Module.GetEffectiveSetting(string.Format(SettingKeyString.BranchRemote, LocalName)) : "";
        set
        {
            if (!IsHead)
            {
                throw new InvalidOperationException("Tracking remote can only be set for local branches.");
            }

            var settingName = string.Format(SettingKeyString.BranchRemote, LocalName);
            if (string.IsNullOrEmpty(value))
            {
                Module.UnsetSetting(settingName);
                _trackingRemote = "";
            }
            else
            {
                Module.SetSetting(settingName, value);
                _trackingRemote = value;

                if (MergeWith == "")
                {
                    MergeWith = LocalName;
                }
            }
        }
    }

    bool IGitRef.IsHead
    {
        get => _type == GitRefType.Head;
    }

    bool IGitRef.IsRemote
    {
        get => _type == GitRefType.Remote;
    }

    bool IGitRef.IsTag
    {
        get => _type == GitRefType.Tag;
    }

    bool IGitRef.IsStash
    {
        get => _type == GitRefType.Stash;
    }

    bool IGitRef.IsDereference { get; }
    bool IGitRef.IsSelected { get; set; }
    bool IGitRef.IsSelectedHeadMergeSource { get; set; }

    bool IGitRef.IsBisect
    {
        get => _type == GitRefType.Bisect;
    }

    bool IGitRef.IsBisectGood
    {
        get => _type == GitRefType.BisectGood;
    }

    bool IGitRef.IsBisectBad
    {
        get => _type == GitRefType.BisectBad;
    }

    public static GitRef NoHead(IGitModule module)
    {
        return new GitRef(module, default, "");
    }

    public override string ToString()
    {
        return CompleteName;
    }

    public static IReadOnlyCollection<string> GetAmbiguousRefNames(IEnumerable<IGitRef> refs)
    {
        HashSet<string> seen = [];
        HashSet<string> ambiguous = [];

        foreach (var r in refs)
        {
            if (!seen.Add(r.Name))
            {
                ambiguous.Add(r.Name);
            }
        }

        return ambiguous;
    }

    internal static GitRefType DetermineType(string completeName)
    {
        return completeName switch
        {
            _ when completeName.StartsWith(GitRefName.RefsHeadsPrefix, StringComparison.Ordinal) => GitRefType.Head,
            _ when completeName.StartsWith(GitRefName.RefsTagsPrefix, StringComparison.Ordinal) => GitRefType.Tag,
            _ when completeName.StartsWith(GitRefName.RefsRemotesPrefix, StringComparison.Ordinal) => GitRefType.Remote,
            _ when completeName.StartsWith(GitRefName.RefsStashPrefix, StringComparison.Ordinal) => GitRefType.Stash,
            _ when completeName.StartsWith(GitRefName.RefsBisectGoodPrefix, StringComparison.Ordinal) => GitRefType
                .BisectGood,
            _ when completeName.StartsWith(GitRefName.RefsBisectBadPrefix, StringComparison.Ordinal) => GitRefType
                .BisectBad,
            _ when completeName.StartsWith(GitRefName.RefsBisectPrefix, StringComparison.Ordinal) => GitRefType.Bisect,
            _ => GitRefType.Other
        };
    }

    /// <summary>
    ///     Computes <see cref="IGitRef.LocalName" /> for a ref,
    ///     given <see cref="IGitRef.Remote" /> and <see cref="INamedGitItem.Name" />.
    /// </summary>
    public static string ComputeLocalName(bool isRemote, string remote, string name)
    {
        if (!isRemote || remote.Length == 0 || name.Length <= remote.Length || name[remote.Length] != '/'
            || !name.StartsWith(remote, StringComparison.Ordinal))
        {
            return name;
        }

        return name[(remote.Length + 1)..];
    }

    public static string ParseName(string completeName)
    {
        var type = DetermineType(completeName);
        var isDereference = type == GitRefType.Tag && completeName.EndsWith(GitRefName.TagDereferenceSuffix);
        return ParseName(completeName, type, isDereference);
    }

    internal static string ParseName(string completeName, GitRefType type, bool isDereference)
    {
        // DetermineType already verified each prefix, so slice directly at the known
        // offset rather than repeating an IndexOf search via SubstringAfter.
        var name = type switch
        {
            GitRefType.Head => completeName[GitRefName.RefsHeadsPrefix.Length..],
            GitRefType.Remote => completeName[GitRefName.RefsRemotesPrefix.Length..],
            GitRefType.Tag => completeName[
                GitRefName.RefsTagsPrefix.Length..(completeName.Length -
                                                   (isDereference ? GitRefName.TagDereferenceSuffix.Length : 0))],
            _ => completeName.SubstringAfter("refs/")
        };

        return name.Length == 0 ? completeName : name;
    }

    #region IGitItem Members

    ObjectId IGitItem.ObjectId { get; }

    string? IGitItem.Guid
    {
        get => ObjectId.IsZero ? null : ObjectId.ToString();
    }

    bool IGitRef.IsTrackingRemote(IGitRef? remote)
    {
        return remote is not null && IsHead && remote.IsRemote
               && MergeWith == remote.LocalName && TrackingRemote == remote.Remote;
    }

    #endregion
}
