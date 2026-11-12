using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class MergeWorkflowTests
{
    private static IGitRef CreateRef(string name, bool isHead = false, bool isRemote = false, bool isSelected = false,
        bool isTag = false)
    {
        var gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns(name);
        gitRef.IsHead.Returns(isHead);
        gitRef.IsRemote.Returns(isRemote);
        gitRef.IsSelected.Returns(isSelected);
        gitRef.IsTag.Returns(isTag);
        return gitRef;
    }

    [Fact]
    public void CanMerge_should_allow_other_local_branches_remote_branches_and_tags()
    {
        MergeWorkflow.CanMerge(CreateRef("feature", true), "main").Should().BeTrue();
        MergeWorkflow.CanMerge(CreateRef("origin/feature", isRemote: true), "main").Should().BeTrue();
        MergeWorkflow.CanMerge(CreateRef("v1", isTag: true), "main").Should().BeTrue();
    }

    [Fact]
    public void CanMerge_should_reject_current_branch_and_detached_head()
    {
        MergeWorkflow.CanMerge(CreateRef("main", true), "main").Should().BeFalse();
        MergeWorkflow.CanMerge(CreateRef("feature", true), string.Empty).Should().BeFalse();
        MergeWorkflow.CanMerge(null, "main").Should().BeFalse();
    }

    [Fact]
    public void BuildMerge_should_use_the_default_fast_forward_merge_options()
    {
        var module = Substitute.For<IGitModule>();

        MergeWorkflow.BuildMerge(module, CreateRef("feature", true), MergeOptions.Default)
            .ToString().Should().Be("merge --ff --no-edit feature");
    }

    [Fact]
    public void BuildMerge_should_apply_squash_no_commit_and_unrelated_history_options()
    {
        var module = Substitute.For<IGitModule>();

        MergeWorkflow.BuildMerge(module, CreateRef("feature", true), new MergeOptions(false, true, true, true))
            .ToString().Should().Be("merge --no-ff --squash --no-commit --allow-unrelated-histories --no-edit feature");
    }

    [Fact]
    public void BuildMerge_should_merge_a_branch_in_a_real_repository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q --allow-empty -m base");
            Git(directory, "branch feature");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q --allow-empty -m main-change");
            Git(directory, "checkout -q feature");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q --allow-empty -m feature-change");
            Git(directory, "checkout -q main");
            Git(directory, "config user.name t");
            Git(directory, "config user.email t@example.com");

            var module = Substitute.For<IGitModule>();
            module.GetPathForGitExecution(Arg.Any<string?>()).Returns(call => call.Arg<string?>());

            Git(directory,
                MergeWorkflow.BuildMerge(module, CreateRef("feature", true), MergeOptions.Default).ToString());

            Git(directory, "log -1 --format=%s").Should().Contain("Merge");
            Git(directory, "rev-list --parents -n 1 HEAD").Split(' ').Should().HaveCount(3);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, true);
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
