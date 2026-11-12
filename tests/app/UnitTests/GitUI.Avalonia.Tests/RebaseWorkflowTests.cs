using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class RebaseWorkflowTests
{
    private const string _gitSequenceEditor = "GIT_SEQUENCE_EDITOR";
    private const string _noOpSequenceEditor = ":";

    [Fact]
    public void Validate_should_reject_detached_head_and_empty_target()
    {
        var module = Substitute.For<IGitModule>();
        module.GetSelectedBranch(true).Returns(string.Empty);

        RebaseWorkflow.Validate(module, "main").Should().Be(TranslatedStrings.RebaseDetachedHeadError);

        module.GetSelectedBranch(true).Returns("feature");
        RebaseWorkflow.Validate(module, null).Should().Be(TranslatedStrings.RebaseTargetRequired);
    }

    [Fact]
    public void Validate_should_reject_current_branch_and_running_operations()
    {
        var module = Substitute.For<IGitModule>();
        module.GetSelectedBranch(true).Returns("feature");

        RebaseWorkflow.Validate(module, "feature").Should().Be(TranslatedStrings.RebaseSameBranchError);

        module.InTheMiddleOfRebase().Returns(true);
        RebaseWorkflow.Validate(module, "main").Should().Be(TranslatedStrings.RebaseOperationInProgressError);
    }

    [Fact]
    public void BuildRebase_should_set_target_and_optional_auto_stash()
    {
        RebaseWorkflow.BuildRebase(" main ", false).ToString()
            .Should().Contain("rebase").And.Contain("\"main\"").And.NotContain("--autostash");
        RebaseWorkflow.BuildRebase("main", true).ToString()
            .Should().Contain("--autostash").And.Contain("\"main\"");
    }

    [Fact]
    public void BuildRebase_should_support_interactive_range_and_autosquash()
    {
        RebaseWorkflow.BuildRebase(new RebaseConfiguration(
                "main",
                true,
                true,
                true,
                "base")).ToString()
            .Should().Contain("-i").And.Contain("--autosquash").And.Contain("--autostash")
            .And.Contain("--onto main \"base\" \"HEAD\"");
    }

    [Fact]
    public void BuildRebase_should_rebase_range_commits_onto_the_selected_branch()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-rebase-range-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            Git(directory, "config user.name t");
            Git(directory, "config user.email t@example.com");
            File.WriteAllText(Path.Combine(directory, "base.txt"), "base\n");
            Git(directory, "add base.txt");
            Git(directory, "commit -q -m base");
            Git(directory, "branch upstream");
            Git(directory, "branch feature");
            Git(directory, "checkout -q feature");
            File.WriteAllText(Path.Combine(directory, "feature-one.txt"), "one\n");
            Git(directory, "add feature-one.txt");
            Git(directory, "commit -q -m feature-one");
            File.WriteAllText(Path.Combine(directory, "feature-two.txt"), "two\n");
            Git(directory, "add feature-two.txt");
            Git(directory, "commit -q -m feature-two");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "target.txt"), "target\n");
            Git(directory, "add target.txt");
            Git(directory, "commit -q -m target");
            Git(directory, "checkout -q feature");

            Dictionary<string, string> editorEnvironment = new() { [_gitSequenceEditor] = _noOpSequenceEditor };
            Git(directory, RebaseWorkflow.BuildRebase(new RebaseConfiguration(
                "main",
                false,
                true,
                false,
                "upstream")).ToString(), environmentVariables: editorEnvironment);

            Git(directory, "merge-base --is-ancestor main HEAD");
            Git(directory, "log --format=%s main..HEAD").Should().Contain("feature-one").And.Contain("feature-two");
            Git(directory, "status --porcelain").Should().BeEmpty();
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BuildContinue_should_use_shared_continue_rebase_command()
    {
        RebaseWorkflow.BuildContinue().ToString().Should().EndWith("rebase --continue");
        RebaseWorkflow.BuildSkip().ToString().Should().EndWith("rebase --skip");
        RebaseWorkflow.BuildAbort().ToString().Should().EndWith("rebase --abort");
        RebaseWorkflow.BuildEditTodo().ToString().Should().EndWith("rebase --edit-todo");
    }

    [Fact]
    public void BuildRebase_and_BuildEditTodo_and_BuildAbort_should_rebase_and_restore_conflicted_repository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-rebase-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            Git(directory, "config user.name t");
            Git(directory, "config user.email t@example.com");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "base\n");
            Git(directory, "add conflict.txt");
            Git(directory, "commit -q -m base");
            Git(directory, "branch feature");
            Git(directory, "checkout -q feature");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "feature\n");
            Git(directory, "commit -q -am feature");
            var featureHead = Git(directory, "rev-parse HEAD");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "main\n");
            Git(directory, "commit -q -am main-change");
            Git(directory, "checkout -q feature");

            Dictionary<string, string> editorEnvironment = new() { [_gitSequenceEditor] = _noOpSequenceEditor };
            Git(directory, RebaseWorkflow.BuildRebase(new RebaseConfiguration(
                "main",
                false,
                true,
                false,
                null)).ToString(), 1, editorEnvironment);
            Git(directory, "rev-parse --verify REBASE_HEAD").Should().NotBeEmpty();
            Git(directory, RebaseWorkflow.BuildEditTodo().ToString(), environmentVariables: editorEnvironment);
            Git(directory, "rev-parse --verify REBASE_HEAD").Should().NotBeEmpty();

            Git(directory, RebaseWorkflow.BuildAbort().ToString());

            Git(directory, "rev-parse HEAD").Should().Be(featureHead);
            Git(directory, "status --porcelain").Should().BeEmpty();
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, true);
        }
    }

    private static string Git(
        string workingDirectory,
        string arguments,
        int expectedExitCode = 0,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        ProcessStartInfo startInfo = new("git", arguments)
        {
            WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (environmentVariables is not null)
        {
            foreach ((var key, var value) in environmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        process.ExitCode.Should().Be(expectedExitCode, error);
        return output;
    }
}
