using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests.Git;

partial class CommandsTests
{
    public static IEnumerable<object?[]> DeleteBranchTestData
    {
        get
        {
            const string name = "local_branch";

            // Test local branches only
            List<IGitRef> localRefs = [];
            var localGitModule = Substitute.For<IGitModule>();
            localGitModule.GetEffectiveSetting($"branch.{name}.merge").Returns(string.Empty);
            localGitModule.GetEffectiveSetting($"branch.{name}.remote").Returns(string.Empty);
            GitRef localBranchRef = new(localGitModule, ObjectId.Random(), $"refs/heads/{name}");

            localRefs.Add(localBranchRef);

            yield return new object?[] { localRefs, /* force */ false, /* expected */ $"branch --delete \"{name}\"" };
            yield return new object?[]
            {
                localRefs, /* force */ true, /* expected */ $"branch --delete --force \"{name}\""
            };

            // Test local and remote branches
            const string remoteName = "origin";
            var completeName = $"refs/remotes/{remoteName}/{name}";
            var remoteBranchRef = SetupRawRemoteRef(name, remoteName, completeName);

            List<IGitRef> mixedRefs = [localBranchRef, remoteBranchRef];

            yield return new object?[]
            {
                mixedRefs, /* force */ false, /* expected */ $"branch --delete --all \"{name}\" \"origin/{name}\""
            };
            yield return new object?[]
            {
                mixedRefs, /* force */ true, /* expected */
                $"branch --delete --force --all \"{name}\" \"origin/{name}\""
            };

            // Test remote branches only
            List<IGitRef> remoteRefs = [remoteBranchRef];

            yield return new object?[]
            {
                remoteRefs, /* force */ false, /* expected */ $"branch --delete --remotes \"origin/{name}\""
            };
            yield return new object?[]
            {
                remoteRefs, /* force */ true, /* expected */ $"branch --delete --force --remotes \"origin/{name}\""
            };
        }
    }

    [Fact]
    public void ctor_should_throw_if_branches_is_null()
    {
        ((Action)(() => GitCommands.Git.Commands.DeleteBranch(null!, false))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ctor_should_throw_if_branches_is_empty()
    {
        ((Action)(() => GitCommands.Git.Commands.DeleteBranch([], false))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ctor_should_have_expected_values()
    {
        const string remoteName = "origin";
        const string completeName = $"refs/remotes/{remoteName}/branch_name";
        var remoteBranchRef = SetupRawRemoteRef("local_branch_name", remoteName, completeName);

        var cmd = GitCommands.Git.Commands.DeleteBranch(new IGitRef[] { remoteBranchRef }, false);

        cmd.AccessesRemote.Should().BeFalse();
        cmd.ChangesRepoState.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(DeleteBranchTestData))]
    public void Arguments_are_Expected(IReadOnlyCollection<IGitRef> branches, bool force, string expected)
    {
        var cmd = GitCommands.Git.Commands.DeleteBranch(branches, force);
        cmd.Arguments.Should().Be(expected);
    }

    private static GitRef SetupRawRemoteRef(string localBranchName, string remoteName, string completeName)
    {
        var localGitModule = Substitute.For<IGitModule>();
        localGitModule.GetEffectiveSetting($"branch.{localBranchName}.merge").Returns(completeName);
        localGitModule.GetEffectiveSetting($"branch.{localBranchName}.remote").Returns(remoteName);
        GitRef remoteBranchRef = new(localGitModule, ObjectId.Random(), completeName, remoteName);
        return remoteBranchRef;
    }
}
