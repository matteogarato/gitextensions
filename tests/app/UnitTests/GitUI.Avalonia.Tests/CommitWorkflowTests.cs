using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using GitUI.CommandsDialogs;
using GitUI.UserControls;

namespace GitUI.AvaloniaTests;

public sealed class CommitWorkflowTests
{
    private static readonly CommitRequest _request = new("message", false, false, false);

    [Fact]
    public void Validate_should_accept_message_with_staged_files()
    {
        CommitWorkflow.Validate(_request, 1, false, false)
            .Should().BeNull();
    }

    [Fact]
    public void Validate_should_reject_unresolved_conflicts()
    {
        CommitWorkflow.Validate(_request, 1, true, true)
            .Should().Be(TranslatedStrings.UnresolvedConflicts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Validate_should_reject_blank_message(string message)
    {
        CommitWorkflow.Validate(_request with { Message = message }, 1, false, false)
            .Should().Be(TranslatedStrings.EnterCommitMessageError);
    }

    [Fact]
    public void Validate_should_reject_an_unchanged_commit_template()
    {
        CommitWorkflow.Validate(
                _request with { Message = "template", UsingCommitTemplate = true, TemplateText = "template" },
                1,
                false,
                false)
            .Should().Be(TranslatedStrings.EnterCommitMessageError);
    }

    [Fact]
    public void BuildMessageHistory_should_put_last_message_first_and_remove_duplicates()
    {
        var messages = CommitWorkflow.BuildMessageHistory(
            ["first\n", "second\n", "first\n"],
            "second",
            3);

        messages.Select(item => item.Message).Should().Equal("second", "first");
        messages.Select(item => item.Preview).Should().Equal("second", "first");
    }

    [Fact]
    public void Validate_should_reject_empty_index_for_plain_commit()
    {
        CommitWorkflow.Validate(_request, 0, false, false)
            .Should().Be(TranslatedStrings.NoStagedChangesError);
    }

    [Fact]
    public void Validate_should_allow_empty_index_for_amend()
    {
        CommitWorkflow.Validate(_request with { Amend = true }, 0, false, false)
            .Should().BeNull();
    }

    [Fact]
    public void Validate_should_allow_empty_index_for_merge_commit()
    {
        CommitWorkflow.Validate(_request, 0, true, false)
            .Should().BeNull();
    }

    [Fact]
    public void BuildCommand_should_map_all_options()
    {
        var arguments = CommitWorkflow.BuildCommand(
            new CommitRequest("m", true, true, true),
            0,
            "/tmp/COMMITMESSAGE",
            path => path).ToString();

        arguments.Should().Be("commit --amend --no-verify --signoff -F \"/tmp/COMMITMESSAGE\" --allow-empty");
    }

    [Fact]
    public void BuildCommand_should_map_author_signing_and_reset_author()
    {
        var arguments = CommitWorkflow.BuildCommand(
            new CommitRequest(
                "m",
                true,
                false,
                false,
                "Name <email@example.com>",
                true,
                "ABC123",
                true),
            1,
            "/tmp/COMMITMESSAGE",
            path => path).ToString();

        arguments.Should()
            .Be(
                "commit --amend --author=\"Name <email@example.com>\" --gpg-sign=ABC123 -F \"/tmp/COMMITMESSAGE\" --reset-author");
    }

    [Fact]
    public void BuildCommand_should_not_allow_empty_when_files_are_staged()
    {
        var arguments = CommitWorkflow.BuildCommand(_request, 2, "/tmp/COMMITMESSAGE", path => path).ToString();

        arguments.Should().Be("commit -F \"/tmp/COMMITMESSAGE\"");
    }

    [Fact]
    public void BuildCommand_should_leave_message_to_git_when_explicit_message_is_disabled()
    {
        var arguments = CommitWorkflow.BuildCommand(
            _request with { UseExplicitCommitMessage = false },
            1,
            string.Empty,
            path => path).ToString();

        arguments.Should().Be("commit");
    }

    [Fact]
    public void BuildCommand_should_create_commit_in_real_repository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-commit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            RunGit(directory, "init -q");
            File.WriteAllText(Path.Combine(directory, "a.txt"), "a");
            RunGit(directory, "add a.txt");
            var messagePath = Path.Combine(directory, ".git", "COMMITMESSAGE");
            File.WriteAllText(messagePath, "first commit\n");

            var arguments = CommitWorkflow.BuildCommand(_request, 1, messagePath, path => path).ToString();

            RunGit(directory, $"-c user.name=t -c user.email=t@example.com {arguments}");
            RunGit(directory, "log -1 --format=%s").Should().Be("first commit");
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

        static string RunGit(string workingDirectory, string arguments)
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

    [AvaloniaFact]
    public void WorkingTreePanel_commit_button_should_raise_request_with_options()
    {
        var originalLastCommitMessage = GitCommands.AppSettings.LastCommitMessage;
        var originalUseFormCommitMessage = GitCommands.AppSettings.UseFormCommitMessage;
        GitCommands.AppSettings.LastCommitMessage = "previous commit";
        GitCommands.AppSettings.UseFormCommitMessage = true;
        try
        {
            WorkingTreePanel panel = new();
            Window window = new() { Content = panel };
            window.Show();
            CommitRequest? received = null;
            panel.CommitRequested += (_, request) => received = request;

            panel.FindControl<TextBox>("txtCommitMessage")!.Text.Should().Be("previous commit");
            panel.FindControl<TextBox>("txtCommitMessage")!.Text = "hello";
            panel.FindControl<CheckBox>("chkAmend")!.IsChecked = true;
            panel.FindControl<CheckBox>("chkSignOff")!.IsChecked = true;
            panel.FindControl<TextBox>("txtCommitAuthor")!.Text = "Name <email@example.com>";
            panel.FindControl<ComboBox>("cbxCommitGpgSigning")!.SelectedIndex = 3;
            panel.FindControl<TextBox>("txtCommitGpgKeyId")!.Text = "ABC123";
            panel.FindControl<Button>("btnCommit")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            received.Should().Be(new CommitRequest(
                "hello",
                true,
                true,
                false,
                "Name <email@example.com>",
                true,
                "ABC123",
                UseExplicitCommitMessage: true));
        }
        finally
        {
            GitCommands.AppSettings.LastCommitMessage = originalLastCommitMessage;
            GitCommands.AppSettings.UseFormCommitMessage = originalUseFormCommitMessage;
        }
    }
}
