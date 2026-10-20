using System.ComponentModel.Design;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using ResourceManager;

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
}
