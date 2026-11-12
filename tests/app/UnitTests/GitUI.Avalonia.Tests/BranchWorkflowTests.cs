using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class BranchWorkflowTests
{
    private static IGitRef CreateRef(string name, bool isHead = false, bool isRemote = false, bool isTag = false,
        bool isSelected = false, string? localName = null, string remoteName = "")
    {
        var gitRef = Substitute.For<IGitRef>();
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
        BranchWorkflow.CanCheckout(CreateRef("main", true, isSelected: true)).Should().BeFalse();
        BranchWorkflow.CanCheckout(CreateRef("v1", isTag: true)).Should().BeFalse();
        BranchWorkflow.CanCheckout(null).Should().BeFalse();
        BranchWorkflow.CanCheckout(CreateRef("dev", true)).Should().BeTrue();
        BranchWorkflow.CanCheckout(CreateRef("origin/dev", isRemote: true)).Should().BeTrue();
    }

    [Fact]
    public void CanCheckoutRevision_should_reject_artificial_revisions()
    {
        BranchWorkflow.CanCheckoutRevision(null).Should().BeFalse();
        BranchWorkflow
            .CanCheckoutRevision(
                new GitUIPluginInterfaces.GitRevision(ObjectId.Parse(GitUIPluginInterfaces.GitRevision.WorkTreeGuid)))
            .Should().BeFalse();
        BranchWorkflow
            .CanCheckoutRevision(
                new GitUIPluginInterfaces.GitRevision(ObjectId.Parse("0123456789012345678901234567890123456789")))
            .Should().BeTrue();
    }

    [Fact]
    public void GetSuggestedBranchName_should_prefer_local_name_and_non_tag_refs()
    {
        var tag = CreateRef("v1", isTag: true, localName: "release-v1");
        var remote = CreateRef("origin/feature", isRemote: true, localName: "feature");
        GitUIPluginInterfaces.GitRevision revision =
            new(ObjectId.Parse("0123456789012345678901234567890123456789")) { Refs = [tag, remote] };

        BranchWorkflow.GetSuggestedBranchName(null, revision).Should().Be("feature");
        BranchWorkflow.GetSuggestedBranchName(tag, revision).Should().Be("release-v1");
        BranchWorkflow.GetSuggestedBranchName(null, null).Should().BeEmpty();
    }

    [Fact]
    public void CanDelete_should_reject_current_branch_and_tags()
    {
        BranchWorkflow.CanDelete(CreateRef("main", true, isSelected: true)).Should().BeFalse();
        BranchWorkflow.CanDelete(CreateRef("v1", isTag: true)).Should().BeFalse();
        BranchWorkflow.CanDelete(CreateRef("dev", true)).Should().BeTrue();
    }

    [Fact]
    public void CanDeleteRemoteBranch_should_accept_remote_branches_except_the_symbolic_head()
    {
        BranchWorkflow
            .CanDeleteRemoteBranch(CreateRef("origin/feature", isRemote: true, localName: "feature",
                remoteName: "origin")).Should().BeTrue();
        BranchWorkflow
            .CanDeleteRemoteBranch(CreateRef("origin/HEAD", isRemote: true, localName: "HEAD", remoteName: "origin"))
            .Should().BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("main", true)).Should().BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("v1", isRemote: true, isTag: true, remoteName: "origin"))
            .Should().BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(CreateRef("origin/feature", isRemote: true, localName: "feature")).Should()
            .BeFalse();
        BranchWorkflow.CanDeleteRemoteBranch(null).Should().BeFalse();
    }

    [Fact]
    public void FindTrackingBranch_should_match_only_the_local_branch_tracking_the_selected_remote()
    {
        var remoteBranch = CreateRef("origin/feature", isRemote: true, localName: "feature", remoteName: "origin");
        var trackingBranch = CreateRef("feature", true);
        var unrelatedBranch = CreateRef("other", true);
        trackingBranch.IsTrackingRemote(remoteBranch).Returns(true);
        unrelatedBranch.IsTrackingRemote(remoteBranch).Returns(false);

        BranchWorkflow.FindTrackingBranch(remoteBranch, [unrelatedBranch, trackingBranch]).Should().Be(trackingBranch);
        BranchWorkflow.FindTrackingBranch(remoteBranch, [unrelatedBranch]).Should().BeNull();
    }

    [Fact]
    public void CanForceDelete_should_allow_only_non_current_local_branches()
    {
        BranchWorkflow.CanForceDelete(CreateRef("feature", true)).Should().BeTrue();
        BranchWorkflow.CanForceDelete(CreateRef("main", true, isSelected: true)).Should().BeFalse();
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
            false);
        GitWorktree detachedWorktree = new(
            "/tmp/detached-worktree",
            GitWorktreeHeadType.Detached,
            "def456",
            null,
            false);
        var deletedWorktree = linkedWorktree with { Path = "/tmp/deleted-worktree", IsDeleted = true };

        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("feature", true), [linkedWorktree, detachedWorktree])
            .Should().Be(linkedWorktree);
        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("other", true), [linkedWorktree, detachedWorktree])
            .Should().BeNull();
        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("feature", true), [deletedWorktree])
            .Should().BeNull();
        BranchWorkflow.FindWorktreeUsingBranch(CreateRef("origin/feature", isRemote: true), [linkedWorktree])
            .Should().BeNull();
    }

    [Fact]
    public void CanRename_should_allow_local_branches_only()
    {
        BranchWorkflow.CanRename(CreateRef("main", true)).Should().BeTrue();
        BranchWorkflow.CanRename(CreateRef("main", true, isSelected: true)).Should().BeTrue();
        BranchWorkflow.CanRename(CreateRef("origin/main", isRemote: true)).Should().BeFalse();
        BranchWorkflow.CanRename(CreateRef("v1", isTag: true)).Should().BeFalse();
        BranchWorkflow.CanRename(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ValidateNewBranchName_should_reject_blank_names(string? name)
    {
        BranchWorkflow.ValidateNewBranchName(Substitute.For<IGitModule>(), name).Should()
            .Be(TranslatedStrings.EnterBranchNameError);
    }

    [Fact]
    public void ValidateNewBranchName_should_use_git_format_check()
    {
        var module = Substitute.For<IGitModule>();
        module.CheckBranchFormat("bad name").Returns(false);
        module.CheckBranchFormat("good").Returns(true);

        BranchWorkflow.ValidateNewBranchName(module, "bad name").Should().Be(TranslatedStrings.InvalidBranchNameError);
        BranchWorkflow.ValidateNewBranchName(module, " good ").Should().BeNull();
    }

    [Fact]
    public void BuildCheckout_should_checkout_local_branch_directly()
    {
        BranchWorkflow.BuildCheckout(CreateRef("dev", true)).Arguments.Should().Be("checkout \"dev\"");
    }

    [Fact]
    public void BuildCheckout_should_merge_local_changes_when_requested()
    {
        BranchWorkflow.BuildCheckout(CreateRef("dev", true), LocalChangesAction.Merge)
            .Arguments.Should().Be("checkout --merge \"dev\"");
    }

    [Fact]
    public void BuildCheckout_should_force_checkout_when_reset_is_requested()
    {
        BranchWorkflow.BuildCheckout(CreateRef("dev", true), LocalChangesAction.Reset)
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
        BranchWorkflow.BuildCheckoutRevision(ObjectId.Parse("0123456789012345678901234567890123456789"))
            .Arguments.Should().Be("checkout \"0123456789012345678901234567890123456789\"");
    }

    [Fact]
    public void BuildDelete_should_not_force_unless_requested()
    {
        BranchWorkflow.BuildDelete(CreateRef("dev", true), false).Arguments.Should().Be("branch --delete \"dev\"");
        BranchWorkflow.BuildDelete(CreateRef("dev", true), true).Arguments.Should()
            .Be("branch --delete --force \"dev\"");
    }

    [Fact]
    public void BuildDeleteRemoteBranch_should_build_a_remote_state_change_command()
    {
        var command = BranchWorkflow.BuildDeleteRemoteBranch(
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
        var normaliser = Substitute.For<IGitBranchNameNormaliser>();

        var result = BranchWorkflow.NormaliseBranchName(" :", normaliser, new GitBranchNameOptions("_"));

        result.Should().Be(" :");
        normaliser.DidNotReceive().Normalise(Arg.Any<string>(), Arg.Any<GitBranchNameOptions>());
    }

    [Fact]
    public void NormaliseBranchName_should_normalise_names_with_valid_path_characters()
    {
        var normaliser = Substitute.For<IGitBranchNameNormaliser>();
        normaliser.Normalise("feature name", Arg.Any<GitBranchNameOptions>()).Returns("feature_name");

        var result = BranchWorkflow.NormaliseBranchName("feature name", normaliser, new GitBranchNameOptions("_"));

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
        var input = ((StackPanel)prompt.Content!).Children.OfType<TextBox>().SingleOrDefault()
                    ?? throw new InvalidOperationException("The text prompt input was not created.");
        input.Text = "feature branch";
        prompt.Show();
        try
        {
            input.Focus();
            var buttons = ((StackPanel)prompt.Content!).Children.OfType<StackPanel>().Single();
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
        BranchWorkflow.BuildCreate("feature", ObjectId.Parse("0123456789012345678901234567890123456789"), false)
            .Arguments.Should().Be("branch \"feature\" 0123456789012345678901234567890123456789");
    }

    [Fact]
    public void BuildCreateOrphan_should_use_the_selected_start_point()
    {
        BranchWorkflow.BuildCreateOrphan("unrelated", ObjectId.Parse("0123456789012345678901234567890123456789"))
            .Arguments.Should().Be("checkout --orphan unrelated 0123456789012345678901234567890123456789");
    }

    [Fact]
    public void Branch_commands_should_create_checkout_and_delete_in_real_repository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-branch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            File.WriteAllText(Path.Combine(directory, "README"), "tracked\n");
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m first");
            var head = Git(directory, "rev-parse HEAD");

            Git(directory, BranchWorkflow.BuildCreate("without-checkout", ObjectId.Parse(head), false).ToString());
            Git(directory, "branch --show-current").Should().Be("main");
            Git(directory, "branch --list without-checkout").Should().Be("without-checkout");

            Git(directory, BranchWorkflow.BuildCreate("feature", ObjectId.Parse(head), true).ToString());
            Git(directory, "branch --show-current").Should().Be("feature");

            Git(directory, BranchWorkflow.BuildCheckout(CreateRef("main", true)).Arguments);
            Git(directory, "branch --show-current").Should().Be("main");

            Git(directory, BranchWorkflow.BuildRename("main", "renamed-main").ToString());
            Git(directory, "branch --show-current").Should().Be("renamed-main");

            Git(directory, BranchWorkflow.BuildRename("feature", "renamed-feature").ToString());
            Git(directory, "branch --list feature").Should().BeEmpty();
            Git(directory, "branch --list renamed-feature").Should().Be("renamed-feature");

            Git(directory, BranchWorkflow.BuildDelete(CreateRef("renamed-feature", true), false).Arguments);
            Git(directory, "branch --list renamed-feature").Should().BeEmpty();

            Git(directory, "checkout -b unmerged");
            File.WriteAllText(Path.Combine(directory, "README"), "unmerged commit\n");
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m unmerged");
            var unmergedHead = Git(directory, "rev-parse HEAD");
            Git(directory, "checkout renamed-main");
            Git(directory, BranchWorkflow.BuildDelete(CreateRef("unmerged", true), true).Arguments);
            Git(directory, "branch --list unmerged").Should().BeEmpty();

            File.WriteAllText(Path.Combine(directory, "README"), "local change\n");
            Git(directory, BranchWorkflow.BuildCheckoutRevision(ObjectId.Parse(unmergedHead)).Arguments, false);
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be("local change\n");
            Git(directory, "status --porcelain=v1").Should().Be("M README");
            File.WriteAllText(Path.Combine(directory, "README"), "tracked\n");
            Git(directory, BranchWorkflow.BuildCheckoutRevision(ObjectId.Parse(unmergedHead)).Arguments);
            Git(directory, "symbolic-ref --quiet --short HEAD", false).Should().BeEmpty();
            Git(directory, "rev-parse HEAD").Should().Be(unmergedHead);
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be("unmerged commit\n");

            Git(directory, BranchWorkflow.BuildCreateOrphan("orphan", ObjectId.Parse(head)).ToString());
            Git(directory, "symbolic-ref --short HEAD").Should().Be("orphan");
            Git(directory, "rev-parse --verify HEAD", false).Should().BeEmpty();
            Git(directory, "ls-files --error-unmatch README").Should().Be("README");
            Git(directory, "status --porcelain=v1").Should().Be("A  README");
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

        static string Git(string workingDirectory, string arguments, bool expectSuccess = true)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true
            })!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            var error = process.StandardError.ReadToEnd();
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
        var root = Path.Combine(Path.GetTempPath(), $"ge-delete-remote-branch-{Guid.NewGuid():N}");
        var workingDirectory = Path.Combine(root, "working");
        var remoteDirectory = Path.Combine(root, "remote.git");
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
                    CreateRef("origin/feature/topic", isRemote: true, localName: "feature/topic", remoteName: "origin"))
                .Arguments);

            Git(workingDirectory, "ls-remote --heads origin refs/heads/feature/topic").Should().BeEmpty();
            Git(workingDirectory, "show-ref --verify --quiet refs/remotes/origin/feature/topic", false);
            Git(workingDirectory, "for-each-ref --format=%(refname:short) refs/heads/feature/topic").Should()
                .Be("feature/topic");
            Git(workingDirectory, BranchWorkflow.BuildDelete(CreateRef("feature/topic", true), false).Arguments);
            Git(workingDirectory, "for-each-ref --format=%(refname:short) refs/heads/feature/topic").Should().BeEmpty();
            Git(workingDirectory, "for-each-ref --format=%(refname:short) refs/heads/main").Should().Be("main");
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

        static string Git(string workingDirectory, string arguments, bool expectSuccess = true)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true
            })!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            var error = process.StandardError.ReadToEnd();
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
        const string baseContent =
            "base first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nbase seventh\n";
        const string topicContent =
            "topic first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nbase seventh\n";
        const string localContent =
            "base first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nlocal seventh\n";
        const string mergedContent =
            "topic first\nbase second\nbase third\nbase fourth\nbase fifth\nbase sixth\nlocal seventh\n";

        var directory = Path.Combine(Path.GetTempPath(), $"ge-checkout-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            File.WriteAllText(Path.Combine(directory, "README"), baseContent);
            Git(directory, "add README");
            Git(directory, "-c user.name=t -c user.email=t@example.com commit -q -m base");

            Git(directory, "checkout -q -b topic");
            File.WriteAllText(Path.Combine(directory, "README"), topicContent);
            Git(directory, "commit -qam topic");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "README"), localContent);

            Git(directory, BranchWorkflow.BuildCheckout(CreateRef("topic", true), LocalChangesAction.Merge).Arguments);

            Git(directory, "branch --show-current").Should().Be("topic");
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be(mergedContent);
            Git(directory, "status --porcelain=v1").Should().Be("M README");
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
    public void CheckoutBranch_should_discard_tracked_changes_when_reset_is_requested()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-checkout-reset-{Guid.NewGuid():N}");
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

            Git(directory, BranchWorkflow.BuildCheckout(CreateRef("topic", true), LocalChangesAction.Reset).Arguments);

            Git(directory, "branch --show-current").Should().Be("topic");
            File.ReadAllText(Path.Combine(directory, "README")).Should().Be("topic content\n");
            Git(directory, "status --porcelain=v1").Should().BeEmpty();
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
