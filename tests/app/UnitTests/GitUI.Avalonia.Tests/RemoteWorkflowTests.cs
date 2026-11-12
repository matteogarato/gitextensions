using System.Diagnostics;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class RemoteWorkflowTests
{
    private static IGitModule CreateModule(string currentRemote, string[] remotes, string branch = "main")
    {
        var module = Substitute.For<IGitModule>();
        module.GetCurrentRemote().Returns(currentRemote);
        module.GetRemoteNames().Returns(remotes);
        module.GetSelectedBranch(true).Returns(branch);
        return module;
    }

    [Fact]
    public void ResolveRemote_should_prefer_tracked_remote()
    {
        RemoteWorkflow.ResolveRemote(CreateModule("upstream", ["origin", "upstream"])).Should().Be("upstream");
    }

    [Fact]
    public void ResolveRemote_should_fall_back_to_origin_then_single_remote()
    {
        RemoteWorkflow.ResolveRemote(CreateModule("", ["fork", "origin"])).Should().Be("origin");
        RemoteWorkflow.ResolveRemote(CreateModule("", ["fork"])).Should().Be("fork");
    }

    [Fact]
    public void ResolveRemote_should_return_null_when_ambiguous_or_missing()
    {
        RemoteWorkflow.ResolveRemote(CreateModule("", ["a", "b"])).Should().BeNull();
        RemoteWorkflow.ResolveRemote(CreateModule("", [])).Should().BeNull();
    }

    [Fact]
    public void Validate_should_report_missing_remote_and_detached_head()
    {
        RemoteWorkflow.Validate(CreateModule("", []), false).Should().Be(TranslatedStrings.NoRemoteConfiguredError);
        RemoteWorkflow.Validate(CreateModule("origin", ["origin"], ""), true).Should()
            .Be(TranslatedStrings.DetachedHeadPushError);
        RemoteWorkflow.Validate(CreateModule("origin", ["origin"], ""), false).Should().BeNull();
        RemoteWorkflow.Validate(CreateModule("origin", ["origin"]), true).Should().BeNull();
    }

    [Fact]
    public void BuildPull_should_delegate_to_module_with_rebase_flag()
    {
        var module = CreateModule("origin", ["origin"]);
        module.PullCmd("origin", null, true).Returns((ArgumentString)"pull --rebase origin");

        RemoteWorkflow.BuildPull(module, "origin", true).ToString().Should().Be("pull --rebase origin");
    }

    [Fact]
    public void BuildPush_should_set_upstream_only_for_untracked_branch()
    {
        RemoteWorkflow.BuildPush(CreateModule("", ["origin"]), "origin", ForcePushOptions.DoNotForce).ToString()
            .Should().Contain("-u");
        RemoteWorkflow.BuildPush(CreateModule("origin", ["origin"]), "origin", ForcePushOptions.DoNotForce).ToString()
            .Should().NotContain("-u");
    }

    [Fact]
    public void BuildPush_should_map_force_with_lease()
    {
        RemoteWorkflow.BuildPush(CreateModule("origin", ["origin"]), "origin", ForcePushOptions.ForceWithLease)
            .ToString()
            .Should().Contain("--force-with-lease");
    }

    [Fact]
    public void BuildPush_should_publish_branch_to_real_bare_remote()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ge-remote-{Guid.NewGuid():N}");
        var work = Path.Combine(root, "work");
        var bare = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(bare);
        try
        {
            Git(bare, "init -q --bare");
            Git(work, "init -q -b main");
            Git(work, "-c user.name=t -c user.email=t@example.com commit -q --allow-empty -m first");
            Git(work, $"remote add origin \"{bare}\"");

            var arguments = RemoteWorkflow
                .BuildPush(CreateModule("", ["origin"]), "origin", ForcePushOptions.DoNotForce).ToString();
            Git(work, arguments);

            Git(bare, "--git-dir=. rev-parse main").Should().Be(Git(work, "rev-parse HEAD"));
            Git(work, "config branch.main.remote").Should().Be("origin");
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(root, true);
        }

        return;

        static string Git(string workingDirectory, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true
            })!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
            return output;
        }
    }
}
