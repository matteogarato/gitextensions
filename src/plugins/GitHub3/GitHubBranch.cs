using Git.hub;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;

namespace GitExtensions.Plugins.GitHub3;

internal sealed class GitHubBranch(Branch branch) : IHostedBranch
{
    string IHostedBranch.Name { get; } = branch.Name;
    ObjectId IHostedBranch.Sha { get; } = ObjectId.Parse(branch.Commit.Sha);
}
