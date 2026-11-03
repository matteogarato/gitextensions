using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitExtensionsTests;

public sealed class CommitPickWorkflowTests
{
    private static readonly ObjectId _id = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    private static (IGitModule Module, GitRevision Revision) Create(bool isMerge)
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.IsMerge(_id).Returns(isMerge);
        return (module, new GitRevision(_id));
    }

    [Fact]
    public void BuildCherryPick_should_commit_a_regular_commit()
    {
        (IGitModule module, GitRevision revision) = Create(isMerge: false);

        ((string)CommitPickWorkflow.BuildCherryPick(module, revision)).Should().Be($"cherry-pick {_id}");
    }

    [Fact]
    public void BuildCherryPick_should_select_mainline_for_merge_commits()
    {
        (IGitModule module, GitRevision revision) = Create(isMerge: true);

        ((string)CommitPickWorkflow.BuildCherryPick(module, revision)).Should().Contain("-m 1");
    }

    [Fact]
    public void BuildCherryPick_should_use_the_selected_mainline_parent()
    {
        (IGitModule module, GitRevision revision) = Create(isMerge: true);

        ((string)CommitPickWorkflow.BuildCherryPick(module, revision, mainlineParent: 2)).Should().Contain("-m 2");
    }

    [Fact]
    public void BuildRevert_should_select_mainline_only_for_merge_commits()
    {
        ((string)CommitPickWorkflow.BuildRevert(Create(false).Module, Create(false).Revision)).Should().Be($"revert {_id}");
        (IGitModule module, GitRevision revision) = Create(isMerge: true);

        ((string)CommitPickWorkflow.BuildRevert(module, revision)).Should().Be($"revert -m 1 {_id}");
    }

    [Fact]
    public void BuildRevert_should_use_the_selected_mainline_parent()
    {
        (IGitModule module, GitRevision revision) = Create(isMerge: true);

        ((string)CommitPickWorkflow.BuildRevert(module, revision, mainlineParent: 2)).Should().Be($"revert -m 2 {_id}");
    }

    [Fact]
    public void BuildCherryPick_and_BuildRevert_should_apply_the_selected_merge_parent_in_a_real_repository()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ge-cherry-pick-mainline-{Guid.NewGuid():N}");
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
            string featureCommit = Git(directory, "rev-parse HEAD");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "main.txt"), "main\n");
            Git(directory, "add main.txt");
            Git(directory, "commit -q -m main");
            Git(directory, "merge -q --no-ff -m merge feature");
            string mergeCommit = Git(directory, "rev-parse HEAD");
            Git(directory, "checkout -q --detach " + featureCommit);

            IGitModule module = Substitute.For<IGitModule>();
            module.IsMerge(ObjectId.Parse(mergeCommit)).Returns(true);

            Git(directory, CommitPickWorkflow.BuildCherryPick(module, new GitRevision(ObjectId.Parse(mergeCommit)), mainlineParent: 2).ToString());

            File.Exists(Path.Combine(directory, "feature.txt")).Should().BeTrue();
            File.Exists(Path.Combine(directory, "main.txt")).Should().BeTrue();
            Git(directory, "rev-parse HEAD^").Should().Be(featureCommit);

            Git(directory, "checkout -q --detach " + mergeCommit);
            Git(directory, CommitPickWorkflow.BuildRevert(module, new GitRevision(ObjectId.Parse(mergeCommit)), mainlineParent: 2).ToString());
            File.Exists(Path.Combine(directory, "feature.txt")).Should().BeTrue();
            File.Exists(Path.Combine(directory, "main.txt")).Should().BeFalse();
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, recursive: true);
        }

        return;

        static string Git(string workingDirectory, string arguments)
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
            return output;
        }
    }

    [Fact]
    public void BuildCherryPick_should_reject_a_nonpositive_mainline_parent()
    {
        (IGitModule module, GitRevision revision) = Create(isMerge: true);

        Action build = () => CommitPickWorkflow.BuildCherryPick(module, revision, mainlineParent: 0);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CanApply_should_reject_missing_and_artificial_revisions()
    {
        CommitPickWorkflow.CanApply(null).Should().BeFalse();
        CommitPickWorkflow.CanApply(new GitRevision(ObjectId.WorkTreeId)).Should().BeFalse();
        CommitPickWorkflow.CanApply(new GitRevision(_id)).Should().BeTrue();
    }

    [Fact]
    public void BuildCherryPickRecordingOrigin_and_BuildRevertWithoutCommit_should_add_their_options()
    {
        (IGitModule module, GitRevision revision) = Create(isMerge: true);

        ((string)CommitPickWorkflow.BuildCherryPickRecordingOrigin(module, revision, 2)).Should().Be($"cherry-pick -x -m 2 {_id}");
        ((string)CommitPickWorkflow.BuildRevertWithoutCommit(module, revision)).Should().Be($"revert --no-commit -m 1 {_id}");
    }

    [Fact]
    public void BuildCherryPickSequence_should_apply_oldest_first_and_BuildRevertSequence_newest_first()
    {
        ObjectId newest = ObjectId.Parse("1111111111111111111111111111111111111111");
        ObjectId oldest = ObjectId.Parse("2222222222222222222222222222222222222222");
        GitRevision[] gridOrder = [new GitRevision(newest), new GitRevision(oldest)];

        ((string)CommitPickWorkflow.BuildCherryPickSequence(gridOrder, recordOrigin: true)).Should().Be($"cherry-pick -x {oldest} {newest}");
        ((string)CommitPickWorkflow.BuildRevertSequence(gridOrder, autoCommit: false)).Should().Be($"revert --no-commit {newest} {oldest}");
    }

    [Fact]
    public void CanApplySequence_should_require_several_non_merge_commits()
    {
        IGitModule module = Substitute.For<IGitModule>();
        ObjectId merge = ObjectId.Parse("3333333333333333333333333333333333333333");
        module.IsMerge(merge).Returns(true);
        GitRevision regular = new(_id);

        CommitPickWorkflow.CanApplySequence(module, [regular]).Should().BeFalse();
        CommitPickWorkflow.CanApplySequence(module, [regular, new GitRevision(merge)]).Should().BeFalse();
        CommitPickWorkflow.CanApplySequence(module, [regular, new GitRevision(ObjectId.Parse("4444444444444444444444444444444444444444"))]).Should().BeTrue();
    }
}
