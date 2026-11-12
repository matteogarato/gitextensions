using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;

namespace GitCommandsTests.Git.Commands;

public sealed class GitCheckoutBranchCmdTest
{
    [Fact]
    public void TestConstructor()
    {
        var cmd = GitCommands.Git.Commands.CheckoutBranch("branchName", true);

        cmd.Should().NotBeNull();
    }

    [Fact]
    public void TestConstructorRemoteIsFalse()
    {
        var cmd = GitCommands.Git.Commands.CheckoutBranch("branchName", false);

        cmd.Should().NotBeNull();
    }

    [Fact]
    public void TestAccessesRemoteIsFalse()
    {
        var cmd = GitCommands.Git.Commands.CheckoutBranch("branchName", true);

        cmd.AccessesRemote.Should().BeFalse();
    }

    [Fact]
    public void TestCollectArgumentsMergeReset()
    {
        // Merge

        GitCommands.Git.Commands.CheckoutBranch("branchName", false, LocalChangesAction.Merge).Arguments.Should()
            .Be("checkout --merge \"branchName\"");

        GitCommands.Git.Commands
            .CheckoutBranch("branchName", true, LocalChangesAction.Merge, CheckoutNewBranchMode.Create, "newBranchName")
            .Arguments.Should().Be("checkout --merge -b \"newBranchName\" --track \"branchName\"");

        GitCommands.Git.Commands
            .CheckoutBranch("branchName", true, LocalChangesAction.Merge, CheckoutNewBranchMode.Reset, "newBranchName")
            .Arguments.Should().Be("checkout --merge -B \"newBranchName\" \"branchName\"");

        // Reset

        GitCommands.Git.Commands.CheckoutBranch("branchName", false, LocalChangesAction.Reset).Arguments.Should()
            .Be("checkout --force \"branchName\"");

        GitCommands.Git.Commands
            .CheckoutBranch("branchName", true, LocalChangesAction.Reset, CheckoutNewBranchMode.Create, "newBranchName")
            .Arguments.Should().Be("checkout --force -b \"newBranchName\" --track \"branchName\"");

        GitCommands.Git.Commands
            .CheckoutBranch("branchName", true, LocalChangesAction.Reset, CheckoutNewBranchMode.Reset, "newBranchName")
            .Arguments.Should().Be("checkout --force -B \"newBranchName\" \"branchName\"");
    }
}
