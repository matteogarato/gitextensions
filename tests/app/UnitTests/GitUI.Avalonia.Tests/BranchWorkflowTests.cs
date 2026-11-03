using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.CommandsDialogs;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitExtensionsTests;

public sealed class BranchWorkflowTests
{
    private static IGitRef CreateRef(string name, bool isHead = false, bool isRemote = false, bool isTag = false, bool isSelected = false, string? localName = null, string remoteName = "")
    {
        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns(name);
        gitRef.LocalName.Returns(localName ?? name);
        gitRef.Remote.Returns(remoteName);
        gitRef.IsHead.Returns(isHead);
        gitRef.IsRemote.Returns(isRemote);
        gitRef.IsTag.Returns(isTag);
        gitRef.IsSelected.Returns(isSelected);
        return gitRef;
    }

    [Fact]
    public void CanCheckout_should_reject_current_branch_tags_and_null()
    {
        BranchWorkflow.CanCheckout(CreateRef("main", isHead: true, isSelected: true)).Should().BeFalse();
        BranchWorkflow.CanCheckout(CreateRef("v1", isTag: true)).Should().BeFalse();
        BranchWorkflow.CanCheckout(null).Should().BeFalse();
        BranchWorkflow.CanCheckout(CreateRef("dev", isHead: true)).Should().BeTrue();
        BranchWorkflow.CanCheckout(CreateRef("origin/dev", isRemote: true)).Should().BeTrue();
    }

    [Fact]
    public void CanCheckoutRevision_should_reject_artificial_revisions()
    {
        BranchWorkflow.CanCheckoutRevision(null).Should().BeFalse();
        BranchWorkflow.CanCheckoutRevision(new GitRevision(GitExtensions.Extensibility.Git.ObjectId.Parse(GitRevision.WorkTreeGuid))).Should().BeFalse();
        BranchWorkflow.CanCheckoutRevision(new GitRevision(GitExtensions.Extensibility.Git.ObjectId.Parse("0123456789012345678901234567890123456789"))).Should().BeTrue();
    }

    [Fact]
    public void GetSuggestedBranchName_should_prefer_local_name_and_non_tag_refs()
    {
        IGitRef tag = CreateRef("v1", isTag: true, localName: "release-v1");
        IGitRef remote = CreateRef("origin/feature", isRemote: true, localName: "feature");
        GitRevision revision = new(GitExtensions.Extensibility.Git.ObjectId.Parse("0123456789012345678901234567890123456789"))
        {
            Refs = [tag, remote],
        };

        BranchWorkflow.GetSuggestedBranchName(null, revision).Should().Be("feature");
        BranchWorkflow.GetSuggestedBranchName(tag, revision).Should().Be("release-v1");
        BranchWorkflow.GetSuggestedBranchName(null, null).Should().BeEmpty();
    }

    [Fact]
    public void CanDelete_should_reject_current_branch_and_tags()
    {
        BranchWorkflow.CanDelete(CreateRef("main", isHead: true, isSelected: true)).Should().BeFalse();
        BranchWorkflow.CanDelete(CreateRef("v1", isTag: true)).Should().BeFalse();
        BranchWorkflow.CanDelete(CreateRef("dev", isHead: true)).Should().BeTrue();
    }

    [Fact]
    public void CanDeleteRemoteBranch_should_accept_remote_branches_except_the_symbolic_head()
    {
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("origin/feature", isRemote: true, localName: "feature", remoteName: "origin")).Should().BeTrue();
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("origin/HEAD", isRemote: true, localName: "HEAD", remoteName: "origin")).Should().BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("main", isHead: true)).Should().BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("v1", isRemote: true, isTag: true, remoteName: "origin")).Should().BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("origin/feature", isRemote: true, localName: "feature")).Should().BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(null).Should().BeFalse();
    }

    [Fact]
    public void FindTrackingBranch_should_match_only_the_local_branch_tracking_the_selected_remote()
    {
        IGitRef remoteBranch = CreateRef("origin/feature", isRemote: true, localName: "feature", remoteName: "origin");
        IGitRef trackingBranch = CreateRef("feature", isHead: true);
        IGitRef unrelatedBranch = CreateRef("other", isHead: true);
        trackingBranch.IsTrackingRemote(remoteBranch).Returns(true);
        unrelatedBranch.IsTrackingRemote(remoteBranch).Returns(false);

        BranchWorkflow.FindTrackingBranch(remoteBranch, [unrelatedBranch, trackingBranch]).Should().Be(trackingBranch);
        BranchWorkflow.FindTrackingBranch(remoteBranch, [unrelatedBranch]).Should().BeNull();
    }

    [Fact]
    public void CanForceDelete_should_allow_only_non_current_local_branches()
    {
        BranchWorkflow.CanForceDelete(CreateRef("feature", isHead: true)).Should().BeTrue();
        BranchWorkflow.CanForceDelete(CreateRef("main", isHead: true, isSelected: true)).Should().BeFalse();
        BranchWorkflow.CanForceDelete(CreateRef("origin/feature", isRemote: true)).Should().BeFalse();
        BranchWorkflow.CanForceDelete(CreateRef("v1", isTag: true)).Should().BeFalse();
        BranchWorkflow.CanForceDelete(null).Should().BeFalse();
    }

    [Fact]
    public void FindWorktreeUsingBranch_should_match_only_checked_out_local_branch()
    {
        GitWorktree linkedWorktree = new(
            "/tmp/feature-worktree",
            GitWorktreeHeadType.Branch,
            "abc123",
            "feature",
            IsDeleted: false);
        GitWorktree detachedWorktree = new(
            "/tmp/detached-worktree",
            GitWorktreeHeadType.Detached,
            "def456",
            Branch: null,
            IsDeleted: false);
        GitWorktree deletedWorktree = linkedWorktree with { Path = "/tmp/deleted-worktree", IsDeleted = true };

        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("feature", isHead: true), [linkedWorktree, detachedWorktree])
            .Should().Be(linkedWorktree);
        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("other", isHead: true), [linkedWorktree, detachedWorktree])
            .Should().BeNull();
        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("feature", isHead: true), [deletedWorktree])
            .Should().BeNull();
        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("origin/feature", isRemote: true), [linkedWorktree])
            .Should().BeNull();
    }

    [Fact]
    public void CanRename_should_allow_local_branches_only()
    {
        BranchWorkflow.CanRename(CreateRef("main", isHead: true)).Should().BeTrue();
        BranchWorkflow.CanRename(CreateRef("main", isHead: true, isSelected: true)).Should().BeTrue();
        BranchWorkflow.CanRename(CreateRef("origin/main", isRemote: true)).Should().BeFalse();
        BranchWorkflow.CanRename(CreateRef("v1", isTag: true)).Should().BeFalse();
        BranchWorkflow.CanRename(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ValidateNewBranchName_should_reject_blank_names(string? name)
    {
        BranchWorkflow.ValidateNewBranchName(Substitute.For<IGitModule>(), name).Should().Be(TranslatedStrings.EnterBranchNameError);
    }

    [Fact]
    public void ValidateNewBranchName_should_use_git_format_check()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.CheckBranchFormat("bad name").Returns(false);
        module.CheckBranchFormat("good").Returns(true);

        BranchWorkflow.ValidateNewBranchName(module, "bad name").Should().Be(TranslatedStrings.InvalidBranchNameError);
        BranchWorkflow.ValidateNewBranchName(module, " good ").Should().BeNull();
    }

    [Fact]
    public void BuildCheckout_should_checkout_local_branch_directly()
    {
        BranchWorkflow.BuildCheckout(CreateRef("dev", isHead: true)).Arguments.Should().Be("checkout \"dev\"");
    }

    [Fact]
    public void BuildCheckout_should_merge_local_changes_when_requested()
    {
        BranchWorkflow.BuildCheckout(CreateRef("dev", isHead: true), LocalChangesAction.Merge)
            .Arguments.Should().Be("checkout --merge \"dev\"");
    }

    [Fact]
    public void BuildCheckout_should_force_checkout_when_reset_is_requested()
    {
        BranchWorkflow.BuildCheckout(CreateRef("dev", isHead: true), LocalChangesAction.Reset)
            .Arguments.Should().Be("checkout --force \"dev\"");
    }

    [Fact]
    public void BuildCheckout_should_create_tracking_branch_for_remote()
    {
        BranchWorkflow.BuildCheckout(CreateRef("origin/dev", isRemote: true, localName: "dev"))
            .Arguments.Should().Be("checkout -b \"dev\" --track \"origin/dev\"");
    }

    [Fact]
    public void BuildCheckoutRevision_should_checkout_the_selected_commit_without_force()
    {
        BranchWorkflow.BuildCheckoutRevision(GitExtensions.Extensibility.Git.ObjectId.Parse("0123456789012345678901234567890123456789"))
            .Arguments.Should().Be("checkout \"0123456789012345678901234567890123456789\"");
    }

    [Fact]
    public void BuildDelete_should_not_force_unless_requested()
    {
        BranchWorkflow.BuildDelete(CreateRef("dev", isHead: true), force: false).Arguments.Should().Be("branch --delete \"dev\"");
        BranchWorkflow.BuildDelete(CreateRef("dev", isHead: true), force: true).Arguments.Should().Be("branch --delete --force \"dev\"");
    }

    [Fact]
    public void BuildDeleteRemoteBranch_should_build_a_remote_state_change_command()
    {
        IGitCommand command = BranchWorkflow.BuildDeleteRemoteBranch(
            CreateRef("origin/feature/topic", isRemote: true, localName: "feature/topic", remoteName: "origin"));

        command.Arguments.Should().Be("push origin :refs/heads/\"feature/topic\"");
        command.AccessesRemote.Should().BeTrue();
        command.ChangesRepoState.Should().BeTrue();
    }

    [Fact]
    public void BuildRename_should_quote_both_branch_names()
    {
        BranchWorkflow.BuildRename("old-name", "new-name").Arguments.Should().Be("branch -m \"old-name\" \"new-name\"");
    }

    [Fact]
    public void NormaliseBranchName_should_skip_names_without_valid_path_characters()
    {
        IGitBranchNameNormaliser normaliser = Substitute.For<IGitBranchNameNormaliser>();

        string result = BranchWorkflow.NormaliseBranchName(" :", normaliser, new GitBranchNameOptions("_"));

        result.Should().Be(" :");
        normaliser.DidNotReceive().Normalise(Arg.Any<string>(), Arg.Any<GitBranchNameOptions>());
    }

    [Fact]
    public void NormaliseBranchName_should_normalise_names_with_valid_path_characters()
    {
        IGitBranchNameNormaliser normaliser = Substitute.For<IGitBranchNameNormaliser>();
        normaliser.Normalise("feature name", Arg.Any<GitBranchNameOptions>()).Returns("feature_name");

        string result = BranchWorkflow.NormaliseBranchName("feature name", normaliser, new GitBranchNameOptions("_"));

        result.Should().Be("feature_name");
        normaliser.Received(1).Normalise("feature name", Arg.Any<GitBranchNameOptions>());
    }

    [AvaloniaFact]
    public void TextPromptWindow_should_normalise_input_when_focus_is_lost()
    {
        TextPromptWindow prompt = new(
            "Rename branch",
            "Branch name:",
            "feature",
            branchName => branchName.Replace(' ', '_'));
        TextBox input = ((StackPanel)prompt.Content!).Children.OfType<TextBox>().SingleOrDefault()
            ?? throw new InvalidOperationException("The text prompt input was not created.");
        input.Text = "feature branch";
        prompt.Show();
        try
        {
            input.Focus();
            StackPanel buttons = ((StackPanel)prompt.Content!).Children.OfType<StackPanel>().Single();
            buttons.Children.OfType<Button>().First().Focus();

            input.Text.Should().Be("feature_branch");
        }
        finally
        {
            prompt.Close();
        }
    }

    [Fact]
    public void BuildCreate_should_allow_creating_without_checkout()
    {
        BranchWorkflow.BuildCreate("feature", GitExtensions.Extensibility.Git.ObjectId.Parse("0123456789012345678901234567890123456789"), checkout: false)
            .Arguments.Should().Be("branch \"feature\" 0123456789012345678901234567890123456789");
    }

    [Fact]
    public void BuildCreateOrphan_should_use_the_selected_start_point()
    {
        BranchWorkflow.BuildCreateOrphan("unrelated", GitExtensions.Extensibility.Git.ObjectId.Parse("0123456789012345678901234567890123456789"))
            .Arguments.Should().Be("checkout --orphan unrelated 0123456789012345678901234567890123456789");
    }

    [Fact]
    public void Branch_commands_should_create_checkout_and_delete_in_real_repository()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ge-branch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            File.WriteAllText(Path.Combine(directory, "README"), "tracked\n");
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m first");
            string head = Git(directory, "rev-parse HEAD");

            Git(directory, BranchWorkflow.BuildCreate("without-checkout", GitExtensions.Extensibility.Git.ObjectId.Parse(head), checkout: false).ToString());
            Git(directory, "branch --show-current").Should().Be("main");
            Git(directory, "branch --list without-checkout").Should().Be("without-checkout");

            Git(directory, BranchWorkflow.BuildCreate("feature", GitExtensions.Extensibility.Git.ObjectId.Parse(head), checkout: true).ToString());
            Git(directory, "branch --show-current").Should().Be("feature");

            Git(directory, BranchWorkflow.BuildCheckout(CreateRef("main", isHead: true)).Arguments);
            Git(directory, "branch --show-current").Should().Be("main");

            Git(directory, BranchWorkflow.BuildRename("main", "renamed-main").ToString());
            Git(directory, "branch --show-current").Should().Be("renamed-main");

            Git(directory, BranchWorkflow.BuildRename("feature", "renamed-feature").ToString());
            Git(directory, "branch --list feature").Should().BeEmpty();
            Git(directory, "branch --list renamed-feature").Should().Be("renamed-feature");

            Git(directory, BranchWorkflow.BuildDelete(CreateRef("renamed-feature", isHead: true), force: false).Arguments);
            Git(directory, "branch --list renamed-feature").Should().BeEmpty();

            Git(directory, "checkout -b unmerged");
            File.WriteAllText(Path.Combine(directory, "README"), "unmerged commit\n");
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m unmerged");
            string unmergedHead = Git(directory, "rev-parse HEAD");
            Git(directory, "checkout renamed-main");
            Git(directory, BranchWorkflow.BuildDelete(CreateRef("unmerged", isHead: true), force: true).Arguments);
            Git(directory, "branch --list unmerged").Should().BeEmpty();

            File.WriteAllText(Path.Combine(directory, "README"), "local change\n");
            Git(directory, BranchWorkflow.BuildCheckoutRevision(GitExtensions.Extensibility.Git.ObjectId.Parse(unmergedHead)).Arguments, expectSuccess: false);
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be("local change\n");
            Git(directory, "status --porcelain=v1").Should().Be("M README");
            File.WriteAllText(Path.Combine(directory, "README"), "tracked\n");
            Git(directory, BranchWorkflow.BuildCheckoutRevision(GitExtensions.Extensibility.Git.ObjectId.Parse(unmergedHead)).Arguments);
            Git(directory, "symbolic-ref --quiet --short HEAD", expectSuccess: false).Should().BeEmpty();
            Git(directory, "rev-parse HEAD").Should().Be(unmergedHead);
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be("unmerged commit\n");

            Git(directory, BranchWorkflow.BuildCreateOrphan("orphan", GitExtensions.Extensibility.Git.ObjectId.Parse(head)).ToString());
            Git(directory, "symbolic-ref --short HEAD").Should().Be("orphan");
            Git(directory, "rev-parse --verify HEAD", expectSuccess: false).Should().BeEmpty();
            Git(directory, "ls-files --error-unmatch README").Should().Be("README");
            Git(directory, "status --porcelain=v1").Should().Be("A  README");
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

        static string Git(string workingDirectory, string arguments, bool expectSuccess = true)
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            string error = process.StandardError.ReadToEnd();
            if (expectSuccess)
            {
                process.ExitCode.Should().Be(0, error);
            }
            else
            {
                process.ExitCode.Should().NotBe(0, error);
            }

            return output;
        }
    }

    [Fact]
    public void DeleteRemoteBranch_should_delete_only_the_remote_branch_in_real_repositories()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ge-delete-remote-branch-{Guid.NewGuid():N}");
        string workingDirectory = Path.Combine(root, "working");
        string remoteDirectory = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(workingDirectory);
        Directory.CreateDirectory(remoteDirectory);
        try
        {
            Git(remoteDirectory, "init --bare -q");
            Git(workingDirectory, "init -q -b main");
            File.WriteAllText(Path.Combine(workingDirectory, "README"), "tracked\n");
            Git(workingDirectory, "add README");
            Git(workingDirectory, "-c user.name=t -c user.email=t@example.com commit -q -m first");
            Git(workingDirectory, $"remote add origin \"{remoteDirectory}\"");
            Git(workingDirectory, "push -q origin main");
            Git(workingDirectory, "push -q origin main:feature/topic");
            Git(workingDirectory, "fetch -q origin");
            Git(workingDirectory, "branch --track feature/topic origin/feature/topic");

            Git(workingDirectory, BranchWorkflow.BuildDeleteRemoteBranch(
                CreateRef("origin/feature/topic", isRemote: true, localName: "feature/topic", remoteName: "origin")).Arguments);

            Git(workingDirectory, "ls-remote --heads origin refs/heads/feature/topic").Should().BeEmpty();
            Git(workingDirectory, "show-ref --verify --quiet refs/remotes/origin/feature/topic", expectSuccess: false);
            Git(workingDirectory, "for-each-ref --format=%(refname:short) refs/heads/feature/topic").Should().Be("feature/topic");
            Git(workingDirectory, BranchWorkflow.BuildDelete(CreateRef("feature/topic", isHead: true), force: false).Arguments);
            Git(workingDirectory, "for-each-ref --format=%(refname:short) refs/heads/feature/topic").Should().BeEmpty();
            Git(workingDirectory, "for-each-ref --format=%(refname:short) refs/heads/main").Should().Be("main");
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(root, recursive: true);
        }

        return;

        static string Git(string workingDirectory, string arguments, bool expectSuccess = true)
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            string error = process.StandardError.ReadToEnd();
            if (expectSuccess)
            {
                process.ExitCode.Should().Be(0, error);
            }
            else
            {
                process.ExitCode.Should().NotBe(0, error);
            }

            return output;
        }
    }

    [Fact]
    public void CheckoutBranch_should_merge_non_conflicting_local_changes_in_real_repository()
    {
        const string BaseContent = "base first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nbase seventh\n";
        const string TopicContent = "topic first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nbase seventh\n";
        const string LocalContent = "base first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nlocal seventh\n";
        const string MergedContent = "topic first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nlocal seventh\n";

        string directory = Path.Combine(Path.GetTempPath(), $"ge-checkout-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            File.WriteAllText(Path.Combine(directory, "README"), BaseContent);
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m base");

            Git(directory, "checkout -q -b topic");
            File.WriteAllText(Path.Combine(directory, "README"), TopicContent);
            Git(directory, "commit -qam topic");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "README"), LocalContent);

            Git(directory, BranchWorkflow.BuildCheckout(CreateRef("topic", isHead: true), LocalChangesAction.Merge).Arguments);

            Git(directory, "branch --show-current").Should().Be("topic");
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be(MergedContent);
            Git(directory, "status --porcelain=v1").Should().Be("M README");
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
    public void CheckoutBranch_should_discard_tracked_changes_when_reset_is_requested()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ge-checkout-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            File.WriteAllText(Path.Combine(directory, "README"), "main content\n");
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m main");

            Git(directory, "checkout -q -b topic");
            File.WriteAllText(Path.Combine(directory, "README"), "topic content\n");
            Git(directory, "commit -qam topic");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "README"), "local content\n");
            Git(directory, "add README");

            Git(directory, BranchWorkflow.BuildCheckout(CreateRef("topic", isHead: true), LocalChangesAction.Reset).Arguments);

            Git(directory, "branch --show-current").Should().Be("topic");
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be("topic content\n");
            Git(directory, "status --porcelain=v1").Should().BeEmpty();
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
}
