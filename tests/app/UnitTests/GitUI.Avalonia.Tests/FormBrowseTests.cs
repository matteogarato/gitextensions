using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitCommands.Remotes;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.Hotkey;
using GitUI.LeftPanel;
using GitUI.ScriptsEngine;
using GitUIPluginInterfaces;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using ResourceManager;
using Keys = GitExtensions.Shims.WinForms.Keys;
using DialogResult = GitExtensions.Shims.WinForms.DialogResult;
using IMessageBoxHost = GitExtensions.Shims.WinForms.IMessageBoxHost;
using IWin32Window = GitExtensions.Shims.WinForms.IWin32Window;
using MessageBoxButtons = GitExtensions.Shims.WinForms.MessageBoxButtons;
using MessageBoxDefaultButton = GitExtensions.Shims.WinForms.MessageBoxDefaultButton;
using MessageBoxIcon = GitExtensions.Shims.WinForms.MessageBoxIcon;
using ShimHost = GitExtensions.Shims.WinForms.ShimHost;

namespace GitExtensionsTests;

public sealed class FormBrowseTests : IDisposable
{
    private ServiceContainer _serviceContainer = null!;
    private string _workingDirectory = null!;

    public FormBrowseTests()
    {
        AvaloniaSynchronizationContext.InstallIfNeeded();
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();

        _serviceContainer = new ServiceContainer();
        GitExtUtils.ServiceContainerRegistry.RegisterServices(_serviceContainer);

        System.IO.Abstractions.FileSystem fileSystem = new();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        RepositoryDescriptionProvider repositoryDescriptionProvider = new(gitDirectoryResolver);
        _serviceContainer.AddService<System.IO.Abstractions.IFileSystem>(fileSystem);
        _serviceContainer.AddService<IGitDirectoryResolver>(gitDirectoryResolver);
        _serviceContainer.AddService<IRepositoryDescriptionProvider>(repositoryDescriptionProvider);
        _serviceContainer.AddService<IAppTitleGenerator>(new AppTitleGenerator(repositoryDescriptionProvider));
        _serviceContainer.AddService<ILinkFactory>(new LinkFactory());
        GitCommands.ServiceContainerRegistry.RegisterServices(_serviceContainer);
        GitUI.ServiceContainerRegistry.RegisterServices(_serviceContainer);

        _workingDirectory = Path.Combine(Path.GetTempPath(), $"GitExtensions.Avalonia.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workingDirectory);
    }

    public void Dispose()
    {
        _serviceContainer.Dispose();
        Directory.Delete(_workingDirectory, recursive: true);
    }

    [Fact]
    public void InitRepositoryWorkflow_Initialize_should_create_a_working_repository()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "new repository");

        GitModule repository = InitRepositoryWorkflow.Initialize(
            _serviceContainer.GetRequiredService<IGitExecutorProvider>(),
            repositoryPath,
            bare: false);

        repository.IsValidGitWorkingDir().Should().BeTrue();
        GitModule.TryFindGitWorkingDir(repositoryPath).Should().NotBeNull();
        File.Exists(Path.Combine(repositoryPath, ".git", "HEAD")).Should().BeTrue();
    }

    [Fact]
    public void InitRepositoryWorkflow_Initialize_should_create_a_bare_repository()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "bare repository");

        GitModule repository = InitRepositoryWorkflow.Initialize(
            _serviceContainer.GetRequiredService<IGitExecutorProvider>(),
            repositoryPath,
            bare: true);

        repository.IsValidGitWorkingDir().Should().BeTrue();
        GitModule.TryFindGitWorkingDir(repositoryPath).Should().NotBeNull();
        File.Exists(Path.Combine(repositoryPath, "HEAD")).Should().BeTrue();
        Directory.Exists(Path.Combine(repositoryPath, ".git")).Should().BeFalse();
    }

    [Fact]
    public void ReflogWorkflow_should_build_reset_and_branch_commands_for_an_entry()
    {
        const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        ReflogEntry entry = new(ObjectId.Parse(Sha), "HEAD@{3}", "commit: lost work");

        ReflogWorkflow.BuildResetTo(entry, ResetMode.Hard).ToString().Should().Contain("reset --hard").And.Contain(Sha);
        ReflogWorkflow.BuildCreateBranch("rescued", entry).ToString().Should().Contain("rescued").And.Contain(Sha);
    }

    [Fact]
    public void ReflogWorkflow_Parse_should_preserve_action_colons_and_skip_invalid_lines()
    {
        ObjectId sha = ObjectId.Parse(new string('a', 40));
        string output = $"{sha} HEAD@{{0}}: commit: initial: message{Environment.NewLine}invalid line";

        IReadOnlyList<ReflogEntry> entries = ReflogWorkflow.Parse(output);

        entries.Should().ContainSingle();
        entries[0].Sha.Should().Be(sha);
        entries[0].Reference.Should().Be("HEAD@{0}");
        entries[0].Action.Should().Be("commit: initial: message");
    }

    [Fact]
    public void ReflogWorkflow_Read_should_return_entries_from_a_real_repository()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        File.WriteAllText(Path.Combine(_workingDirectory, "tracked.txt"), "content");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "tracked.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });
        ObjectId head = module.GetCurrentCheckout();

        IReadOnlyList<ReflogEntry> entries = ReflogWorkflow.Read(module, "HEAD");

        entries.Select(entry => entry.Sha).Should().Contain(head);
        entries.Any(entry => entry.Reference == "HEAD@{0}" && entry.Action.Contains("initial", StringComparison.Ordinal))
            .Should().BeTrue();
    }

    [Fact]
    public void PatchWorkflow_should_create_a_patch_for_a_selected_revision()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "patch-target.txt";
        string filePath = Path.Combine(_workingDirectory, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });
        ObjectId parent = module.GetCurrentCheckout();

        File.WriteAllText(filePath, "patched\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "patch-source" });
        ObjectId revision = module.GetCurrentCheckout();
        string outputDirectory = Path.Combine(_workingDirectory, "patch output");
        Directory.CreateDirectory(outputDirectory);

        string output = module.GitExecutable.GetOutput(PatchWorkflow.BuildFormatPatch(
            module,
            parent.ToString(),
            revision.ToString(),
            outputDirectory));

        string[] patchFiles = Directory.GetFiles(outputDirectory, "*.patch");
        patchFiles.Should().ContainSingle();
        output.Should().Contain(Path.GetFileName(patchFiles[0]));
        File.ReadAllText(patchFiles[0]).Should().Contain("Subject: [PATCH] patch-source");
        module.GetCurrentCheckout().Should().Be(revision);

        string rootOutputDirectory = Path.Combine(_workingDirectory, "root patch output");
        Directory.CreateDirectory(rootOutputDirectory);
        string rootOutput = module.GitExecutable.GetOutput(PatchWorkflow.BuildFormatPatch(
            module,
            string.Empty,
            parent.ToString(),
            rootOutputDirectory));

        string[] rootPatchFiles = Directory.GetFiles(rootOutputDirectory, "*.patch");
        rootPatchFiles.Should().ContainSingle();
        rootOutput.Should().Contain(Path.GetFileName(rootPatchFiles[0]));
        File.ReadAllText(rootPatchFiles[0]).Should().Contain("Subject: [PATCH] base");
        module.GetCurrentCheckout().Should().Be(revision);

        ObjectId[] selectedRevisions = [];
        for (int commitNumber = 1; commitNumber <= 3; commitNumber++)
        {
            File.AppendAllText(filePath, $"change-{commitNumber}\n");
            module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
            module.GitExecutable.RunCommand(new GitArgumentBuilder("commit")
            {
                "--quiet",
                "-m",
                $"patch-{commitNumber}",
            });
            selectedRevisions = [.. selectedRevisions, module.GetCurrentCheckout()];
        }

        ObjectId[] selectedRevisionsOldestFirst = [.. selectedRevisions];
        GitRevision[] revisions = selectedRevisionsOldestFirst
            .Select((objectId, index) => new GitRevision(objectId)
            {
                ParentIds = [index == 0 ? revision : selectedRevisionsOldestFirst[index - 1]],
            })
            .ToArray();
        string multiPatchOutputDirectory = Path.Combine(_workingDirectory, "multi patch output");
        Directory.CreateDirectory(multiPatchOutputDirectory);

        IReadOnlyList<ArgumentString> multiPatchCommands = PatchWorkflow.BuildFormatPatches(module, revisions, multiPatchOutputDirectory);
        string multiPatchOutput = string.Join(
            Environment.NewLine,
            multiPatchCommands.Select(command => module.GitExecutable.GetOutput(command)));

        string[] multiPatchFiles = Directory.GetFiles(multiPatchOutputDirectory, "*.patch")
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        multiPatchFiles.Should().HaveCount(3);
        multiPatchOutput.Should().Contain(Path.GetFileName(multiPatchFiles[0]));
        Path.GetFileName(multiPatchFiles[0]).Should().StartWith("0001-");
        Path.GetFileName(multiPatchFiles[1]).Should().StartWith("0002-");
        Path.GetFileName(multiPatchFiles[2]).Should().StartWith("0003-");
        string[] patchContents = multiPatchFiles.Select(File.ReadAllText).ToArray();
        patchContents.Should().Contain(file => file.Contains("Subject: [PATCH] patch-1", StringComparison.Ordinal));
        patchContents.Should().Contain(file => file.Contains("Subject: [PATCH] patch-2", StringComparison.Ordinal));
        patchContents.Should().Contain(file => file.Contains("Subject: [PATCH] patch-3", StringComparison.Ordinal));
        module.GetCurrentCheckout().Should().Be(selectedRevisions[^1]);
    }

    [AvaloniaFact]
    public void FormBrowse_should_display_and_run_enabled_user_menu_scripts()
    {
        ScriptInfo expectedScript = new()
        {
            Enabled = true,
            Name = "&Run helper",
            Command = "helper",
            OnEvent = ScriptEvent.ShowInUserMenuBar,
        };
        BindingList<ScriptInfo> scripts =
        [
            expectedScript,
            new()
            {
                Enabled = false,
                Name = "Disabled",
                Command = "disabled",
                OnEvent = ScriptEvent.ShowInUserMenuBar,
            },
            new()
            {
                Enabled = true,
                Name = "Lifecycle",
                Command = "lifecycle",
                OnEvent = ScriptEvent.AfterPull,
            },
        ];
        IScriptsManager scriptsManager = Substitute.For<IScriptsManager>();
        scriptsManager.GetScripts().Returns(scripts);
        _serviceContainer.RemoveService(typeof(IScriptsManager));
        _serviceContainer.AddService<IScriptsManager>(scriptsManager);

        IScriptsRunner scriptsRunner = Substitute.For<IScriptsRunner>();
        _serviceContainer.RemoveService(typeof(IScriptsRunner));
        _serviceContainer.AddService<IScriptsRunner>(scriptsRunner);

        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        FormBrowse form = new(new GitUICommands(_serviceContainer, module));
        try
        {
            MenuItem undoLastCommit = form.FindControl<MenuItem>("undoLastCommitMenuItem")
                ?? throw new InvalidOperationException("Undo-last-commit menu item was not created.");
            undoLastCommit.Header.Should().Be("_Undo last commit...");

            MenuItem scriptsMenu = form.FindControl<MenuItem>("userScriptsMenuItem")
                ?? throw new InvalidOperationException("User scripts menu was not created.");
            scriptsMenu.Items.Should().HaveCount(1);
            MenuItem scriptMenuItem = (MenuItem)scriptsMenu.Items[0]!;
            scriptMenuItem.Header.Should().Be("Run helper");

            scriptMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            scriptsRunner.Received(1).RunScript(
                expectedScript,
                form,
                Arg.Any<IGitUICommands>(),
                Arg.Any<IScriptOptionsProvider>());
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public void FormBrowse_force_push_should_default_to_no_and_not_run_when_cancelled()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet", "-b", "main" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("remote") { "add", "origin", "https://example.test/repository.git" });

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());

        IMessageBoxHost originalMessageBoxHost;
        try
        {
            originalMessageBoxHost = ShimHost.MessageBoxHost;
        }
        catch (InvalidOperationException)
        {
            originalMessageBoxHost = Substitute.For<IMessageBoxHost>();
        }

        IMessageBoxHost messageBoxHost = Substitute.For<IMessageBoxHost>();
        messageBoxHost.Show(
            Arg.Any<IWin32Window>(),
            string.Format(GitUI.TranslatedStrings.ConfirmForcePush, "main", "origin"),
            GitUI.TranslatedStrings.ForcePushMenu,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2)
            .Returns(DialogResult.No);
        ShimHost.MessageBoxHost = messageBoxHost;

        FormBrowse form = new(commands);
        try
        {
            MenuItem forcePush = form.FindControl<MenuItem>("forcePushMenuItem")
                ?? throw new InvalidOperationException("The force-push menu item was not created.");

            forcePush.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            messageBoxHost.Received(1).Show(
                form,
                string.Format(GitUI.TranslatedStrings.ConfirmForcePush, "main", "origin"),
                GitUI.TranslatedStrings.ForcePushMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            commands.DidNotReceive().StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>());
        }
        finally
        {
            form.Close();
            ShimHost.MessageBoxHost = originalMessageBoxHost;
        }
    }

    [AvaloniaFact]
    public void FormBrowse_should_expose_database_maintenance_commands()
    {
        GitModule module = CreateRepositoryWithRemote();
        IGitUICommands commands = CreateCommands(module, startSucceeds: true);
        FormBrowse form = new(commands);
        try
        {
            MenuItem maintenanceMenu = form.FindControl<MenuItem>("gitMaintenanceToolStripMenuItem")
                ?? throw new InvalidOperationException("The Git maintenance menu was not created.");
            MenuItem compressDatabase = form.FindControl<MenuItem>("compressGitDatabaseToolStripMenuItem")
                ?? throw new InvalidOperationException("The database compression menu item was not created.");
            MenuItem verifyRepository = form.FindControl<MenuItem>("verifyRepositoryMenuItem")
                ?? throw new InvalidOperationException("The repository verification menu item was not created.");
            MenuItem deleteIndexLock = form.FindControl<MenuItem>("deleteIndexLockToolStripMenuItem")
                ?? throw new InvalidOperationException("The index-lock menu item was not created.");

            maintenanceMenu.Header.Should().Be(GitUI.TranslatedStrings.GitMaintenanceMenu);
            compressDatabase.Header.Should().Be(GitUI.TranslatedStrings.CompressGitDatabaseMenu);
            verifyRepository.Header.Should().Be(GitUI.TranslatedStrings.VerifyRepositoryMenu);
            deleteIndexLock.Header.Should().Be(GitUI.TranslatedStrings.DeleteIndexLockMenu);

            compressDatabase.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            commands.Received(1).StartCommandLineProcessDialog(
                form,
                Arg.Is<IGitCommand>(command =>
                    command.Arguments == "gc"
                    && !command.AccessesRemote
                    && command.ChangesRepoState));

            string indexLockPath = module.ResolveGitInternalPath("index.lock");
            File.WriteAllText(indexLockPath, string.Empty);
            deleteIndexLock.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            File.Exists(indexLockPath).Should().BeFalse();
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public void FormBrowse_undo_last_commit_should_run_soft_reset_and_notify()
    {
        bool originalDontConfirm = AppSettings.DontConfirmUndoLastCommit.Value;
        IMessageBoxHost originalMessageBoxHost;
        try
        {
            originalMessageBoxHost = ShimHost.MessageBoxHost;
        }
        catch (InvalidOperationException)
        {
            originalMessageBoxHost = Substitute.For<IMessageBoxHost>();
        }

        try
        {
            AppSettings.DontConfirmUndoLastCommit.Value = false;
            IMessageBoxHost messageBoxHost = Substitute.For<IMessageBoxHost>();
            messageBoxHost.Show(
                Arg.Any<IWin32Window>(),
                GitUI.TranslatedStrings.UndoLastCommitConfirmation,
                GitUI.TranslatedStrings.UndoLastCommitCaption,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)
                .Returns(DialogResult.Yes);
            ShimHost.MessageBoxHost = messageBoxHost;

            IGitModule module = Substitute.For<IGitModule>();
            module.WorkingDir.Returns(_workingDirectory);
            module.IsValidGitWorkingDir().Returns(false);
            ILockableNotifier notifier = Substitute.For<ILockableNotifier>();
            IAppTitleGenerator appTitleGenerator = Substitute.For<IAppTitleGenerator>();
            appTitleGenerator.Generate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>()).Returns("Git Extensions");
            AvaloniaHotkeySettingsManager hotkeySettingsManager = new();
            IScriptsManager scriptsManager = Substitute.For<IScriptsManager>();
            scriptsManager.GetScripts().Returns([]);

            IGitUICommands commands = Substitute.For<IGitUICommands>();
            commands.Module.Returns(module);
            commands.RepoChangedNotifier.Returns(notifier);
            commands.GetService(typeof(IAppTitleGenerator)).Returns(appTitleGenerator);
            commands.GetService(typeof(IHotkeySettingsLoader)).Returns(hotkeySettingsManager);
            commands.GetService(typeof(IScriptsManager)).Returns(scriptsManager);
            commands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>()).Returns(true);

            FormBrowse form = new(commands);
            try
            {
                MenuItem undoLastCommit = form.FindControl<MenuItem>("undoLastCommitMenuItem")
                    ?? throw new InvalidOperationException("Undo-last-commit menu item was not created.");
                undoLastCommit.IsEnabled = true;
                undoLastCommit.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

                commands.Received(1).StartGitCommandProcessDialog(
                    form,
                    Arg.Is<ArgumentString>(arguments => arguments.ToString() == ResetWorkflow.BuildUndoLastCommit().ToString()));
                notifier.Received(1).Notify();
                messageBoxHost.Received(1).Show(
                    form,
                    GitUI.TranslatedStrings.UndoLastCommitConfirmation,
                    GitUI.TranslatedStrings.UndoLastCommitCaption,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
            }
            finally
            {
                form.Close();
            }
        }
        finally
        {
            AppSettings.DontConfirmUndoLastCommit.Value = originalDontConfirm;
            ShimHost.MessageBoxHost = originalMessageBoxHost;
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_reload_after_repository_changed_notification()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        File.WriteAllText(Path.Combine(_workingDirectory, "tracked.txt"), "content");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "tracked.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });

        GitUICommands commands = new(_serviceContainer, module);
        FormBrowse form = new(commands);
        try
        {
            form.Show();
            RevisionGridControl revisionGrid = form.FindControl<RevisionGridControl>("RevisionGrid")
                ?? throw new InvalidOperationException("Revision grid was not created.");
            TextBlock loadingStatus = revisionGrid.FindControl<TextBlock>("lblLoadingStatus")
                ?? throw new InvalidOperationException("Revision loading status was not created.");

            await WaitUntilAsync(() => loadingStatus.Text == "1 revisions");

            File.AppendAllText(Path.Combine(_workingDirectory, "tracked.txt"), "updated");
            module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "second" });
            ObjectId remoteCommit = module.GetCurrentCheckout();
            module.GitExecutable.RunCommand(new GitArgumentBuilder("reset") { "--quiet", "--hard", "HEAD~" });
            module.GitExecutable.RunCommand(new GitArgumentBuilder("update-ref") { "refs/remotes/origin/main", remoteCommit });

            bool reloadStarted = false;
            loadingStatus.PropertyChanged += (_, e) =>
                reloadStarted |= e.Property == TextBlock.TextProperty && loadingStatus.Text == "Loading…";
            commands.RepoChangedNotifier.Notify();

            await WaitUntilAsync(() => reloadStarted && loadingStatus.Text == "2 revisions");
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_cancel_commit_when_before_commit_script_cancels()
    {
        string originalLastCommitMessage = AppSettings.LastCommitMessage;
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        File.WriteAllText(Path.Combine(_workingDirectory, "tracked.txt"), "initial");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "tracked.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });
        ObjectId initialHead = module.GetCurrentCheckout();
        File.AppendAllText(Path.Combine(_workingDirectory, "tracked.txt"), " staged");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "tracked.txt" });

        IScriptsRunner scriptsRunner = Substitute.For<IScriptsRunner>();
        scriptsRunner.RunEventScripts<FormBrowse>(ScriptEvent.BeforeCommit, Arg.Any<FormBrowse>()).Returns(false);
        _serviceContainer.RemoveService(typeof(IScriptsRunner));
        _serviceContainer.AddService<IScriptsRunner>(scriptsRunner);

        FormBrowse form = new(new GitUICommands(_serviceContainer, module));
        try
        {
            form.Show();
            WorkingTreePanel panel = form.FindControl<WorkingTreePanel>("workingTreePanel")
                ?? throw new InvalidOperationException("The working-tree panel was not created.");
            await WaitUntilAsync(() => panel.StagedCount == 1);

            TextBox commitMessage = panel.FindControl<TextBox>("txtCommitMessage")
                ?? throw new InvalidOperationException("The commit message field was not created.");
            Button commitButton = panel.FindControl<Button>("btnCommit")
                ?? throw new InvalidOperationException("The commit button was not created.");
            commitMessage.Text = "cancelled commit";
            commitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            scriptsRunner.Received(1).RunEventScripts(ScriptEvent.BeforeCommit, form);
            scriptsRunner.DidNotReceive().RunEventScripts(ScriptEvent.AfterCommit, Arg.Any<FormBrowse>());
            AppSettings.LastCommitMessage.Should().Be("cancelled commit");
            module.GetCurrentCheckout().Should().Be(initialHead);
            module.GitExecutable.GetOutput(new GitArgumentBuilder("log") { "-1", "--format=%s" }).Trim().Should().Be("initial");
        }
        finally
        {
            form.Close();
            AppSettings.LastCommitMessage = originalLastCommitMessage;
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_run_after_commit_script_after_successful_commit()
    {
        string originalLastCommitMessage = AppSettings.LastCommitMessage;
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        File.WriteAllText(Path.Combine(_workingDirectory, "tracked.txt"), "initial");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "tracked.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });
        File.AppendAllText(Path.Combine(_workingDirectory, "tracked.txt"), " staged");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "tracked.txt" });

        IScriptsRunner scriptsRunner = Substitute.For<IScriptsRunner>();
        scriptsRunner.RunEventScripts<FormBrowse>(ScriptEvent.BeforeCommit, Arg.Any<FormBrowse>()).Returns(true);
        scriptsRunner.RunEventScripts<FormBrowse>(ScriptEvent.AfterCommit, Arg.Any<FormBrowse>()).Returns(true);
        _serviceContainer.RemoveService(typeof(IScriptsRunner));
        _serviceContainer.AddService<IScriptsRunner>(scriptsRunner);

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartGitCommandProcessDialog(Arg.Any<GitExtensions.Shims.WinForms.IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(true);

        FormBrowse form = new(commands);
        try
        {
            form.Show();
            WorkingTreePanel panel = form.FindControl<WorkingTreePanel>("workingTreePanel")
                ?? throw new InvalidOperationException("The working-tree panel was not created.");
            await WaitUntilAsync(() => panel.StagedCount == 1);

            TextBox commitMessage = panel.FindControl<TextBox>("txtCommitMessage")
                ?? throw new InvalidOperationException("The commit message field was not created.");
            Button commitButton = panel.FindControl<Button>("btnCommit")
                ?? throw new InvalidOperationException("The commit button was not created.");
            commitMessage.Text = "hooked commit";
            commitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Received.InOrder(() =>
            {
                scriptsRunner.RunEventScripts<FormBrowse>(ScriptEvent.BeforeCommit, form);
                commands.StartGitCommandProcessDialog(form, Arg.Any<ArgumentString>());
                scriptsRunner.RunEventScripts<FormBrowse>(ScriptEvent.AfterCommit, form);
            });
        }
        finally
        {
            form.Close();
            AppSettings.LastCommitMessage = originalLastCommitMessage;
        }
    }

    [AvaloniaTheory]
    [InlineData("pullMergeMenuItem", ScriptEvent.BeforePull, ScriptEvent.AfterPull)]
    [InlineData("pushMenuItem", ScriptEvent.BeforePush, ScriptEvent.AfterPush)]
    public void FormBrowse_should_run_remote_lifecycle_scripts_around_the_command(string menuItemName, ScriptEvent beforeEvent, ScriptEvent afterEvent)
    {
        GitModule module = CreateRepositoryWithRemote();
        IScriptsRunner scriptsRunner = Substitute.For<IScriptsRunner>();
        scriptsRunner.RunEventScripts<FormBrowse>(Arg.Any<ScriptEvent>(), Arg.Any<FormBrowse>()).Returns(true);
        _serviceContainer.RemoveService(typeof(IScriptsRunner));
        _serviceContainer.AddService<IScriptsRunner>(scriptsRunner);
        IGitUICommands commands = CreateCommands(module, startSucceeds: true);

        FormBrowse form = new(commands);
        try
        {
            form.Show();
            form.FindControl<MenuItem>(menuItemName)!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Received.InOrder(() =>
            {
                scriptsRunner.RunEventScripts<FormBrowse>(beforeEvent, form);
                commands.StartGitCommandProcessDialog(form, Arg.Any<ArgumentString>());
                scriptsRunner.RunEventScripts<FormBrowse>(afterEvent, form);
            });
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("pullMergeMenuItem", ScriptEvent.BeforePull, ScriptEvent.AfterPull)]
    [InlineData("pushMenuItem", ScriptEvent.BeforePush, ScriptEvent.AfterPush)]
    public void FormBrowse_should_not_run_remote_command_when_before_script_cancels(string menuItemName, ScriptEvent beforeEvent, ScriptEvent afterEvent)
    {
        GitModule module = CreateRepositoryWithRemote();
        IScriptsRunner scriptsRunner = Substitute.For<IScriptsRunner>();
        scriptsRunner.RunEventScripts<FormBrowse>(beforeEvent, Arg.Any<FormBrowse>()).Returns(false);
        _serviceContainer.RemoveService(typeof(IScriptsRunner));
        _serviceContainer.AddService<IScriptsRunner>(scriptsRunner);
        IGitUICommands commands = CreateCommands(module, startSucceeds: true);

        FormBrowse form = new(commands);
        try
        {
            form.Show();
            form.FindControl<MenuItem>(menuItemName)!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            commands.DidNotReceive().StartGitCommandProcessDialog(Arg.Any<GitExtensions.Shims.WinForms.IWin32Window>(), Arg.Any<ArgumentString>());
            scriptsRunner.DidNotReceive().RunEventScripts(afterEvent, Arg.Any<FormBrowse>());
        }
        finally
        {
            form.Close();
        }
    }

    private GitModule CreateRepositoryWithRemote()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        File.WriteAllText(Path.Combine(_workingDirectory, "tracked.txt"), "initial");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "tracked.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("remote") { "add", "origin", "https://example.invalid/repo.git" });

        return module;
    }

    private IGitUICommands CreateCommands(GitModule module, bool startSucceeds)
    {
        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartCommandLineProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<IGitCommand>())
            .Returns(startSucceeds);
        commands.StartGitCommandProcessDialog(Arg.Any<GitExtensions.Shims.WinForms.IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(startSucceeds);

        return commands;
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_show_and_open_a_recent_repository_from_the_dashboard()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "recent repository");
        Directory.CreateDirectory(repositoryPath);
        GitModule repositoryModule = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        repositoryModule.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        repositoryModule.SetSetting("user.name", "Avalonia Test");
        repositoryModule.SetSetting("user.email", "avalonia@example.com");
        repositoryModule.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "--allow-empty", "-m", "initial" });
        IList<Repository> recentRepositories = await RepositoryHistoryManager.Locals.AddAsMostRecentAsync(repositoryPath);
        string recordedPath = recentRepositories[0].Path;

        GitModule emptyModule = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        FormBrowse form = new(new GitUICommands(_serviceContainer, emptyModule));
        try
        {
            form.Show();
            ListBox recentRepositoriesList = form.FindControl<ListBox>("lstRecentRepositories")
                ?? throw new InvalidOperationException("The recent repositories list was not created.");
            Button openSelectedButton = form.FindControl<Button>("btnDashboardOpen")
                ?? throw new InvalidOperationException("The dashboard open button was not created.");
            TextBlock repositoryPathLabel = form.FindControl<TextBlock>("lblRepoPath")
                ?? throw new InvalidOperationException("The repository path label was not created.");
            Border dashboardPanel = form.FindControl<Border>("dashboardPanel")
                ?? throw new InvalidOperationException("The dashboard was not created.");

            await WaitUntilAsync(() =>
                dashboardPanel.IsVisible
                && recentRepositoriesList.Items.OfType<Repository>().Any(repository => repository.Path == recordedPath));

            recentRepositoriesList.SelectedItem = recentRepositoriesList.Items
                .OfType<Repository>()
                .Single(repository => repository.Path == recordedPath);
            openSelectedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await WaitUntilAsync(() =>
                !dashboardPanel.IsVisible
                && repositoryPathLabel.Text?.Contains(repositoryPath, StringComparison.Ordinal) is true);
        }
        finally
        {
            form.Close();
            await RepositoryHistoryManager.Locals.RemoveRecentAsync(recordedPath);
        }
    }

    [AvaloniaFact]
    public void FormBrowse_should_dispatch_a_configured_refresh_hotkey()
    {
        string originalSerializedHotkeys = AppSettings.SerializedHotkeys ?? string.Empty;
        try
        {
            AppSettings.SerializedHotkeys = string.Empty;
            AvaloniaHotkeySettingsManager settingsManager = new();
            HotkeyCommand[] hotkeys = [.. settingsManager.LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName)];
            hotkeys.Single(command => command.CommandCode == (int)AvaloniaBrowseHotkeyCommand.Refresh).KeyData = Keys.F6;
            settingsManager.SaveBrowseHotkeys(hotkeys);

            IGitModule module = Substitute.For<IGitModule>();
            module.WorkingDir.Returns(_workingDirectory);
            module.IsValidGitWorkingDir().Returns(false);
            ILockableNotifier notifier = Substitute.For<ILockableNotifier>();
            IAppTitleGenerator appTitleGenerator = Substitute.For<IAppTitleGenerator>();
            appTitleGenerator.Generate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>()).Returns("Git Extensions");

            IGitUICommands commands = Substitute.For<IGitUICommands>();
            commands.Module.Returns(module);
            commands.RepoChangedNotifier.Returns(notifier);
            commands.GetService(typeof(IAppTitleGenerator)).Returns(appTitleGenerator);
            commands.GetService(typeof(IHotkeySettingsLoader)).Returns(settingsManager);
            IScriptsManager scriptsManager = Substitute.For<IScriptsManager>();
            scriptsManager.GetScripts().Returns([]);
            commands.GetService(typeof(IScriptsManager)).Returns(scriptsManager);

            FormBrowse form = new(commands);
            KeyEventArgs keyEvent = new()
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.F6,
                KeyModifiers = KeyModifiers.None,
            };
            form.RaiseEvent(keyEvent);

            keyEvent.Handled.Should().BeTrue();
            notifier.Received(1).Notify();
        }
        finally
        {
            AppSettings.SerializedHotkeys = originalSerializedHotkeys;
        }
    }

    [AvaloniaFact]
    public async Task PartialStageWorkflow_should_stage_only_selected_changed_lines()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "partial-stage.txt";
        File.WriteAllText(Path.Combine(_workingDirectory, fileName), "first original\nkeep\nsecond original\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });
        File.WriteAllText(Path.Combine(_workingDirectory, fileName), "first updated\nkeep\nsecond updated\n");

        (Patch? diff, string? errorMessage) = await module.GetSingleDiffAsync(
            ObjectId.IndexId,
            ObjectId.WorkTreeId,
            fileName,
            oldFileName: null,
            extraDiffArguments: "",
            module.FilesEncoding,
            cacheResult: false,
            isTracked: true,
            useGitColoring: false,
            GitCommandConfiguration.Default,
            CancellationToken.None);
        errorMessage.Should().BeNull();
        string diffText = diff?.Text ?? throw new InvalidOperationException("The working-tree diff was not created.");
        int selectionStart = diffText.IndexOf("-first original", StringComparison.Ordinal);
        int selectionEnd = diffText.IndexOf("+first updated", StringComparison.Ordinal) + "+first updated".Length;
        selectionStart.Should().BeGreaterThanOrEqualTo(0);
        selectionEnd.Should().BeGreaterThan(selectionStart);
        GitItemStatus item = module.GetAllChangedFilesWithSubmodulesStatus(CancellationToken.None)
            .Single(status => status.Name == fileName);

        PartialStageWorkflow.CanStage(item).Should().BeTrue();
        byte[] patch = PartialStageWorkflow.CreatePatch(
            module,
            item,
            diffText,
            selectionStart,
            selectionEnd - selectionStart)
            ?? throw new InvalidOperationException("The selected changes did not produce a patch.");

        ExecutionResult applyResult = PartialStageWorkflow.ApplyPatch(module, patch);
        applyResult.ExitedSuccessfully.Should().BeTrue(applyResult.AllOutput);

        (Patch? stagedDiff, _) = await module.GetSingleDiffAsync(
            module.GetCurrentCheckout(),
            ObjectId.IndexId,
            fileName,
            oldFileName: null,
            extraDiffArguments: "",
            module.FilesEncoding,
            cacheResult: false,
            isTracked: true,
            useGitColoring: false,
            GitCommandConfiguration.Default,
            CancellationToken.None);
        (Patch? unstagedDiff, _) = await module.GetSingleDiffAsync(
            ObjectId.IndexId,
            ObjectId.WorkTreeId,
            fileName,
            oldFileName: null,
            extraDiffArguments: "",
            module.FilesEncoding,
            cacheResult: false,
            isTracked: true,
            useGitColoring: false,
            GitCommandConfiguration.Default,
            CancellationToken.None);

        stagedDiff?.Text.Should().Contain("\n+first updated");
        stagedDiff?.Text.Should().NotContain("\n+second updated");
        unstagedDiff?.Text.Should().Contain("\n+second updated");
        unstagedDiff?.Text.Should().NotContain("\n+first updated");
    }

    [Fact]
    public void BuildApply_should_apply_raw_diff_to_the_working_tree_without_committing()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "patch-target.txt";
        string filePath = Path.Combine(_workingDirectory, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });
        string baseCommit = module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD" }).Trim();

        File.WriteAllText(filePath, "patched\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "patch-source" });
        string patch = module.GitExecutable.GetOutput(new GitArgumentBuilder("show") { "--format=", "--binary", "HEAD" });
        string patchFile = Path.Combine(_workingDirectory, "change.diff");
        File.WriteAllText(patchFile, patch);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("reset") { "--quiet", "--hard", "HEAD~" });

        ExecutionResult result = module.GitExecutable.Execute(
            PatchWorkflow.BuildApply(module, patchFile),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        File.Delete(patchFile);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        File.ReadAllText(filePath).Should().Be("patched\n");
        module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD" }).Trim().Should().Be(baseCommit);
        module.GitExecutable.GetOutput(new GitArgumentBuilder("diff") { "--name-only" }).Trim().Should().Be(fileName);
        module.GitExecutable.GetOutput(new GitArgumentBuilder("diff") { "--cached", "--name-only" }).Trim().Should().BeEmpty();
    }

    [Fact]
    public void BuildApply_should_apply_mailbox_patch_as_a_commit()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "patch-target.txt";
        string filePath = Path.Combine(_workingDirectory, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });

        File.WriteAllText(filePath, "patched\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "patch-source" });
        string patch = module.GitExecutable.GetOutput(new GitArgumentBuilder("format-patch") { "--stdout", "-1" });
        string patchFile = Path.Combine(_workingDirectory, "change.patch");
        File.WriteAllText(patchFile, patch);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("reset") { "--quiet", "--hard", "HEAD~" });

        ExecutionResult result = module.GitExecutable.Execute(
            PatchWorkflow.BuildApply(module, patchFile),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        File.Delete(patchFile);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        File.ReadAllText(filePath).Should().Be("patched\n");
        module.GitExecutable.GetOutput(new GitArgumentBuilder("log") { "-1", "--format=%s" }).Trim().Should().Be("patch-source");
        module.GitExecutable.GetOutput(new GitArgumentBuilder("status") { "--porcelain=v1" }).Trim().Should().BeEmpty();
    }

    [Fact]
    public void BuildApply_should_sign_off_mailbox_patch_and_report_the_patch_kind()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "patch-target.txt";
        string filePath = Path.Combine(_workingDirectory, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });
        File.WriteAllText(filePath, "patched\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "patch-source" });
        string patchFile = Path.Combine(_workingDirectory, "signoff.patch");
        File.WriteAllText(patchFile, module.GitExecutable.GetOutput(new GitArgumentBuilder("format-patch") { "--stdout", "-1" }));
        module.GitExecutable.RunCommand(new GitArgumentBuilder("reset") { "--quiet", "--hard", "HEAD~" });

        PatchWorkflow.IsDiffPatch(patchFile).Should().BeFalse();
        ExecutionResult result = module.GitExecutable.Execute(
            PatchWorkflow.BuildApply(module, patchFile, new PatchApplyOptions(IgnoreWhitespace: true, SignOff: true)),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        File.Delete(patchFile);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        module.GitExecutable.GetOutput(new GitArgumentBuilder("log") { "-1", "--format=%B" }).Should().Contain("Signed-off-by: Avalonia Test");
    }

    [AvaloniaFact]
    public async Task PartialStageWorkflow_should_unstage_only_selected_changed_lines()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "partial-unstage.txt";
        string filePath = Path.Combine(_workingDirectory, fileName);
        File.WriteAllText(filePath, "first original\nkeep\nsecond original\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });
        File.WriteAllText(filePath, "first updated\nkeep\nsecond updated\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });

        (Patch? diff, string? errorMessage) = await module.GetSingleDiffAsync(
            module.GetCurrentCheckout(),
            ObjectId.IndexId,
            fileName,
            oldFileName: null,
            extraDiffArguments: "",
            module.FilesEncoding,
            cacheResult: false,
            isTracked: true,
            useGitColoring: false,
            GitCommandConfiguration.Default,
            CancellationToken.None);
        errorMessage.Should().BeNull();
        string diffText = diff?.Text ?? throw new InvalidOperationException("The staged diff was not created.");
        int selectionStart = diffText.IndexOf("-first original", StringComparison.Ordinal);
        int selectionEnd = diffText.IndexOf("+first updated", StringComparison.Ordinal) + "+first updated".Length;
        GitItemStatus item = module.GetAllChangedFilesWithSubmodulesStatus(CancellationToken.None)
            .Single(status => status.Name == fileName && status.Staged == StagedStatus.Index);

        PartialStageWorkflow.CanUnstage(item).Should().BeTrue();
        PartialStageWorkflow.CanStage(item).Should().BeFalse();
        byte[] patch = PartialStageWorkflow.CreatePatch(module, item, diffText, selectionStart, selectionEnd - selectionStart)
            ?? throw new InvalidOperationException("The selected changes did not produce a patch.");
        ExecutionResult result = PartialStageWorkflow.ApplyPatch(module, patch, unstage: true);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        string staged = module.GitExecutable.GetOutput(new GitArgumentBuilder("diff") { "--cached" });
        staged.Should().Contain("+second updated").And.NotContain("+first updated");
        File.ReadAllText(filePath).Should().Be("first updated\nkeep\nsecond updated\n");
        module.GitExecutable.GetOutput(new GitArgumentBuilder("diff")).Should().Contain("+first updated");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialStageWorkflow_should_discard_only_selected_changed_lines(bool staged)
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "partial-discard.txt";
        string filePath = Path.Combine(_workingDirectory, fileName);
        File.WriteAllText(filePath, "first original\nkeep\nsecond original\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });
        File.WriteAllText(filePath, "first updated\nkeep\nsecond updated\n");
        if (staged)
        {
            module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        }

        (Patch? diff, string? errorMessage) = await module.GetSingleDiffAsync(
            staged ? module.GetCurrentCheckout() : ObjectId.IndexId,
            staged ? ObjectId.IndexId : ObjectId.WorkTreeId,
            fileName,
            oldFileName: null,
            extraDiffArguments: "",
            module.FilesEncoding,
            cacheResult: false,
            isTracked: true,
            useGitColoring: false,
            GitCommandConfiguration.Default,
            CancellationToken.None);
        errorMessage.Should().BeNull();
        string diffText = diff?.Text ?? throw new InvalidOperationException("The diff was not created.");
        int selectionStart = diffText.IndexOf("-first original", StringComparison.Ordinal);
        int selectionEnd = diffText.IndexOf("+first updated", StringComparison.Ordinal) + "+first updated".Length;
        GitItemStatus item = module.GetAllChangedFilesWithSubmodulesStatus(CancellationToken.None)
            .Single(status => status.Name == fileName && status.Staged == (staged ? StagedStatus.Index : StagedStatus.WorkTree));

        byte[] patch = PartialStageWorkflow.CreateDiscardPatch(module, item, diffText, selectionStart, selectionEnd - selectionStart)
            ?? throw new InvalidOperationException("The selected changes did not produce a patch.");
        ExecutionResult result = PartialStageWorkflow.ApplyDiscardPatch(module, patch, staged);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        File.ReadAllText(filePath).Should().Be("first original\nkeep\nsecond updated\n");
    }

    [AvaloniaFact]
    public void PartialStageWorkflow_should_reject_unsupported_working_tree_items()
    {
        GitItemStatus[] unsupportedItems =
        [
            new("new.txt") { Staged = StagedStatus.WorkTree, IsNew = true },
            new("deleted.txt") { Staged = StagedStatus.WorkTree, IsTracked = true, IsChanged = true, IsDeleted = true },
            new("copied.txt") { Staged = StagedStatus.WorkTree, IsTracked = true, IsChanged = true, IsCopied = true },
            new("conflict.txt") { Staged = StagedStatus.WorkTree, IsTracked = true, IsChanged = true, IsUnmerged = true },
            new("submodule") { Staged = StagedStatus.WorkTree, IsTracked = true, IsChanged = true, IsSubmodule = true },
            new("staged.txt") { Staged = StagedStatus.Index, IsTracked = true, IsChanged = true },
        ];

        unsupportedItems.Should().OnlyContain(item => !PartialStageWorkflow.CanStage(item));
    }

    [AvaloniaFact]
    public async Task RevisionGridControl_should_filter_by_message_author_committer_and_clear()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Search Author");
        module.SetSetting("user.email", "search@example.com");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "--allow-empty", "-m", "needle-result" });
        module.SetSetting("user.name", "Other Author");
        module.SetSetting("user.email", "other@example.com");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "--allow-empty", "-m", "ordinary-result" });

        FormBrowse form = new(new GitUICommands(_serviceContainer, module));
        try
        {
            form.Show();
            RevisionGridControl revisionGrid = form.FindControl<RevisionGridControl>("RevisionGrid")
                ?? throw new InvalidOperationException("Revision grid was not created.");
            TextBox searchBox = revisionGrid.FindControl<TextBox>("txtRevisionFilter")
                ?? throw new InvalidOperationException("Revision search box was not created.");
            ComboBox fieldPicker = revisionGrid.FindControl<ComboBox>("cbxRevisionFilterField")
                ?? throw new InvalidOperationException("Revision search field picker was not created.");
            Button applyButton = revisionGrid.FindControl<Button>("btnApplyRevisionFilter")
                ?? throw new InvalidOperationException("Revision filter button was not created.");
            Button clearButton = revisionGrid.FindControl<Button>("btnClearRevisionFilter")
                ?? throw new InvalidOperationException("Clear revision filter button was not created.");
            TextBlock loadingStatus = revisionGrid.FindControl<TextBlock>("lblLoadingStatus")
                ?? throw new InvalidOperationException("Revision loading status was not created.");

            await WaitUntilAsync(() => loadingStatus.Text == "2 revisions");

            ListBox revisionList = revisionGrid.FindControl<ListBox>("lstRevisions")
                ?? throw new InvalidOperationException("Revision list was not created.");
            revisionList.SelectedIndex = 0;
            revisionList.SelectedItems?.Add(revisionList.Items.OfType<GitRevision>().ElementAt(1));
            form.GetSelectedRevisions().Select(revision => revision.Subject)
                .Should().Equal("ordinary-result", "needle-result");

            searchBox.Text = "needle";
            applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => loadingStatus.Text == "1 revisions");
            revisionGrid.SelectedRevision?.Subject.Should().Be("needle-result");

            fieldPicker.SelectedItem = fieldPicker.Items
                .OfType<ComboBoxItem>()
                .Single(item => item.Content?.ToString() == GitUI.TranslatedStrings.RevisionFilterAuthor);
            searchBox.Text = "Search Author";
            applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => loadingStatus.Text == "1 revisions");
            revisionGrid.SelectedRevision?.Subject.Should().Be("needle-result");

            fieldPicker.SelectedItem = fieldPicker.Items
                .OfType<ComboBoxItem>()
                .Single(item => item.Content?.ToString() == GitUI.TranslatedStrings.RevisionFilterCommitter);
            searchBox.Text = "Other Author";
            applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => loadingStatus.Text == "1 revisions");
            revisionGrid.SelectedRevision?.Subject.Should().Be("ordinary-result");

            clearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => loadingStatus.Text == "2 revisions");
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FileHistoryWindow_should_follow_renames_and_blame_the_selected_revision()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Original Author");
        module.SetSetting("user.email", "original@example.com");
        File.WriteAllText(Path.Combine(_workingDirectory, "history.txt"), "original line\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "history.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial-file" });

        module.GitExecutable.RunCommand(new GitArgumentBuilder("mv") { "history.txt", "renamed.txt" });
        File.AppendAllText(Path.Combine(_workingDirectory, "renamed.txt"), "new line\n");
        module.SetSetting("user.name", "Recent Author");
        module.SetSetting("user.email", "recent@example.com");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "rename-update" });

        FileHistoryWindow window = new(module, "renamed.txt", module.GetCurrentCheckout());
        try
        {
            window.Show();
            Grid content = window.Content as Grid
                ?? throw new InvalidOperationException("File history content was not created.");
            ListBox history = content.Children[0] as ListBox
                ?? throw new InvalidOperationException("File history list was not created.");
            TextBlock status = content.Children[1] as TextBlock
                ?? throw new InvalidOperationException("File history status was not created.");
            TextBox details = content.Children[2] as TextBox
                ?? throw new InvalidOperationException("File history details view was not created.");
            StackPanel actions = content.Children[3] as StackPanel
                ?? throw new InvalidOperationException("File history actions were not created.");
            Button showBlame = actions.Children[0] as Button
                ?? throw new InvalidOperationException("File blame button was not created.");
            Button showDiff = actions.Children[1] as Button
                ?? throw new InvalidOperationException("File history diff button was not created.");

            await WaitUntilAsync(() => status.Text == GitUI.TranslatedStrings.FileHistoryLoaded);
            history.Items.Count.Should().Be(2);
            showBlame.IsEnabled.Should().BeTrue();
            showDiff.IsEnabled.Should().BeTrue();

            showDiff.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => status.Text == GitUI.TranslatedStrings.DiffLoaded);
            details.Text.Should().Contain("rename from history.txt");
            details.Text.Should().Contain("rename to renamed.txt");
            details.Text.Should().Contain("+new line");

            showBlame.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => status.Text == GitUI.TranslatedStrings.BlameLoaded);

            string[] blameLines = details.Text.Split(Environment.NewLine);
            blameLines.Should().Contain(line => line.Contains("Original Author", StringComparison.Ordinal) && line.Contains("original line", StringComparison.Ordinal));
            blameLines.Should().Contain(line => line.Contains("Recent Author", StringComparison.Ordinal) && line.Contains("new line", StringComparison.Ordinal));

            history.SelectedIndex = history.Items
                .OfType<GitRevision>()
                .Select((revision, index) => (revision, index))
                .Single(item => item.revision.Subject == "initial-file")
                .index;
            details.Text.Should().BeEmpty();
            status.Text.Should().Be(GitUI.TranslatedStrings.FileHistoryLoaded);

            showDiff.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => status.Text == GitUI.TranslatedStrings.DiffLoaded
                || (status.Text != GitUI.TranslatedStrings.LoadingData && status.Text != GitUI.TranslatedStrings.FileHistoryLoaded));
            status.Text.Should().Be(GitUI.TranslatedStrings.DiffLoaded);
            details.Text.Should().Contain("+original line");
            details.Text.Should().NotContain("+new line");

            showBlame.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => status.Text == GitUI.TranslatedStrings.BlameLoaded
                || (status.Text != GitUI.TranslatedStrings.LoadingData && status.Text != GitUI.TranslatedStrings.FileHistoryLoaded));
            status.Text.Should().Be(GitUI.TranslatedStrings.BlameLoaded);
            details.Text.Split(Environment.NewLine).Should().ContainSingle(line => line.Contains("Original Author", StringComparison.Ordinal) && line.Contains("original line", StringComparison.Ordinal));

            int currentRevisionIndex = history.Items
                .OfType<GitRevision>()
                .Select((revision, index) => (revision, index))
                .Single(item => item.revision.Subject == "rename-update")
                .index;
            showDiff.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            status.Text.Should().Be(GitUI.TranslatedStrings.LoadingData);
            history.SelectedIndex = currentRevisionIndex;
            await WaitUntilAsync(() => status.Text == GitUI.TranslatedStrings.FileHistoryLoaded);
            details.Text.Should().BeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_detach_repository_notifications_when_closed()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns(_workingDirectory);
        module.IsValidGitWorkingDir().Returns(false);
        GitUICommands commands = new(_serviceContainer, module);
        FormBrowse form = new(commands);

        form.Show();
        form.Close();
        commands.RepoChangedNotifier.Notify();

        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();
        module.Received(1).IsValidGitWorkingDir();
    }

    [Fact]
    public void RemoteConfiguration_should_add_rename_toggle_and_remove_remotes_in_a_real_repository()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        ConfigFileRemoteSettingsManager manager = new(() => module);

        ConfigFileRemoteSaveResult createResult = manager.SaveRemote(
            remote: null,
            remoteName: "origin",
            remoteUrl: "https://example.test/repository.git",
            remotePushUrl: null,
            remotePuttySshKey: string.Empty,
            remoteColor: null,
            remotePrefix: null);
        createResult.UserMessage.Should().BeNullOrEmpty();
        createResult.ShouldUpdateRemote.Should().BeTrue();

        ConfigFileRemote existing = manager.LoadRemotes(loadDisabled: true).Single();
        ConfigFileRemoteSaveResult renameResult = manager.SaveRemote(
            existing,
            remoteName: "upstream",
            remoteUrl: "ssh://example.test/repository.git",
            remotePushUrl: null,
            remotePuttySshKey: existing.PuttySshKey ?? string.Empty,
            remoteColor: existing.Color,
            remotePrefix: existing.Prefix);
        renameResult.UserMessage.Should().BeNullOrEmpty();
        renameResult.ShouldUpdateRemote.Should().BeTrue();

        manager.ToggleRemoteState("upstream", disabled: true);
        manager.LoadRemotes(loadDisabled: true).Single().Disabled.Should().BeTrue();

        manager.ToggleRemoteState("upstream", disabled: false);
        ConfigFileRemote activeRemote = manager.LoadRemotes(loadDisabled: true).Single();
        manager.RemoveRemote(activeRemote).Should().BeEmpty();
        module.GetRemoteNames().Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_offer_conflict_resolution_after_stash_apply_conflicts()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        File.WriteAllText(Path.Combine(_workingDirectory, "conflict.txt"), "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "conflict.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });

        File.WriteAllText(Path.Combine(_workingDirectory, "conflict.txt"), "stashed change\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("stash") { "push", "--quiet", "-m", "conflict" });
        File.WriteAllText(Path.Combine(_workingDirectory, "conflict.txt"), "committed change\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "head" });

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(call => module.GitExecutable.RunCommand((ArgumentString)call[1], throwOnErrorExit: false));

        FormBrowse form = new(commands);
        IMessageBoxHost originalMessageBoxHost;
        try
        {
            originalMessageBoxHost = ShimHost.MessageBoxHost;
        }
        catch (InvalidOperationException)
        {
            originalMessageBoxHost = Substitute.For<IMessageBoxHost>();
        }

        IMessageBoxHost messageBoxHost = Substitute.For<IMessageBoxHost>();
        messageBoxHost.Show(
            Arg.Any<IWin32Window>(),
            GitUI.TranslatedStrings.OfferResolveStashConflicts,
            GitUI.TranslatedStrings.ResolveMergeConflicts,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1)
            .Returns(DialogResult.Yes);
        ShimHost.MessageBoxHost = messageBoxHost;

        try
        {
            form.Show();
            RepoObjectsTree repoObjectsTree = form.FindControl<RepoObjectsTree>("repoObjectsTree")
                ?? throw new InvalidOperationException("The repository objects tree was not created.");
            TreeView tree = repoObjectsTree.FindControl<TreeView>("treeMain")
                ?? throw new InvalidOperationException("The repository tree was not created.");
            await WaitUntilAsync(() => tree.ItemsSource is IEnumerable<TreeViewItem> roots
                && roots.Last().ItemsSource is IEnumerable<TreeViewItem> stashItems
                && stashItems.Any());

            TreeViewItem stashesNode = ((IEnumerable<TreeViewItem>)tree.ItemsSource!).Last();
            TreeViewItem stashItem = ((IEnumerable<TreeViewItem>)stashesNode.ItemsSource!).Single();
            tree.SelectedItem = stashItem;
            MenuItem applyStash = ((IEnumerable<MenuItem>)repoObjectsTree.ContextMenu!.ItemsSource!)
                .Single(item => Equals(item.Header, "_Apply stash"));
            applyStash.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            module.InTheMiddleOfConflictedMerge().Should().BeTrue();
            IReadOnlyList<ConflictData> conflicts = await module.GetConflictsAsync();
            conflicts.Select(conflict => conflict.Filename).Should().ContainSingle().Which.Should().Be("conflict.txt");
            messageBoxHost.Received(1).Show(
                form,
                GitUI.TranslatedStrings.OfferResolveStashConflicts,
                GitUI.TranslatedStrings.ResolveMergeConflicts,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1);
            commands.Received(1).StartResolveConflictsDialog(form, offerCommit: false);
        }
        finally
        {
            form.Close();
            ShimHost.MessageBoxHost = originalMessageBoxHost;
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_offer_patch_recovery_after_mailbox_conflict()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "repo");
        Directory.CreateDirectory(repositoryPath);
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "conflict.txt";
        string filePath = Path.Combine(repositoryPath, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });

        File.WriteAllText(filePath, "patch change\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "patch-change" });
        string patch = module.GitExecutable.GetOutput(new GitArgumentBuilder("format-patch") { "--stdout", "-1" });
        string patchFile = Path.Combine(_workingDirectory, "conflict.patch");
        File.WriteAllText(patchFile, patch);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("reset") { "--quiet", "--hard", "HEAD~" });

        File.WriteAllText(filePath, "local change\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "local-change" });
        string localCommit = module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD" }).Trim();
        ExecutionResult applyResult = module.GitExecutable.Execute(
            PatchWorkflow.BuildApply(module, patchFile),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);

        applyResult.ExitedSuccessfully.Should().BeFalse();
        module.InTheMiddleOfPatch().Should().BeTrue();
        module.InTheMiddleOfConflictedMerge().Should().BeTrue();

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(call => module.GitExecutable.RunCommand((ArgumentString)call[1], throwOnErrorExit: false));

        FormBrowse form = new(commands);
        try
        {
            form.Show();
            Border operationStatusPanel = form.FindControl<Border>("operationStatusPanel")
                ?? throw new InvalidOperationException("Operation status was not created.");
            TextBlock operationStatus = form.FindControl<TextBlock>("lblOperationStatus")
                ?? throw new InvalidOperationException("Operation status text was not created.");
            Button abortPatchButton = form.FindControl<Button>("btnAbortPatch")
                ?? throw new InvalidOperationException("Patch recovery action was not created.");
            await WaitUntilAsync(() => operationStatusPanel.IsVisible && abortPatchButton.IsVisible);

            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.PatchInProgress);
            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.UnresolvedConflicts);
            abortPatchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            commands.Received(1).StartGitCommandProcessDialog(form, Commands.Abort());
            module.InTheMiddleOfPatch().Should().BeFalse();
            module.InTheMiddleOfConflictedMerge().Should().BeFalse();
            module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD" }).Trim().Should().Be(localCommit);
            File.ReadAllText(filePath).Should().Be("local change\n");
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_continue_mailbox_after_staging_conflict_resolution()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "repo");
        Directory.CreateDirectory(repositoryPath);
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "conflict.txt";
        string filePath = Path.Combine(repositoryPath, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });

        File.WriteAllText(filePath, "patch change\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "patch-change" });
        string patch = module.GitExecutable.GetOutput(new GitArgumentBuilder("format-patch") { "--stdout", "-1" });
        string patchFile = Path.Combine(_workingDirectory, "conflict.patch");
        File.WriteAllText(patchFile, patch);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("reset") { "--quiet", "--hard", "HEAD~" });

        File.WriteAllText(filePath, "local change\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "local-change" });
        string localCommit = module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD" }).Trim();
        ExecutionResult applyResult = module.GitExecutable.Execute(
            PatchWorkflow.BuildApply(module, patchFile),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        applyResult.ExitedSuccessfully.Should().BeFalse();

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(call => module.GitExecutable.RunCommand((ArgumentString)call[1], throwOnErrorExit: false));

        FormBrowse form = new(commands);
        try
        {
            form.Show();
            Button resolveConflictsButton = form.FindControl<Button>("btnResolveConflicts")
                ?? throw new InvalidOperationException("Conflict resolution action was not created.");
            Button continuePatchButton = form.FindControl<Button>("btnContinuePatch")
                ?? throw new InvalidOperationException("Patch continue action was not created.");
            await WaitUntilAsync(() => resolveConflictsButton.IsVisible);
            continuePatchButton.IsVisible.Should().BeFalse();

            File.WriteAllText(filePath, "resolved change\n");
            module.StageFile(fileName);
            commands.PostRepositoryChanged += Raise.Event<EventHandler<GitUIEventArgs>>(
                commands,
                new GitUIEventArgs(null, commands, null));
            await WaitUntilAsync(() => continuePatchButton.IsVisible);

            resolveConflictsButton.IsVisible.Should().BeFalse();
            continuePatchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            commands.Received(1).StartGitCommandProcessDialog(form, Commands.Resolved());
            module.InTheMiddleOfPatch().Should().BeFalse();
            module.InTheMiddleOfConflictedMerge().Should().BeFalse();
            module.GitExecutable.GetOutput(new GitArgumentBuilder("log") { "-1", "--format=%s" }).Trim()
                .Should().Be("patch-change");
            module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD^" }).Trim()
                .Should().Be(localCommit);
            File.ReadAllText(filePath).Should().Be("resolved change\n");
            module.GitExecutable.GetOutput(new GitArgumentBuilder("status") { "--porcelain=v1" }).Trim().Should().BeEmpty();
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_continue_a_conflicted_cherry_pick_after_staging_resolution()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "cherry-pick-repository");
        Directory.CreateDirectory(repositoryPath);
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet", "--initial-branch=main" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        module.SetSetting("core.editor", "true");

        const string fileName = "conflict.txt";
        string filePath = Path.Combine(repositoryPath, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "-b", "topic" });
        File.WriteAllText(filePath, "topic\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "topic-change" });
        ObjectId topicCommit = module.GetCurrentCheckout();
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "main" });
        File.WriteAllText(filePath, "main\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "main-change" });
        ObjectId mainCommit = module.GetCurrentCheckout();

        ExecutionResult cherryPickResult = module.GitExecutable.Execute(
            CommitPickWorkflow.BuildCherryPick(module, new GitRevision(topicCommit)),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        cherryPickResult.ExitedSuccessfully.Should().BeFalse();
        File.Exists(module.ResolveGitInternalPath("CHERRY_PICK_HEAD")).Should().BeTrue();

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(call => module.GitExecutable.RunCommand((ArgumentString)call[1], throwOnErrorExit: false));

        FormBrowse form = new(commands);
        try
        {
            form.Show();
            TextBlock operationStatus = form.FindControl<TextBlock>("lblOperationStatus")
                ?? throw new InvalidOperationException("Operation status text was not created.");
            Button resolveConflictsButton = form.FindControl<Button>("btnResolveConflicts")
                ?? throw new InvalidOperationException("Conflict resolution action was not created.");
            Button continueButton = form.FindControl<Button>("btnContinueCommitPick")
                ?? throw new InvalidOperationException("Commit-pick continue action was not created.");
            Button abortButton = form.FindControl<Button>("btnAbortCommitPick")
                ?? throw new InvalidOperationException("Commit-pick abort action was not created.");
            await WaitUntilAsync(() => resolveConflictsButton.IsVisible && abortButton.IsVisible);

            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.CherryPickInProgress);
            continueButton.IsVisible.Should().BeFalse();

            File.WriteAllText(filePath, "resolved\n");
            module.StageFile(fileName);
            commands.PostRepositoryChanged += Raise.Event<EventHandler<GitUIEventArgs>>(
                commands,
                new GitUIEventArgs(null, commands, null));
            await WaitUntilAsync(() => continueButton.IsVisible);

            continueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            File.Exists(module.ResolveGitInternalPath("CHERRY_PICK_HEAD")).Should().BeFalse();
            module.GetCurrentCheckout().Should().NotBe(topicCommit);
            module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD^" }).Trim()
                .Should().Be(mainCommit.ToString());
            File.ReadAllText(filePath).Should().Be("resolved\n");
            commands.Received(1).StartGitCommandProcessDialog(form, Commands.ContinueCherryPick());
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_abort_a_conflicted_revert_without_changing_head()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "revert-repository");
        Directory.CreateDirectory(repositoryPath);
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string fileName = "conflict.txt";
        string filePath = Path.Combine(repositoryPath, fileName);
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", fileName });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });
        File.WriteAllText(filePath, "target\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "target-change" });
        ObjectId targetCommit = module.GetCurrentCheckout();
        File.WriteAllText(filePath, "newer\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "newer-change" });
        ObjectId currentCommit = module.GetCurrentCheckout();

        ExecutionResult revertResult = module.GitExecutable.Execute(
            CommitPickWorkflow.BuildRevert(module, new GitRevision(targetCommit)),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        revertResult.ExitedSuccessfully.Should().BeFalse();
        File.Exists(module.ResolveGitInternalPath("REVERT_HEAD")).Should().BeTrue();

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(call => module.GitExecutable.RunCommand((ArgumentString)call[1], throwOnErrorExit: false));

        FormBrowse form = new(commands);
        try
        {
            form.Show();
            TextBlock operationStatus = form.FindControl<TextBlock>("lblOperationStatus")
                ?? throw new InvalidOperationException("Operation status text was not created.");
            Button abortButton = form.FindControl<Button>("btnAbortCommitPick")
                ?? throw new InvalidOperationException("Commit-pick abort action was not created.");
            await WaitUntilAsync(() => abortButton.IsVisible);

            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.RevertInProgress);
            abortButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            File.Exists(module.ResolveGitInternalPath("REVERT_HEAD")).Should().BeFalse();
            module.GetCurrentCheckout().Should().Be(currentCommit);
            File.ReadAllText(filePath).Should().Be("newer\n");
            commands.Received(1).StartGitCommandProcessDialog(form, Commands.AbortRevert());
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_continue_a_conflicted_merge_after_staging_resolution()
    {
        (GitModule module, string filePath, ObjectId mainCommit, ObjectId sideCommit) = CreateConflictedMerge("merge-continue");
        File.Exists(module.ResolveGitInternalPath("MERGE_HEAD")).Should().BeTrue();

        IGitUICommands commands = CreateRecoveryCommands(module);
        FormBrowse form = new(commands);
        try
        {
            form.Show();
            TextBlock operationStatus = form.FindControl<TextBlock>("lblOperationStatus")
                ?? throw new InvalidOperationException("Operation status text was not created.");
            Button continueButton = form.FindControl<Button>("btnContinueMerge")
                ?? throw new InvalidOperationException("Merge continue action was not created.");
            Button abortButton = form.FindControl<Button>("btnAbortMerge")
                ?? throw new InvalidOperationException("Merge abort action was not created.");
            await WaitUntilAsync(() => operationStatus.Text?.Contains(GitUI.TranslatedStrings.MergeInProgress, StringComparison.Ordinal) is true
                && abortButton.IsVisible);

            continueButton.IsVisible.Should().BeFalse();
            File.WriteAllText(filePath, "resolved\n");
            module.StageFile(Path.GetFileName(filePath));
            commands.PostRepositoryChanged += Raise.Event<EventHandler<GitUIEventArgs>>(
                commands,
                new GitUIEventArgs(null, commands, null));
            await WaitUntilAsync(() => continueButton.IsVisible);

            continueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            File.Exists(module.ResolveGitInternalPath("MERGE_HEAD")).Should().BeFalse();
            module.GetCurrentCheckout().Should().NotBe(mainCommit);
            string[] parents = module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-list") { "--parents", "-n", "1", "HEAD" })
                .Trim()
                .Split(' ');
            parents.Should().HaveCount(3);
            parents.Should().Contain(mainCommit.ToString());
            parents.Should().Contain(sideCommit.ToString());
            File.ReadAllText(filePath).Should().Be("resolved\n");
            commands.Received(1).StartGitCommandProcessDialog(form, Commands.ContinueMerge());
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_abort_a_conflicted_merge_without_changing_head_or_worktree()
    {
        (GitModule module, string filePath, ObjectId mainCommit, _) = CreateConflictedMerge("merge-abort");
        File.Exists(module.ResolveGitInternalPath("MERGE_HEAD")).Should().BeTrue();

        IGitUICommands commands = CreateRecoveryCommands(module);
        FormBrowse form = new(commands);
        try
        {
            form.Show();
            Button abortButton = form.FindControl<Button>("btnAbortMerge")
                ?? throw new InvalidOperationException("Merge abort action was not created.");
            await WaitUntilAsync(() => abortButton.IsVisible);

            abortButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            File.Exists(module.ResolveGitInternalPath("MERGE_HEAD")).Should().BeFalse();
            module.GetCurrentCheckout().Should().Be(mainCommit);
            File.ReadAllText(filePath).Should().Be("main\n");
            module.GitExecutable.GetOutput(new GitArgumentBuilder("status") { "--porcelain=v1" }).Trim().Should().BeEmpty();
            commands.Received(1).StartGitCommandProcessDialog(form, Commands.AbortMerge());
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_continue_a_conflicted_rebase_after_staging_resolution()
    {
        (GitModule module, string filePath, ObjectId mainCommit, ObjectId topicCommit) = CreateConflictedRebase("rebase-continue");
        module.InTheMiddleOfRebase().Should().BeTrue();

        IGitUICommands commands = CreateRecoveryCommands(module);
        FormBrowse form = new(commands);
        try
        {
            form.Show();
            TextBlock operationStatus = form.FindControl<TextBlock>("lblOperationStatus")
                ?? throw new InvalidOperationException("Operation status text was not created.");
            Button continueButton = form.FindControl<Button>("btnContinueRebase")
                ?? throw new InvalidOperationException("Rebase continue action was not created.");
            Button skipButton = form.FindControl<Button>("btnSkipRebase")
                ?? throw new InvalidOperationException("Rebase skip action was not created.");
            Button abortButton = form.FindControl<Button>("btnAbortRebase")
                ?? throw new InvalidOperationException("Rebase abort action was not created.");
            await WaitUntilAsync(() => operationStatus.Text?.Contains(GitUI.TranslatedStrings.RebaseInProgress, StringComparison.Ordinal) is true
                && skipButton.IsVisible
                && abortButton.IsVisible);

            continueButton.IsVisible.Should().BeFalse();
            File.WriteAllText(filePath, "resolved\n");
            module.StageFile(Path.GetFileName(filePath));
            commands.PostRepositoryChanged += Raise.Event<EventHandler<GitUIEventArgs>>(
                commands,
                new GitUIEventArgs(null, commands, null));
            await WaitUntilAsync(() => continueButton.IsVisible);

            continueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            module.InTheMiddleOfRebase().Should().BeFalse();
            string currentCommit = module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD" }).Trim();
            currentCommit.Should().NotBe(topicCommit.ToString());
            module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD^" }).Trim()
                .Should().Be(mainCommit.ToString());
            File.ReadAllText(filePath).Should().Be("resolved\n");
            commands.Received(1).StartGitCommandProcessDialog(form, Commands.ContinueRebase());
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task FormBrowse_should_skip_a_conflicted_rebase_commit()
    {
        (GitModule module, string filePath, ObjectId mainCommit, _) = CreateConflictedRebase("rebase-skip");
        module.InTheMiddleOfRebase().Should().BeTrue();

        IGitUICommands commands = CreateRecoveryCommands(module);
        FormBrowse form = new(commands);
        try
        {
            form.Show();
            Button skipButton = form.FindControl<Button>("btnSkipRebase")
                ?? throw new InvalidOperationException("Rebase skip action was not created.");
            await WaitUntilAsync(() => skipButton.IsVisible);

            skipButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            module.InTheMiddleOfRebase().Should().BeFalse();
            module.InTheMiddleOfRebase().Should().BeFalse();
            module.GitExecutable.GetOutput(new GitArgumentBuilder("rev-parse") { "HEAD" }).Trim()
                .Should().Be(mainCommit.ToString());
            module.GitExecutable.GetOutput(new GitArgumentBuilder("status") { "--porcelain=v1" }).Trim().Should().BeEmpty();
            File.ReadAllText(filePath).Should().Be("main\n");
            module.GitExecutable.GetOutput(new GitArgumentBuilder("log") { "--format=%s", $"{mainCommit}..HEAD" })
                .Should().BeEmpty();
            commands.Received(1).StartGitCommandProcessDialog(form, Commands.SkipRebase());
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task OpenRepository_should_switch_to_parent_repository_and_show_working_tree_status()
    {
        string repositoryPath = Path.Combine(_workingDirectory, "repo-ü");
        Directory.CreateDirectory(repositoryPath);
        Directory.CreateDirectory(Path.Combine(repositoryPath, "nested", "directory"));

        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        const string unicodeFileName = "tracked ü file.txt";
        File.WriteAllText(Path.Combine(repositoryPath, "tracked.txt"), "base");
        File.WriteAllText(Path.Combine(repositoryPath, "clean.txt"), "base");
        File.WriteAllText(Path.Combine(repositoryPath, unicodeFileName), "base");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "." });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });

        string initialBranch = module.GetCurrentBranchName();
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "-b", "side" });
        File.WriteAllText(Path.Combine(repositoryPath, "tracked.txt"), "side");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "side" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", initialBranch });
        File.WriteAllText(Path.Combine(repositoryPath, "tracked.txt"), "main");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "main" });

        ExecutionResult mergeResult = module.GitExecutable.Execute(
            new GitArgumentBuilder("merge") { "--no-edit", "side" },
            throwOnErrorExit: false);
        mergeResult.ExitedSuccessfully.Should().BeFalse();

        ObjectId headId = module.GetCurrentCheckout();
        File.WriteAllText(Path.Combine(repositoryPath, "staged.txt"), "staged");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "staged.txt" });
        File.WriteAllText(Path.Combine(repositoryPath, "clean.txt"), "working tree change");
        File.WriteAllText(Path.Combine(repositoryPath, unicodeFileName), "unicode working tree change");
        File.WriteAllText(Path.Combine(repositoryPath, "untracked.txt"), "untracked");

        GitModule initialModule = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        GitUICommands commands = new(_serviceContainer, initialModule);
        FormBrowse form = new(commands);
        try
        {
            form.Show();
            form.OpenRepository(Path.Combine(repositoryPath, "nested", "directory")).Should().BeTrue();

            TextBlock status = form.FindControl<TextBlock>("lblStatus")
                ?? throw new InvalidOperationException("Repository status was not created.");
            RevisionGridControl revisionGrid = form.FindControl<RevisionGridControl>("RevisionGrid")
                ?? throw new InvalidOperationException("Revision grid was not created.");
            TextBlock loadingStatus = revisionGrid.FindControl<TextBlock>("lblLoadingStatus")
                ?? throw new InvalidOperationException("Revision loading status was not created.");

            await WaitUntilAsync(() =>
                loadingStatus.Text == "3 revisions"
                && status.Text?.Contains("UU tracked.txt", StringComparison.Ordinal) is true
                && status.Text.Contains("A  staged.txt", StringComparison.Ordinal)
                && status.Text.Contains(" M clean.txt", StringComparison.Ordinal)
                && status.Text.Contains($" M {unicodeFileName}", StringComparison.Ordinal)
                && status.Text.Contains("?? untracked.txt", StringComparison.Ordinal));

            Border operationStatusPanel = form.FindControl<Border>("operationStatusPanel")
                ?? throw new InvalidOperationException("Operation status was not created.");
            TextBlock operationStatus = form.FindControl<TextBlock>("lblOperationStatus")
                ?? throw new InvalidOperationException("Operation status text was not created.");
            Button abortMergeButton = form.FindControl<Button>("btnAbortMerge")
                ?? throw new InvalidOperationException("Merge recovery action was not created.");
            await WaitUntilAsync(() => operationStatusPanel.IsVisible && abortMergeButton.IsVisible);
            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.MergeInProgress);
            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.UnresolvedConflicts);

            form.Module.WorkingDir.TrimEnd(Path.DirectorySeparatorChar).Should().Be(repositoryPath);
            revisionGrid.SelectRevision(headId);
            GitUI.CommitInfo.CommitInfo commitInfo = form.FindControl<GitUI.CommitInfo.CommitInfo>("CommitInfo")
                ?? throw new InvalidOperationException("Commit information was not created.");
            await WaitUntilAsync(() => commitInfo.Revision?.ObjectId == headId);

            FileStatusList fileStatusList = form.FindControl<FileStatusList>("fileStatusList")
                ?? throw new InvalidOperationException("Changed files were not created.");
            GitUI.Editor.FileViewer fileViewer = form.FindControl<GitUI.Editor.FileViewer>("fileViewer")
                ?? throw new InvalidOperationException("The diff viewer was not created.");
            AvaloniaEdit.TextEditor textEditor = fileViewer.FindControl<AvaloniaEdit.TextEditor>("TextEditor")
                ?? throw new InvalidOperationException("The diff text editor was not created.");
            await WaitUntilAsync(() =>
                fileStatusList.SelectedItem?.Name == "tracked.txt"
                && textEditor.Document.Text.Contains("+main", StringComparison.Ordinal));

            TabControl fileListTabs = form.FindControl<TabControl>("fileListTabs")
                ?? throw new InvalidOperationException("File list tabs were not created.");
            TabItem workingTreeTab = form.FindControl<TabItem>("workingTreeTab")
                ?? throw new InvalidOperationException("Working tree tab was not created.");
            fileListTabs.SelectedItem = workingTreeTab;

            WorkingTreePanel workingTreePanel = form.FindControl<WorkingTreePanel>("workingTreePanel")
                ?? throw new InvalidOperationException("Working tree changes were not created.");
            ListBox unstagedList = workingTreePanel.FindControl<ListBox>("lstUnstaged")
                ?? throw new InvalidOperationException("Unstaged files were not created.");
            GitItemStatus untrackedFile = unstagedList.Items
                .OfType<GitItemStatus>()
                .Single(item => item.Name == "untracked.txt");
            unstagedList.SelectedItems!.Add(untrackedFile);
            await WaitUntilAsync(() => textEditor.Document.Text.Contains("+untracked", StringComparison.Ordinal));
            unstagedList.SelectedItems.Clear();

            GitItemStatus unicodeFile = unstagedList.Items
                .OfType<GitItemStatus>()
                .Single(item => item.Name == unicodeFileName);
            unstagedList.SelectedItems!.Add(unicodeFile);
            await WaitUntilAsync(() => textEditor.Document.Text.Contains("+unicode working tree change", StringComparison.Ordinal));
            workingTreePanel.SelectedItems.Select(item => item.Name).Should().Contain(unicodeFileName);
            string oldStatusText = status.Text ?? string.Empty;

            Button stageButton = workingTreePanel.FindControl<Button>("btnStageSelected")
                ?? throw new InvalidOperationException("Stage button was not created.");
            stageButton.IsEnabled.Should().BeTrue();
            workingTreePanel.StageSelectedFiles();
            stageButton.IsEnabled.Should().BeFalse();

            TabControl statusTabs = workingTreePanel.FindControl<TabControl>("statusTabs")
                ?? throw new InvalidOperationException("Working tree status tabs were not created.");
            TabItem stagedTab = workingTreePanel.FindControl<TabItem>("stagedTab")
                ?? throw new InvalidOperationException("Staged files tab was not created.");
            statusTabs.SelectedItem = stagedTab;

            await WaitUntilAsync(() =>
                module.GetAllChangedFiles().Any(item => item.Name == unicodeFileName && item.Staged == StagedStatus.Index)
                || status.Text?.Contains("fatal:", StringComparison.Ordinal) is true);
            module.GetAllChangedFiles()
                .Should()
                .Contain(item => item.Name == unicodeFileName && item.Staged == StagedStatus.Index, $"status was {status.Text}");

            ListBox stagedList = workingTreePanel.FindControl<ListBox>("lstStaged")
                ?? throw new InvalidOperationException("Staged files were not created.");
            await WaitUntilAsync(() =>
                stagedList.Items.OfType<GitItemStatus>().Any(item => item.Name == unicodeFileName)
                || !string.Equals(status.Text, oldStatusText, StringComparison.Ordinal));
            stagedList.Items
                .OfType<GitItemStatus>()
                .Should()
                .Contain(item => item.Name == unicodeFileName, $"working-tree status was {status.Text}");

            GitItemStatus stagedUnicodeFile = stagedList.Items
                .OfType<GitItemStatus>()
                .Single(item => item.Name == unicodeFileName);
            stagedList.SelectedItems!.Add(stagedUnicodeFile);
            workingTreePanel.UnstageSelectedFiles();

            TabItem unstagedTab = workingTreePanel.FindControl<TabItem>("unstagedTab")
                ?? throw new InvalidOperationException("Unstaged files tab was not created.");
            statusTabs.SelectedItem = unstagedTab;
            await WaitUntilAsync(() =>
                unstagedList.Items.OfType<GitItemStatus>().Any(item => item.Name == unicodeFileName)
                && module.GetAllChangedFiles().Any(item => item.Name == "staged.txt" && item.Staged == StagedStatus.Index));
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public async Task OpenRepository_should_show_rebase_recovery_actions_for_an_interrupted_rebase()
    {
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), _workingDirectory);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        string sharedFile = Path.Combine(_workingDirectory, "shared.txt");
        File.WriteAllText(sharedFile, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "shared.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "initial" });

        string mainBranch = module.GetCurrentBranchName();
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "-b", "topic" });
        File.WriteAllText(sharedFile, "topic\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "shared.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "topic" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", mainBranch });
        File.WriteAllText(sharedFile, "main\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "shared.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "main" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "topic" });

        ExecutionResult rebaseResult = module.GitExecutable.Execute(
            new GitArgumentBuilder("rebase") { mainBranch },
            throwOnErrorExit: false);
        rebaseResult.ExitedSuccessfully.Should().BeFalse();
        module.InTheMiddleOfRebase().Should().BeTrue();

        FormBrowse form = new(new GitUICommands(_serviceContainer, module));
        try
        {
            form.Show();

            Border operationStatusPanel = form.FindControl<Border>("operationStatusPanel")
                ?? throw new InvalidOperationException("Operation status was not created.");
            TextBlock operationStatus = form.FindControl<TextBlock>("lblOperationStatus")
                ?? throw new InvalidOperationException("Operation status text was not created.");
            Button continueRebaseButton = form.FindControl<Button>("btnContinueRebase")
                ?? throw new InvalidOperationException("Continue rebase action was not created.");
            Button skipRebaseButton = form.FindControl<Button>("btnSkipRebase")
                ?? throw new InvalidOperationException("Skip rebase action was not created.");
            Button abortRebaseButton = form.FindControl<Button>("btnAbortRebase")
                ?? throw new InvalidOperationException("Abort rebase action was not created.");

            await WaitUntilAsync(() => operationStatusPanel.IsVisible && skipRebaseButton.IsVisible && abortRebaseButton.IsVisible);
            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.RebaseInProgress);
            operationStatus.Text.Should().Contain(GitUI.TranslatedStrings.UnresolvedConflicts);
            continueRebaseButton.IsVisible.Should().BeFalse();
        }
        finally
        {
            form.Close();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        condition().Should().BeTrue("the repository reload should complete before the timeout");
    }

    private (GitModule Module, string FilePath, ObjectId MainCommit, ObjectId SideCommit) CreateConflictedMerge(string name)
    {
        string repositoryPath = Path.Combine(_workingDirectory, name);
        Directory.CreateDirectory(repositoryPath);
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet", "--initial-branch=main" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        module.SetSetting("core.editor", "true");

        string filePath = Path.Combine(repositoryPath, "conflict.txt");
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "conflict.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "-b", "side" });
        File.WriteAllText(filePath, "side\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "side-change" });
        ObjectId sideCommit = module.GetCurrentCheckout();
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "main" });
        File.WriteAllText(filePath, "main\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "main-change" });
        ObjectId mainCommit = module.GetCurrentCheckout();

        ExecutionResult mergeResult = module.GitExecutable.Execute(
            new GitArgumentBuilder("merge") { "--no-edit", "side" },
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        mergeResult.ExitedSuccessfully.Should().BeFalse();

        return (module, filePath, mainCommit, sideCommit);
    }

    private (GitModule Module, string FilePath, ObjectId MainCommit, ObjectId TopicCommit) CreateConflictedRebase(string name)
    {
        string repositoryPath = Path.Combine(_workingDirectory, name);
        Directory.CreateDirectory(repositoryPath);
        GitModule module = new(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), repositoryPath);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("init") { "--quiet", "--initial-branch=main" });
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        module.SetSetting("core.editor", "true");

        string filePath = Path.Combine(repositoryPath, "conflict.txt");
        File.WriteAllText(filePath, "base\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--", "conflict.txt" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", "base" });
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "-b", "topic" });
        File.WriteAllText(filePath, "topic\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "topic-change" });
        ObjectId topicCommit = module.GetCurrentCheckout();
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "main" });
        File.WriteAllText(filePath, "main\n");
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-am", "main-change" });
        ObjectId mainCommit = module.GetCurrentCheckout();
        module.GitExecutable.RunCommand(new GitArgumentBuilder("checkout") { "--quiet", "topic" });

        ExecutionResult rebaseResult = module.GitExecutable.Execute(
            RebaseWorkflow.BuildRebase("main", autoStash: false),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);
        rebaseResult.ExitedSuccessfully.Should().BeFalse();

        return (module, filePath, mainCommit, topicCommit);
    }

    private IGitUICommands CreateRecoveryCommands(GitModule module)
    {
        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.GetService(Arg.Any<Type>()).Returns(call => _serviceContainer.GetService((Type)call[0]));
        commands.RepoChangedNotifier.Returns(Substitute.For<ILockableNotifier>());
        commands.StartGitCommandProcessDialog(Arg.Any<IWin32Window>(), Arg.Any<ArgumentString>())
            .Returns(call => module.GitExecutable.RunCommand((ArgumentString)call[1], throwOnErrorExit: false));
        return commands;
    }
}
