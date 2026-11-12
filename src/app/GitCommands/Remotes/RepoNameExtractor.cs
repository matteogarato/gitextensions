using GitCommands.Config;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Remotes;

public interface IRepoNameExtractor
{
    /// <summary>
    ///     Get a "repo shortname" from the current repo URL
    ///     There is no official Git repo shortname, this is one possible definition:
    ///     The filename without extension for the remote URL
    ///     This function could have been included in GitModule
    /// </summary>
    (string repoProject, string repoName) Get();
}

public sealed class RepoNameExtractor(Func<IGitModule> getModule) : IRepoNameExtractor
{
    /// <summary>
    ///     Get a "repo shortname" from the current repo URL
    ///     There is no official Git repo shortname, this is one possible definition:
    ///     The filename without extension for the remote URL
    ///     This function could have been included in GitModule
    /// </summary>
    (string repoProject, string repoName) IRepoNameExtractor.Get()
    {
        var module = getModule();

        // Extract "name of repo" from remote url
        var remoteName = module.GetCurrentRemote();

        if (string.IsNullOrWhiteSpace(remoteName))
        {
            // No remote for the branch, for instance a submodule. Use first remote.
            var remotes = module.GetRemoteNames();
            if (remotes.Count > 0)
            {
                remoteName = remotes[0];
            }
        }

        var remoteUrl = module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remoteName));
        var repoName = Path.GetFileNameWithoutExtension(remoteUrl);

        return (GetRepoProject(), repoName);

        string GetRepoProject()
        {
            try
            {
                return Path.GetFileNameWithoutExtension(Path.GetDirectoryName(remoteUrl))!;
            }
            catch
            {
                return "";
            }
        }
    }
}
