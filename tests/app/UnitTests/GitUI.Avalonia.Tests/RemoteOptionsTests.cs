using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitExtensionsTests;

public sealed class RemoteOptionsTests
{
    private static IGitModule CreateModule()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GetSelectedBranch(emptyIfDetached: true).Returns("main");
        return module;
    }

    [Fact]
    public void BuildPull_should_insert_autostash_after_pull_verb()
    {
        IGitModule module = CreateModule();
        module.PullCmd("origin", null, true, null, false).Returns((ArgumentString)"-c fetch.parallel=0 pull --rebase --progress origin");

        string arguments = RemoteWorkflow.BuildPull(module, new PullOptions("origin", " ", PullMode.Rebase, PullTagsMode.Default, false, false, true)).ToString();

        arguments.Should().Be("-c fetch.parallel=0 pull --autostash --rebase --progress origin");
    }

    [Fact]
    public void BuildPull_should_not_add_autostash_when_disabled()
    {
        IGitModule module = CreateModule();
        module.PullCmd("origin", "dev", false, true, true).Returns((ArgumentString)"pull --tags origin dev");

        RemoteWorkflow.BuildPull(module, new PullOptions("origin", "dev", PullMode.Merge, PullTagsMode.All, true, true, false)).ToString()
            .Should().Be("pull --tags origin dev");
    }

    [Fact]
    public void BuildPull_should_use_fetch_with_prune_for_fetch_only()
    {
        IGitModule module = CreateModule();
        module.FetchCmd("origin", null, null, false, false, true, false).Returns((ArgumentString)"fetch --prune origin");

        RemoteWorkflow.BuildPull(module, new PullOptions("origin", null, PullMode.FetchOnly, PullTagsMode.None, false, true, true)).ToString()
            .Should().Be("fetch --prune origin");
    }

    [Fact]
    public void BuildPush_should_target_remote_branch_with_force_and_tracking()
    {
        string arguments = RemoteWorkflow.BuildPush(CreateModule(), new PushOptions("origin", "release", ForcePushOptions.ForceWithLease, true, 0, PushScope.CurrentBranch)).ToString();

        arguments.Should().Contain("--force-with-lease").And.Contain(" -u ").And.Contain("origin").And.Contain("main:refs/heads/release");
    }

    [Fact]
    public void BuildPush_should_push_all_branches_and_tags_by_scope()
    {
        IGitModule module = CreateModule();

        RemoteWorkflow.BuildPush(module, new PushOptions("origin", null, ForcePushOptions.DoNotForce, false, 0, PushScope.AllBranches)).ToString()
            .Should().Contain("--all");
        RemoteWorkflow.BuildPush(module, new PushOptions("origin", "ignored", ForcePushOptions.DoNotForce, false, 0, PushScope.Tags)).ToString()
            .Should().Contain("--tags").And.NotContain("ignored");
    }
}
