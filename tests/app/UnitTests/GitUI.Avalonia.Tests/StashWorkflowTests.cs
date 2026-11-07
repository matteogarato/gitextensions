using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.CommandsDialogs;
using GitUIPluginInterfaces;

namespace GitExtensionsTests;

public sealed class StashWorkflowTests
{
    private static GitStash CreateStash(int index = 0) => new(index, "On main: wip");

    [Fact]
    public void BuildSave_should_include_message_and_untracked_flag()
    {
        string args = StashWorkflow.BuildSave(includeUntrackedFiles: true, keepIndex: false, "  wip  ");

        args.Should().Contain("stash").And.Contain("-u").And.Contain("wip");
    }

    [Fact]
    public void BuildApply_should_target_the_stash_name()
    {
        ((string)StashWorkflow.BuildApply(CreateStash(2))).Should().Contain("apply").And.Contain("stash@{2}");
    }

    [Fact]
    public void BuildApplyLatest_should_target_the_newly_created_stash()
    {
        ((string)StashWorkflow.BuildApplyLatest()).Should().Contain("stash").And.Contain("apply").And.Contain("stash@{0}");
    }

    [Fact]
    public void BuildPop_should_allow_missing_stash()
    {
        ((string)StashWorkflow.BuildPop(null)).Trim().Should().EndWith("pop");
        ((string)StashWorkflow.BuildPop(CreateStash(1))).Should().Contain("stash@{1}");
    }

    [Fact]
    public void BuildClear_should_clear_all_stashes()
    {
        ((string)StashWorkflow.BuildClear()).Should().Be("stash clear");
    }

    [Fact]
    public void BuildSaveStaged_should_push_only_the_index_with_the_message()
    {
        ((string)StashWorkflow.BuildSaveStaged(" wip ")).Should().Be("stash push --staged -m \"wip\"");
        ((string)StashWorkflow.BuildSaveStaged(null)).Should().Be("stash push --staged");
    }

    [Fact]
    public void BuildDrop_should_target_the_stash_name()
    {
        ((string)StashWorkflow.BuildDrop(CreateStash())).Should().Contain("drop").And.Contain("stash@{0}");
    }

    [Fact]
    public void ValidateSave_should_require_changes()
    {
        StashWorkflow.ValidateSave(0).Should().NotBeNull();
        StashWorkflow.ValidateSave(1).Should().BeNull();
    }

    [Fact]
    public void BuildSave_and_BuildApplyLatest_should_preserve_changes_across_checkout()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ge-stash-checkout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            File.WriteAllText(Path.Combine(directory, "README"), "base\n");
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m base");

            Git(directory, "checkout -q -b topic");
            File.WriteAllText(Path.Combine(directory, "topic.txt"), "topic\n");
            Git(directory, "add topic.txt");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m topic");
            Git(directory, "checkout -q main");

            File.WriteAllText(Path.Combine(directory, "README"), "local edit\n");
            File.WriteAllText(Path.Combine(directory, "untracked.txt"), "untracked edit\n");
            Git(directory, StashWorkflow.BuildSave(includeUntrackedFiles: true, keepIndex: false, message: string.Empty).ToString());
            Git(directory, "status --porcelain=v1").Should().BeEmpty();

            Git(directory, "checkout topic");
            Git(directory, StashWorkflow.BuildApplyLatest().ToString());

            Git(directory, "branch --show-current").Should().Be("topic");
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be("local edit\n");
            File.ReadAllText(Path.Combine(directory, "untracked.txt")).Should().Be("untracked edit\n");
            Git(directory, "stash list").Should().Contain("stash@{0}");
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
    public void BuildSave_with_selected_files_should_stash_only_matching_tracked_and_untracked_changes()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ge-stash-partial-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            File.WriteAllText(Path.Combine(directory, "selected.txt"), "base\n");
            File.WriteAllText(Path.Combine(directory, "other.txt"), "base\n");
            Git(directory, "add selected.txt other.txt");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m base");

            File.WriteAllText(Path.Combine(directory, "selected.txt"), "selected change\n");
            File.WriteAllText(Path.Combine(directory, "other.txt"), "other change\n");
            File.WriteAllText(Path.Combine(directory, "selected-untracked.txt"), "untracked change\n");
            File.WriteAllText(Path.Combine(directory, "other-untracked.txt"), "untracked other change\n");

            Git(directory, StashWorkflow.BuildSave(
                includeUntrackedFiles: true,
                keepIndex: false,
                message: "partial",
                selectedFiles: ["selected.txt", "selected-untracked.txt"]).ToString());

            File.ReadAllText(Path.Combine(directory, "selected.txt")).Should().Be("base\n");
            File.ReadAllText(Path.Combine(directory, "other.txt")).Should().Be("other change\n");
            File.Exists(Path.Combine(directory, "selected-untracked.txt")).Should().BeFalse();
            File.ReadAllText(Path.Combine(directory, "other-untracked.txt")).Should().Be("untracked other change\n");
            Git(directory, "stash show --stat --include-untracked stash@{0}")
                .Should().Contain("selected.txt").And.Contain("selected-untracked.txt")
                .And.NotContain("other.txt").And.NotContain("other-untracked.txt");
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
    public void BuildPop_when_stash_conflicts_should_preserve_stash_and_report_unmerged_file()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ge-stash-conflict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            Git(directory, "config user.name t");
            Git(directory, "config user.email t@example.com");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "base\n");
            Git(directory, "add conflict.txt");
            Git(directory, "commit -q -m base");

            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "stashed change\n");
            Git(directory, "stash push -q -m conflict");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "committed change\n");
            Git(directory, "commit -q -am head");

            Git(directory, StashWorkflow.BuildPop(CreateStash()).ToString(), expectedExitCode: 1);

            Git(directory, "status --porcelain=v1").Should().Contain("UU conflict.txt");
            File.ReadAllText(Path.Combine(directory, "conflict.txt")).Should().Contain("<<<<<<<");
            Git(directory, "stash list").Should().Contain("stash@{0}");
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

        static string Git(string workingDirectory, string arguments, int expectedExitCode = 0)
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.ExitCode.Should().Be(expectedExitCode, error);
            return output;
        }
    }

    [AvaloniaFact]
    public void WorkingTreePanel_should_request_stashing_only_selected_unstaged_files()
    {
        WorkingTreePanel panel = new();
        Window window = new() { Content = panel };
        window.Show();
        ListBox unstagedList = panel.FindControl<ListBox>("lstUnstaged")
            ?? throw new InvalidOperationException("The unstaged files list was not created.");
        GitItemStatus item = new("selected.txt") { Staged = StagedStatus.WorkTree };
        panel.SetStatus([item]);
        List<string>? selectedFiles = null;
        panel.StashSelectedFilesRequested += (_, _) => selectedFiles = [.. panel.SelectedItems.Select(status => status.Name)];

        unstagedList.SelectedItems!.Add(item);
        Button stashButton = panel.FindControl<Button>("btnStashSelected")
            ?? throw new InvalidOperationException("The stash selected button was not created.");
        stashButton.IsEnabled.Should().BeTrue();
        panel.StashSelectedFiles();

        selectedFiles.Should().Equal("selected.txt");
    }

    [AvaloniaFact]
    public async Task StashOptionsWindow_should_return_updated_options_when_saved()
    {
        Window owner = new();
        owner.Show();
        StashOptionsWindow dialog = new(includeUntrackedFiles: false, keepIndex: false);
        Task<ManualStashOptions?> result = dialog.ShowDialog<ManualStashOptions?>(owner);
        StackPanel content = (StackPanel)dialog.Content!;
        CheckBox[] options = [.. content.Children.OfType<CheckBox>()];
        options[0].IsChecked = true;
        options[1].IsChecked = true;
        Button save = content.Children.OfType<StackPanel>().Single().Children.OfType<Button>().First();
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        (await result).Should().Be(new ManualStashOptions(IncludeUntrackedFiles: true, KeepIndex: true));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task StashOptionsWindow_should_return_null_when_cancelled()
    {
        Window owner = new();
        owner.Show();
        StashOptionsWindow dialog = new(includeUntrackedFiles: true, keepIndex: false);
        Task<ManualStashOptions?> result = dialog.ShowDialog<ManualStashOptions?>(owner);
        StackPanel content = (StackPanel)dialog.Content!;
        CheckBox[] options = [.. content.Children.OfType<CheckBox>()];
        options[0].IsChecked.Should().BeTrue();
        options[1].IsChecked.Should().BeFalse();
        Button cancel = content.Children.OfType<StackPanel>().Single().Children.OfType<Button>().Last();
        cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        (await result).Should().BeNull();
        owner.Close();
    }
}
