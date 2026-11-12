using Avalonia.Controls;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Shims.WinForms;
using GitExtUtils;
using GitUI.CommandsDialogs;
using GitUI.HelperDialogs;
using Image = GitExtensions.Shims.WinForms.Image;

namespace GitUI;

// Twin of GitUI/GitUICommands.cs. Members are implemented as their dialogs get ported;
// everything else throws NotImplementedException naming the member, so a missing port
// surfaces clearly instead of failing silently.

/// <summary>Contains methods to invoke Git Extensions forms, dialogs, etc.</summary>
public sealed class GitUiCommands : IGitUiCommands
{
    private readonly IServiceProvider _serviceProvider;

    public GitUiCommands(IServiceProvider serviceProvider, IGitModule module)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(module);

        _serviceProvider = serviceProvider;
        Module = module;

        RepoChangedNotifier = new ActionNotifier(() => InvokeEvent(null, PostRepositoryChanged));
    }

    private IGitModule Module { get; }

    IGitModule IGitUiCommands.Module
    {
        get => Module;
    }

    ILockableNotifier IGitUiCommands.RepoChangedNotifier { get; }
    IBrowseRepo? IGitUiCommands.BrowseRepo { get; set; }

    object? IServiceProvider.GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType);
    }

    IGitUiCommands IGitUiCommands.WithGitModule(IGitModule module)
    {
        return new GitUiCommands(_serviceProvider, module);
    }

    IGitUiCommands IGitUiCommands.WithWorkingDirectory(string? workingDirectory)
    {
        return new GitUiCommands(_serviceProvider,
            new GitModule(this.GetRequiredService<IGitExecutorProvider>(), workingDirectory));
    }

    bool IGitUiCommands.StartCommandLineProcessDialog(IWin32Window? owner, IGitCommand command)
    {
        // TODO(avalonia-port): use FormRemoteProcess for remote commands once it is ported.
        var success = FormProcess.ShowDialog(owner, this, command.Arguments, Module.WorkingDir, null, true);

        if (success && command.ChangesRepoState)
        {
            RepoChangedNotifier.Notify();
        }

        return success;
    }

    bool IGitUiCommands.StartCommandLineProcessDialog(IWin32Window? owner, string? command, ArgumentString arguments)
    {
        return FormProcess.ShowDialog(owner, this, arguments, Module.WorkingDir, null, true, command);
    }

    bool IGitUiCommands.StartGitCommandProcessDialog(IWin32Window? owner, ArgumentString arguments)
    {
        return FormProcess.ShowDialog(owner, this, arguments, Module.WorkingDir, null, true);
    }

    bool IGitUiCommands.StashApply(IWin32Window? owner, string stashName)
    {
        return RunStashCommand(owner, new GitArgumentBuilder("stash") { "apply", stashName.Quote() });
    }

    bool IGitUiCommands.StashDrop(IWin32Window? owner, string stashName)
    {
        return RunStashCommand(owner, new GitArgumentBuilder("stash") { "drop", stashName.Quote() });
    }

    bool IGitUiCommands.StashPop(IWin32Window? owner, string stashName = "")
    {
        return RunStashCommand(owner, new GitArgumentBuilder("stash") { "pop", stashName.QuoteNe() });
    }

    bool IGitUiCommands.StashSave(IWin32Window? owner, bool includeUntrackedFiles, bool keepIndex = false,
        string message = "", IReadOnlyList<string>? selectedFiles = null)
    {
        return RunStashCommand(owner, Commands.StashSave(includeUntrackedFiles, keepIndex, message, selectedFiles));
    }

    private bool RunStashCommand(IWin32Window? owner, ArgumentString arguments)
    {
        FormProcess.ShowDialog(owner, this, arguments, Module.WorkingDir, null, true);

        // git-stash may have changed commits also if aborted, the grid must be refreshed
        RepoChangedNotifier.Notify();
        return true;
    }

    private void InvokeEvent(IWin32Window? ownerForm, EventHandler<GitUiEventArgs>? gitUiEventHandler)
    {
        try
        {
            gitUiEventHandler?.Invoke(this, new GitUiEventArgs(ownerForm, this));
        }
        catch (Exception ex)
        {
            MessageBoxes.ShowError(ownerForm, $"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
    }

    private static NotImplementedException NotPorted(string member)
    {
        return new NotImplementedException($"{member} is not ported to the Avalonia UI yet.");
    }

    #region Events

    // Raised like WinForms as the corresponding actions get ported.
#pragma warning disable CS0067 // The event is never used
    private event EventHandler<GitUiEventArgs>? PreCheckoutRevision;

    event EventHandler<GitUiEventArgs>? IGitUiCommands.PreCheckoutRevision
    {
        add => PreCheckoutRevision += value;
        remove => PreCheckoutRevision -= value;
    }

    private event EventHandler<GitUiPostActionEventArgs>? PostCheckoutRevision;

    event EventHandler<GitUiPostActionEventArgs>? IGitUiCommands.PostCheckoutRevision
    {
        add => PostCheckoutRevision += value;
        remove => PostCheckoutRevision -= value;
    }

    private event EventHandler<GitUiEventArgs>? PreCheckoutBranch;

    event EventHandler<GitUiEventArgs>? IGitUiCommands.PreCheckoutBranch
    {
        add => PreCheckoutBranch += value;
        remove => PreCheckoutBranch -= value;
    }

    private event EventHandler<GitUiPostActionEventArgs>? PostCheckoutBranch;

    event EventHandler<GitUiPostActionEventArgs>? IGitUiCommands.PostCheckoutBranch
    {
        add => PostCheckoutBranch += value;
        remove => PostCheckoutBranch -= value;
    }

    private event EventHandler<GitUiEventArgs>? PreCommit;

    event EventHandler<GitUiEventArgs>? IGitUiCommands.PreCommit
    {
        add => PreCommit += value;
        remove => PreCommit -= value;
    }

    private event EventHandler<GitUiPostActionEventArgs>? PostCommit;

    event EventHandler<GitUiPostActionEventArgs>? IGitUiCommands.PostCommit
    {
        add => PostCommit += value;
        remove => PostCommit -= value;
    }

    private event EventHandler<GitUiPostActionEventArgs>? PostEditGitIgnore;

    event EventHandler<GitUiPostActionEventArgs>? IGitUiCommands.PostEditGitIgnore
    {
        add => PostEditGitIgnore += value;
        remove => PostEditGitIgnore -= value;
    }

    private event EventHandler<GitUiPostActionEventArgs>? PostSettings;

    event EventHandler<GitUiPostActionEventArgs>? IGitUiCommands.PostSettings
    {
        add => PostSettings += value;
        remove => PostSettings -= value;
    }

    private event EventHandler<GitUiPostActionEventArgs>? PostUpdateSubmodules;

    event EventHandler<GitUiPostActionEventArgs>? IGitUiCommands.PostUpdateSubmodules
    {
        add => PostUpdateSubmodules += value;
        remove => PostUpdateSubmodules -= value;
    }

    private event EventHandler<GitUiEventArgs>? PostBrowseInitialize;

    event EventHandler<GitUiEventArgs>? IGitUiCommands.PostBrowseInitialize
    {
        add => PostBrowseInitialize += value;
        remove => PostBrowseInitialize -= value;
    }

    /// <summary>
    ///     listeners for changes being made to repository
    /// </summary>
    private event EventHandler<GitUiEventArgs>? PostRepositoryChanged;

    /// <summary>
    ///     listeners for changes being made to repository
    /// </summary>
    event EventHandler<GitUiEventArgs>? IGitUiCommands.PostRepositoryChanged
    {
        add => PostRepositoryChanged += value;
        remove => PostRepositoryChanged -= value;
    }

    private event EventHandler<GitUiEventArgs>? PostRegisterPlugin;
    event EventHandler<GitUiEventArgs>? IGitUiCommands.PostRegisterPlugin
    {
        add => PostRegisterPlugin += value;
        remove => PostRegisterPlugin -= value;
    }
#pragma warning restore CS0067

    #endregion

    #region Not ported yet

    void IGitUiCommands.AddCommitTemplate(string key, Func<string> addingText, Image? icon, bool isRegex = false)
    {
        throw NotPorted(nameof(AddCommitTemplate));
    }

    void IGitUiCommands.AddUpstreamRemote(IWin32Window? owner, IRepositoryHostPlugin gitHoster)
    {
        throw NotPorted(nameof(AddUpstreamRemote));
    }

    IGitRemoteCommand IGitUiCommands.CreateRemoteCommand()
    {
        throw NotPorted(nameof(CreateRemoteCommand));
    }

    bool IGitUiCommands.DoActionOnRepo(Func<bool> action)
    {
        throw NotPorted(nameof(DoActionOnRepo));
    }

    void IGitUiCommands.OpenWithDifftool(IWin32Window? owner, IReadOnlyList<GitRevision?> revisions, string fileName,
        string? oldFileName, RevisionDiffKind diffKind, bool isTracked, string? customTool = null)
    {
        throw NotPorted(nameof(OpenWithDifftool));
    }

    void IGitUiCommands.RaisePostBrowseInitialize(IWin32Window? owner)
    {
        InvokeEvent(owner, PostBrowseInitialize);
    }

    void IGitUiCommands.RaisePostRegisterPlugin(IWin32Window? owner)
    {
        InvokeEvent(owner, PostRegisterPlugin);
    }

    void IGitUiCommands.RemoveCommitTemplate(string key)
    {
        throw NotPorted(nameof(RemoveCommitTemplate));
    }

    bool IGitUiCommands.RunCommand(IReadOnlyList<string> args)
    {
        throw NotPorted(nameof(RunCommand));
    }

    void IGitUiCommands.ShowModelessForm(IWin32Window? owner, bool requiresValidWorkingDir,
        EventHandler<GitUiEventArgs>? preEvent, EventHandler<GitUiPostActionEventArgs>? postEvent,
        Func<Form> provideForm)
    {
        throw NotPorted(nameof(ShowModelessForm));
    }

    bool IGitUiCommands.StartAddFilesDialog(IWin32Window? owner, string? addFiles = null)
    {
        throw NotPorted(nameof(StartAddFilesDialog));
    }

    bool IGitUiCommands.StartAddToGitIgnoreDialog(IWin32Window? owner, bool localExclude, params string[] filePattern)
    {
        throw NotPorted(nameof(StartAddToGitIgnoreDialog));
    }

    bool IGitUiCommands.StartAmendCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        throw NotPorted(nameof(StartAmendCommitDialog));
    }

    bool IGitUiCommands.StartApplyPatchDialog(IWin32Window? owner, string? patchFile = null)
    {
        throw NotPorted(nameof(StartApplyPatchDialog));
    }

    bool IGitUiCommands.StartArchiveDialog(IWin32Window? owner = null, GitRevision? revision = null,
        GitRevision? revision2 = null, string? path = null)
    {
        if (revision is null)
        {
            return false;
        }

        ArchiveWindow dialog = new(this, revision, revision2, path);
        if (owner is Window ownerWindow)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () => await dialog.ShowDialog<bool>(ownerWindow));
        }

        if (owner is null)
        {
            dialog.Show();
            return true;
        }

        throw new ArgumentException("The archive dialog owner must be an Avalonia window.", nameof(owner));
    }

    void IGitUiCommands.StartBatchFileProcessDialog(string batchFile)
    {
        throw NotPorted(nameof(StartBatchFileProcessDialog));
    }

    bool IGitUiCommands.StartBrowseDialog(IWin32Window? owner, BrowseArguments? args = null)
    {
        throw NotPorted(nameof(StartBrowseDialog));
    }

    bool IGitUiCommands.StartCheckoutBranch(IWin32Window? owner, IReadOnlyList<ObjectId>? containObjectIds)
    {
        throw NotPorted(nameof(StartCheckoutBranch));
    }

    bool IGitUiCommands.StartCheckoutBranch(IWin32Window? owner, string branch = "", bool remote = false,
        IReadOnlyList<ObjectId>? containObjectIds = null)
    {
        throw NotPorted(nameof(StartCheckoutBranch));
    }

    bool IGitUiCommands.StartCheckoutRemoteBranch(IWin32Window? owner, string branch)
    {
        throw NotPorted(nameof(StartCheckoutRemoteBranch));
    }

    bool IGitUiCommands.StartCheckoutRevisionDialog(IWin32Window? owner, string? revision = null)
    {
        throw NotPorted(nameof(StartCheckoutRevisionDialog));
    }

    bool IGitUiCommands.StartCherryPickDialog(IWin32Window? owner = null, GitRevision? revision = null)
    {
        throw NotPorted(nameof(StartCherryPickDialog));
    }

    bool IGitUiCommands.StartCherryPickDialog(IWin32Window? owner, IEnumerable<GitRevision> revisions)
    {
        throw NotPorted(nameof(StartCherryPickDialog));
    }

    bool IGitUiCommands.StartCleanupRepositoryDialog(IWin32Window? owner = null, string? path = null)
    {
        CleanupRepositoryWindow dialog = new(this, path);
        if (owner is Window ownerWindow)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () => await dialog.ShowDialog<bool>(ownerWindow));
        }

        if (owner is null)
        {
            dialog.Show();
            return true;
        }

        throw new ArgumentException("The cleanup dialog owner must be an Avalonia window.", nameof(owner));
    }

    bool IGitUiCommands.StartCloneDialog(IWin32Window? owner, string url,
        EventHandler<GitModuleEventArgs> gitModuleChanged)
    {
        return StartCloneDialog(owner, url, openedFromProtocolHandler: false, gitModuleChanged);
    }

    bool IGitUiCommands.StartCloneDialog(IWin32Window? owner, string? url = null,
        bool openedFromProtocolHandler = false, EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
    {
        CloneRepositoryWindow dialog = new(
            this,
            url,
            path => gitModuleChanged?.Invoke(
                this,
                new GitModuleEventArgs(new GitModule(this.GetRequiredService<IGitExecutorProvider>(), path))));
        if (owner is Window ownerWindow)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () => await dialog.ShowDialog<bool>(ownerWindow));
        }

        if (owner is null)
        {
            dialog.Show();
            return true;
        }

        throw new ArgumentException("The clone dialog owner must be an Avalonia window.", nameof(owner));
    }

    void IGitUiCommands.StartCloneForkFromHoster(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        EventHandler<GitModuleEventArgs>? gitModuleChanged)
    {
        throw NotPorted(nameof(StartCloneForkFromHoster));
    }

    bool IGitUiCommands.StartCommitDialog(IWin32Window? owner, string? commitMessage = null,
        bool showOnlyWhenChanges = false)
    {
        throw NotPorted(nameof(StartCommitDialog));
    }

    bool IGitUiCommands.StartCompareRevisionsDialog(IWin32Window? owner = null)
    {
        throw NotPorted(nameof(StartCompareRevisionsDialog));
    }

    bool IGitUiCommands.StartCreateBranchDialog(IWin32Window? owner = null, ObjectId objectId = default,
        string? newBranchNamePrefix = null)
    {
        throw NotPorted(nameof(StartCreateBranchDialog));
    }

    bool IGitUiCommands.StartCreateBranchDialog(IWin32Window? owner, string? branch)
    {
        throw NotPorted(nameof(StartCreateBranchDialog));
    }

    void IGitUiCommands.StartCreatePullRequest(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartCreatePullRequest));
    }

    void IGitUiCommands.StartCreatePullRequest(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        string? chooseRemote = null, string? chooseBranch = null)
    {
        throw NotPorted(nameof(StartCreatePullRequest));
    }

    bool IGitUiCommands.StartCreateTagDialog(IWin32Window? owner = null, GitRevision? revision = null)
    {
        throw NotPorted(nameof(StartCreateTagDialog));
    }

    bool IGitUiCommands.StartDeleteBranchDialog(IWin32Window? owner, IEnumerable<string> branches)
    {
        throw NotPorted(nameof(StartDeleteBranchDialog));
    }

    bool IGitUiCommands.StartDeleteBranchDialog(IWin32Window? owner, string branch)
    {
        throw NotPorted(nameof(StartDeleteBranchDialog));
    }

    bool IGitUiCommands.StartDeleteRemoteBranchDialog(IWin32Window? owner, string remoteBranch)
    {
        throw NotPorted(nameof(StartDeleteRemoteBranchDialog));
    }

    bool IGitUiCommands.StartDeleteTagDialog(IWin32Window? owner, string? tag)
    {
        throw NotPorted(nameof(StartDeleteTagDialog));
    }

    bool IGitUiCommands.StartEditGitAttributesDialog(IWin32Window? owner = null)
    {
        throw NotPorted(nameof(StartEditGitAttributesDialog));
    }

    bool IGitUiCommands.StartEditGitIgnoreDialog(IWin32Window? owner, bool localExcludes)
    {
        throw NotPorted(nameof(StartEditGitIgnoreDialog));
    }

    bool IGitUiCommands.StartFileEditorDialog(string? filename, bool showWarning = false, int? lineNumber = null)
    {
        throw NotPorted(nameof(StartFileEditorDialog));
    }

    void IGitUiCommands.StartFileHistoryDialog(IWin32Window? owner, string fileName, GitRevision? revision = null,
        bool filterByRevision = false, bool showBlame = false)
    {
        throw NotPorted(nameof(StartFileHistoryDialog));
    }

    bool IGitUiCommands.StartFixupCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        throw NotPorted(nameof(StartFixupCommitDialog));
    }

    bool IGitUiCommands.StartFormCommitDiff(ObjectId objectId)
    {
        throw NotPorted(nameof(StartFormCommitDiff));
    }

    bool IGitUiCommands.StartFormatPatchDialog(IWin32Window? owner = null)
    {
        throw NotPorted(nameof(StartFormatPatchDialog));
    }

    bool IGitUiCommands.StartGeneralSettingsDialog(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartGeneralSettingsDialog));
    }

    bool IGitUiCommands.StartInitializeDialog(IWin32Window? owner = null, string? dir = null,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
    {
        throw NotPorted(nameof(StartInitializeDialog));
    }

    bool IGitUiCommands.StartInteractiveRebase(IWin32Window? owner, string onto)
    {
        throw NotPorted(nameof(StartInteractiveRebase));
    }

    bool IGitUiCommands.StartMailMapDialog(IWin32Window? owner = null)
    {
        throw NotPorted(nameof(StartMailMapDialog));
    }

    bool IGitUiCommands.StartMergeBranchDialog(IWin32Window? owner, string? branch)
    {
        throw NotPorted(nameof(StartMergeBranchDialog));
    }

    bool IGitUiCommands.StartPluginSettingsDialog(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartPluginSettingsDialog));
    }

    bool IGitUiCommands.StartPullDialog(IWin32Window? owner = null, string? remoteBranch = null, string? remote = null,
        GitPullAction pullAction = GitPullAction.None)
    {
        throw NotPorted(nameof(StartPullDialog));
    }

    bool IGitUiCommands.StartPullDialogAndPullImmediately(IWin32Window? owner = null, string? remoteBranch = null,
        string? remote = null, GitPullAction pullAction = GitPullAction.None)
    {
        throw NotPorted(nameof(StartPullDialogAndPullImmediately));
    }

    bool IGitUiCommands.StartPullDialogAndPullImmediately(out bool pullCompleted, IWin32Window? owner = null,
        string? remoteBranch = null, string? remote = null, GitPullAction pullAction = GitPullAction.None)
    {
        throw NotPorted(nameof(StartPullDialogAndPullImmediately));
    }

    void IGitUiCommands.StartPullRequestsDialog(IWin32Window? owner, IRepositoryHostPlugin gitHoster)
    {
        throw NotPorted(nameof(StartPullRequestsDialog));
    }

    bool IGitUiCommands.StartPushDialog(IWin32Window? owner, bool pushOnShow)
    {
        throw NotPorted(nameof(StartPushDialog));
    }

    bool IGitUiCommands.StartPushDialog(IWin32Window? owner, bool pushOnShow, bool forceWithLease,
        out bool pushCompleted, string? branchName = null)
    {
        throw NotPorted(nameof(StartPushDialog));
    }

    bool IGitUiCommands.StartRebase(IWin32Window? owner, string onto)
    {
        throw NotPorted(nameof(StartRebase));
    }

    bool IGitUiCommands.StartRebaseDialog(IWin32Window? owner, string? from, string? to, string? onto,
        bool interactive = false, bool startRebaseImmediately = true)
    {
        throw NotPorted(nameof(StartRebaseDialog));
    }

    bool IGitUiCommands.StartRebaseDialog(IWin32Window? owner, string? onto)
    {
        throw NotPorted(nameof(StartRebaseDialog));
    }

    bool IGitUiCommands.StartRebaseDialogWithAdvOptions(IWin32Window? owner, string onto, string from = "")
    {
        throw NotPorted(nameof(StartRebaseDialogWithAdvOptions));
    }

    bool IGitUiCommands.StartRemotesDialog(IWin32Window? owner, string? preselectRemote = null,
        string? preselectLocal = null)
    {
        throw NotPorted(nameof(StartRemotesDialog));
    }

    bool IGitUiCommands.StartRenameDialog(IWin32Window? owner, string branch)
    {
        throw NotPorted(nameof(StartRenameDialog));
    }

    bool IGitUiCommands.StartRepoSettingsDialog(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartRepoSettingsDialog));
    }

    bool IGitUiCommands.StartResetChangesDialog(IWin32Window? owner, IReadOnlyCollection<GitItemStatus> workTreeFiles,
        bool onlyWorkTree)
    {
        throw NotPorted(nameof(StartResetChangesDialog));
    }

    bool IGitUiCommands.StartResetCurrentBranchDialog(IWin32Window? owner, string branch)
    {
        throw NotPorted(nameof(StartResetCurrentBranchDialog));
    }

    bool IGitUiCommands.StartResolveConflictsDialog(IWin32Window? owner = null, bool offerCommit = true)
    {
        ConflictResolutionWindow dialog = new(Module, offerCommit);
        if (owner is Window ownerWindow)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () => await dialog.ShowDialog(ownerWindow));
            if (dialog.ContinueRequested && ConflictWorkflow.BuildContinue(Module) is { } continueCommand)
            {
                StartGitCommandProcessDialog(owner, continueCommand);
            }
        }
        else if (owner is null)
        {
            dialog.Show();
        }
        else
        {
            throw new ArgumentException("The conflict dialog owner must be an Avalonia window.", nameof(owner));
        }

        return true;
    }

    bool IGitUiCommands.StartRevertCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        throw NotPorted(nameof(StartRevertCommitDialog));
    }

    bool IGitUiCommands.StartSettingsDialog(IGitPlugin gitPlugin)
    {
        throw NotPorted(nameof(StartSettingsDialog));
    }

    bool IGitUiCommands.StartSettingsDialog(IWin32Window? owner, SettingsPageReference? initialPage = null)
    {
        throw NotPorted(nameof(StartSettingsDialog));
    }

    bool IGitUiCommands.StartSettingsDialog(Type pageType)
    {
        throw NotPorted(nameof(StartSettingsDialog));
    }

    bool IGitUiCommands.StartSparseWorkingCopyDialog(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartSparseWorkingCopyDialog));
    }

    bool IGitUiCommands.StartSquashCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        throw NotPorted(nameof(StartSquashCommitDialog));
    }

    bool IGitUiCommands.StartStashDialog(IWin32Window? owner = null, bool manageStashes = true,
        string? initialStash = null)
    {
        throw NotPorted(nameof(StartStashDialog));
    }

    bool IGitUiCommands.StartSubmodulesDialog(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartSubmodulesDialog));
    }

    bool IGitUiCommands.StartSyncSubmodulesDialog(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartSyncSubmodulesDialog));
    }

    bool IGitUiCommands.StartTheContinueRebaseDialog(IWin32Window? owner)
    {
        throw NotPorted(nameof(StartTheContinueRebaseDialog));
    }

    bool IGitUiCommands.StartUpdateSubmoduleDialog(IWin32Window? owner, string submoduleLocalPath,
        string submoduleParentPath)
    {
        throw NotPorted(nameof(StartUpdateSubmoduleDialog));
    }

    bool IGitUiCommands.StartUpdateSubmodulesDialog(IWin32Window? owner, string submoduleLocalPath = "")
    {
        throw NotPorted(nameof(StartUpdateSubmodulesDialog));
    }

    bool IGitUiCommands.StartVerifyDatabaseDialog(IWin32Window? owner = null)
    {
        VerifyRepositoryWindow dialog = new(this);
        if (owner is Window ownerWindow)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () => await dialog.ShowDialog<bool>(ownerWindow));
        }

        if (owner is null)
        {
            dialog.Show();
            return true;
        }

        throw new ArgumentException("The verify dialog owner must be an Avalonia window.", nameof(owner));
    }

    bool IGitUiCommands.StartViewPatchDialog(IWin32Window? owner, string? patchFile = null)
    {
        throw NotPorted(nameof(StartViewPatchDialog));
    }

    bool IGitUiCommands.StartViewPatchDialog(string patchFile)
    {
        throw NotPorted(nameof(StartViewPatchDialog));
    }

    bool IGitUiCommands.StashStaged(IWin32Window? owner)
    {
        throw NotPorted(nameof(StashStaged));
    }

    void IGitUiCommands.UpdateSubmodules(IWin32Window? owner)
    {
        throw NotPorted(nameof(UpdateSubmodules));
    }

    bool IGitUiCommands.WorktreeCreate(IWin32Window? owner, string mainWorktreePath)
    {
        throw NotPorted(nameof(WorktreeCreate));
    }

    bool IGitUiCommands.WorktreeDelete(IWin32Window? owner, string worktreePath)
    {
        throw NotPorted(nameof(WorktreeDelete));
    }

    bool IGitUiCommands.WorktreeSwitch(IWin32Window? owner, string worktreePath)
    {
        throw NotPorted(nameof(WorktreeSwitch));
    }

    #endregion
}
