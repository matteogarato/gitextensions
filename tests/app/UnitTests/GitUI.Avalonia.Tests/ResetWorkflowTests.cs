using System.Diagnostics;
using System.Text;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class ResetWorkflowTests
{
    private static readonly ObjectId _id = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    private static GitItemStatus CreateItem(string name, bool isNew = false)
    {
        return new GitItemStatus(name) { Staged = StagedStatus.WorkTree, IsNew = isNew, IsTracked = !isNew };
    }

    [Fact]
    public void BuildDiscardConfirmation_should_be_null_without_files()
    {
        ResetWorkflow.BuildDiscardConfirmation([]).Should().BeNull();
    }

    [Fact]
    public void BuildDiscardConfirmation_should_warn_about_deleting_new_files()
    {
        ResetWorkflow.BuildDiscardConfirmation([CreateItem("a")]).Should().NotContain("deleted");
        ResetWorkflow.BuildDiscardConfirmation([CreateItem("a"), CreateItem("b", true)]).Should().Contain("deleted").And
            .Contain("2");
    }

    [Fact]
    public void Discard_should_delete_only_when_new_files_are_selected()
    {
        var module = Substitute.For<IGitModule>();
        var resolver = Substitute.For<IFullPathResolver>();
        module.ResetChanges(default, Arg.Any<IReadOnlyList<GitItemStatus>>(), Arg.Any<bool>(), resolver,
            out Arg.Any<StringBuilder>(), null).Returns(call =>
        {
            call[4] = new StringBuilder();
            return true;
        });

        ResetWorkflow.Discard(module, [CreateItem("a")], resolver, out var output).Should().BeTrue();
        output.Should().BeEmpty();
        module.Received(1).ResetChanges(default, Arg.Any<IReadOnlyList<GitItemStatus>>(), false, resolver,
            out Arg.Any<StringBuilder>(), null);

        ResetWorkflow.Discard(module, [CreateItem("b", true)], resolver, out _);
        module.Received(1).ResetChanges(default, Arg.Any<IReadOnlyList<GitItemStatus>>(), true, resolver,
            out Arg.Any<StringBuilder>(), null);
    }

    [Fact]
    public void Discard_should_not_touch_git_without_files()
    {
        var module = Substitute.For<IGitModule>();

        ResetWorkflow.Discard(module, [], Substitute.For<IFullPathResolver>(), out _).Should().BeFalse();
        module.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void ResetAll_should_keep_untracked_files()
    {
        var module = Substitute.For<IGitModule>();
        module.ResetAllChanges(false).Returns(true);

        ResetWorkflow.ResetAll(module).Should().BeTrue();
        module.Received(1).ResetAllChanges(false);
    }

    [Fact]
    public void BuildUndoLastCommit_should_soft_reset_to_the_parent_of_head()
    {
        string arguments = ResetWorkflow.BuildUndoLastCommit();

        arguments.Should().Contain("reset --soft").And.Contain("HEAD~1");
    }

    [Fact]
    public void BuildUndoLastCommit_should_move_head_back_and_keep_commit_changes_staged()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-undo-commit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git("init -q -b main");
            Git("config user.name Test");
            Git("config user.email test@example.com");
            var filePath = Path.Combine(directory, "tracked.txt");
            File.WriteAllText(filePath, "before\n");
            Git("add tracked.txt");
            Git("commit -q -m before");
            var parentCommit = Git("rev-parse HEAD");

            File.WriteAllText(filePath, "after\n");
            Git("commit -q -am after");
            Git(ResetWorkflow.BuildUndoLastCommit().ToString());

            Git("rev-parse HEAD").Should().Be(parentCommit);
            Git("diff --cached --name-only").Should().Be("tracked.txt");
            Git("diff --name-only").Should().BeEmpty();
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

        string Git(string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true
            }) ?? throw new InvalidOperationException("Failed to start git.");
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
            return output;
        }
    }

    [Theory]
    [InlineData(ResetMode.Soft, "--soft")]
    [InlineData(ResetMode.Mixed, "--mixed")]
    [InlineData(ResetMode.Keep, "--keep")]
    [InlineData(ResetMode.Merge, "--merge")]
    [InlineData(ResetMode.Hard, "--hard")]
    public void BuildResetTo_should_select_reset_mode_and_commit(ResetMode mode, string expectedMode)
    {
        string arguments = ResetWorkflow.BuildResetTo(new GitUIPluginInterfaces.GitRevision(_id), mode);

        arguments.Should().Contain(expectedMode).And.Contain(_id.ToString());
    }

    [Fact]
    public void CanResetTo_should_reject_missing_and_artificial_revisions()
    {
        ResetWorkflow.CanResetTo(null).Should().BeFalse();
        ResetWorkflow.CanResetTo(new GitUIPluginInterfaces.GitRevision(ObjectId.WorkTreeId)).Should().BeFalse();
        ResetWorkflow.CanResetTo(new GitUIPluginInterfaces.GitRevision(_id)).Should().BeTrue();
    }

    [Fact]
    public void BuildResetTo_should_reset_a_real_repository_using_selected_modes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-reset-revision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            Git(directory, "config user.name Test");
            Git(directory, "config user.email test@example.com");
            var filePath = Path.Combine(directory, "tracked.txt");
            File.WriteAllText(filePath, "target\n");
            Git(directory, "add tracked.txt");
            Git(directory, "commit -q -m target");
            var targetCommit = Git(directory, "rev-parse HEAD");
            File.WriteAllText(filePath, "newer\n");
            Git(directory, "commit -q -am newer");

            File.WriteAllText(filePath, "local change\n");
            Git(directory,
                ResetWorkflow.BuildResetTo(new GitUIPluginInterfaces.GitRevision(ObjectId.Parse(targetCommit)),
                    ResetMode.Soft).ToString());
            Git(directory, "rev-parse HEAD").Should().Be(targetCommit);
            Git(directory, "show :tracked.txt").Should().Be("newer");
            File.ReadAllText(filePath).Should().Be("local change\n");

            Git(directory,
                ResetWorkflow.BuildResetTo(new GitUIPluginInterfaces.GitRevision(ObjectId.Parse(targetCommit)),
                    ResetMode.Mixed).ToString());
            Git(directory, "rev-parse HEAD").Should().Be(targetCommit);
            Git(directory, "show :tracked.txt").Should().Be("target");
            File.ReadAllText(filePath).Should().Be("local change\n");

            Git(directory,
                ResetWorkflow.BuildResetTo(new GitUIPluginInterfaces.GitRevision(ObjectId.Parse(targetCommit)),
                    ResetMode.Hard).ToString());
            Git(directory, "rev-parse HEAD").Should().Be(targetCommit);
            File.ReadAllText(filePath).Should().Be("target\n");
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
    public void BuildResetTo_should_reject_artificial_revisions()
    {
        Action build = () =>
            ResetWorkflow.BuildResetTo(new GitUIPluginInterfaces.GitRevision(ObjectId.WorkTreeId), ResetMode.Hard);

        build.Should().Throw<ArgumentException>();
    }
}
