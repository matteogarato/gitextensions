using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using GitCommands;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Translations;
using GitExtensions.Shims.WinForms;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs;
using GitUI.Editor;
using GitUI.Hotkey;
using GitUI.LeftPanel;
using GitUI.ScriptsEngine;
using GitUI.UserControls;
using GitUI.UserControls.RevisionGrid;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using ResourceManager;
using Application = Avalonia.Application;
using CheckBox = Avalonia.Controls.CheckBox;
using ListBox = Avalonia.Controls.ListBox;
using TextBox = Avalonia.Controls.TextBox;

namespace GitUI.AvaloniaTests;

public sealed class ViewConstructionTests
{
    [Fact]
    public void Translator_should_find_the_shared_translations()
    {
        Translator.GetAllTranslations().Should().NotBeEmpty();
    }

    [AvaloniaFact]
    public void FormBrowse_should_construct()
    {
        FormBrowse form = new();
        form.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void CommandLogWindow_should_show_command_entries_and_details()
    {
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        var arguments = $"status {Guid.NewGuid():N}";
        var expectedCommandLine = $"git {arguments}";
        var operation = CommandLog.LogProcessStart("git", arguments, Path.GetTempPath());
        operation.LogProcessEnd(0);

        CommandLogWindow window = new();
        try
        {
            var content = window.Content as Grid
                          ?? throw new InvalidOperationException("Command log content was not created.");
            var entries = content.Children.OfType<ListBox>().Single();
            var entry = entries.Items
                .OfType<CommandLogEntry>()
                .Single(command => command.CommandLine == expectedCommandLine);
            entries.SelectedItem = entry;
            var details = content.Children.OfType<TextBox>().Single();

            details.Text.Should().Contain($"Arguments:   {arguments}");
            details.Text.Should().Contain(Path.GetTempPath());
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void CommandLogWindow_BuildReport_should_include_command_line_and_details_per_entry()
    {
        var arguments = $"log {Guid.NewGuid():N}";
        var operation = CommandLog.LogProcessStart("git", arguments, Path.GetTempPath());
        operation.LogProcessEnd(0);
        var entry = CommandLog.Commands.Single(command => command.CommandLine == $"git {arguments}");

        var report = CommandLogWindow.BuildReport([entry, entry]);

        report.Should().Contain(entry.ColumnLine);
        report.Should().Contain($"Arguments:   {arguments}");
        report.Split(entry.ColumnLine).Should().HaveCount(3);
    }

    [AvaloniaFact]
    public void FormBrowse_refresh_should_notify_repository_changed()
    {
        var module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns(Path.GetTempPath());
        module.IsValidGitWorkingDir().Returns(false);

        var notifier = Substitute.For<ILockableNotifier>();
        var appTitleGenerator = Substitute.For<IAppTitleGenerator>();
        appTitleGenerator.Generate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>()).Returns("Git Extensions");

        var commands = Substitute.For<IGitUiCommands>();
        commands.Module.Returns(module);
        commands.RepoChangedNotifier.Returns(notifier);
        commands.GetService(typeof(IAppTitleGenerator)).Returns(appTitleGenerator);
        AddHotkeySettingsLoader(commands);

        FormBrowse form = new(commands);
        var refresh = form.FindControl<MenuItem>("refreshToolStripMenuItem")
                      ?? throw new InvalidOperationException("Refresh menu item was not created.");
        refresh.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        notifier.Received(1).Notify();
    }

    [AvaloniaFact]
    public void FormBrowse_should_use_existing_translation_keys_for_menu_headers()
    {
        FormBrowse form = new();
        var translation = Substitute.For<ITranslation>();
        translation.TranslateItem(
                nameof(FormBrowse),
                "fetchAllToolStripMenuItem",
                "Text",
                Arg.Any<Func<string?>>())
            .Returns("&Fetch translated");

        form.AddTranslationItems(translation);
        form.TranslateItems(translation);

        translation.Received(1).AddTranslationItem(
            nameof(FormBrowse),
            "fetchAllToolStripMenuItem",
            "Text",
            "Fetch &all");
        var fetchAll = form.FindControl<MenuItem>("fetchAllToolStripMenuItem")
                       ?? throw new InvalidOperationException("Fetch-all menu item was not created.");
        fetchAll.Header.Should().Be("_Fetch translated");

        var commandLog = form.FindControl<MenuItem>("commandLogMenuItem")
                         ?? throw new InvalidOperationException("Command-log menu item was not created.");
        commandLog.Header.Should().Be(TranslatedStrings.CommandLogTitle);
    }

    [AvaloniaFact]
    public void FormBrowse_should_expose_remote_management_menu()
    {
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        var module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns(Path.GetTempPath());
        module.IsValidGitWorkingDir().Returns(false);

        var appTitleGenerator = Substitute.For<IAppTitleGenerator>();
        appTitleGenerator.Generate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>()).Returns("Git Extensions");

        var commands = Substitute.For<IGitUiCommands>();
        commands.Module.Returns(module);
        commands.GetService(typeof(IAppTitleGenerator)).Returns(appTitleGenerator);
        AddHotkeySettingsLoader(commands);

        FormBrowse form = new(commands);
        var remotes = form.FindControl<MenuItem>("remoteManagementMenuItem")
                      ?? throw new InvalidOperationException("Remote management menu item was not created.");
        var initializeRepository = form.FindControl<MenuItem>("initializeRepositoryMenuItem")
                                   ?? throw new InvalidOperationException(
                                       "Initialize repository menu item was not created.");
        var cleanupRepository = form.FindControl<MenuItem>("cleanupRepositoryMenuItem")
                                ?? throw new InvalidOperationException(
                                    "Cleanup repository menu item was not created.");
        var verifyRepository = form.FindControl<MenuItem>("verifyRepositoryMenuItem")
                               ?? throw new InvalidOperationException(
                                   "Verify repository menu item was not created.");
        var plugins = form.FindControl<MenuItem>("pluginsMenuItem")
                      ?? throw new InvalidOperationException("Plugins menu item was not created.");
        var reflog = form.FindControl<MenuItem>("reflogMenuItem")
                     ?? throw new InvalidOperationException("Reflog menu item was not created.");
        var submodules = form.FindControl<MenuItem>("submoduleManagementMenuItem")
                         ?? throw new InvalidOperationException("Submodule management menu item was not created.");
        var applyPatch = form.FindControl<MenuItem>("applyPatchMenuItem")
                         ?? throw new InvalidOperationException("Apply patch menu item was not created.");
        var viewPatch = form.FindControl<MenuItem>("viewPatchMenuItem")
                        ?? throw new InvalidOperationException("View patch menu item was not created.");
        var formatPatch = form.FindControl<MenuItem>("formatPatchMenuItem")
                          ?? throw new InvalidOperationException("Format patch menu item was not created.");
        var viewMenu = form.FindControl<MenuItem>("viewToolStripMenuItem")
                       ?? throw new InvalidOperationException("View menu item was not created.");
        var pullMerge = form.FindControl<MenuItem>("pullMergeMenuItem")
                        ?? throw new InvalidOperationException("Pull-merge menu item was not created.");
        var pullRebase = form.FindControl<MenuItem>("pullRebaseMenuItem")
                         ?? throw new InvalidOperationException("Pull-rebase menu item was not created.");
        var push = form.FindControl<MenuItem>("pushMenuItem")
                   ?? throw new InvalidOperationException("Push menu item was not created.");
        var forcePush = form.FindControl<MenuItem>("forcePushMenuItem")
                        ?? throw new InvalidOperationException("Force-push menu item was not created.");
        var stashChanges = form.FindControl<MenuItem>("stashChangesMenuItem")
                           ?? throw new InvalidOperationException("Stash-changes menu item was not created.");
        var resetAll = form.FindControl<MenuItem>("resetAllMenuItem")
                       ?? throw new InvalidOperationException("Reset-all menu item was not created.");
        var undoLastCommit = form.FindControl<MenuItem>("undoLastCommitMenuItem")
                             ?? throw new InvalidOperationException("Undo-last-commit menu item was not created.");
        var commitMenu = form.FindControl<MenuItem>("commitMenuItem")
                         ?? throw new InvalidOperationException("Commit menu item was not created.");
        commitMenu.Header.Should().Be(TranslatedStrings.CommitMenu);
        var goToCommit = form.FindControl<MenuItem>("goToCommitMenuItem")
                         ?? throw new InvalidOperationException("Go-to-commit menu item was not created.");
        var goToSuperproject = form.FindControl<MenuItem>("goToSuperprojectMenuItem")
                               ?? throw new InvalidOperationException("Go-to-parent menu item was not created.");
        goToCommit.Header.Should().Be(TranslatedStrings.GoToCommitMenu);
        goToSuperproject.Header.Should().Be(TranslatedStrings.GoToSuperprojectMenu);
        formatPatch.Header.Should().Be(TranslatedStrings.FormatPatchMenu);
        viewMenu.Header.Should().Be("_View");
        pullMerge.Header.Should().Be("_Pull (merge)");
        pullRebase.Header.Should().Be("Pull (_rebase)");
        push.Header.Should().Be("Pu_sh");
        forcePush.Header.Should().Be("Force push (with _lease)");
        stashChanges.Header.Should().Be("_Stash changes...");
        resetAll.Header.Should().Be("_Reset all changes...");
        undoLastCommit.Header.Should().Be("_Undo last commit...");
        var repoObjectsTree = form.FindControl<RepoObjectsTree>("repoObjectsTree")
                              ?? throw new InvalidOperationException(
                                  "Repository objects tree was not created.");
        var branchContextMenu = repoObjectsTree.ContextMenu
                                ?? throw new InvalidOperationException("Branch context menu was not created.");
        var branchMenuItems = branchContextMenu.ItemsSource as IEnumerable<MenuItem>
                              ?? throw new InvalidOperationException(
                                  "Branch context menu items were not created.");
        var revisionGrid = form.FindControl<RevisionGridControl>("RevisionGrid")
                           ?? throw new InvalidOperationException("Revision grid was not created.");
        var revisionContextMenu = revisionGrid.ContextMenu
                                  ?? throw new InvalidOperationException(
                                      "Revision context menu was not created.");
        var revisionMenuItems = revisionContextMenu.ItemsSource as IEnumerable<MenuItem>
                                ?? throw new InvalidOperationException(
                                    "Revision context menu items were not created.");
        var forceDelete =
            branchMenuItems.First(item => Equals(item.Header, TranslatedStrings.ForceDeleteBranchMenu));

        remotes.Header.Should().Be(TranslatedStrings.RemoteManagementMenu);
        initializeRepository.Header.Should().Be(TranslatedStrings.InitializeRepository);
        cleanupRepository.Header.Should().Be(TranslatedStrings.CleanupRepositoryMenu);
        cleanupRepository.IsEnabled.Should().BeFalse();
        verifyRepository.Header.Should().Be(TranslatedStrings.VerifyRepositoryMenu);
        verifyRepository.IsEnabled.Should().BeFalse();
        plugins.Header.Should().Be("_Plugins");
        reflog.Header.Should().Be("Show reflo_g...");
        reflog.IsEnabled.Should().BeFalse();
        submodules.Header.Should().Be(TranslatedStrings.Submodules);
        applyPatch.Header.Should().Be(TranslatedStrings.ApplyPatchMenu);
        applyPatch.IsEnabled.Should().BeFalse();
        viewPatch.Header.Should().Be(TranslatedStrings.ViewPatchMenu);
        viewPatch.IsEnabled.Should().BeFalse();
        branchMenuItems.Should().Contain(item => Equals(item.Header, TranslatedStrings.RenameBranchMenu));
        branchMenuItems.Should().Contain(item => Equals(item.Header, TranslatedStrings.CheckoutBranchMergeChangesMenu));
        branchMenuItems.Should().Contain(item => Equals(item.Header, TranslatedStrings.CheckoutBranchStashChangesMenu));
        branchMenuItems.Should()
            .Contain(item => Equals(item.Header, TranslatedStrings.CheckoutBranchDiscardChangesMenu));
        branchMenuItems.Should().Contain(item => Equals(item.Header, "Create _branch here..."));
        branchMenuItems.Should().Contain(item => Equals(item.Header, "_Merge into current branch"));
        branchMenuItems.Should().Contain(item => Equals(item.Header, "_Apply stash"));
        branchMenuItems.Should().Contain(item => Equals(item.Header, "_Pop stash"));
        branchMenuItems.Should().Contain(item => Equals(item.Header, "_Drop stash"));
        branchMenuItems.Should()
            .Contain(item => Equals(item.Header, TranslatedStrings.CreateBranchWithoutCheckoutMenu));
        branchMenuItems.Should().Contain(item => Equals(item.Header, TranslatedStrings.CreateOrphanBranchMenu));
        forceDelete.Header.Should().Be(TranslatedStrings.ForceDeleteBranchMenu);
        revisionMenuItems.Should().Contain(item => Equals(item.Header, TranslatedStrings.CheckoutRevisionMenu));
        var resetToRevision = revisionMenuItems.First(item =>
            Equals(item.Header, TranslatedStrings.ResetCurrentBranchToRevisionMenu));
        revisionMenuItems.Should().Contain(item => Equals(item.Header, TranslatedStrings.ArchiveRevisionMenu));
        var resetModes = resetToRevision.ItemsSource as IEnumerable<MenuItem>
                         ?? throw new InvalidOperationException(
                             "Reset-to-revision mode items were not created.");
        resetModes.Select(item => item.Header).Should().Contain(new[]
        {
            TranslatedStrings.ResetSoftToRevisionMenu, TranslatedStrings.ResetMixedToRevisionMenu,
            TranslatedStrings.ResetKeepToRevisionMenu, TranslatedStrings.ResetMergeToRevisionMenu,
            TranslatedStrings.ResetHardToRevisionMenu
        });
    }

    [AvaloniaFact]
    public void RemoteManagementWindow_should_construct_for_empty_remote_list()
    {
        var module = Substitute.For<IGitModule>();
        module.GetRemoteNames().Returns([]);
        module.GetAllLocalSettings().Returns([]);

        RemoteManagementWindow window = new(module);

        window.Content.Should().NotBeNull();
        window.Title.Should().Be(TranslatedStrings.RemoteManagementTitle);
    }

    [AvaloniaFact]
    public void SubmoduleManagementWindow_should_construct_for_empty_submodule_list()
    {
        var module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns("/repo");
        module.GetSubmodulesInfo().Returns([]);

        SubmoduleManagementWindow window = new(module, Substitute.For<IGitUiCommands>(), _ => false);

        window.Content.Should().NotBeNull();
        window.Title.Should().Be(TranslatedStrings.Submodules);
        window.FindControl<TextBlock>("lblNoSubmodules")!.IsVisible.Should().BeTrue();
        window.FindControl<Button>("btnAddSubmodule")!.IsEnabled.Should().BeTrue();
    }

    [AvaloniaFact]
    public void CleanupRepositoryWindow_should_construct_with_default_clean_options()
    {
        CleanupRepositoryWindow window = new(Substitute.For<IGitUiCommands>());

        window.Content.Should().NotBeNull();
        window.Title.Should().Be(TranslatedStrings.CleanupRepositoryTitle);
    }

    [AvaloniaFact]
    public void VerifyRepositoryWindow_should_construct()
    {
        VerifyRepositoryWindow window = new(Substitute.For<IGitUiCommands>());

        window.Content.Should().NotBeNull();
        window.Title.Should().Be(TranslatedStrings.VerifyRepositoryTitle);
    }

    [AvaloniaFact]
    public void AddSubmoduleWindow_should_enable_add_after_required_fields_are_entered()
    {
        var commands = Substitute.For<IGitUiCommands>();
        commands.StartGitCommandProcessDialog(
            Arg.Any<IWin32Window>(),
            Arg.Any<ArgumentString>()).Returns(true);

        AddSubmoduleWindow window = new(commands);
        window.Show();
        var add = window.FindControl<Button>("btnAddSubmodule")!;
        var remotePath = window.FindControl<TextBox>("txtSubmoduleUrl")!;
        var localPath = window.FindControl<TextBox>("txtSubmoduleLocalPath")!;
        var branch = window.FindControl<TextBox>("txtSubmoduleBranch")!;
        var force = window.FindControl<CheckBox>("chkSubmoduleForce")!;

        add.IsEnabled.Should().BeFalse();
        remotePath.Text = "https://example.org/team/child.git";
        localPath.Text.Should().Be("child");
        add.IsEnabled.Should().BeTrue();
        branch.Text = "main";
        force.IsChecked = true;
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        commands.Received(1).StartGitCommandProcessDialog(
            Arg.Any<IWin32Window>(),
            Arg.Is<ArgumentString>(arguments =>
                arguments.ToString().Contains("add", StringComparison.Ordinal)
                && arguments.ToString().Contains("-f", StringComparison.Ordinal)
                && arguments.ToString().Contains("-b", StringComparison.Ordinal)
                && arguments.ToString().Contains("main", StringComparison.Ordinal)));
        window.IsVisible.Should().BeFalse();
    }

    [AvaloniaFact]
    public void ReflogWindow_should_construct_for_a_repository()
    {
        var module = Substitute.For<IGitModule>();

        ReflogWindow window = new(module);

        window.Content.Should().NotBeNull();
        window.Title.Should().Be(TranslatedStrings.ReflogWindowTitle);
    }

    [AvaloniaFact]
    public void SubmoduleManagementWindow_should_update_and_sync_the_selected_submodule()
    {
        var submodule = Substitute.For<IGitSubmoduleInfo>();
        submodule.LocalPath.Returns("components/nested");
        submodule.IsInitialized.Returns(false);
        submodule.IsUpToDate.Returns(false);
        var modifiedSubmodule = Substitute.For<IGitSubmoduleInfo>();
        modifiedSubmodule.LocalPath.Returns("components/modified");
        modifiedSubmodule.IsInitialized.Returns(true);
        modifiedSubmodule.IsUpToDate.Returns(false);

        var module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns("/repo");
        module.GetSubmodulesInfo().Returns([submodule, modifiedSubmodule]);
        module.GetSubmoduleFullPath("components/modified").Returns("/repo/components/modified");

        var submoduleModule = Substitute.For<IGitModule>();
        submoduleModule.WorkingDir.Returns("/repo/components/modified");
        submoduleModule.GetSubmodulesInfo().Returns([]);
        submoduleModule.GetCurrentRemote().Returns("origin");
        submoduleModule.GetRemoteNames().Returns(["origin"]);
        submoduleModule.PullCmd("origin", null, false).Returns((ArgumentString)"pull origin");
        submoduleModule.PullCmd("origin", null, true).Returns((ArgumentString)"pull --rebase origin");
        var submoduleCommands = Substitute.For<IGitUiCommands>();
        submoduleCommands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(true);
        var moduleCommands = Substitute.For<IGitUiCommands>();
        moduleCommands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>()).Returns(true);

        var commands = Substitute.For<IGitUiCommands>();
        commands.WithGitModule(module).Returns(moduleCommands);
        commands.WithGitModule(submoduleModule).Returns(submoduleCommands);
        module.GetSubmodule("components/modified").Returns(submoduleModule);
        List<string> openedRepositories = [];

        SubmoduleManagementWindow window = new(module, commands, path =>
        {
            openedRepositories.Add(path);
            return true;
        });
        window.Show();
        var list = window.FindControl<ListBox>("lstSubmodules")!;
        list.Items.OfType<string>().Should().BeEquivalentTo(
            "components/nested (Not initialized)",
            "components/modified (Modified)");
        list.SelectedIndex = 0;

        var update = window.FindControl<Button>("btnUpdateSubmodule")!;
        var pullMerge = window.FindControl<Button>("btnPullSubmoduleMerge")!;
        var pullRebase = window.FindControl<Button>("btnPullSubmoduleRebase")!;
        var remove = window.FindControl<Button>("btnRemoveSubmodule")!;
        var open = window.FindControl<Button>("btnOpenSubmodule")!;
        open.IsEnabled.Should().BeFalse();
        pullMerge.IsEnabled.Should().BeFalse();
        pullRebase.IsEnabled.Should().BeFalse();
        update.IsEnabled.Should().BeTrue();
        remove.IsEnabled.Should().BeTrue();
        update.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        openedRepositories.Should().BeEmpty();

        moduleCommands.Received(1).StartGitCommandProcessDialog(
            Arg.Any<IWin32Window>(),
            Arg.Is<ArgumentString>(arguments =>
                arguments.ToString().Contains("submodule update", StringComparison.Ordinal)
                && arguments.ToString().Contains("components/nested", StringComparison.Ordinal)));

        var sync = window.FindControl<Button>("btnSyncSubmodule")!;
        sync.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        moduleCommands.Received(1).StartGitCommandProcessDialog(
            Arg.Any<IWin32Window>(),
            Arg.Is<ArgumentString>(arguments =>
                arguments.ToString().Contains("submodule sync", StringComparison.Ordinal)
                && arguments.ToString().Contains("components/nested", StringComparison.Ordinal)));

        list.SelectedIndex = 1;
        open.IsEnabled.Should().BeTrue();
        pullMerge.IsEnabled.Should().BeTrue();
        pullRebase.IsEnabled.Should().BeTrue();
        pullMerge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        submoduleCommands.Received(1).StartGitCommandProcessDialog(
            Arg.Any<IWin32Window>(),
            Arg.Is<ArgumentString>(arguments => arguments.ToString() == "pull origin"));
        pullRebase.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        submoduleCommands.Received(1).StartGitCommandProcessDialog(
            Arg.Any<IWin32Window>(),
            Arg.Is<ArgumentString>(arguments => arguments.ToString() == "pull --rebase origin"));

        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        openedRepositories.Should().ContainSingle().Which.Should().Be("/repo/components/modified");
        window.IsVisible.Should().BeFalse();
        window.HasChanges.Should().BeTrue();
    }

    [AvaloniaFact]
    public void SubmoduleManagementWindow_should_show_and_update_nested_submodules()
    {
        var submodule = Substitute.For<IGitSubmoduleInfo>();
        submodule.LocalPath.Returns("components/child");
        submodule.IsInitialized.Returns(true);
        submodule.IsUpToDate.Returns(true);
        var nestedSubmodule = Substitute.For<IGitSubmoduleInfo>();
        nestedSubmodule.LocalPath.Returns("dependencies/nested");
        nestedSubmodule.IsInitialized.Returns(false);
        nestedSubmodule.IsUpToDate.Returns(false);

        var module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns("/repo");
        module.GetSubmodulesInfo().Returns([submodule]);
        module.GetSubmoduleFullPath("components/child").Returns("/repo/components/child");

        var childModule = Substitute.For<IGitModule>();
        childModule.WorkingDir.Returns("/repo/components/child");
        childModule.GetSubmodulesInfo().Returns([nestedSubmodule]);
        module.GetSubmodule("components/child").Returns(childModule);

        var childCommands = Substitute.For<IGitUiCommands>();
        childCommands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>()).Returns(true);
        var commands = Substitute.For<IGitUiCommands>();
        commands.WithGitModule(childModule).Returns(childCommands);

        SubmoduleManagementWindow window = new(module, commands, _ => false);
        window.Show();
        var list = window.FindControl<ListBox>("lstSubmodules")!;
        list.Items.OfType<string>().Should().BeEquivalentTo(
            $"components/child ({TranslatedStrings.SubmoduleUpToDate})",
            "components/child/dependencies/nested (Not initialized)");

        list.SelectedIndex = 1;
        window.FindControl<Button>("btnUpdateSubmodule")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        childCommands.Received(1).StartGitCommandProcessDialog(
            Arg.Any<IWin32Window>(),
            Arg.Is<ArgumentString>(arguments =>
                arguments.ToString().Contains("submodule update", StringComparison.Ordinal)
                && arguments.ToString().Contains("dependencies/nested", StringComparison.Ordinal)));
        window.HasChanges.Should().BeTrue();
    }

    [AvaloniaFact]
    public void HotkeySettingsWindow_should_construct()
    {
        HotkeySettingsWindow window = new(new AvaloniaHotkeySettingsManager());

        window.Content.Should().NotBeNull();
        window.Title.Should().Be(ResourceManager.TranslatedStrings.KeyboardShortcutsTitle);
    }

    [AvaloniaFact]
    public void FormBrowse_theme_menu_should_apply_and_store_the_selected_theme()
    {
        var originalTheme = AppSettings.ThemeId;

        try
        {
            var module = Substitute.For<IGitModule>();
            module.WorkingDir.Returns(Path.GetTempPath());
            module.IsValidGitWorkingDir().Returns(false);

            var appTitleGenerator = Substitute.For<IAppTitleGenerator>();
            appTitleGenerator.Generate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>()).Returns("Git Extensions");

            var commands = Substitute.For<IGitUiCommands>();
            commands.Module.Returns(module);
            commands.GetService(typeof(IAppTitleGenerator)).Returns(appTitleGenerator);
            AddHotkeySettingsLoader(commands);

            FormBrowse form = new(commands);
            var darkTheme = form.FindControl<MenuItem>("darkThemeMenuItem")
                            ?? throw new InvalidOperationException("Dark theme menu item was not created.");

            darkTheme.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            AssertionExtensions.Should().Be(ThemeId.DefaultDark);
            Application.Current?.RequestedThemeVariant.Should().Be(ThemeVariant.Dark);
            GitExtensions.Shims.WinForms.Application.SystemColorMode.Should().Be(SystemColorMode.Dark);
            darkTheme.IsChecked.Should().BeTrue();
        }
        finally
        {
            AppSettings.ThemeId = originalTheme;
            ThemePreference.Apply(originalTheme);
        }
    }

    [AvaloniaFact]
    public void ThemePreference_Apply_should_let_Avalonia_follow_the_system_for_the_system_theme()
    {
        var originalTheme = AppSettings.ThemeId;
        try
        {
            ThemePreference.Apply(ThemeId.WindowsAppColorModeId);

            Application.Current?.RequestedThemeVariant.Should().Be(ThemeVariant.Default);
        }
        finally
        {
            ThemePreference.Apply(originalTheme);
        }
    }

    [AvaloniaFact]
    public void RevisionGridControl_should_construct()
    {
        RevisionGridControl control = new();
        control.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void FileStatusList_should_construct()
    {
        FileStatusList control = new();
        control.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void FileViewer_should_construct_and_view_a_patch()
    {
        FileViewer control = new();
        control.ViewPatch("@@ -1,1 +1,1 @@\n-old\n+new\n");
        control.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void CommitInfo_should_construct()
    {
        CommitInfo.CommitInfo control = new();
        control.Revision.Should().BeNull();
    }

    [AvaloniaFact]
    public void RepoObjectsTree_should_construct()
    {
        RepoObjectsTree control = new();
        control.SetRefs([]);
        control.SelectedRef.Should().BeNull();
    }

    private static void AddHotkeySettingsLoader(IGitUiCommands commands)
    {
        commands.GetService(typeof(IHotkeySettingsLoader)).Returns(new AvaloniaHotkeySettingsManager());
        var scriptsManager = Substitute.For<IScriptsManager>();
        scriptsManager.GetScripts().Returns(new BindingList<ScriptInfo>());
        commands.GetService(typeof(IScriptsManager)).Returns(scriptsManager);
    }
}
