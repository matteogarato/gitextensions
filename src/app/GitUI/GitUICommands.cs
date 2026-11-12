using System.Collections.Immutable;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Text;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.CommandsDialogs;
using GitUI.CommandsDialogs.RepoHosting;
using GitUI.CommandsDialogs.SettingsDialog;
using GitUI.CommandsDialogs.SettingsDialog.Pages;
using GitUI.CommandsDialogs.WorktreeDialog;
using GitUI.HelperDialogs;
using GitUI.Plugin;
using JetBrains.Annotations;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI;

/// <summary>Contains methods to invoke GitEx forms, dialogs, etc.</summary>
public sealed class GitUiCommands : IGitUiCommands
{
    private const string _blameHistoryCommand = "blamehistory";
    private const string _fileHistoryCommand = "filehistory";

    private const string _filterByRevisionArg = "--filter-by-revision";
    private const string _pathFilterArg = "--pathFilter";

    public static IServiceProvider EmptyServiceProvider = new ServiceContainer();
    private readonly ICommitTemplateManager _commitTemplateManager;
    private readonly IFindFilePredicateProvider _findFilePredicateProvider;
    private readonly IFullPathResolver _fullPathResolver;

    private readonly IServiceProvider _serviceProvider;

    public GitUiCommands(IServiceProvider serviceProvider, IGitModule module)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(module);

        _serviceProvider = serviceProvider;
        Module = module;

        _commitTemplateManager = new CommitTemplateManager(() => module);
        RepoChangedNotifier = new ActionNotifier(() => InvokeEvent(null, PostRepositoryChanged));

        _fullPathResolver = new FullPathResolver(() => Module.WorkingDir);
        _findFilePredicateProvider = new FindFilePredicateProvider();
    }

    private IGitModule Module { get; set; }

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

    void IGitUiCommands.StartBatchFileProcessDialog(string batchFile)
    {
        var tempFile = Path.Join(Path.GetTempPath(), $"GitExtensions-{Guid.NewGuid():N}.cmd");

        try
        {
            using (StreamWriter writer = new(tempFile))
            {
                writer.WriteLine("@prompt $G");
                writer.Write(batchFile);
            }

            FormProcess.ShowDialog(null, this, $"/C \"{tempFile}\"", Module.WorkingDir, null, true, "cmd.exe");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    bool IGitUiCommands.StartCommandLineProcessDialog(IWin32Window? owner, IGitCommand command)
    {
        var success = command.AccessesRemote
            ? FormRemoteProcess.ShowDialog(owner, this, command.Arguments)
            : FormProcess.ShowDialog(owner, this, command.Arguments, Module.WorkingDir, null, true);

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

    bool IGitUiCommands.StartDeleteBranchDialog(IWin32Window? owner, string branch)
    {
        return StartDeleteBranchDialog(owner, new[] { branch });
    }

    bool IGitUiCommands.StartDeleteBranchDialog(IWin32Window? owner, IEnumerable<string> branches)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormDeleteBranch form = new(this, branches);
            form.ShowDialog(owner);
            return true;
        }, changesRepo: false);
    }

    bool IGitUiCommands.StartDeleteRemoteBranchDialog(IWin32Window? owner, string remoteBranch)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormDeleteRemoteBranch form = new(this, remoteBranch);
            form.ShowDialog(owner);
            return true;
        }, changesRepo: false);
    }

    bool IGitUiCommands.StartCheckoutRevisionDialog(IWin32Window? owner, string? revision = null)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormCheckoutRevision form = new(this);
            form.SetRevision(revision);
            return form.ShowDialog(owner) == DialogResult.OK;
        }, preEvent: PreCheckoutRevision, postEvent: PostCheckoutRevision);
    }

    bool IGitUiCommands.StartResetCurrentBranchDialog(IWin32Window? owner, string branch)
    {
        var objectId = Module.RevParse(branch);
        if (objectId.IsZero)
        {
            MessageBoxes.Show($"Branch \"{branch}\" could not be resolved.", TranslatedStrings.Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        using var form = FormResetCurrentBranch.Create(this, Module.GetRevision(objectId));
        return form.ShowDialog(owner) == DialogResult.OK;
    }

    bool IGitUiCommands.StashSave(IWin32Window? owner, bool includeUntrackedFiles, bool keepIndex = false,
        string message = "", IReadOnlyList<string>? selectedFiles = null)
    {
        bool Action()
        {
            var arguments = Commands.StashSave(includeUntrackedFiles, keepIndex, message, selectedFiles);
            FormProcess.ShowDialog(owner, this, arguments, Module.WorkingDir, null, true);

            // git-stash may have changed commits also if aborted, the grid must be refreshed
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StashStaged(IWin32Window? owner)
    {
        bool Action()
        {
            FormProcess.ShowDialog(owner, this, "stash --staged", Module.WorkingDir, null, true);

            // git-stash may have changed commits also if aborted, the grid must be refreshed
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StashPop(IWin32Window? owner, string stashName = "")
    {
        bool Action()
        {
            FormProcess.ShowDialog(owner, this, $"stash pop {stashName.QuoteNe()}", Module.WorkingDir, null, true);
            MergeConflictHandler.HandleMergeConflicts(this, owner, false, false);

            // git-stash may have changed commits also if aborted, the grid must be refreshed
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StashDrop(IWin32Window? owner, string stashName)
    {
        bool Action()
        {
            FormProcess.ShowDialog(owner, this, $"stash drop {stashName.Quote()}", Module.WorkingDir, null, true);

            // git-stash may have changed commits also if aborted, the grid must be refreshed
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StashApply(IWin32Window? owner, string stashName)
    {
        bool Action()
        {
            FormProcess.ShowDialog(owner, this, $"stash apply {stashName.Quote()}", Module.WorkingDir, null, true);
            MergeConflictHandler.HandleMergeConflicts(this, owner, false, false);

            // git-stash may have changed commits also if aborted, the grid must be refreshed
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.WorktreeDelete(IWin32Window? owner, string worktreePath)
    {
        // The main worktree holds the repository in a ".git" directory, while a linked worktree only
        // has a ".git" file pointing into it, so deleting the former destroys the repository - which
        // is why git refuses to remove it even with --force. Deleting is done by removing the
        // directory rather than by "git worktree remove", so that refusal has to be repeated here.
        // This does not compare paths and therefore also holds where the current worktree is not
        // recognised, e.g. when the repository is opened through a junction or a mount point.
        if (Directory.Exists(Path.Combine(worktreePath, ".git")))
        {
            TaskDialog.ShowDialog(owner!,
                new TaskDialogPage
                {
                    Text = string.Format(TranslatedStrings.DeleteMainWorktreeRefused, worktreePath),
                    Caption = TranslatedStrings.DeleteWorktreeCaption,
                    Icon = TaskDialogIcon.Error,
                    SizeToContent = true
                });

            return false;
        }

        return DoActionOnRepo(owner, () =>
        {
            var result = TaskDialog.ShowDialog(owner!,
                new TaskDialogPage
                {
                    Text = string.Format(TranslatedStrings.DeleteWorktreeConfirmation, worktreePath),
                    Caption = TranslatedStrings.DeleteWorktreeCaption,
                    Heading = TranslatedStrings.CannotBeUndone,
                    Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
                    Icon = TaskDialogIcon.Warning,
                    SizeToContent = true
                });

            if (result != TaskDialogButton.Yes)
            {
                return false;
            }

            if (!worktreePath.TryDeleteDirectory(out var errorMessage))
            {
                TaskDialog.ShowDialog(owner!,
                    new TaskDialogPage
                    {
                        Text =
                            $"{string.Format(TranslatedStrings.DeleteWorktreeFailed, worktreePath)}\n{errorMessage}",
                        Caption = TranslatedStrings.Error,
                        Icon = TaskDialogIcon.Error,
                        SizeToContent = true
                    });

                return false;
            }

            StartCommandLineProcessDialog(owner, command: null, "worktree prune");
            return true;
        });
    }

    bool IGitUiCommands.WorktreeSwitch(IWin32Window? owner, string worktreePath)
    {
        if (!MessageBoxes.ConfirmSuppressible(owner,
                string.Format(TranslatedStrings.SwitchWorktreeConfirmation, worktreePath),
                TranslatedStrings.SwitchWorktreeCaption, AppSettings.DontConfirmSwitchWorktree))
        {
            return false;
        }

        if (!Directory.Exists(worktreePath))
        {
            return false;
        }

        if (FindFormBrowse(owner) is { } browse)
        {
            browse.SetWorkingDir(Path.GetFullPath(worktreePath));
        }

        return true;
    }

    bool IGitUiCommands.WorktreeCreate(IWin32Window? owner, string mainWorktreePath)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormCreateWorktree form = new(this, mainWorktreePath);
            if (form.ShowDialog(owner) != DialogResult.OK)
            {
                return false;
            }

            // Offer to switch to the freshly created worktree, mirroring the clone flow.
            WorktreeSwitch(owner, form.WorktreeDirectory);

            return true;
        });
    }

    void IGitUiCommands.ShowModelessForm(IWin32Window? owner, bool requiresValidWorkingDir,
        EventHandler<GitUiEventArgs>? preEvent, EventHandler<GitUiPostActionEventArgs>? postEvent,
        Func<Form> provideForm)
    {
        if (requiresValidWorkingDir && !RequiresValidWorkingDir(owner))
        {
            return;
        }

        if (!InvokeEvent(owner, preEvent))
        {
            return;
        }

        var form = provideForm();

        void FormClosed(object? sender, FormClosedEventArgs e)
        {
            form.FormClosed -= FormClosed;
            InvokePostEvent(owner, true, postEvent);
        }

        form.FormClosed += FormClosed;
        form.ShowInTaskbar = true;

        if (Application.OpenForms.Count > 0)
        {
            form.Show();
        }
        else
        {
            form.ShowDialog();
        }
    }

    bool IGitUiCommands.DoActionOnRepo(Func<bool> action)
    {
        return DoActionOnRepo(null, action, false);
    }

    bool IGitUiCommands.StartCompareRevisionsDialog(IWin32Window? owner = null)
    {
        bool Action()
        {
            using FormLog form = new(this);
            return form.ShowDialog(owner) == DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartAddFilesDialog(IWin32Window? owner, string? addFiles = null)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormAddFiles form = new(this, addFiles);
            form.ShowDialog(owner);
            return true;
        });
    }

    bool IGitUiCommands.StartCreateBranchDialog(IWin32Window? owner, string? branch)
    {
        var objectId = Module.RevParse(branch!);
        if (objectId.IsZero)
        {
            MessageBoxes.Show($"Branch \"{branch}\" could not be resolved.", TranslatedStrings.Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        return StartCreateBranchDialog(owner, objectId);
    }

    bool IGitUiCommands.StartCreateBranchDialog(IWin32Window? owner = null, ObjectId objectId = default,
        string? newBranchNamePrefix = null)
    {
        if (Module.IsBareRepository() || objectId.IsArtificial)
        {
            return false;
        }

        bool Action()
        {
            using FormCreateBranch form = new(this, objectId, newBranchNamePrefix);
            return form.ShowDialog(owner) == DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartCloneDialog(IWin32Window? owner, string? url = null,
        bool openedFromProtocolHandler = false, EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
    {
        bool Action()
        {
            using FormClone form = new(this, url, openedFromProtocolHandler, gitModuleChanged);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, false, false);
    }

    bool IGitUiCommands.StartCloneDialog(IWin32Window? owner, string url,
        EventHandler<GitModuleEventArgs> gitModuleChanged)
    {
        return StartCloneDialog(owner, url, false, gitModuleChanged);
    }

    bool IGitUiCommands.StartCleanupRepositoryDialog(IWin32Window? owner = null, string? path = null)
    {
        using FormCleanupRepository form = new(this);
        form.SetPathArgument(path);
        form.ShowDialog(owner);

        return true;
    }

    bool IGitUiCommands.StartSquashCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        bool Action()
        {
            using FormCommit form = new(this, CommitKind.Squash, revision);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(Action);
    }

    bool IGitUiCommands.StartFixupCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        bool Action()
        {
            using FormCommit form = new(this, CommitKind.Fixup, revision);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(Action);
    }

    bool IGitUiCommands.StartAmendCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        bool Action()
        {
            using FormCommit form = new(this, CommitKind.Amend, revision);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(Action);
    }

    bool IGitUiCommands.StartCommitDialog(IWin32Window? owner, string? commitMessage = null,
        bool showOnlyWhenChanges = false)
    {
        if (Module.IsBareRepository())
        {
            return false;
        }

        // Commit dialog can be opened on its own without the main form
        // If it is opened by itself, we need to ensure plugins are loaded because some of them
        // may have hooks into the commit flow
        var werePluginsRegistered = PluginRegistry.PluginsRegistered;

        try
        {
            // Load plugins synchronously
            // if the commit dialog is opened from the main form, all plugins are already loaded and we return instantly,
            // if the dialog is loaded on its own, plugins need to be loaded before we load the form
            if (!werePluginsRegistered)
            {
                PluginRegistry.InitializeForCommitForm();
                PluginRegistry.Register(this);
            }
        }
        catch (Exception exception)
        {
            // Nothing: we don't want plugin loading to crash the application here
            Trace.WriteLine(exception);
        }

        bool Action()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            using FormCommit form = new(this, commitMessage: commitMessage);
            if (showOnlyWhenChanges)
            {
                form.ShowDialogWhenChanges(owner);
            }
            else
            {
                form.ShowDialog(owner);
            }

            return true;
        }

        try
        {
            return DoActionOnRepo(owner, Action, changesRepo: false, preEvent: PreCommit, postEvent: PostCommit);
        }
        finally
        {
            try
            {
                if (!werePluginsRegistered)
                {
                    PluginRegistry.Unregister(this);
                }
            }
            catch (Exception exception)
            {
                // Nothing: we don't want plugin loading to crash the application here
                Trace.WriteLine(exception);
            }
        }
    }

    bool IGitUiCommands.StartInitializeDialog(IWin32Window? owner = null, string? dir = null,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
    {
        bool Action()
        {
            dir ??= Module.IsValidGitWorkingDir() ? Module.WorkingDir : string.Empty;

            using FormInit frm = new(this, dir, gitModuleChanged);
            frm.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, false, false);
    }

    bool IGitUiCommands.StartPullDialogAndPullImmediately(IWin32Window? owner = null, string? remoteBranch = null,
        string? remote = null, GitPullAction pullAction = GitPullAction.None)
    {
        return StartPullDialogAndPullImmediately(out _, owner, remoteBranch, remote, pullAction);
    }

    /// <param name="pullCompleted">true if pull completed with no errors.</param>
    /// <returns>if revision grid should be refreshed.</returns>
    bool IGitUiCommands.StartPullDialogAndPullImmediately(out bool pullCompleted, IWin32Window? owner = null,
        string? remoteBranch = null, string? remote = null, GitPullAction pullAction = GitPullAction.None)
    {
        return StartPullDialogInternal(owner, true, out pullCompleted, remoteBranch, remote, pullAction);
    }

    bool IGitUiCommands.StartPullDialog(IWin32Window? owner = null, string? remoteBranch = null, string? remote = null,
        GitPullAction pullAction = GitPullAction.None)
    {
        return StartPullDialogInternal(owner, false, out _, remoteBranch, remote, pullAction);
    }

    bool IGitUiCommands.StartViewPatchDialog(IWin32Window? owner, string? patchFile = null)
    {
        bool Action()
        {
            using FormViewPatch viewPatch = new(this);
            if (!string.IsNullOrEmpty(patchFile))
            {
                viewPatch.LoadPatch(patchFile);
            }

            viewPatch.ShowDialog(owner);

            return true;
        }

        return DoActionOnRepo(owner, Action, false, false);
    }

    bool IGitUiCommands.StartFormCommitDiff(ObjectId objectId)
    {
        bool Action()
        {
            using FormCommitDiff viewPatch = new(this, objectId);
            viewPatch.ShowDialog(null);
            return true;
        }

        return DoActionOnRepo(null, Action, false, false);
    }

    bool IGitUiCommands.StartViewPatchDialog(string patchFile)
    {
        return StartViewPatchDialog(null, patchFile);
    }

    bool IGitUiCommands.StartSparseWorkingCopyDialog(IWin32Window? owner)
    {
        bool Action()
        {
            using FormSparseWorkingCopy form = new(this);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false);
    }

    void IGitUiCommands.AddCommitTemplate(string key, Func<string> addingText, Image? icon, bool isRegex)
    {
        _commitTemplateManager.Register(key, addingText, icon, isRegex);
    }

    void IGitUiCommands.RemoveCommitTemplate(string key)
    {
        _commitTemplateManager.Unregister(key);
    }

    bool IGitUiCommands.StartFormatPatchDialog(IWin32Window? owner = null)
    {
        bool Action()
        {
            using FormFormatPatch form = new(this);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false);
    }

    bool IGitUiCommands.StartStashDialog(IWin32Window? owner = null, bool manageStashes = true,
        string? initialStash = null)
    {
        bool Action()
        {
            using FormStash form = new(this, initialStash) { ManageStashes = manageStashes };
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false);
    }

    /// <summary>
    ///     Reset all changes to HEAD.
    /// </summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="workTreeFiles">Worktree files, to determine the status for the popup dialog.</param>
    /// <param name="onlyWorkTree">Only reset worktree files.</param>
    /// <returns><see langword="true" /> if executed.</returns>
    bool IGitUiCommands.StartResetChangesDialog(IWin32Window? owner, IReadOnlyCollection<GitItemStatus> workTreeFiles,
        bool onlyWorkTree)
    {
        // Show a form asking the user if they want to reset the changes.
        var resetType = FormResetChanges.ShowResetDialog(owner,
            workTreeFiles.Any(item => !item.IsNew), workTreeFiles.Any(item => item.IsNew));

        if (resetType == FormResetChanges.ActionEnum.Cancel)
        {
            return false;
        }

        return DoActionOnRepo(owner, Action);

        bool Action()
        {
            return Module.ResetAllChanges(resetType == FormResetChanges.ActionEnum.ResetAndDelete, onlyWorkTree);
        }
    }

    bool IGitUiCommands.StartRevertCommitDialog(IWin32Window? owner, GitRevision revision)
    {
        bool Action()
        {
            using FormRevertCommit form = new(this, revision);
            return form.ShowDialog(owner) == DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartResolveConflictsDialog(IWin32Window? owner = null, bool offerCommit = true)
    {
        bool Action()
        {
            using FormResolveConflicts form = new(this, offerCommit);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartCherryPickDialog(IWin32Window? owner = null, GitRevision? revision = null)
    {
        bool Action()
        {
            using FormCherryPick form = new(this, revision);
            return form.ShowDialog(owner) == DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartCherryPickDialog(IWin32Window? owner, IEnumerable<GitRevision> revisions)
    {
        ArgumentNullException.ThrowIfNull(revisions);

        bool Action()
        {
            FormCherryPick? prevForm = null;

            try
            {
                var repoChanged = false;

                // ReSharper disable once PossibleMultipleEnumeration
                foreach (var r in revisions)
                {
                    FormCherryPick frm = new(this, r);
                    if (prevForm is not null)
                    {
                        frm.CopyOptions(prevForm);
                        prevForm.Dispose();
                    }

                    prevForm = frm;
                    if (frm.ShowDialog(owner) == DialogResult.OK)
                    {
                        repoChanged = true;
                    }
                    else
                    {
                        return repoChanged;
                    }
                }

                return repoChanged;
            }
            finally
            {
                prevForm?.Dispose();
            }
        }

        return DoActionOnRepo(owner, Action);
    }

    /// <summary>Start Merge dialog, using the specified branch.</summary>
    /// <param name="owner">Owner of the dialog.</param>
    /// <param name="branch">Branch to merge into the current branch.</param>
    bool IGitUiCommands.StartMergeBranchDialog(IWin32Window? owner, string? branch)
    {
        bool Action()
        {
            using FormMergeBranch form = new(this, branch);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false);
    }

    bool IGitUiCommands.StartCreateTagDialog(IWin32Window? owner = null, GitRevision? revision = null)
    {
        if (revision?.IsArtificial is true)
        {
            return false;
        }

        bool Action()
        {
            using FormCreateTag form = new(this, revision?.ObjectId ?? default);
            return form.ShowDialog(owner) == DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartDeleteTagDialog(IWin32Window? owner, string? tag)
    {
        bool Action()
        {
            using FormDeleteTag form = new(this, tag);
            return form.ShowDialog(owner) == DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartEditGitIgnoreDialog(IWin32Window? owner, bool localExcludes)
    {
        bool Action()
        {
            using FormGitIgnore form = new(this, localExcludes);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false, postEvent: PostEditGitIgnore);
    }

    bool IGitUiCommands.StartAddToGitIgnoreDialog(IWin32Window? owner, bool localExclude, params string[] filePattern)
    {
        bool Action()
        {
            using FormAddToGitIgnore frm = new(this, localExclude, filePattern);
            frm.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false, postEvent: PostEditGitIgnore);
    }

    bool IGitUiCommands.StartSettingsDialog(IWin32Window? owner, SettingsPageReference? initialPage = null)
    {
        bool Action()
        {
            return FormSettings.ShowSettingsDialog(this, owner, initialPage)
                is DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action, false, postEvent: PostSettings);
    }

    bool IGitUiCommands.StartSettingsDialog(IGitPlugin gitPlugin)
    {
        // TODO: how to pass the main dialog as owner of the SettingsDialog (first parameter):
        return StartSettingsDialog(null, new SettingsPageReferenceByPlugin(gitPlugin));
    }

    bool IGitUiCommands.StartSettingsDialog(Type pageType)
    {
        return StartSettingsDialog(null, new SettingsPageReferenceByType(pageType));
    }

    /// <summary>
    ///     Open the archive dialog.
    /// </summary>
    /// <param name="revision">Revision to create an archive from.</param>
    /// <param name="revision2">Revision for differential archive.</param>
    /// <param name="path">Files path for archive.</param>
    bool IGitUiCommands.StartArchiveDialog(IWin32Window? owner = null, GitRevision? revision = null,
        GitRevision? revision2 = null, string? path = null)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormArchive form = new(this) { SelectedRevision = revision };
            form.SetDiffSelectedRevision(revision2);
            form.SetPathArgument(path);
            form.ShowDialog(owner);

            return true;
        }, changesRepo: false);
    }

    bool IGitUiCommands.StartMailMapDialog(IWin32Window? owner = null)
    {
        bool Action()
        {
            using FormMailMap form = new(this);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false);
    }

    bool IGitUiCommands.StartVerifyDatabaseDialog(IWin32Window? owner = null)
    {
        bool Action()
        {
            using FormVerify form = new(this);
            form.ShowDialog(owner);
            return true;
        }

        // TODO: move Notify to FormVerify and friends
        return DoActionOnRepo(owner, Action);
    }

    /// <inheritdoc />
    bool IGitUiCommands.StartRemotesDialog(IWin32Window? owner, string? preselectRemote = null,
        string? preselectLocal = null)
    {
        bool Action()
        {
            using FormRemotes form = new(this)
            {
                PreselectRemoteOnLoad = preselectRemote, PreselectLocalOnLoad = preselectLocal
            };
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartRebase(IWin32Window? owner, string onto)
    {
        return StartRebaseDialog(owner, from: "", to: null, onto: onto,
            interactive: false, startRebaseImmediately: true);
    }

    bool IGitUiCommands.StartTheContinueRebaseDialog(IWin32Window? owner)
    {
        return StartRebaseDialog(owner, from: "", to: null, onto: null, interactive: false,
            startRebaseImmediately: false);
    }

    bool IGitUiCommands.StartInteractiveRebase(IWin32Window? owner, string onto)
    {
        return StartRebaseDialog(owner, from: "", to: null, onto: onto,
            interactive: true, startRebaseImmediately: true);
    }

    bool IGitUiCommands.StartRebaseDialogWithAdvOptions(IWin32Window? owner, string onto, string from = "")
    {
        return StartRebaseDialog(owner, from: from, to: null, onto, interactive: false, startRebaseImmediately: false);
    }

    bool IGitUiCommands.StartRebaseDialog(IWin32Window? owner, string? onto)
    {
        return StartRebaseDialog(owner, from: "", to: null, onto, interactive: false, startRebaseImmediately: false);
    }

    bool IGitUiCommands.StartRebaseDialog(IWin32Window? owner, string? from, string? to, string? onto,
        bool interactive = false, bool startRebaseImmediately = true)
    {
        bool Action()
        {
            using FormRebase form = new(this, from, to, onto, interactive, startRebaseImmediately);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartRenameDialog(IWin32Window? owner, string branch)
    {
        bool Action()
        {
            using FormRenameBranch form = new(this, branch);
            return form.ShowDialog(owner) == DialogResult.OK;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartSubmodulesDialog(IWin32Window? owner)
    {
        bool Action()
        {
            using FormSubmodules form = new(this);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action);
    }

    bool IGitUiCommands.StartUpdateSubmodulesDialog(IWin32Window? owner, string submoduleLocalPath = "")
    {
        bool Action()
        {
            return FormProcess.ShowDialog(owner, this, Commands.SubmoduleUpdate(submoduleLocalPath), Module.WorkingDir,
                null, true);
        }

        return DoActionOnRepo(owner, Action, postEvent: PostUpdateSubmodules);
    }

    bool IGitUiCommands.StartUpdateSubmoduleDialog(IWin32Window? owner, string submoduleLocalPath,
        string submoduleParentPath)
    {
        bool Action()
        {
            // Execute the submodule update comment from the submodule's parent directory
            return FormProcess.ShowDialog(owner, this, Commands.SubmoduleUpdate(submoduleLocalPath),
                submoduleParentPath, null, true);
        }

        return DoActionOnRepo(owner, Action, postEvent: PostUpdateSubmodules);
    }

    bool IGitUiCommands.StartSyncSubmodulesDialog(IWin32Window? owner)
    {
        bool Action()
        {
            return FormProcess.ShowDialog(owner, this, Commands.SubmoduleSync(""), Module.WorkingDir, null, true);
        }

        return DoActionOnRepo(owner, Action);
    }

    void IGitUiCommands.UpdateSubmodules(IWin32Window? owner)
    {
        if (!Module.HasSubmodules())
        {
            return;
        }

        var updateSubmodules = AppSettings.UpdateSubmodulesOnCheckout ??
                               AppSettings.DontConfirmUpdateSubmodulesOnCheckout ??
                               MessageBoxes.ConfirmUpdateSubmodules(owner);

        if (updateSubmodules)
        {
            StartUpdateSubmodulesDialog(owner);
        }
    }

    bool IGitUiCommands.StartGeneralSettingsDialog(IWin32Window? owner)
    {
        return StartSettingsDialog(owner, GeneralSettingsPage.GetPageReference());
    }

    bool IGitUiCommands.StartPluginSettingsDialog(IWin32Window? owner)
    {
        return StartSettingsDialog(owner, PluginsSettingsGroup.GetPageReference());
    }

    bool IGitUiCommands.StartRepoSettingsDialog(IWin32Window? owner)
    {
        return StartSettingsDialog(owner, GitConfigSettingsPage.GetPageReference());
    }

    /// <summary>
    ///     Open Browse - main GUI including dashboard.
    /// </summary>
    /// <param name="owner">current window owner.</param>
    /// <param name="args">The start up arguments.</param>
    bool IGitUiCommands.StartBrowseDialog(IWin32Window? owner, BrowseArguments? args = null)
    {
        FormBrowse form = new(this, args ?? new BrowseArguments());

        if (Application.MessageLoop)
        {
            form.Show(owner);
        }
        else
        {
            Application.Run(form);
        }

        return true;
    }

    void IGitUiCommands.StartFileHistoryDialog(IWin32Window? owner, string fileName, GitRevision? revision = null,
        bool filterByRevision = false, bool showBlame = false)
    {
        var useBrowseForFileHistory = AppSettings.UseBrowseForFileHistory.Value;
        var arguments = useBrowseForFileHistory
            ? $"browse {_pathFilterArg}={fileName.Quote()}{GetCommitIdArg()}"
            : $"{(showBlame ? _blameHistoryCommand : _fileHistoryCommand)} {fileName.Quote()}{GetCommitIdArg()} {(filterByRevision ? _filterByRevisionArg : string.Empty)}";
        Launch(arguments, Module.WorkingDir);

        return;

        string GetCommitIdArg()
        {
            if (revision is null)
            {
                return "";
            }

            if (useBrowseForFileHistory)
            {
                return $" -commit={revision.ObjectId}";
            }

            // Avoid a race condition in FormFileHistory selecting an artificial commit.
            // Without a hash passed, it automatically selects the first real revision.
            if (revision.IsArtificial)
            {
                return "";
            }

            return $" {revision.ObjectId}";
        }
    }

    void IGitUiCommands.OpenWithDifftool(IWin32Window? owner, IReadOnlyList<GitRevision?> revisions, string fileName,
        string? oldFileName, RevisionDiffKind diffKind, bool isTracked, string? customTool = null)
    {
        // Note: Order in revisions is that first clicked is last in array

        if (!RevisionDiffInfoProvider.TryGet(revisions, diffKind, out var firstRevision, out var secondRevision,
                out var error))
        {
            MessageBoxes.Show(owner, error, TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            Module.OpenWithDifftool(fileName, oldFileName, firstRevision, secondRevision, isTracked: isTracked,
                customTool: customTool);
        }
    }

    bool IGitUiCommands.StartPushDialog(IWin32Window? owner, bool pushOnShow, bool forceWithLease,
        out bool pushCompleted, string? branchName = null)
    {
        var pushed = false;

        bool Action()
        {
            using FormPush form = new(this, branchName);
            if (forceWithLease)
            {
                form.CheckForceWithLease();
            }

            var dlgResult = pushOnShow
                ? form.PushAndShowDialogWhenFailed(owner)
                : form.ShowDialog(owner);

            if (dlgResult == DialogResult.OK)
            {
                pushed = !form.ErrorOccurred;
            }

            return dlgResult == DialogResult.OK;
        }

        var done = DoActionOnRepo(owner, Action);

        pushCompleted = pushed;

        return done;
    }

    bool IGitUiCommands.StartPushDialog(IWin32Window? owner, bool pushOnShow)
    {
        return StartPushDialog(owner, pushOnShow, forceWithLease: false, out _, branchName: null);
    }

    bool IGitUiCommands.StartApplyPatchDialog(IWin32Window? owner, string? patchFile = null)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormApplyPatch form = new(this);
            if (Directory.Exists(patchFile!))
            {
                form.SetPatchDir(patchFile!);
            }
            else
            {
                form.SetPatchFile(patchFile ?? "");
            }

            form.ShowDialog(owner);

            return true;
        }, changesRepo: false);
    }

    bool IGitUiCommands.StartEditGitAttributesDialog(IWin32Window? owner = null)
    {
        bool Action()
        {
            using FormGitAttributes form = new(this);
            form.ShowDialog(owner);
            return true;
        }

        return DoActionOnRepo(owner, Action, changesRepo: false);
    }

    void IGitUiCommands.StartCloneForkFromHoster(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        EventHandler<GitModuleEventArgs>? gitModuleChanged)
    {
        WrapRepoHostingCall(TranslatedStrings.ForkCloneRepo, gitHoster, gh =>
        {
            using ForkAndCloneForm frm = new(this, gh, gitModuleChanged);
            frm.ShowDialog(owner);
        });
    }

    void IGitUiCommands.StartPullRequestsDialog(IWin32Window? owner, IRepositoryHostPlugin gitHoster)
    {
        WrapRepoHostingCall(TranslatedStrings.ViewPullRequest, gitHoster,
            gh =>
            {
                ViewPullRequestsForm frm = new(this, gh) { ShowInTaskbar = true };
                frm.Show(owner);
            });
    }

    void IGitUiCommands.AddUpstreamRemote(IWin32Window? owner, IRepositoryHostPlugin gitHoster)
    {
        WrapRepoHostingCall(TranslatedStrings.AddUpstreamRemote, gitHoster,
            gh =>
            {
                ThreadHelper.FileAndForget(async () =>
                {
                    var remoteName = await gh.AddUpstreamRemoteAsync();
                    if (!string.IsNullOrEmpty(remoteName))
                    {
                        StartPullDialogAndPullImmediately(owner, remoteBranch: null, remoteName, GitPullAction.Fetch);
                    }
                });
            });
    }

    void IGitUiCommands.StartCreatePullRequest(IWin32Window? owner)
    {
        List<IRepositoryHostPlugin> relevantHosts =
            [.. PluginRegistry.GitHosters.Where(gh => gh.GitModuleIsRelevantToMe())];

        if (relevantHosts.Count == 0)
        {
            MessageBoxes.Show(owner, "Could not find any repo hosts for current working directory",
                TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else if (relevantHosts.Count == 1)
        {
            StartCreatePullRequest(owner, relevantHosts[0]);
        }
        else
        {
            MessageBoxes.Show("StartCreatePullRequest:Selection not implemented!", TranslatedStrings.Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void IGitUiCommands.StartCreatePullRequest(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        string? chooseRemote = null, string? chooseBranch = null)
    {
        WrapRepoHostingCall(
            TranslatedStrings.CreatePullRequest,
            gitHoster,
            gh =>
            {
                CreatePullRequestForm form = new(this, gh, chooseRemote, chooseBranch) { ShowInTaskbar = true };

                form.Show(owner);
            });
    }

    bool IGitUiCommands.RunCommand(IReadOnlyList<string> args)
    {
        var arguments = InitializeArguments(args);

        if (args.Count <= 1)
        {
            return false;
        }

        var command = args[1];

        if (command == "blame" && args.Count <= 2)
        {
            MessageBoxes.Show("Cannot open blame, there is no file selected.", "Blame", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        if (command == "difftool" && args.Count <= 2)
        {
            MessageBoxes.Show("Cannot open difftool, there is no file selected.", "Difftool", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        if (command is _blameHistoryCommand or _fileHistoryCommand && args.Count <= 2)
        {
            MessageBoxes.Show("Cannot open blame / file history, there is no file selected.", "Blame / file history",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        if (command == "fileeditor" && args.Count <= 2)
        {
            MessageBoxes.Show("Cannot open file editor, there is no file selected.", "File editor",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        if (command == "revert" && args.Count <= 2)
        {
            MessageBoxes.Show("Cannot open revert, there is no file selected.", "Revert", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        return RunCommandBasedOnArgument(args, arguments);
    }

    bool IGitUiCommands.StartFileEditorDialog(string? filename, bool showWarning = false, int? lineNumber = null)
    {
        using FormEditor formEditor = new(this, filename, showWarning, lineNumber: lineNumber);
        return !formEditor.IsDisposed && formEditor.ShowDialog() != DialogResult.Cancel;
    }

    void IGitUiCommands.RaisePostBrowseInitialize(IWin32Window? owner)
    {
        InvokeEvent(owner, PostBrowseInitialize);
    }

    void IGitUiCommands.RaisePostRegisterPlugin(IWin32Window? owner)
    {
        InvokeEvent(owner, PostRegisterPlugin);
    }

    IGitRemoteCommand IGitUiCommands.CreateRemoteCommand()
    {
        return new GitRemoteCommand(this);
    }

    /// <summary>
    ///     Creates a new instance of <see cref="IGitUiCommands" /> for a git repository specified by
    ///     <paramref name="module" />.
    /// </summary>
    /// <param name="module">The git repository.</param>
    /// <returns>A new instance of <see cref="IGitUiCommands" />.</returns>
    IGitUiCommands IGitUiCommands.WithGitModule(IGitModule module)
    {
        return new GitUiCommands(_serviceProvider, module);
    }

    /// <summary>
    ///     Creates a new instance of <see cref="IGitUiCommands" /> for a git repository specified by
    ///     <paramref name="workingDirectory" />.
    /// </summary>
    /// <param name="workingDirectory">The git repository working directory.</param>
    /// <returns>A new instance of <see cref="IGitUiCommands" />.</returns>
    IGitUiCommands IGitUiCommands.WithWorkingDirectory(string? workingDirectory)
    {
        return new GitUiCommands(_serviceProvider,
            new GitModule(this.GetRequiredService<IGitExecutorProvider>(), workingDirectory));
    }

    private bool RequiresValidWorkingDir(object? owner)
    {
        if (!Module.IsValidGitWorkingDir())
        {
            MessageBoxes.NotValidGitDirectory(owner as IWin32Window);
            return false;
        }

        return true;
    }

    private static FormBrowse? FindFormBrowse(IWin32Window? window)
    {
        // The owner may be a child control (e.g. the repository objects tree), so resolve its containing form first.
        if (window is Control control and not Form)
        {
            window = control.FindForm();
        }

        for (var form = window as Form; form is not null; form = form.Owner)
        {
            if (form is FormBrowse formBrowse)
            {
                return formBrowse;
            }
        }

        return null;
    }

    /// <param name="requiresValidWorkingDir">If action requires valid working directory.</param>
    /// <param name="owner">Owner window.</param>
    /// <param name="changesRepo">if successfully done action changes repo state.</param>
    /// <param name="preEvent">Event invoked before performing action.</param>
    /// <param name="postEvent">Event invoked after performing action.</param>
    /// <param name="action">Action to do. Return true to indicate that the action was successfully done.</param>
    /// <returns>true if action was successfully done, false otherwise.</returns>
    private bool DoActionOnRepo(
        IWin32Window? owner,
        [InstantHandle] Func<bool> action,
        bool requiresValidWorkingDir = true,
        bool changesRepo = true,
        EventHandler<GitUiEventArgs>? preEvent = null,
        EventHandler<GitUiPostActionEventArgs>? postEvent = null)
    {
        var actionDone = false;
        RepoChangedNotifier.Lock();
        try
        {
            if (requiresValidWorkingDir && !RequiresValidWorkingDir(owner))
            {
                return false;
            }

            if (!InvokeEvent(owner, preEvent))
            {
                return false;
            }

            try
            {
                actionDone = action();
            }
            finally
            {
                InvokePostEvent(owner, actionDone, postEvent);
            }
        }
        finally
        {
            // The action may not have required a valid working directory to run, but if there isn't one,
            // we shouldn't send a "repo changed" notify.
            var requestNotify = actionDone && changesRepo && Module.IsValidGitWorkingDir();
            RepoChangedNotifier.UnLock(requestNotify);
        }

        return actionDone;
    }

    /// <summary>
    ///     Launches a new GE instance.
    /// </summary>
    /// <param name="arguments">The command line arguments.</param>
    /// <param name="workingDir">The working directory for the new process.</param>
    /// <returns>The <see cref="IProcess" /> object for controlling the launched instance.</returns>
    public static IProcess Launch(string arguments, string workingDir = "")
    {
        return new Executable(Application.ExecutablePath, workingDir).Start(arguments);
    }

    /// <summary>
    ///     Launch FormBrowse in a new GE instance.
    /// </summary>
    /// <param name="workingDir">The working directory for the new process.</param>
    /// <param name="selectedId">The optional commit to be selected.</param>
    /// <param name="firstId">The first commit to be selected, the first commit in a diff.</param>
    internal static void LaunchBrowse(string workingDir = "", ObjectId selectedId = default, ObjectId firstId = default)
    {
        if (!Directory.Exists(workingDir))
        {
            MessageBoxes.GitExtensionsDirectoryDoesNotExist(null, workingDir);
            return;
        }

        StringBuilder arguments = new("browse");

        if (selectedId.IsZero)
        {
            selectedId = firstId;
            firstId = default;
        }

        if (!selectedId.IsZero)
        {
            arguments.Append(" -commit=").Append(selectedId);
            if (!firstId.IsZero)
            {
                arguments.Append(',').Append(firstId);
            }
        }

        Launch(arguments.ToString(), workingDir);
    }

    private bool StartPullDialogInternal(IWin32Window? owner, bool pullOnShow, out bool pullCompleted,
        string? remoteBranch, string? remote, GitPullAction pullAction)
    {
        var pulled = false;

        bool Action()
        {
            using FormPull formPull = new(this, remoteBranch, remote, pullAction);
            var dlgResult = pullOnShow
                ? formPull.PullAndShowDialogWhenFailed(owner, remote, pullAction)
                : formPull.ShowDialog(owner);

            if (dlgResult == DialogResult.OK)
            {
                pulled = !formPull.ErrorOccurred;
            }

            return dlgResult == DialogResult.OK;
        }

        var done = DoActionOnRepo(owner, Action);

        pullCompleted = pulled;

        return done;
    }

    /// <summary>
    ///     Resets changes of passed files or folders (with absolute or relative paths).<br />
    ///     If no <paramref name="names" /> are passed all changes are reset.
    /// </summary>
    /// <returns><see langword="false" /> if cancelled or if no items match.</returns>
    private bool StartResetChangesDialog(string[] names)
    {
        ImmutableHashSet<string> relativeFilePaths =
            [.. names.Select(fileName => Path.GetRelativePath(Module.WorkingDir, fileName).ToPosixPath())];
        ImmutableHashSet<string> relativeFolderPaths =
            [.. relativeFilePaths.Where(name => Directory.Exists(Path.Join(Module.WorkingDir, name)))];
        var allItems = relativeFolderPaths.Contains(".");
        GitItemStatus[] selectedItems =
        [
            .. Module.GetAllChangedFilesWithSubmodulesStatus(default)
                .Where(item =>
                    allItems || relativeFilePaths.Contains(item.Name) ||
                    relativeFolderPaths.Any(folder => item.Path.Value.StartsWith(folder)))
        ];

        // Show a form asking the user if they want to reset the changes.
        var resetType = FormResetChanges.ShowResetDialog(null,
            selectedItems.Any(item => item.IsTracked), selectedItems.Any(item => item.IsNew));

        if (resetType == FormResetChanges.ActionEnum.Cancel)
        {
            return false;
        }

        using (WaitCursorScope.Enter())
        {
            // Reset all changes.
            if (names.Length == 0)
            {
                return Module.ResetAllChanges(resetType == FormResetChanges.ActionEnum.ResetAndDelete);
            }

            if (selectedItems.Length == 0)
            {
                return false;
            }

            Module.ResetChanges(default, selectedItems, resetType == FormResetChanges.ActionEnum.ResetAndDelete,
                _fullPathResolver, out var output, null);
            if (output.Length > 0)
            {
                MessageBoxes.Show(null, output.ToString(), TranslatedStrings.ResetChangesCaption, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        return true;
    }

    private bool InvokeEvent(IWin32Window? ownerForm, EventHandler<GitUiEventArgs>? gitUiEventHandler)
    {
        if (gitUiEventHandler is not null)
        {
            try
            {
                GitUiEventArgs e = new(ownerForm, this);
                gitUiEventHandler.Invoke(this, e);
                return !e.Cancel;
            }
            catch (Exception ex)
            {
                MessageBoxes.Show(ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        return true;
    }

    private void InvokePostEvent(IWin32Window? ownerForm, bool actionDone,
        EventHandler<GitUiPostActionEventArgs>? gitUiEventHandler)
    {
        if (gitUiEventHandler is not null)
        {
            GitUiPostActionEventArgs e = new(ownerForm, this, actionDone);
            gitUiEventHandler(this, e);
        }
    }

    private void WrapRepoHostingCall(string name, IRepositoryHostPlugin gitHoster, Action<IRepositoryHostPlugin> call)
    {
        if (!gitHoster.ConfigurationOk)
        {
            GitUiEventArgs eventArgs = new(null, this);
            gitHoster.Execute(eventArgs);
        }

        if (gitHoster.ConfigurationOk)
        {
            try
            {
                call(gitHoster);
            }
            catch (Exception ex)
            {
                MessageBoxes.Show(
                    string.Format("ERROR: {0} failed. Message: {1}\r\n\r\n{2}", name, ex.Message, ex.StackTrace),
                    "Error! :(", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Please update FormCommandlineHelp if you add or change commands
    private bool RunCommandBasedOnArgument(IReadOnlyList<string> args, IReadOnlyDictionary<string, string?> arguments)
    {
#pragma warning disable SA1025 // Code should not contain multiple whitespace in a row
        var command = args[1];
        switch (command)
        {
            case "about":
                Application.Run(new FormAbout { StartPosition = FormStartPosition.CenterScreen });
                return true;
            case "add":
            case "addfiles":
                // If filenames have been specified, quote them and pass them to the dialog, else pass '.' for current dir.
                return StartAddFilesDialog(owner: null,
                    addFiles: args.Count < 3 ? "." : string.Join(' ', args.Skip(2).Select(file => file.Quote())));
            case "apply": // [filename]
            case "applypatch":
                return StartApplyPatchDialog(null, args.Count == 3 ? args[2] : "");
            case "blame": // filename
                return RunBlameCommand(args);
            case "branch":
                return StartCreateBranchDialog();
            case "browse": // [path] [--pathFilter=filname] [-filter] [-commit=selected[,first]]
                return RunBrowseCommand(args);
            case "checkout":
            case "checkoutbranch":
                return StartCheckoutBranch(null);
            case "checkoutrevision":
                return StartCheckoutRevisionDialog(null);
            case "cherry":
                return StartCherryPickDialog();
            case "cleanup":
                return StartCleanupRepositoryDialog();
            case "clone": // [path]
                return RunCloneCommand(args);
            case "commit": // [--quiet]
                return Commit(arguments);
            case "difftool": // filename
                try
                {
                    Module.OpenWithDifftool(args[2]);
                    return true;
                }
                catch
                {
                    return false;
                }

            case _blameHistoryCommand:
            case _fileHistoryCommand:
                // filename [revision [--filter-by-revision]]
                if (Module.WorkingDir.TrimEnd('\\') == Path.GetFullPath(args[2]) &&
                    Module.SuperprojectModule is not null)
                {
                    Module = Module.SuperprojectModule;
                }

                return RunFileHistoryCommand(args, command == _blameHistoryCommand);
            case "fileeditor": // filename
                return StartFileEditorDialog(args[2]);
            case "formatpatch":
                return StartFormatPatchDialog();
            case "gitignore":
                return StartEditGitIgnoreDialog(null, false);
            case "init": // [path]
                return RunInitCommand(args);
            case "merge": // [--branch name]
                return RunMergeCommand(arguments);
            case "mergeconflicts":
            case "mergetool": // [--quiet]
                return RunMergeToolOrConflictCommand(arguments);
            case "openrepo": // [path]
                return RunOpenRepoCommand(args);
            case "pull": // [--rebase] [--merge] [--fetch] [--quiet] [--remotebranch name]
                return Pull(arguments);
            case "push": // [--quiet]
                return Push(arguments);
            case "rebase": // [--branch name]
                return RunRebaseCommand(arguments);
            case "remotes":
                return StartRemotesDialog(owner: null);
            case "revert":
            case "reset":
                // If names of files or folders have been specified, pass them
                return StartResetChangesDialog([.. args.Skip(2)]);
            case "searchfile":
                return RunSearchFileCommand();
            case "settings":
                return StartSettingsDialog(owner: null);
            case "stash":
                return StartStashDialog();
            case "synchronize": // [--rebase] [--merge] [--fetch] [--quiet]
                return RunSynchronizeCommand(arguments);
            case "tag":
                return StartCreateTagDialog();
            case "viewdiff":
                return StartCompareRevisionsDialog();
            case "viewpatch": // [filename]
                return StartViewPatchDialog(args.Count == 3 ? args[2] : "");
            case "uninstall":
                return UninstallEditor();
            default:
                if (args[1].StartsWith("git://") || args[1].StartsWith("http://") || args[1].StartsWith("https://"))
                {
                    return StartCloneDialog(null, args[1], true);
                }

                if (args[1].StartsWith("github-windows://openRepo/"))
                {
                    return StartCloneDialog(null, args[1].Replace("github-windows://openRepo/", ""), true);
                }

                if (args[1].StartsWith("github-mac://openRepo/"))
                {
                    return StartCloneDialog(null, args[1].Replace("github-mac://openRepo/", ""), true);
                }

                // User supplied a path. Open the repository if its a valid path
                var dir = !string.IsNullOrWhiteSpace(command) && File.Exists(command)
                    ? Path.GetDirectoryName(command)
                    : command;
                if (args.Count == 2 && Directory.Exists(dir))
                {
                    LaunchBrowse(dir);
                    return true;
                }

                break;
        }
#pragma warning restore SA1025 // Code should not contain multiple whitespace in a row

        Application.Run(new FormCommandlineHelp { StartPosition = FormStartPosition.CenterScreen });
        return true;
    }

    private static bool UninstallEditor()
    {
        GitConfigSettings globalSettings = new(new Executable(AppSettings.GitCommand), GitSettingLevel.Global);
        var coreEditor = globalSettings.GetValue("core.editor");
        var path = AppSettings.GetInstallDir().ToPosixPath();
        if (path is not null && coreEditor?.Contains(path, StringComparison.InvariantCultureIgnoreCase) is true)
        {
            globalSettings.SetValue("core.editor", value: null);
            globalSettings.Save();
        }

        return true;
    }

    private bool RunMergeCommand(IReadOnlyDictionary<string, string?> arguments)
    {
        arguments.TryGetValue("branch", out var branch);

        return StartMergeBranchDialog(null, branch);
    }

    private bool RunSearchFileCommand()
    {
        SearchWindow<string> searchWindow = new(FindFileMatches);
        Application.Run(searchWindow);
        if (searchWindow.SelectedItem is not null)
        {
            // We need to return the file that has been found, the visual studio plugin uses the return value
            // to open the selected file.
            Console.WriteLine(Path.Combine(Module.WorkingDir, searchWindow.SelectedItem));
            return true;
        }

        return false;
    }

    private bool RunBrowseCommand(IReadOnlyList<string> args)
    {
        var arg = GetParameterOrEmptyStringAsDefault(args, "-commit");
        if (arg == "")
        {
            return StartBrowseDialog(owner: null,
                new BrowseArguments
                {
                    RevFilter = GetParameterOrEmptyStringAsDefault(args, "-filter"),
                    PathFilter = GetParameterOrEmptyStringAsDefault(args, _pathFilterArg),
                    IsFileHistoryMode = args.Any(arg => arg.StartsWith(_pathFilterArg))
                });
        }

        if (TryGetObjectIds(arg, Module, out var selectedId, out var firstId))
        {
            return StartBrowseDialog(owner: null,
                new BrowseArguments
                {
                    RevFilter = GetParameterOrEmptyStringAsDefault(args, "-filter"),
                    PathFilter = GetParameterOrEmptyStringAsDefault(args, _pathFilterArg),
                    SelectedId = selectedId,
                    FirstId = firstId,
                    IsFileHistoryMode = args.Any(arg => arg.StartsWith(_pathFilterArg))
                });
        }

        Console.Error.WriteLine($"No commit found matching: {arg}");
        return false;

        static bool TryGetObjectIds(string arg, IGitModule module, out ObjectId selectedId, out ObjectId firstId)
        {
            selectedId = default;
            firstId = default;
            foreach (var part in arg.LazySplit(','))
            {
                if (!module.TryResolvePartialCommitId(part, out var objectId))
                {
                    return false;
                }

                if (selectedId.IsZero)
                {
                    selectedId = objectId;
                }
                else if (firstId.IsZero)
                {
                    firstId = objectId;

                    // just ignore further commits
                    break;
                }
            }

            return true;
        }
    }

    private static string GetParameterOrEmptyStringAsDefault(IReadOnlyList<string> args, string paramName)
    {
        var withEquals = paramName + "=";

        for (var i = 2; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(withEquals))
            {
                return arg.Replace(withEquals, "");
            }
        }

        return "";
    }

    private bool RunOpenRepoCommand(IReadOnlyList<string> args)
    {
        IGitUiCommands c = this;
        if (args.Count > 2)
        {
            if (File.Exists(args[2]))
            {
                var path = File.ReadAllText(args[2]).Trim().LazySplit('\n').FirstOrDefault();
                if (Directory.Exists(path))
                {
                    c = WithWorkingDirectory(path);
                }
            }
        }

        return c.StartBrowseDialog(null,
            new BrowseArguments
            {
                RevFilter = GetParameterOrEmptyStringAsDefault(args, "-filter"),
                PathFilter = GetParameterOrEmptyStringAsDefault(args, _pathFilterArg)
            });
    }

    private bool RunSynchronizeCommand(IReadOnlyDictionary<string, string?> arguments)
    {
        var successful = true;
        successful = Commit(arguments) && successful;
        successful = Pull(arguments) && successful;
        successful = Push(arguments) && successful;
        return successful;
    }

    private bool RunRebaseCommand(IReadOnlyDictionary<string, string?> arguments)
    {
        arguments.TryGetValue("branch", out var branch);

        return StartRebaseDialog(owner: null, onto: branch);
    }

    /// <summary>
    ///     Remove working directory from filename and convert to POSIX path.
    ///     This is to prevent filenames that are too long while there is room left when the workingdir was not in the path.
    /// </summary>
    private string NormalizeFileName(string fileName)
    {
        fileName = fileName.ToPosixPath();
        return string.IsNullOrEmpty(Module.WorkingDir)
            ? fileName
            : fileName.Replace(Module.WorkingDir.ToPosixPath(), "");
    }

    /// <returns>false on error.</returns>
    private bool RunFileHistoryCommand(IReadOnlyList<string> args, bool showBlame)
    {
        // Use the capitalization of the filename as passed because filenames in Git may differ from Windows file system.
        var fileHistoryFileName = NormalizeFileName(args[2]);

        if (string.IsNullOrWhiteSpace(fileHistoryFileName))
        {
            return false;
        }

        GitRevision? revision = null;
        if (args.Count > 3)
        {
            if (!ObjectId.TryParse(args[3], out var objectId))
            {
                return false;
            }

            revision = new GitRevision(objectId);
        }

        var filterByRevision = false;
        if (args.Count > 4)
        {
            if (args[4] != _filterByRevisionArg)
            {
                return false;
            }

            filterByRevision = true;
        }

        // Similar to StartFileHistoryDialog()
        if (AppSettings.UseBrowseForFileHistory.Value)
        {
            // NOTE: fileHistoryFileName doesn't need to be quoted, as it the filter will get quoted
            // when the filter gets set.

            ShowModelessForm(owner: null, requiresValidWorkingDir: true, preEvent: null, postEvent: null,
                () => new FormBrowse(commands: this,
                    new BrowseArguments
                    {
                        RevFilter = filterByRevision ? revision?.ObjectId.ToString() : null,
                        PathFilter = fileHistoryFileName,
                        SelectedId = revision?.ObjectId ?? default,
                        IsFileHistoryMode = true
                    }));
        }
        else
        {
            // NOTE: fileHistoryFileName must be quoted.

            ShowModelessForm(owner: null, requiresValidWorkingDir: true, preEvent: null, postEvent: null,
                () => new FormFileHistory(this, fileHistoryFileName.QuoteNe(), revision, filterByRevision, showBlame));
        }

        return true;
    }

    private bool RunCloneCommand(IReadOnlyList<string> args)
    {
        return StartCloneDialog(null, args.Count > 2 ? args[2] : null);
    }

    private bool RunInitCommand(IReadOnlyList<string> args)
    {
        return StartInitializeDialog(null, args.Count > 2 ? args[2] : null);
    }

    /// <returns>false on error.</returns>
    private bool RunBlameCommand(IReadOnlyList<string> args)
    {
        var blameFileName = NormalizeFileName(args[2]);

        int? initialLine = null;
        if (args.Count > 3)
        {
            if (int.TryParse(args[3], out var temp))
            {
                initialLine = temp;
            }
        }

        return DoActionOnRepo(null, () =>
        {
            using FormBlame frm = new(this, blameFileName, null, initialLine);
            frm.ShowDialog(null);
            return true;
        }, changesRepo: false);
    }

    private bool RunMergeToolOrConflictCommand(IReadOnlyDictionary<string, string?> arguments)
    {
        if (!arguments.ContainsKey("quiet") || Module.InTheMiddleOfConflictedMerge())
        {
            return StartResolveConflictsDialog();
        }

        return true;
    }

    private static IReadOnlyDictionary<string, string?> InitializeArguments(IReadOnlyList<string> args)
    {
        Dictionary<string, string?> arguments = [];

        for (var i = 2; i < args.Count; i++)
        {
            if (args[i].StartsWith("--") && i + 1 < args.Count && !args[i + 1].StartsWith("--"))
            {
                arguments.Add(args[i].TrimStart('-'), args[++i]);
            }
            else if (args[i].StartsWith("--"))
            {
                arguments.Add(args[i].TrimStart('-'), null);
            }
        }

        return arguments;
    }

    private IEnumerable<string> FindFileMatches(string name)
    {
        var candidates = Module.GetFullTree("HEAD");

        var predicate = _findFilePredicateProvider.Get(name, Module.WorkingDir);

        return candidates.Where(predicate);
    }

    private bool Commit(IReadOnlyDictionary<string, string?> arguments)
    {
        arguments.TryGetValue("message", out var overridingMessage);
        var showOnlyWhenChanges = arguments.ContainsKey("quiet");
        return StartCommitDialog(null, overridingMessage, showOnlyWhenChanges);
    }

    private bool Push(IReadOnlyDictionary<string, string?> arguments)
    {
        return StartPushDialog(null, arguments.ContainsKey("quiet"));
    }

    private bool Pull(IReadOnlyDictionary<string, string?> arguments)
    {
        UpdateSettingsBasedOnArguments(arguments);

        arguments.TryGetValue("remotebranch", out var remoteBranch);

        var isQuiet = arguments.ContainsKey("quiet");

        if (isQuiet)
        {
            return StartPullDialogAndPullImmediately(remoteBranch: remoteBranch);
        }

        return StartPullDialog(remoteBranch: remoteBranch);
    }

    private static void UpdateSettingsBasedOnArguments(IReadOnlyDictionary<string, string?> arguments)
    {
        if (arguments.ContainsKey("merge"))
        {
            AppSettings.DefaultPullAction = GitPullAction.Merge;
        }

        if (arguments.ContainsKey("rebase"))
        {
            AppSettings.DefaultPullAction = GitPullAction.Rebase;
        }

        if (arguments.ContainsKey("fetch"))
        {
            AppSettings.DefaultPullAction = GitPullAction.Fetch;
        }

        if (arguments.ContainsKey("autostash"))
        {
            AppSettings.AutoStash = true;
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    #region Nested class: GitRemoteCommand

    private sealed class GitRemoteCommand : IGitRemoteCommand
    {
        private readonly IGitUiCommands _commands;

        internal GitRemoteCommand(IGitUiCommands commands)
        {
            _commands = commands;
        }

        private bool ErrorOccurred { get; set; }

        private string? CommandOutput { get; set; }
        object? IGitRemoteCommand.OwnerForm { get; set; }
        string? IGitRemoteCommand.Remote { get; set; }
        string? IGitRemoteCommand.Title { get; set; }
        string? IGitRemoteCommand.CommandText { get; set; }

        bool IGitRemoteCommand.ErrorOccurred
        {
            get => ErrorOccurred;
        }

        string? IGitRemoteCommand.CommandOutput
        {
            get => CommandOutput;
        }

        event EventHandler<GitRemoteCommandCompletedEventArgs>? IGitRemoteCommand.Completed
        {
            add => Completed += value;
            remove => Completed -= value;
        }

        void IGitRemoteCommand.Execute()
        {
            if (CommandText is null)
            {
                throw new InvalidOperationException("CommandText is required");
            }

            using FormRemoteProcess form = new(_commands, CommandText);
            if (Title is not null)
            {
                form.Text = Title;
            }

            if (Remote is not null)
            {
                form.Remote = Remote;
            }

            form.HandleOnExitCallback = HandleOnExit;

            form.ShowDialog(OwnerForm as IWin32Window);

            ErrorOccurred = form.ErrorOccurred();
            CommandOutput = form.GetOutputString();
        }

        private event EventHandler<GitRemoteCommandCompletedEventArgs>? Completed;

        private bool HandleOnExit(ref bool isError, FormProcess form)
        {
            CommandOutput = form.GetOutputString();

            GitRemoteCommandCompletedEventArgs e = new(this, isError, false);

            Completed?.Invoke(form, e);

            isError = e.IsError;

            return e.Handled;
        }
    }

    #endregion

    internal readonly struct TestAccessor
    {
        private readonly GitUiCommands _commands;

        internal TestAccessor(GitUiCommands commands)
        {
            _commands = commands;
        }

        internal readonly string NormalizeFileName(string fileName)
        {
            return _commands.NormalizeFileName(fileName);
        }

        internal readonly bool RunCommandBasedOnArgument(string[] args)
        {
            return _commands.RunCommandBasedOnArgument(args, InitializeArguments(args));
        }

        internal readonly void ShowFileHistoryDialog(string fileName)
        {
            _commands.RunFileHistoryCommand(new[] { "", "", fileName }, false);
        }
    }

    #region Events

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

    #endregion

    #region Checkout

    bool IGitUiCommands.StartCheckoutBranch(IWin32Window? owner, string branch = "", bool remote = false,
        IReadOnlyList<ObjectId>? containObjectIds = null)
    {
        return DoActionOnRepo(owner, () =>
        {
            using FormCheckoutBranch form = new(this, branch, remote, containObjectIds);
            return form.DoDefaultActionOrShow(owner) != DialogResult.Cancel;
        }, preEvent: PreCheckoutBranch, postEvent: PostCheckoutBranch);
    }

    bool IGitUiCommands.StartCheckoutBranch(IWin32Window? owner, IReadOnlyList<ObjectId>? containObjectIds)
    {
        return StartCheckoutBranch(owner, "", false, containObjectIds);
    }

    bool IGitUiCommands.StartCheckoutRemoteBranch(IWin32Window? owner, string branch)
    {
        return StartCheckoutBranch(owner, branch, true);
    }

    #endregion
}
