using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class CommitPickWorkflowTests
{
    private static readonly ObjectId _id = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    private static (IGitModule Module, GitUIPluginInterfaces.GitRevision Revision) Create(bool isMerge)
    {
        var module = Substitute.For<IGitModule>();
        module.IsMerge(_id).Returns(isMerge);
        return (module, new GitUIPluginInterfaces.GitRevision(_id));
    }

    [Fact]
    public void BuildCherryPick_should_commit_a_regular_commit()
    {
        var (module, revision) = Create(false);

        ((string)CommitPickWorkflow.BuildCherryPick(module, revision)).Should().Be($"cherry-pick {_id}");
    }

    [Fact]
    public void BuildCherryPick_should_select_mainline_for_merge_commits()
    {
        var (module, revision) = Create(true);

        ((string)CommitPickWorkflow.BuildCherryPick(module, revision)).Should().Contain("-m 1");
    }

    [Fact]
    public void BuildCherryPick_should_use_the_selected_mainline_parent()
    {
        var (module, revision) = Create(true);

        ((string)CommitPickWorkflow.BuildCherryPick(module, revision, mainlineParent: 2)).Should().Contain("-m 2");
    }

    [Fact]
    public void BuildRevert_should_select_mainline_only_for_merge_commits()
    {
        ((string)CommitPickWorkflow.BuildRevert(Create(false).Module, Create(false).Revision)).Should()
            .Be($"revert {_id}");
        var (module, revision) = Create(true);

        ((string)CommitPickWorkflow.BuildRevert(module, revision)).Should().Be($"revert -m 1 {_id}");
    }

    [Fact]
    public void BuildRevert_should_use_the_selected_mainline_parent()
    {
        var (module, revision) = Create(true);

        ((string)CommitPickWorkflow.BuildRevert(module, revision, mainlineParent: 2)).Should().Be($"revert -m 2 {_id}");
    }

    [Fact]
    public void BuildCherryPick_and_BuildRevert_should_apply_the_selected_merge_parent_in_a_real_repository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-cherry-pick-mainline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            Git(directory, "config user.name Test");
            Git(directory, "config user.email test@example.com");
            File.WriteAllText(Path.Combine(directory, "base.txt"), "base\n");
            Git(directory, "add base.txt");
            Git(directory, "commit -q -m base");
            Git(directory, "checkout -q -b feature");
            File.WriteAllText(Path.Combine(directory, "feature.txt"), "feature\n");
            Git(directory, "add feature.txt");
            Git(directory, "commit -q -m feature");
            var featureCommit = Git(directory, "rev-parse HEAD");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "main.txt"), "main\n");
            Git(directory, "add main.txt");
            Git(directory, "commit -q -m main");
            Git(directory, "merge -q --no-ff -m merge feature");
            var mergeCommit = Git(directory, "rev-parse HEAD");
            Git(directory, "checkout -q --detach " + featureCommit);

            var module = Substitute.For<IGitModule>();
            module.IsMerge(ObjectId.Parse(mergeCommit)).Returns(true);

            Git(directory,
                CommitPickWorkflow.BuildCherryPick(module,
                    new GitUIPluginInterfaces.GitRevision(ObjectId.Parse(mergeCommit)), mainlineParent: 2).ToString());

            File.Exists(Path.Combine(directory, "feature.txt")).Should().BeTrue();
            File.Exists(Path.Combine(directory, "main.txt")).Should().BeTrue();
            Git(directory, "rev-parse HEAD^").Should().Be(featureCommit);

            Git(directory, "checkout -q --detach " + mergeCommit);
            Git(directory,
                CommitPickWorkflow.BuildRevert(module,
                    new GitUIPluginInterfaces.GitRevision(ObjectId.Parse(mergeCommit)), mainlineParent: 2).ToString());
            File.Exists(Path.Combine(directory, "feature.txt")).Should().BeTrue();
            File.Exists(Path.Combine(directory, "main.txt")).Should().BeFalse();
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

    [Fact]
    public void BuildCherryPick_should_reject_a_nonpositive_mainline_parent()
    {
        var (module, revision) = Create(true);

        Action build = () => CommitPickWorkflow.BuildCherryPick(module, revision, mainlineParent: 0);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CanApply_should_reject_missing_and_artificial_revisions()
    {
        CommitPickWorkflow.CanApply(null).Should().BeFalse();
        CommitPickWorkflow.CanApply(new GitUIPluginInterfaces.GitRevision(ObjectId.WorkTreeId)).Should().BeFalse();
        CommitPickWorkflow.CanApply(new GitUIPluginInterfaces.GitRevision(_id)).Should().BeTrue();
    }

    [Fact]
    public void BuildCherryPickRecordingOrigin_and_BuildRevertWithoutCommit_should_add_their_options()
    {
        var (module, revision) = Create(true);

        ((string)CommitPickWorkflow.BuildCherryPickRecordingOrigin(module, revision, 2)).Should()
            .Be($"cherry-pick -x -m 2 {_id}");
        ((string)CommitPickWorkflow.BuildRevertWithoutCommit(module, revision)).Should()
            .Be($"revert --no-commit -m 1 {_id}");
    }

    [Fact]
    public void BuildCherryPickSequence_should_apply_oldest_first_and_BuildRevertSequence_newest_first()
    {
        var newest = ObjectId.Parse("1111111111111111111111111111111111111111");
        var oldest = ObjectId.Parse("2222222222222222222222222222222222222222");
        GitUIPluginInterfaces.GitRevision[] gridOrder =
            [new GitUIPluginInterfaces.GitRevision(newest), new GitUIPluginInterfaces.GitRevision(oldest)];

        ((string)CommitPickWorkflow.BuildCherryPickSequence(gridOrder, recordOrigin: true)).Should()
            .Be($"cherry-pick -x {oldest} {newest}");
        ((string)CommitPickWorkflow.BuildRevertSequence(gridOrder, autoCommit: false)).Should()
            .Be($"revert --no-commit {newest} {oldest}");
    }

    [Fact]
    public void CanApplySequence_should_require_several_non_merge_commits()
    {
        var module = Substitute.For<IGitModule>();
        var merge = ObjectId.Parse("3333333333333333333333333333333333333333");
        module.IsMerge(merge).Returns(true);
        GitUIPluginInterfaces.GitRevision regular = new(_id);

        CommitPickWorkflow.CanApplySequence(module, [regular]).Should().BeFalse();
        CommitPickWorkflow.CanApplySequence(module, [regular, new GitUIPluginInterfaces.GitRevision(merge)]).Should()
            .BeFalse();
        CommitPickWorkflow.CanApplySequence(module,
        [
            regular, new GitUIPluginInterfaces.GitRevision(ObjectId.Parse("4444444444444444444444444444444444444444"))
        ]).Should().BeTrue();
    }
}
