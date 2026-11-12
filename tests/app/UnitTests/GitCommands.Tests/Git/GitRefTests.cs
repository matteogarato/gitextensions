using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests.Git;

public sealed class GitRefTests
{
    [Fact]
    public void IsTrackingRemote_should_return_true_when_tracking_remote()
    {
        var remoteBranchShortName = "remote_branch";
        var remoteName = "origin";
        var localBranchRef = SetupLocalBranchWithATrackingReference(remoteBranchShortName, remoteName);

        var remoteBranchRef = SetupRemoteRef(remoteBranchShortName, remoteName);

        localBranchRef.IsTrackingRemote(remoteBranchRef).Should().BeTrue();
    }

    [Fact]
    public void IsTrackingRemote_should_return_false_when_remote_is_null()
    {
        var localBranchRef = SetupLocalBranchWithATrackingReference("remote_branch", "origin");

        localBranchRef.IsTrackingRemote(null).Should().BeFalse();
    }

    [Fact]
    public void IsTrackingRemote_should_return_false_when_tracking_another_remote()
    {
        var remoteBranchShortName = "remote_branch";
        var localBranchRef = SetupLocalBranchWithATrackingReference(remoteBranchShortName, "origin");

        var remoteBranchRef = SetupRemoteRef(remoteBranchShortName, "upstream");

        localBranchRef.IsTrackingRemote(remoteBranchRef).Should().BeFalse();
    }

    [Fact]
    public void IsTrackingRemote_should_return_false_when_tracking_another_remote_branch()
    {
        var localBranchRef = SetupLocalBranchWithATrackingReference("one_remote_branch", "origin");

        var remoteBranchRef = SetupRemoteRef("another_remote_branch", "origin");

        localBranchRef.IsTrackingRemote(remoteBranchRef).Should().BeFalse();
    }

    [Fact]
    public void IsTrackingRemote_should_return_false_when_supposedly_local_branch_is_a_remote_ref()
    {
        var localBranchRef = SetupRemoteRef("a_remote_branch", "origin");

        var remoteBranchRef = SetupRemoteRef("a_remote_branch", "origin");

        localBranchRef.IsTrackingRemote(remoteBranchRef).Should().BeFalse();
    }

    [Fact]
    public void IsTrackingRemote_should_return_false_when_supposedly_remote_branch_is_a_local_ref()
    {
        var localBranchRef = SetupLocalBranchWithATrackingReference("a_remote_branch", "origin");

        var remoteBranchRef = SetupLocalBranchWithATrackingReference("a_remote_branch", "origin");

        localBranchRef.IsTrackingRemote(remoteBranchRef).Should().BeFalse();
    }

    [Fact]
    public void IsTrackingRemote_should_return_false_when_local_branch_is_tracking_nothing()
    {
        var localGitModule = Substitute.For<IGitModule>();
        localGitModule.GetEffectiveSetting("branch.local_branch.merge").Returns(string.Empty);
        localGitModule.GetEffectiveSetting("branch.local_branch.remote").Returns(string.Empty);
        IGitRef localBranchRef = new GitRef(localGitModule, ObjectId.Random(), "refs/heads/local_branch");

        var remoteBranchRef = SetupLocalBranchWithATrackingReference("a_remote_branch", "origin");

        localBranchRef.IsTrackingRemote(remoteBranchRef).Should().BeFalse();
    }

    [Fact]
    public void Remote_Should_prefix_LocalName_for_Name()
    {
        var remoteName = "origin";
        var name = "local_branch";
        var completeName = $"refs/remotes/{remoteName}/{name}";

        var remoteBranchRef = SetupRawRemoteRef(remoteName, completeName);
        name.Should().Be(remoteBranchRef.LocalName);
    }

    [Fact]
    public void If_Remote_is_not_prefix_of_Name_then_LocalName_should_return_Name()
    {
        // Not standard behavior but seem to occur for git-svn
        var remoteName = "Remote_longer_than_Name";
        var name = "a_short_name";
        var completeName = $"refs/remotes/{name}";

        var remoteBranchRef = SetupRawRemoteRef(remoteName, completeName);
        name.Should().Be(remoteBranchRef.LocalName);
    }

    private static IGitRef SetupRawRemoteRef(string remoteName, string completeName)
    {
        var localGitModule = Substitute.For<IGitModule>();
        localGitModule.GetEffectiveSetting("branch.local_branch.merge").Returns(completeName);
        localGitModule.GetEffectiveSetting("branch.local_branch.remote").Returns(remoteName);
        return new GitRef(localGitModule, ObjectId.Random(), completeName, remoteName);
    }

    private static IGitRef SetupRemoteRef(string remoteBranchShortName, string remoteName)
    {
        var remoteGitModule = Substitute.For<IGitModule>();
        return new GitRef(remoteGitModule, ObjectId.Random(), $"refs/remotes/{remoteName}/{remoteBranchShortName}",
            remoteName);
    }

    private static IGitRef SetupLocalBranchWithATrackingReference(string remoteShortName, string remoteName)
    {
        var localGitModule = Substitute.For<IGitModule>();
        localGitModule.GetEffectiveSetting("branch.local_branch.merge").Returns($"refs/heads/{remoteShortName}");
        localGitModule.GetEffectiveSetting("branch.local_branch.remote").Returns(remoteName);
        return new GitRef(localGitModule, ObjectId.Random(), "refs/heads/local_branch");
    }
}
