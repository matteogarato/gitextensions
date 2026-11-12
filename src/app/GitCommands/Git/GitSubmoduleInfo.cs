using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

public sealed class GitSubmoduleInfo : IGitSubmoduleInfo
{
    public GitSubmoduleInfo(string name, string localPath, string remotePath, ObjectId currentCommitId, string branch,
        bool isInitialized, bool isUpToDate)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        LocalPath = localPath ?? throw new ArgumentNullException(nameof(localPath));
        RemotePath = remotePath ?? throw new ArgumentNullException(nameof(remotePath));
        if (currentCommitId.IsZero)
        {
            throw new ArgumentNullException(nameof(currentCommitId));
        }

        CurrentCommitId = currentCommitId;
        Branch = branch ?? throw new ArgumentNullException(nameof(branch));
        IsInitialized = isInitialized;
        IsUpToDate = isUpToDate;
    }

    string IGitSubmoduleInfo.Name { get; }
    string IGitSubmoduleInfo.LocalPath { get; }
    string IGitSubmoduleInfo.RemotePath { get; }
    ObjectId IGitSubmoduleInfo.CurrentCommitId { get; }
    string IGitSubmoduleInfo.Branch { get; }
    bool IGitSubmoduleInfo.IsInitialized { get; }
    bool IGitSubmoduleInfo.IsUpToDate { get; }

    string IGitSubmoduleInfo.Status
    {
        get
        {
            if (!IsInitialized)
            {
                return "Not initialized";
            }

            if (!IsUpToDate)
            {
                return "Modified";
            }

            return "Up-to-date";
        }
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(Branch))
        {
            return LocalPath;
        }

        return LocalPath + " [" + Branch + "]";
    }
}
