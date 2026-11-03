using GitCommands;
using GitExtensions.Extensibility.Translations;
using ResourceManager;

namespace GitUI;

internal sealed class TranslatedStrings : Translate
{
    private readonly TranslationString _error = new("Error");
    private readonly TranslationString _warning = new("Warning");
    private readonly TranslationString _yes = new("Yes");
    private readonly TranslationString _no = new("No");
    private readonly TranslationString _okText = new("OK");
    private readonly TranslationString _cancelText = new("Cancel");
    private readonly TranslationString _closeText = new("Close");
    private readonly TranslationString _dontShowAgain = new("&Don't show me this message again");

    private readonly TranslationString _buttonCheckoutBranch = new("Checkout branch");
    private readonly TranslationString _buttonContinue = new("Continue");
    private readonly TranslationString _buttonCreateBranch = new("Create branch");
    private readonly TranslationString _initializeRepository = new("Initialize repository...");
    private readonly TranslationString _repositoryDirectory = new("Repository directory");
    private readonly TranslationString _browseForDirectory = new("Browse...");
    private readonly TranslationString _bareRepository = new("Create a bare repository");
    private readonly TranslationString _chooseDirectory = new("Please choose a directory.");
    private readonly TranslationString _cannotInitializeRepositoryOnFile = new("Cannot initialize a repository on a file.");
    private readonly TranslationString _buttonIgnore = new("Ignore");
    private readonly TranslationString _buttonPush = new("&Push");
    private readonly TranslationString _buttonReportBug = new("Report bug!");
    private readonly TranslationString _buttonViewDetails = new("View details");

    private readonly TranslationString _containedInCurrentCommitText = new("'{0}' is contained in the currently selected commit");
    private readonly TranslationString _containedInBranchesText = new("Contained in branches:");
    private readonly TranslationString _containedInNoBranchText = new("Contained in no branch");
    private readonly TranslationString _containedInTagsText = new("Contained in tags:");
    private readonly TranslationString _containedInNoTagText = new("Contained in no tag");
    private readonly TranslationString _invisibleCommitText = new("'{0}' is not currently visible");
    private readonly TranslationString _addUpstreamRemote = new("Add upstream remote");
    private readonly TranslationString _viewPullRequest = new("View pull requests");
    private readonly TranslationString _createPullRequest = new("Create pull request");
    private readonly TranslationString _forkCloneRepo = new("Fork or clone a repository");
    private readonly TranslationString _branchText = new("Branch");
    private readonly TranslationString _branchesText = new("Branches");
    private readonly TranslationString _remotesText = new("Remotes");
    private readonly TranslationString _tagsText = new("Tags");
    private readonly TranslationString _stashesText = new("Stashes");
    private readonly TranslationString _submodulesText = new("Submodules");
    private readonly TranslationString _removeSubmodule = new("Remove");
    private readonly TranslationString _confirmRemoveSubmodule = new("Remove submodule '{0}' from this repository? Its working directory will not be deleted.");
    private readonly TranslationString _addSubmodule = new("Add submodule");
    private readonly TranslationString _submoduleUrl = new("Path to submodule");
    private readonly TranslationString _submoduleLocalPath = new("Local path");
    private readonly TranslationString _submoduleBranch = new("Branch (optional)");
    private readonly TranslationString _submoduleForce = new("Force");
    private readonly TranslationString _submoduleUpdate = new("Update / initialize");
    private readonly TranslationString _submoduleSync = new("Synchronize URLs");
    private readonly TranslationString _submoduleNotInitialized = new("Not initialized");
    private readonly TranslationString _submoduleModified = new("Modified");
    private readonly TranslationString _submoduleUpToDate = new("Up-to-date");
    private readonly TranslationString _noSubmodulesConfigured = new("No submodules are configured.");
    private readonly TranslationString _formatPatchMenu = new("Format patch...");
    private readonly TranslationString _applyPatchMenu = new("Apply patch file...");
    private readonly TranslationString _viewPatchMenu = new("View patch file...");
    private readonly TranslationString _archiveRevisionMenu = new("Archive revision...");
    private readonly TranslationString _archiveRevisionTitle = new("Archive revision");
    private readonly TranslationString _selectOnlyOneOrTwoRevisions = new("Select only one or two revisions. Abort.");
    private readonly TranslationString _archiveFormat = new("Archive format");
    private readonly TranslationString _archiveSpecificPathsOnly = new("Archive specific paths only");
    private readonly TranslationString _archiveChangedPathsOnly = new("Take the files that have changed from the revision above to this one and archive only those");
    private readonly TranslationString _archiveRevisionLabel = new("This revision will be archived:");
    private readonly TranslationString _archiveSaveAs = new("Save as...");
    private readonly TranslationString _saveArchiveAs = new("Save archive as");
    private readonly TranslationString _zipFileFilter = new("Zip file (*.zip)");
    private readonly TranslationString _tarFileFilter = new("Tar file (*.tar)");
    private readonly TranslationString _archiveFilePathRequired = new("Please choose a local file for the archive.");
    private readonly TranslationString _pluginsMenu = new("&Plugins");
    private readonly TranslationString _verifyRepositoryMenu = new("Verify repository...");
    private readonly TranslationString _gitMaintenanceMenu = new("Git maintenance");
    private readonly TranslationString _compressGitDatabaseMenu = new("Compress git database");
    private readonly TranslationString _deleteIndexLockMenu = new("Delete index.lock");
    private readonly TranslationString _verifyRepositoryTitle = new("Verify repository");
    private readonly TranslationString _verifyOptions = new("Verification options");
    private readonly TranslationString _verifyObjectTypes = new("Object types");
    private readonly TranslationString _verifyShowUnreachable = new("Show unreachable objects");
    private readonly TranslationString _verifyFullCheck = new("Check alternate object pools");
    private readonly TranslationString _verifyNoReflogs = new("Ignore reflogs");
    private readonly TranslationString _verifyShowCommitsAndTags = new("Show commits and tags");
    private readonly TranslationString _verifyShowOtherObjects = new("Show blobs, trees, and other objects");
    private readonly TranslationString _verifyScan = new("Scan");
    private readonly TranslationString _verifySaveLostFound = new("Save to lost-found");
    private readonly TranslationString _verifyRecoverSelected = new("Recover selected objects");
    private readonly TranslationString _verifyPrune = new("Remove dangling objects");
    private readonly TranslationString _verifyDeleteLostFoundTags = new("Delete LOST_FOUND tags");
    private readonly TranslationString _verifySelectObjects = new("Select at least one object to recover.");
    private readonly TranslationString _verifyPruneConfirm = new("Are you sure you want to delete dangling objects?");
    private readonly TranslationString _verifyPruneCaption = new("Remove dangling objects");
    private readonly TranslationString _verifyDeleteTagsConfirm = new("Are you sure you want to delete all LOST_FOUND tags?");
    private readonly TranslationString _verifyDeleteTagsCaption = new("Delete LOST_FOUND tags");
    private readonly TranslationString _cleanupRepositoryMenu = new("Clean working directory...");
    private readonly TranslationString _cleanupRepositoryTitle = new("Clean working directory");
    private readonly TranslationString _cleanupRemoveUntrackedGroup = new("Remove untracked files from working directory");
    private readonly TranslationString _cleanupRemoveAllFiles = new("Remove all untracked files");
    private readonly TranslationString _cleanupRemoveNonIgnoredFiles = new("Remove only non-ignored untracked files");
    private readonly TranslationString _cleanupRemoveIgnoredFiles = new("Remove only ignored untracked files");
    private readonly TranslationString _cleanupRemoveDirectories = new("Remove untracked directories");
    private readonly TranslationString _cleanupSubmodulesRecursive = new("Clean submodules recursively");
    private readonly TranslationString _cleanupIncludePaths = new("Affect the following directory path(s) only:");
    private readonly TranslationString _cleanupExcludePaths = new("Exclude the following file path(s):");
    private readonly TranslationString _cleanupOnePathPerLine = new("(one path per line)");
    private readonly TranslationString _cleanupPreview = new("Preview");
    private readonly TranslationString _cleanupAction = new("Cleanup");
    private readonly TranslationString _cleanupLog = new("Log:");
    private readonly TranslationString _cleanupConfirmQuestion = new("Are you sure you want to cleanup the repository?");
    private readonly TranslationString _cleanupConfirmCaption = new("Cleanup");
    private readonly TranslationString _cleanupPathRequired = new("Add at least one path to each enabled path filter.");
    private readonly TranslationString _cleanupPathOutsideRepository = new("The selected path must be inside the repository.");
    private readonly TranslationString _cleanupNoOutput = new("No untracked files matched the cleanup options.");
    private readonly TranslationString _patchFileReadError = new("Unable to read the selected patch file.");
    private readonly TranslationString _worktreesText = new("Worktrees");
    private readonly TranslationString _bodyNotLoaded = new("\n\nFull message text is not present in older commits.\nSelect this commit to populate the full message.");
    private readonly TranslationString _searchingFor = new("Searching for: ");
    private readonly TranslationString _loadingDataText = new("Loading data...");
    private readonly TranslationString _uninterestingDiffOmitted = new("Uninteresting diff hunks are omitted.");
    private readonly TranslationString _openReport = new("Open report");
    private readonly TranslationString _noResultsFound = new("<No results found>");
    private readonly TranslationString _local = new("Local");
    private readonly TranslationString _tag = new("Tag");
    private readonly TranslationString _remote = new("Remote");
    private readonly TranslationString _openWithGitExtensions = new("&Open with Git Extensions");
    private readonly TranslationString _contScrollToNextFileOnlyWithAlt = new("Enable automatic continuous scroll (without ALT button)");
    private readonly TranslationString _noRevision = new("No revision");

    private readonly TranslationString _authored = new("authored");
    private readonly TranslationString _committed = new("committed");
    private readonly TranslationString _authoredAndCommitted = new("authored and committed");
    private readonly TranslationString _markBisectAsGood = new("Marked as good in bisect");
    private readonly TranslationString _markBisectAsBad = new("Marked as bad in bisect");

    private readonly TranslationString _errorCaptionFailedDeleteFile = new("Failed to delete file");
    private readonly TranslationString _errorCaptionFailedDeleteFolder = new("Failed to delete directory");
    private readonly TranslationString _errorCaptionNotOnBranch = new("Not on a branch");

    private readonly TranslationString _isLocalBranch = new("is a local branch");
    private readonly TranslationString _isRemoteBranch = new("is a remote branch");
    private readonly TranslationString _isTag = new("is a tag");
    private readonly TranslationString _isTrackedBy_Branch_AheadBehind = new("is tracked by [{0}]   {1}");
    private readonly TranslationString _isTracking_Remote = new("is tracking [{0}]");
    private readonly TranslationString _wasTracking_Remote = new("was tracking [{0}], but the remote is gone");

    private readonly TranslationString _mainInstructionNotOnBranch = new("You are not working on a branch");

    private readonly TranslationString _noBranch = new("no branch");
    private readonly TranslationString _removeSelectedInvalidRepository = new("Remove the selected invalid repository");
    private readonly TranslationString _removeAllInvalidRepositories = new("Remove all {0} invalid repositories");
    private readonly TranslationString _open = new("Open");
    private readonly TranslationString _directoryIsNotAValidRepository = new("The selected item is not a valid git repository.");

    private readonly TranslationString _sortBy = new("&Sort by");
    private readonly TranslationString _sortGroupBy = new("&Sort and group by");
    private readonly TranslationString _sortOrder = new("&Sort order");

    private readonly TranslationString _diffSelectedWithRememberedFile = new("&Diff with \"{0}\"");
    private readonly TranslationString _diffWithParent = new("Diff with A ");
    private readonly TranslationString _diffBaseWith = new("Diff BASE with");
    private readonly TranslationString _diffRange = new("Range diff");
    private readonly TranslationString _combinedDiff = new("Combined diff");

    private readonly TranslationString _showDiffForAllParentsText = new("Show file differences for all parents in browse dialog");
    private readonly TranslationString _showDiffForAllParentsTooltip = new(@"Show all differences between the selected commits, not limiting to only one difference.

- For a single selected commit, show the difference with its parent commit.
- For a single selected merge commit, show the difference with all parents.
- For two selected commits with a common ancestor (BASE), show the difference
between the commits as well as the difference from BASE to the selected commits.
See documentation for more details about icons and range diffs.
- For multiple selected commits (up to four), show the difference for
all the first selected with the last selected commit.
- For more than four selected commits, show the difference from the first to
the last selected commit.");

    private readonly TranslationString _stageSelectedLines = new("Stage selected line(s)");
    private readonly TranslationString _unstageSelectedLines = new("Unstage selected line(s)");
    private readonly TranslationString _stageSelectedFiles = new("&Stage selected");
    private readonly TranslationString _unstageSelectedFiles = new("&Unstage selected");
    private readonly TranslationString _stashSelectedFiles = new("Stash selected");
    private readonly TranslationString _stagedChanges = new("Staged changes");
    private readonly TranslationString _unstagedChanges = new("Unstaged changes");
    private readonly TranslationString _commitFiles = new("Commit files");
    private readonly TranslationString _workingTree = new("Working tree");
    private readonly TranslationString _view = new("&View");
    private readonly TranslationString _lightTheme = new("Light theme");
    private readonly TranslationString _darkTheme = new("Dark theme");
    private readonly TranslationString _mergeInProgress = new("Merge in progress");
    private readonly TranslationString _rebaseInProgress = new("Rebase in progress");
    private readonly TranslationString _cherryPickInProgress = new("Cherry-pick in progress");
    private readonly TranslationString _revertInProgress = new("Revert in progress");
    private readonly TranslationString _patchInProgress = new("Patch application in progress");
    private readonly TranslationString _bisectInProgress = new("Bisect in progress");
    private readonly TranslationString _unresolvedConflicts = new("Unresolved conflicts remain");
    private readonly TranslationString _enterBranchNameError = new("Please enter a branch name");
    private readonly TranslationString _invalidBranchNameError = new("The branch name is not valid");
    private readonly TranslationString _checkoutBranchMenu = new("&Checkout");
    private readonly TranslationString _checkoutBranchMergeChangesMenu = new("Checkout with merge of local changes");
    private readonly TranslationString _checkoutBranchStashChangesMenu = new("Stash local changes and checkout...");
    private readonly TranslationString _checkoutBranchDiscardChangesMenu = new("Discard local changes and checkout...");
    private readonly TranslationString _confirmCheckoutDiscardChanges = new("This may discard tracked changes and untracked files that conflict with checkout '{0}'. Continue?");
    private readonly TranslationString _confirmReapplyStashedChanges = new("Checkout to '{0}' succeeded. Reapply the stashed changes? The stash will be kept.");
    private readonly TranslationString _checkoutFailedChangesStashed = new("Checkout failed. Changes included by the auto-stash setting are safe in the stash; excluded files may remain in the working tree.");
    private readonly TranslationString _checkoutRevisionMenu = new("Checkout revision");
    private readonly TranslationString _createBranchMenu = new("Create &branch here...");
    private readonly TranslationString _createBranchWithoutCheckoutMenu = new("Create branch here without checkout...");
    private readonly TranslationString _createOrphanBranchMenu = new("Create orphan branch here...");
    private readonly TranslationString _renameBranchMenu = new("Rename branch");
    private readonly TranslationString _deleteBranchMenu = new("&Delete branch");
    private readonly TranslationString _deleteRemoteBranchMenu = new("Delete remote branch");
    private readonly TranslationString _forceDeleteBranchMenu = new("Force delete branch");
    private readonly TranslationString _branchNamePrompt = new("Branch name:");
    private readonly TranslationString _confirmDeleteBranch = new("Delete branch '{0}'?");
    private readonly TranslationString _confirmDeleteRemoteBranch = new("Delete remote branch '{0}' from '{1}'? This may make commits unreachable. The local branch that tracks it, if any, will remain.");
    private readonly TranslationString _confirmDeleteLocalTrackingBranch = new("Also delete the local tracking branch '{0}'? Git will refuse if it has unmerged commits.");
    private readonly TranslationString _confirmForceDeleteBranch = new("Force-delete branch '{0}' even if it is not fully merged?");
    private readonly TranslationString _branchCheckedOutInWorktree = new("Cannot delete branch '{0}' because it is checked out in a worktree at:\n{1}");
    private readonly TranslationString _createTagMenu = new("Create tag...");
    private readonly TranslationString _deleteTagMenu = new("Delete tag");
    private readonly TranslationString _tagOptionsTitle = new("Create tag");
    private readonly TranslationString _tagNamePrompt = new("Tag name:");
    private readonly TranslationString _annotatedTagOption = new("Create an annotated tag");
    private readonly TranslationString _tagMessagePrompt = new("Tag message:");
    private readonly TranslationString _enterTagNameError = new("Please enter a tag name.");
    private readonly TranslationString _invalidTagNameError = new("The tag name is not valid.");
    private readonly TranslationString _tagAlreadyExistsError = new("A tag with this name already exists.");
    private readonly TranslationString _tagRevisionRequiredError = new("Select a commit to create the tag on.");
    private readonly TranslationString _tagMessageRequiredError = new("Enter a message for the annotated tag.");
    private readonly TranslationString _confirmDeleteTag = new("Delete tag '{0}'? This cannot be undone.");
    private readonly TranslationString _noRemoteConfiguredError = new("No remote is configured for this repository");
    private readonly TranslationString _detachedHeadPushError = new("HEAD is detached; check out a branch before pushing");
    private readonly TranslationString _pullMergeMenu = new("&Pull (merge)");
    private readonly TranslationString _pullRebaseMenu = new("Pull (&rebase)");
    private readonly TranslationString _pushMenu = new("Pu&sh");
    private readonly TranslationString _forcePushMenu = new("Force push (with &lease)");
    private readonly TranslationString _confirmForcePush = new("Force push '{0}' to '{1}'? This can overwrite commits on the remote.");
    private readonly TranslationString _remoteManagementMenu = new("Manage remotes...");
    private readonly TranslationString _remoteManagementTitle = new("Manage remotes");
    private readonly TranslationString _remoteName = new("Remote name:");
    private readonly TranslationString _fetchUrl = new("Fetch URL:");
    private readonly TranslationString _pushUrl = new("Push URL (optional):");
    private readonly TranslationString _addRemote = new("Add remote");
    private readonly TranslationString _saveRemote = new("Save remote");
    private readonly TranslationString _deleteRemote = new("Delete remote");
    private readonly TranslationString _activateRemote = new("Activate remote");
    private readonly TranslationString _deactivateRemote = new("Deactivate remote");
    private readonly TranslationString _remoteInactive = new("{0} (inactive)");
    private readonly TranslationString _remoteNameRequiredError = new("Enter a name for the remote.");
    private readonly TranslationString _remoteUrlRequiredError = new("Enter a fetch URL for the remote.");
    private readonly TranslationString _remoteAlreadyExistsError = new("A remote named \"{0}\" already exists.");
    private readonly TranslationString _confirmDeleteRemote = new("Delete remote '{0}'?");
    private readonly TranslationString _fileHistory = new("History");
    private readonly TranslationString _blameFile = new("Blame");
    private readonly TranslationString _fileHistoryWindowTitle = new("File history: {0}");
    private readonly TranslationString _fileHistoryLoaded = new("File history loaded");
    private readonly TranslationString _autostashRevision = new("autostash");
    private readonly TranslationString _blameSelectedRevision = new("Blame selected revision");
    private readonly TranslationString _blameLoaded = new("Blame loaded");
    private readonly TranslationString _diffSelectedRevision = new("View selected revision's diff");
    private readonly TranslationString _diffLoaded = new("Diff loaded");
    private readonly TranslationString _revisionFilterWatermark = new("Search revisions...");
    private readonly TranslationString _revisionFilterMessage = new("Commit message");
    private readonly TranslationString _revisionFilterAuthor = new("Author");
    private readonly TranslationString _revisionFilterCommitter = new("Committer");
    private readonly TranslationString _applyFilter = new("Filter");
    private readonly TranslationString _clearFilter = new("Clear");
    private readonly TranslationString _nothingToStashError = new("There are no changes to stash");
    private readonly TranslationString _stashChangesMenu = new("&Stash changes...");
    private readonly TranslationString _stashOptionsMenu = new("Stash options...");
    private readonly TranslationString _stashOptionsTitle = new("Stash options");
    private readonly TranslationString _includeUntrackedFilesInStash = new("Include untracked files");
    private readonly TranslationString _keepIndexWhenStashing = new("Keep index");
    private readonly TranslationString _offerResolveStashConflicts = new("Applying this stash caused conflicts. Would you like to open the conflict resolution window?");
    private readonly TranslationString _stashMessagePrompt = new("Stash message (optional):");
    private readonly TranslationString _saveStashOptions = new("Save");
    private readonly TranslationString _stashApplyMenu = new("&Apply stash");
    private readonly TranslationString _stashPopMenu = new("&Pop stash");
    private readonly TranslationString _stashDropMenu = new("&Drop stash");
    private readonly TranslationString _confirmStashDrop = new("Drop '{0}'? The stashed changes will be lost.");
    private readonly TranslationString _discardSelectedFiles = new("&Discard selected");
    private readonly TranslationString _resetAllChangesMenu = new("&Reset all changes...");
    private readonly TranslationString _confirmDiscardChanges = new("Discard changes in {0} file(s)? This cannot be undone.");
    private readonly TranslationString _confirmDiscardChangesAndDelete = new("Discard changes in {0} file(s)? New files will be deleted. This cannot be undone.");
    private readonly TranslationString _confirmResetAllChanges = new("Reset all tracked files to HEAD? All staged and unstaged changes will be lost. This cannot be undone.");
    private readonly TranslationString _undoLastCommitMenu = new("&Undo last commit...");
    private readonly TranslationString _undoLastCommitConfirmation = new("You will still be able to find all the commit's changes in the staging area\n\nDo you want to continue?");
    private readonly TranslationString _undoLastCommitCaption = new("Undo last commit");
    private readonly TranslationString _resetCurrentBranchToRevisionMenu = new("Reset current branch to this commit");
    private readonly TranslationString _resetSoftToRevisionMenu = new("Soft: keep index and working tree");
    private readonly TranslationString _resetMixedToRevisionMenu = new("Mixed: reset index, keep working tree");
    private readonly TranslationString _resetKeepToRevisionMenu = new("Keep: update working tree unless local changes conflict");
    private readonly TranslationString _resetMergeToRevisionMenu = new("Merge: keep non-conflicting local changes");
    private readonly TranslationString _resetHardToRevisionMenu = new("Hard: discard all local changes");
    private readonly TranslationString _confirmHardResetToRevision = new("Reset to this commit and discard all local changes? This cannot be undone.");
    private readonly TranslationString _resetHardToRevisionTitle = new("Reset current branch");
    private readonly TranslationString _cherryPickMenu = new("&Cherry pick this commit");
    private readonly TranslationString _revertCommitMenu = new("&Revert this commit");
    private readonly TranslationString _mainlineParentPromptTitle = new("Select mainline parent");
    private readonly TranslationString _mainlineParentPrompt = new("Mainline parent number (1-{0}):");
    private readonly TranslationString _invalidMainlineParentError = new("Enter a number between 1 and {0}.");
    private readonly TranslationString _mergeBranchMenu = new("&Merge into current branch");
    private readonly TranslationString _resolveMergeConflicts = new("Resolve conflicts...");
    private readonly TranslationString _openMergeTool = new("Open in merge tool");
    private readonly TranslationString _stageConflictResolution = new("Stage selected resolution");
    private readonly TranslationString _useOursResolution = new("Use ours");
    private readonly TranslationString _useTheirsResolution = new("Use theirs");
    private readonly TranslationString _continueAfterResolution = new("All conflicts resolved: continue");
    private readonly TranslationString _mergeStrategyLabel = new("Strategy:");
    private readonly TranslationString _mergeStrategyDefault = new("Default");
    private readonly TranslationString _mergeMessageLabel = new("Merge commit message:");
    private readonly TranslationString _mergeMessageHint = new("Leave empty for the generated message");
    private readonly TranslationString _refreshConflicts = new("Refresh conflicts");
    private readonly TranslationString _mergeOptionsTitle = new("Merge options");
    private readonly TranslationString _mergeOptionsBranchPrompt = new("Merge branch '{0}' into the current branch.");
    private readonly TranslationString _mergeAllowFastForward = new("Allow fast-forward");
    private readonly TranslationString _mergeSquash = new("Squash commits (do not create a merge commit)");
    private readonly TranslationString _mergeNoCommit = new("Do not commit the merge automatically");
    private readonly TranslationString _mergeAllowUnrelatedHistories = new("Allow unrelated histories");
    private readonly TranslationString _mergeExecute = new("Merge");
    private readonly TranslationString _rebaseOntoMenu = new("Rebase current branch onto this branch");
    private readonly TranslationString _rebaseOptionsTitle = new("Rebase options");
    private readonly TranslationString _rebaseOptionsBranchPrompt = new("Rebase the current branch onto '{0}'.");
    private readonly TranslationString _rebaseInteractive = new("Interactive rebase (use the configured editor to edit the commit plan)");
    private readonly TranslationString _rebaseAutosquash = new("Autosquash fixup commits");
    private readonly TranslationString _rebaseSpecificRange = new("Rebase commits after a specific revision");
    private readonly TranslationString _rebaseRangeUpstreamPrompt = new("Upstream revision (exclusive)");
    private readonly TranslationString _rebaseRangeUpstreamRequired = new("Enter the upstream revision for the rebase range.");
    private readonly TranslationString _rebaseAutoStash = new("Stash and restore local changes automatically");
    private readonly TranslationString _rebaseExecute = new("Rebase");
    private readonly TranslationString _editRebaseTodo = new("Edit todo...");
    private readonly TranslationString _rebaseDetachedHeadError = new("Cannot rebase while HEAD is detached.");
    private readonly TranslationString _rebaseTargetRequired = new("Select a branch to rebase onto.");
    private readonly TranslationString _rebaseSameBranchError = new("The current branch cannot be rebased onto itself.");
    private readonly TranslationString _rebaseOperationInProgressError = new("Another Git operation is already in progress.");
    private readonly TranslationString _commitButton = new("&Commit");
    private readonly TranslationString _commitMessageWatermark = new("Enter commit message");
    private readonly TranslationString _commitMessageHistory = new("Previous commit messages");
    private readonly TranslationString _commitTemplates = new("Commit templates");
    private readonly TranslationString _gitCommitTemplate = new("Git commit template");
    private readonly TranslationString _commitAuthor = new("Author (name <email>)");
    private readonly TranslationString _commitGpgSigning = new("Commit signing");
    private readonly TranslationString _commitGpgDefault = new("Use Git configuration");
    private readonly TranslationString _commitGpgDisabled = new("Do not sign");
    private readonly TranslationString _commitGpgDefaultKey = new("Sign with default key");
    private readonly TranslationString _commitGpgSpecificKey = new("Sign with specific key");
    private readonly TranslationString _commitGpgKeyId = new("GPG key ID");
    private readonly TranslationString _commitResetAuthor = new("Reset author to committer");
    private readonly TranslationString _amendCommitConfirmation = new("You are about to rewrite history." + Environment.NewLine
        + "Only use Amend if the commit has not been published yet!" + Environment.NewLine
        + Environment.NewLine
        + "Do you want to continue?");
    private readonly TranslationString _amendCommitCaption = new("Amend commit");
    private readonly TranslationString _commitAndPush = new("Commit and push");
    private readonly TranslationString _commitValidationCaption = new("Commit validation");
    private readonly TranslationString _commitMsgFirstLineInvalid = new("First line of commit message contains too many characters."
        + Environment.NewLine + "Do you want to continue?");
    private readonly TranslationString _commitMsgLineInvalid = new("The following line of commit message contains too many characters:"
        + Environment.NewLine + Environment.NewLine + "{0}" + Environment.NewLine + Environment.NewLine + "Do you want to continue?");
    private readonly TranslationString _commitMsgSecondLineNotEmpty = new("Second line of commit message is not empty."
        + Environment.NewLine + "Do you want to continue?");
    private readonly TranslationString _commitMsgRegExNotMatched = new("Commit message does not match RegEx."
        + Environment.NewLine + "Do you want to continue?");
    private readonly TranslationString _amendCommitOption = new("&Amend last commit");
    private readonly TranslationString _signOffCommitOption = new("S&ign off");
    private readonly TranslationString _noVerifyCommitOption = new("&Skip hooks (--no-verify)");
    private readonly TranslationString _enterCommitMessageError = new("Please enter commit message");
    private readonly TranslationString _noStagedChangesError = new("There are no staged changes");
    private readonly TranslationString _unrecognizedConflictOperation = new("Unresolved conflicts are present; the active operation could not be identified");
    private readonly TranslationString _unrecognizedOperationInProgress = new("A Git operation is in progress, but its state could not be identified");
    private readonly TranslationString _recoveryActionStateChanged = new("The repository operation changed; no recovery command was run");
    private readonly TranslationString _continueRebase = new("&Continue rebase");
    private readonly TranslationString _skipRebase = new("&Skip commit");
    private readonly TranslationString _abortRebase = new("&Abort rebase");
    private readonly TranslationString _abortMerge = new("&Abort merge");
    private readonly TranslationString _abortOperation = new("&Abort operation");
    private readonly TranslationString _abortPatch = new("&Abort patch");
    private readonly TranslationString _resetSelectedLines = new("Reset selected line(s)");
    private readonly TranslationString _resetSelectedLinesConfirmation = new("Are you sure you want to reset the changes to the selected lines?");
    private readonly TranslationString _resetChangesCaption = new("Reset changes");

    private readonly TranslationString _rotInactive = new("[ Inactive ]");

    private readonly TranslationString _argumentsText = new("Arguments");
    private readonly TranslationString _commandText = new("Command");
    private readonly TranslationString _exitCodeText = new("Exit code");
    private readonly TranslationString _workingDirectoryText = new("Working directory");
    private readonly TranslationString _reportBugText = new("If you think this was caused by Git Extensions, you can report a bug for the team to investigate.");
    private readonly TranslationString _reportIssueDescription = new($"for the team to investigate - if you think this was caused by {AppSettings.ApplicationName}");
    private readonly TranslationString _externalErrorDescription = new("error from git or from other external application");

    private readonly TranslationString _filterFileInGrid = new("Filter file in &grid");

    private readonly TranslationString _remoteInError = new("{0}\n\nRemote: {1}");

    private readonly TranslationString _noChanges = new("No changes");

    private readonly TranslationString _settingsTypeToFind = new("Type to find");

    // FormRevisionFilter
    private readonly TranslationString _since = new("Since");
    private readonly TranslationString _until = new("Until");
    private readonly TranslationString _author = new("Author");
    private readonly TranslationString _committer = new("Committer");
    private readonly TranslationString _message = new("Message");
    private readonly TranslationString _diffContent = new("Diff contains");
    private readonly TranslationString _pathFilter = new("Path filter");
    private readonly TranslationString _showOnlyFirstParent = new("Show only first parent");
    private readonly TranslationString _showReflog = new("Show reflog");
    private readonly TranslationString _showReflogTooltip = new("Show all reflog references");
    private readonly TranslationString _showReflogMenu = new("Show reflo&g...");
    private readonly TranslationString _reflogWindowTitle = new("Reflog");
    private readonly TranslationString _reflogReference = new("Reference");
    private readonly TranslationString _reflogSha = new("SHA");
    private readonly TranslationString _reflogAction = new("Action");
    private readonly TranslationString _commandLogTitle = new("Git Command Log");
    private readonly TranslationString _showCurrentBranchOnly = new("Show current branch only");
    private readonly TranslationString _simplifyByDecoration = new("Simplify by decoration");

    private readonly TranslationString _stashDropConfirmTitle = new("Drop Stash Confirmation");
    private readonly TranslationString _cannotBeUndone = new("This action cannot be undone.");
    private readonly TranslationString _areYouSure = new("Are you sure you want to drop the stash? This action cannot be undone.");

    private readonly TranslationString _switchWorktreeConfirmation = new("Switch to worktree at {0}?");
    private readonly TranslationString _switchWorktreeCaption = new("Open worktree");
    private readonly TranslationString _deleteWorktreeConfirmation = new("Are you sure you want to delete the worktree at {0}?");
    private readonly TranslationString _deleteWorktreeCaption = new("Delete worktree");
    private readonly TranslationString _deleteWorktreeFailed = new("Failed to delete worktree: {0}");
    private readonly TranslationString _deleteMainWorktreeRefused = new("{0} is the main worktree of the repository.\n\nDeleting it would delete the repository itself, with all of its branches and stashes, so git refuses to remove it as well.");

    private readonly TranslationString _createWorktree = new("Create worktree...");
    private readonly TranslationString _pruneWorktrees = new("Prune worktrees");
    private readonly TranslationString _manageWorktrees = new("Manage worktrees...");
    private readonly TranslationString _worktreeDirectoryNotFound = new("Worktree directory does not exist: {0}");

    private readonly TranslationString _nonexistingGitRevision = new("Git revision does not exist");
    private readonly TranslationString _ignoringReference = new("\"{0}\" is not a Git revision and will be ignored.");

    private readonly TranslationString _errorPuTTYNotFound = new("SSH agent could not be found");
    private readonly TranslationString _errorSshKeyNotFound = new("SSH key file could not be found");
    private readonly TranslationString _errorSshPuTTYInstalled = new("Is PuTTY installed?");
    private readonly TranslationString _errorSshPuTTYNotConfigured = new("PuTTY is not configured as SSH client");
    private readonly TranslationString _errorSshPuTTYWhereConfigure = new("SSH client can be configured in Settings > SSH.");

    // Dubious ownership popup
    private readonly TranslationString _gitSecurityError = new("Git security error");
    private readonly TranslationString _gitDubiousOwnershipHeader = new("Do you want to trust this repository by adding a security exception?");
    private readonly TranslationString _gitDubiousOwnershipText = new(@"Git detected a security problem that prevents opening the repository.
Git-tracked directories are considered unsafe if they are owned by someone other than the current user.

To be able to open this repository, you need to either:
- add a security exception for the repository to make git trust it, or
- correct the ownership of the repository.");
    private readonly TranslationString _gitDubiousOwnershipTrustRepository = new("Trust this repository");
    private readonly TranslationString _gitDubiousOwnershipOpenRepositoryFolder = new("Open repository in Explorer");
    private readonly TranslationString _gitDubiousOwnershipSeeGitCommandOutput = new("See git command output...");
    private readonly TranslationString _gitDubiousOwnershipHideGitCommandOutput = new("Hide git command output...");

    private readonly TranslationString _seeErrorMessage = new("See error message...");
    private readonly TranslationString _hideErrorMessage = new("Hide error message...");
    private readonly TranslationString _reportIssue = new("Report the issue");
    private readonly TranslationString _reportReproducedIssueDescription = new("If you can consistently reproduce the issue, please report it.");
    private readonly TranslationString _restartApplication = new($"Restart {AppSettings.ApplicationName}");
    private readonly TranslationString _restartApplicationDescription = new("Usually restarting the application solves the issue.\r\nSometimes Windows may need to be restarted too.");
    private readonly TranslationString _failedToLoadAnAssembly = new("Failed to load an assembly");
    private readonly TranslationString _failedToLoadFileOrAssemblyFormat = new("Could not load file or assembly '{0}'.");
    private readonly TranslationString _failedToLoadFileOrAssemblyText = new("Most of the times the error is temporary, likely caused by Windows Update.");
    private readonly TranslationString _notConfigured = new("{0} not configured");

    private readonly TranslationString _failedToLoadPlugin = new("Plugin loading failure");
    private readonly TranslationString _failedToLoadPluginPopupText = new(@"Fail to load a plugin. Error:

{0}

Copy error details to clipboard?");

    private readonly TranslationString _remoteLabel = new("Remote:");
    private readonly TranslationString _remoteBranchLabel = new("Remote branch:");
    private readonly TranslationString _remoteBranchHint = new("Leave empty for the default branch");
    private readonly TranslationString _tagsLabel = new("Tags:");
    private readonly TranslationString _tagsDefault = new("Default");
    private readonly TranslationString _tagsAll = new("Fetch all tags");
    private readonly TranslationString _tagsNone = new("Fetch no tags");
    private readonly TranslationString _pullOptionsTitle = new("Pull");
    private readonly TranslationString _pullModeMerge = new("Merge fetched changes");
    private readonly TranslationString _pullModeRebase = new("Rebase onto fetched changes");
    private readonly TranslationString _pullModeFetchOnly = new("Fetch only");
    private readonly TranslationString _pullUnshallow = new("Fetch the full history (unshallow)");
    private readonly TranslationString _pullPrune = new("Prune remote-tracking branches");
    private readonly TranslationString _pullAutoStash = new("Auto-stash local changes");
    private readonly TranslationString _pullExecute = new("Pull");
    private readonly TranslationString _pushOptionsTitle = new("Push");
    private readonly TranslationString _pushScopeCurrentBranch = new("Current branch ({0})");
    private readonly TranslationString _pushScopeAllBranches = new("All branches");
    private readonly TranslationString _pushScopeTags = new("All tags");
    private readonly TranslationString _pushTrackUpstream = new("Track the remote branch as upstream");
    private readonly TranslationString _pushExecute = new("Push");
    private readonly TranslationString _forceLabel = new("Force:");
    private readonly TranslationString _forceNone = new("Do not force");
    private readonly TranslationString _forceWithLease = new("Force with lease");
    private readonly TranslationString _forceFull = new("Force");
    private readonly TranslationString _submodulesLabel = new("Submodules:");
    private readonly TranslationString _submodulesNone = new("Ignore");
    private readonly TranslationString _submodulesCheck = new("Check that they are pushed");
    private readonly TranslationString _submodulesOnDemand = new("Push on demand");

    private readonly TranslationString _settingsTitle = new("Settings");
    private readonly TranslationString _settingsApplicationTab = new("Application");
    private readonly TranslationString _settingsGitTab = new("Git configuration");
    private readonly TranslationString _settingsScopeLabel = new("Configuration level:");
    private readonly TranslationString _settingsScopeGlobal = new("Global (user)");
    private readonly TranslationString _settingsScopeLocal = new("Local (repository)");
    private readonly TranslationString _settingsUnset = new("(not set)");
    private readonly TranslationString _settingsSave = new("Save");
    private readonly TranslationString _settingsRebaseAutoStash = new("Auto-stash local changes when rebasing");
    private readonly TranslationString _settingsCloseProcessDialog = new("Close process dialogs when the command succeeds");
    private readonly TranslationString _settingsNormaliseBranchName = new("Normalise new branch names");
    private readonly TranslationString _settingsShowAuthorDate = new("Show the author date in the revision grid");
    private readonly TranslationString _settingsShowGitStatus = new("Show the Git status in the toolbar");
    private readonly TranslationString _settingsMenu = new("&Settings...");

    private readonly TranslationString _cherryPickRecordOriginMenu = new("Cherry pick this commit (record origin with -x)");
    private readonly TranslationString _revertNoCommitMenu = new("Revert this commit without committing (edit message later)");
    private readonly TranslationString _cherryPickSelectedMenu = new("Cherry pick the selected commits");
    private readonly TranslationString _revertSelectedMenu = new("Revert the selected commits");

    private readonly TranslationString _diffToolWorkingTreeMenu = new("Open working tree changes in the diff tool");
    private readonly TranslationString _diffToolRevisionsMenu = new("Compare in the diff tool");
    private readonly TranslationString _noDiffToolConfiguredError = new("No diff tool is configured. Set diff.tool in Settings > Git configuration.");

    private readonly TranslationString _commandLogCopy = new("Copy all");
    private readonly TranslationString _commandLogExport = new("Export to file...");

    private readonly TranslationString _patchIgnoreWhitespace = new("Ignore whitespace changes");
    private readonly TranslationString _patchOptionsTitle = new("Apply patch options");
    private readonly TranslationString _patchApplyButton = new("Apply");

    private readonly TranslationString _systemTheme = new("Follow system theme");

    private readonly TranslationString _mergeIncludeLog = new("Include merged commit summaries in the message (--log)");

    private readonly TranslationString _discardSelectedLines = new("Discard selected line(s)");

    private readonly TranslationString _confirmDiscardSelectedLines = new("Discard the selected lines? This cannot be undone.");

    // public only because of FormTranslate
    public TranslatedStrings()
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    private static Lazy<TranslatedStrings> _instance = new();

    public static void Reinitialize()
    {
        if (_instance.IsValueCreated)
        {
            _instance = new();
        }
    }

    public static string Error => _instance.Value._error.Text;
    public static string Warning => _instance.Value._warning.Text;
    public static string Yes => _instance.Value._yes.Text;
    public static string No => _instance.Value._no.Text;
    public static string OK => _instance.Value._okText.Text;
    public static string Cancel => _instance.Value._cancelText.Text;
    public static string RemoteLabel => _instance.Value._remoteLabel.Text;
    public static string RemoteBranchLabel => _instance.Value._remoteBranchLabel.Text;
    public static string RemoteBranchHint => _instance.Value._remoteBranchHint.Text;
    public static string TagsLabel => _instance.Value._tagsLabel.Text;
    public static string TagsDefault => _instance.Value._tagsDefault.Text;
    public static string TagsAll => _instance.Value._tagsAll.Text;
    public static string TagsNone => _instance.Value._tagsNone.Text;
    public static string PullOptionsTitle => _instance.Value._pullOptionsTitle.Text;
    public static string PullModeMerge => _instance.Value._pullModeMerge.Text;
    public static string PullModeRebase => _instance.Value._pullModeRebase.Text;
    public static string PullModeFetchOnly => _instance.Value._pullModeFetchOnly.Text;
    public static string PullUnshallow => _instance.Value._pullUnshallow.Text;
    public static string PullPrune => _instance.Value._pullPrune.Text;
    public static string PullAutoStash => _instance.Value._pullAutoStash.Text;
    public static string PullExecute => _instance.Value._pullExecute.Text;
    public static string PushOptionsTitle => _instance.Value._pushOptionsTitle.Text;
    public static string PushScopeCurrentBranch => _instance.Value._pushScopeCurrentBranch.Text;
    public static string PushScopeAllBranches => _instance.Value._pushScopeAllBranches.Text;
    public static string PushScopeTags => _instance.Value._pushScopeTags.Text;
    public static string PushTrackUpstream => _instance.Value._pushTrackUpstream.Text;
    public static string PushExecute => _instance.Value._pushExecute.Text;
    public static string ForceLabel => _instance.Value._forceLabel.Text;
    public static string ForceNone => _instance.Value._forceNone.Text;
    public static string ForceWithLease => _instance.Value._forceWithLease.Text;
    public static string ForceFull => _instance.Value._forceFull.Text;
    public static string SubmodulesLabel => _instance.Value._submodulesLabel.Text;
    public static string SubmodulesNone => _instance.Value._submodulesNone.Text;
    public static string SubmodulesCheck => _instance.Value._submodulesCheck.Text;
    public static string SubmodulesOnDemand => _instance.Value._submodulesOnDemand.Text;
    public static string SettingsTitle => _instance.Value._settingsTitle.Text;
    public static string SettingsApplicationTab => _instance.Value._settingsApplicationTab.Text;
    public static string SettingsGitTab => _instance.Value._settingsGitTab.Text;
    public static string SettingsScopeLabel => _instance.Value._settingsScopeLabel.Text;
    public static string SettingsScopeGlobal => _instance.Value._settingsScopeGlobal.Text;
    public static string SettingsScopeLocal => _instance.Value._settingsScopeLocal.Text;
    public static string SettingsUnset => _instance.Value._settingsUnset.Text;
    public static string SettingsSave => _instance.Value._settingsSave.Text;
    public static string SettingsRebaseAutoStash => _instance.Value._settingsRebaseAutoStash.Text;
    public static string SettingsCloseProcessDialog => _instance.Value._settingsCloseProcessDialog.Text;
    public static string SettingsNormaliseBranchName => _instance.Value._settingsNormaliseBranchName.Text;
    public static string SettingsShowAuthorDate => _instance.Value._settingsShowAuthorDate.Text;
    public static string SettingsShowGitStatus => _instance.Value._settingsShowGitStatus.Text;
    public static string SettingsMenu => _instance.Value._settingsMenu.Text;
    public static string CherryPickRecordOriginMenu => _instance.Value._cherryPickRecordOriginMenu.Text;
    public static string RevertNoCommitMenu => _instance.Value._revertNoCommitMenu.Text;
    public static string CherryPickSelectedMenu => _instance.Value._cherryPickSelectedMenu.Text;
    public static string RevertSelectedMenu => _instance.Value._revertSelectedMenu.Text;
    public static string DiffToolWorkingTreeMenu => _instance.Value._diffToolWorkingTreeMenu.Text;
    public static string DiffToolRevisionsMenu => _instance.Value._diffToolRevisionsMenu.Text;
    public static string NoDiffToolConfiguredError => _instance.Value._noDiffToolConfiguredError.Text;
    public static string CommandLogCopy => _instance.Value._commandLogCopy.Text;
    public static string CommandLogExport => _instance.Value._commandLogExport.Text;
    public static string PatchIgnoreWhitespace => _instance.Value._patchIgnoreWhitespace.Text;
    public static string PatchOptionsTitle => _instance.Value._patchOptionsTitle.Text;
    public static string PatchApplyButton => _instance.Value._patchApplyButton.Text;
    public static string SystemTheme => _instance.Value._systemTheme.Text;
    public static string MergeIncludeLog => _instance.Value._mergeIncludeLog.Text;
    public static string UnstageSelectedLines => _instance.Value._unstageSelectedLines.Text;
    public static string DiscardSelectedLines => _instance.Value._discardSelectedLines.Text;
    public static string ConfirmDiscardSelectedLines => _instance.Value._confirmDiscardSelectedLines.Text;
    public static string Close => _instance.Value._closeText.Text;
    public static string DontShowAgain => _instance.Value._dontShowAgain.Text;

    public static string ButtonContinue => _instance.Value._buttonContinue.Text;
    public static string ButtonCheckoutBranch => _instance.Value._buttonCheckoutBranch.Text;
    public static string ButtonCreateBranch => _instance.Value._buttonCreateBranch.Text;
    public static string InitializeRepository => _instance.Value._initializeRepository.Text;
    public static string RepositoryDirectory => _instance.Value._repositoryDirectory.Text;
    public static string BrowseForDirectory => _instance.Value._browseForDirectory.Text;
    public static string BareRepository => _instance.Value._bareRepository.Text;
    public static string ChooseDirectory => _instance.Value._chooseDirectory.Text;
    public static string CannotInitializeRepositoryOnFile => _instance.Value._cannotInitializeRepositoryOnFile.Text;
    public static string ButtonIgnore => _instance.Value._buttonIgnore.Text;
    public static string ButtonPush => _instance.Value._buttonPush.Text;
    public static string ButtonReportBug => _instance.Value._buttonReportBug.Text;
    public static string ButtonViewDetails => _instance.Value._buttonViewDetails.Text;

    public static string ContainedInCurrentCommit => _instance.Value._containedInCurrentCommitText.Text;
    public static string ContainedInBranches => _instance.Value._containedInBranchesText.Text;
    public static string ContainedInNoBranch => _instance.Value._containedInNoBranchText.Text;
    public static string ContainedInTags => _instance.Value._containedInTagsText.Text;
    public static string ContainedInNoTag => _instance.Value._containedInNoTagText.Text;
    public static string InvisibleCommit => _instance.Value._invisibleCommitText.Text;

    public static string CreatePullRequest => _instance.Value._createPullRequest.Text;
    public static string ForkCloneRepo => _instance.Value._forkCloneRepo.Text;
    public static string ViewPullRequest => _instance.Value._viewPullRequest.Text;
    public static string AddUpstreamRemote => _instance.Value._addUpstreamRemote.Text;

    public static string Branch => _instance.Value._branchText.Text;
    public static string Branches => _instance.Value._branchesText.Text;
    public static string Remotes => _instance.Value._remotesText.Text;
    public static string Tags => _instance.Value._tagsText.Text;
    public static string Stashes => _instance.Value._stashesText.Text;
    public static string Submodules => _instance.Value._submodulesText.Text;
    public static string RemoveSubmodule => _instance.Value._removeSubmodule.Text;
    public static string ConfirmRemoveSubmodule => _instance.Value._confirmRemoveSubmodule.Text;
    public static string AddSubmodule => _instance.Value._addSubmodule.Text;
    public static string SubmoduleUrl => _instance.Value._submoduleUrl.Text;
    public static string SubmoduleLocalPath => _instance.Value._submoduleLocalPath.Text;
    public static string SubmoduleBranch => _instance.Value._submoduleBranch.Text;
    public static string SubmoduleForce => _instance.Value._submoduleForce.Text;
    public static string SubmoduleUpdate => _instance.Value._submoduleUpdate.Text;
    public static string SubmoduleSync => _instance.Value._submoduleSync.Text;
    public static string SubmoduleNotInitialized => _instance.Value._submoduleNotInitialized.Text;
    public static string SubmoduleModified => _instance.Value._submoduleModified.Text;
    public static string SubmoduleUpToDate => _instance.Value._submoduleUpToDate.Text;
    public static string NoSubmodulesConfigured => _instance.Value._noSubmodulesConfigured.Text;
    public static string FormatPatchMenu => _instance.Value._formatPatchMenu.Text;
    public static string ApplyPatchMenu => _instance.Value._applyPatchMenu.Text;
    public static string ViewPatchMenu => _instance.Value._viewPatchMenu.Text;
    public static string ArchiveRevisionMenu => _instance.Value._archiveRevisionMenu.Text;
    public static string ArchiveRevisionTitle => _instance.Value._archiveRevisionTitle.Text;
    public static string SelectOnlyOneOrTwoRevisions => _instance.Value._selectOnlyOneOrTwoRevisions.Text;
    public static string ArchiveFormat => _instance.Value._archiveFormat.Text;
    public static string ArchiveSpecificPathsOnly => _instance.Value._archiveSpecificPathsOnly.Text;
    public static string ArchiveChangedPathsOnly => _instance.Value._archiveChangedPathsOnly.Text;
    public static string ArchiveRevisionLabel => _instance.Value._archiveRevisionLabel.Text;
    public static string ArchiveSaveAs => _instance.Value._archiveSaveAs.Text;
    public static string SaveArchiveAs => _instance.Value._saveArchiveAs.Text;
    public static string ZipFileFilter => _instance.Value._zipFileFilter.Text;
    public static string TarFileFilter => _instance.Value._tarFileFilter.Text;
    public static string ArchiveFilePathRequired => _instance.Value._archiveFilePathRequired.Text;
    public static string PluginsMenu => _instance.Value._pluginsMenu.Text;
    public static string VerifyRepositoryMenu => _instance.Value._verifyRepositoryMenu.Text;
    public static string GitMaintenanceMenu => _instance.Value._gitMaintenanceMenu.Text;
    public static string CompressGitDatabaseMenu => _instance.Value._compressGitDatabaseMenu.Text;
    public static string DeleteIndexLockMenu => _instance.Value._deleteIndexLockMenu.Text;
    public static string VerifyRepositoryTitle => _instance.Value._verifyRepositoryTitle.Text;
    public static string VerifyOptions => _instance.Value._verifyOptions.Text;
    public static string VerifyObjectTypes => _instance.Value._verifyObjectTypes.Text;
    public static string VerifyShowUnreachable => _instance.Value._verifyShowUnreachable.Text;
    public static string VerifyFullCheck => _instance.Value._verifyFullCheck.Text;
    public static string VerifyNoReflogs => _instance.Value._verifyNoReflogs.Text;
    public static string VerifyShowCommitsAndTags => _instance.Value._verifyShowCommitsAndTags.Text;
    public static string VerifyShowOtherObjects => _instance.Value._verifyShowOtherObjects.Text;
    public static string VerifyScan => _instance.Value._verifyScan.Text;
    public static string VerifySaveLostFound => _instance.Value._verifySaveLostFound.Text;
    public static string VerifyRecoverSelected => _instance.Value._verifyRecoverSelected.Text;
    public static string VerifyPrune => _instance.Value._verifyPrune.Text;
    public static string VerifyDeleteLostFoundTags => _instance.Value._verifyDeleteLostFoundTags.Text;
    public static string VerifySelectObjects => _instance.Value._verifySelectObjects.Text;
    public static string VerifyPruneConfirm => _instance.Value._verifyPruneConfirm.Text;
    public static string VerifyPruneCaption => _instance.Value._verifyPruneCaption.Text;
    public static string VerifyDeleteTagsConfirm => _instance.Value._verifyDeleteTagsConfirm.Text;
    public static string VerifyDeleteTagsCaption => _instance.Value._verifyDeleteTagsCaption.Text;
    public static string CleanupRepositoryMenu => _instance.Value._cleanupRepositoryMenu.Text;
    public static string CleanupRepositoryTitle => _instance.Value._cleanupRepositoryTitle.Text;
    public static string CleanupRemoveUntrackedGroup => _instance.Value._cleanupRemoveUntrackedGroup.Text;
    public static string CleanupRemoveAllFiles => _instance.Value._cleanupRemoveAllFiles.Text;
    public static string CleanupRemoveNonIgnoredFiles => _instance.Value._cleanupRemoveNonIgnoredFiles.Text;
    public static string CleanupRemoveIgnoredFiles => _instance.Value._cleanupRemoveIgnoredFiles.Text;
    public static string CleanupRemoveDirectories => _instance.Value._cleanupRemoveDirectories.Text;
    public static string CleanupSubmodulesRecursive => _instance.Value._cleanupSubmodulesRecursive.Text;
    public static string CleanupIncludePaths => _instance.Value._cleanupIncludePaths.Text;
    public static string CleanupExcludePaths => _instance.Value._cleanupExcludePaths.Text;
    public static string CleanupOnePathPerLine => _instance.Value._cleanupOnePathPerLine.Text;
    public static string CleanupPreview => _instance.Value._cleanupPreview.Text;
    public static string CleanupAction => _instance.Value._cleanupAction.Text;
    public static string CleanupLog => _instance.Value._cleanupLog.Text;
    public static string CleanupConfirmQuestion => _instance.Value._cleanupConfirmQuestion.Text;
    public static string CleanupConfirmCaption => _instance.Value._cleanupConfirmCaption.Text;
    public static string CleanupPathRequired => _instance.Value._cleanupPathRequired.Text;
    public static string CleanupPathOutsideRepository => _instance.Value._cleanupPathOutsideRepository.Text;
    public static string CleanupNoOutput => _instance.Value._cleanupNoOutput.Text;
    public static string PatchFileReadError => _instance.Value._patchFileReadError.Text;
    public static string Worktrees => _instance.Value._worktreesText.Text;

    public static string BodyNotLoaded => _instance.Value._bodyNotLoaded.Text;
    public static string SearchingFor => _instance.Value._searchingFor.Text;

    public static string LoadingData => _instance.Value._loadingDataText.Text;
    public static string UninterestingDiffOmitted => _instance.Value._uninterestingDiffOmitted.Text;
    public static string NoResultsFound => _instance.Value._noResultsFound.Text;
    public static string Local => _instance.Value._local.Text;
    public static string Tag => _instance.Value._tag.Text;
    public static string Remote => _instance.Value._remote.Text;
    public static string OpenWithGitExtensions => _instance.Value._openWithGitExtensions.Text;
    public static string FilterFileInGrid => _instance.Value._filterFileInGrid.Text;
    public static string ContScrollToNextFileOnlyWithAlt => _instance.Value._contScrollToNextFileOnlyWithAlt.Text;
    public static string NoRevision => _instance.Value._noRevision.Text;

    public static string OpenReport => _instance.Value._openReport.Text;

    public static string Authored => _instance.Value._authored.Text;
    public static string Committed => _instance.Value._committed.Text;
    public static string AuthoredAndCommitted => _instance.Value._authoredAndCommitted.Text;

    public static string MarkBisectAsGood => _instance.Value._markBisectAsGood.Text;
    public static string MarkBisectAsBad => _instance.Value._markBisectAsBad.Text;

    public static string ErrorCaptionFailedDeleteFile => _instance.Value._errorCaptionFailedDeleteFile.Text;
    public static string ErrorCaptionFailedDeleteFolder => _instance.Value._errorCaptionFailedDeleteFolder.Text;
    public static string ErrorCaptionNotOnBranch => _instance.Value._errorCaptionNotOnBranch.Text;

    public static string ErrorFileNotFound => _instance.Value._errorPuTTYNotFound.Text;
    public static string ErrorSshKeyNotFound => _instance.Value._errorSshKeyNotFound.Text;
    public static string ErrorSshPuTTYInstalled => _instance.Value._errorSshPuTTYInstalled.Text;
    public static string ErrorSshPuTTYNotConfigured => _instance.Value._errorSshPuTTYNotConfigured.Text;
    public static string ErrorSshPuTTYWhereConfigure => _instance.Value._errorSshPuTTYWhereConfigure.Text;

    public static string IsLocalBranch => _instance.Value._isLocalBranch.Text;
    public static string IsRemoteBranch => _instance.Value._isRemoteBranch.Text;
    public static string IsTag => _instance.Value._isTag.Text;
    public static string IsTrackedBy_Branch_AheadBehind => _instance.Value._isTrackedBy_Branch_AheadBehind.Text;
    public static string IsTracking_Remote => _instance.Value._isTracking_Remote.Text;
    public static string WasTracking_Remote => _instance.Value._wasTracking_Remote.Text;

    public static string ErrorInstructionNotOnBranch => _instance.Value._mainInstructionNotOnBranch.Text;

    public static string NoBranch => _instance.Value._noBranch.Text;
    public static string RemoveSelectedInvalidRepository => _instance.Value._removeSelectedInvalidRepository.Text;
    public static string RemoveAllInvalidRepositories => _instance.Value._removeAllInvalidRepositories.Text;
    public static string Open => _instance.Value._open.Text;
    public static string DirectoryInvalidRepository => _instance.Value._directoryIsNotAValidRepository.Text;

    public static string SortBy => _instance.Value._sortBy.Text;
    public static string SortGroupBy => _instance.Value._sortGroupBy.Text;
    public static string SortOrder => _instance.Value._sortOrder.Text;

    public static string DiffSelectedWithRememberedFile => _instance.Value._diffSelectedWithRememberedFile.Text;
    public static string DiffWithParent => _instance.Value._diffWithParent.Text;
    public static string DiffBaseWith => _instance.Value._diffBaseWith.Text;
    public static string DiffRange => _instance.Value._diffRange.Text;
    public static string CombinedDiff => _instance.Value._combinedDiff.Text;
    public static string ShowDiffForAllParentsText => _instance.Value._showDiffForAllParentsText.Text;
    public static string ShowDiffForAllParentsTooltip => _instance.Value._showDiffForAllParentsTooltip.Text;

    public static string StageSelectedLines => _instance.Value._stageSelectedLines.Text;
    public static string StageSelectedFiles => _instance.Value._stageSelectedFiles.Text;
    public static string UnstageSelectedFiles => _instance.Value._unstageSelectedFiles.Text;
    public static string StashSelectedFiles => _instance.Value._stashSelectedFiles.Text;
    public static string StagedChanges => _instance.Value._stagedChanges.Text;
    public static string UnstagedChanges => _instance.Value._unstagedChanges.Text;
    public static string CommitFiles => _instance.Value._commitFiles.Text;
    public static string WorkingTree => _instance.Value._workingTree.Text;
    public static string View => _instance.Value._view.Text;
    public static string LightTheme => _instance.Value._lightTheme.Text;
    public static string DarkTheme => _instance.Value._darkTheme.Text;
    public static string MergeInProgress => _instance.Value._mergeInProgress.Text;
    public static string RebaseInProgress => _instance.Value._rebaseInProgress.Text;

    /// <summary>
    ///  Gets the message shown while a cherry-pick operation is in progress.
    /// </summary>
    public static string CherryPickInProgress => _instance.Value._cherryPickInProgress.Text;

    /// <summary>
    ///  Gets the message shown while a revert operation is in progress.
    /// </summary>
    public static string RevertInProgress => _instance.Value._revertInProgress.Text;
    public static string PatchInProgress => _instance.Value._patchInProgress.Text;
    public static string BisectInProgress => _instance.Value._bisectInProgress.Text;
    public static string UnresolvedConflicts => _instance.Value._unresolvedConflicts.Text;
    public static string EnterBranchNameError => _instance.Value._enterBranchNameError.Text;
    public static string InvalidBranchNameError => _instance.Value._invalidBranchNameError.Text;
    public static string CheckoutBranchMenu => _instance.Value._checkoutBranchMenu.Text;
    public static string CheckoutBranchMergeChangesMenu => _instance.Value._checkoutBranchMergeChangesMenu.Text;
    public static string CheckoutBranchStashChangesMenu => _instance.Value._checkoutBranchStashChangesMenu.Text;
    public static string CheckoutBranchDiscardChangesMenu => _instance.Value._checkoutBranchDiscardChangesMenu.Text;
    public static string ConfirmCheckoutDiscardChanges => _instance.Value._confirmCheckoutDiscardChanges.Text;
    public static string ConfirmReapplyStashedChanges => _instance.Value._confirmReapplyStashedChanges.Text;
    public static string CheckoutFailedChangesStashed => _instance.Value._checkoutFailedChangesStashed.Text;
    public static string CheckoutRevisionMenu => _instance.Value._checkoutRevisionMenu.Text;
    public static string CreateBranchMenu => _instance.Value._createBranchMenu.Text;
    public static string CreateBranchWithoutCheckoutMenu => _instance.Value._createBranchWithoutCheckoutMenu.Text;
    public static string CreateOrphanBranchMenu => _instance.Value._createOrphanBranchMenu.Text;
    public static string RenameBranchMenu => _instance.Value._renameBranchMenu.Text;
    public static string DeleteBranchMenu => _instance.Value._deleteBranchMenu.Text;
    public static string DeleteRemoteBranchMenu => _instance.Value._deleteRemoteBranchMenu.Text;
    public static string ForceDeleteBranchMenu => _instance.Value._forceDeleteBranchMenu.Text;
    public static string BranchNamePrompt => _instance.Value._branchNamePrompt.Text;
    public static string ConfirmDeleteBranch => _instance.Value._confirmDeleteBranch.Text;
    public static string ConfirmDeleteRemoteBranch => _instance.Value._confirmDeleteRemoteBranch.Text;
    public static string ConfirmDeleteLocalTrackingBranch => _instance.Value._confirmDeleteLocalTrackingBranch.Text;
    public static string ConfirmForceDeleteBranch => _instance.Value._confirmForceDeleteBranch.Text;
    public static string BranchCheckedOutInWorktree => _instance.Value._branchCheckedOutInWorktree.Text;
    public static string CreateTagMenu => _instance.Value._createTagMenu.Text;
    public static string DeleteTagMenu => _instance.Value._deleteTagMenu.Text;
    public static string TagOptionsTitle => _instance.Value._tagOptionsTitle.Text;
    public static string TagNamePrompt => _instance.Value._tagNamePrompt.Text;
    public static string AnnotatedTagOption => _instance.Value._annotatedTagOption.Text;
    public static string TagMessagePrompt => _instance.Value._tagMessagePrompt.Text;
    public static string EnterTagNameError => _instance.Value._enterTagNameError.Text;
    public static string InvalidTagNameError => _instance.Value._invalidTagNameError.Text;
    public static string TagAlreadyExistsError => _instance.Value._tagAlreadyExistsError.Text;
    public static string TagRevisionRequiredError => _instance.Value._tagRevisionRequiredError.Text;
    public static string TagMessageRequiredError => _instance.Value._tagMessageRequiredError.Text;
    public static string ConfirmDeleteTag => _instance.Value._confirmDeleteTag.Text;
    public static string NoRemoteConfiguredError => _instance.Value._noRemoteConfiguredError.Text;
    public static string DetachedHeadPushError => _instance.Value._detachedHeadPushError.Text;
    public static string PullMergeMenu => _instance.Value._pullMergeMenu.Text;
    public static string PullRebaseMenu => _instance.Value._pullRebaseMenu.Text;
    public static string PushMenu => _instance.Value._pushMenu.Text;
    public static string ForcePushMenu => _instance.Value._forcePushMenu.Text;
    public static string ConfirmForcePush => _instance.Value._confirmForcePush.Text;
    public static string RemoteManagementMenu => _instance.Value._remoteManagementMenu.Text;
    public static string RemoteManagementTitle => _instance.Value._remoteManagementTitle.Text;
    public static string RemoteName => _instance.Value._remoteName.Text;
    public static string FetchUrl => _instance.Value._fetchUrl.Text;
    public static string PushUrl => _instance.Value._pushUrl.Text;
    public static string AddRemote => _instance.Value._addRemote.Text;
    public static string SaveRemote => _instance.Value._saveRemote.Text;
    public static string DeleteRemote => _instance.Value._deleteRemote.Text;
    public static string ActivateRemote => _instance.Value._activateRemote.Text;
    public static string DeactivateRemote => _instance.Value._deactivateRemote.Text;
    public static string RemoteInactive => _instance.Value._remoteInactive.Text;
    public static string RemoteNameRequiredError => _instance.Value._remoteNameRequiredError.Text;
    public static string RemoteUrlRequiredError => _instance.Value._remoteUrlRequiredError.Text;
    public static string RemoteAlreadyExistsError => _instance.Value._remoteAlreadyExistsError.Text;
    public static string ConfirmDeleteRemote => _instance.Value._confirmDeleteRemote.Text;
    public static string FileHistory => _instance.Value._fileHistory.Text;
    public static string BlameFile => _instance.Value._blameFile.Text;
    public static string FileHistoryWindowTitle => _instance.Value._fileHistoryWindowTitle.Text;
    public static string FileHistoryLoaded => _instance.Value._fileHistoryLoaded.Text;
    public static string AutostashRevision => _instance.Value._autostashRevision.Text;
    public static string BlameSelectedRevision => _instance.Value._blameSelectedRevision.Text;
    public static string BlameLoaded => _instance.Value._blameLoaded.Text;
    public static string DiffSelectedRevision => _instance.Value._diffSelectedRevision.Text;
    public static string DiffLoaded => _instance.Value._diffLoaded.Text;
    public static string RevisionFilterWatermark => _instance.Value._revisionFilterWatermark.Text;
    public static string RevisionFilterMessage => _instance.Value._revisionFilterMessage.Text;
    public static string RevisionFilterAuthor => _instance.Value._revisionFilterAuthor.Text;
    public static string RevisionFilterCommitter => _instance.Value._revisionFilterCommitter.Text;
    public static string ApplyFilter => _instance.Value._applyFilter.Text;
    public static string ClearFilter => _instance.Value._clearFilter.Text;
    public static string NothingToStashError => _instance.Value._nothingToStashError.Text;
    public static string StashChangesMenu => _instance.Value._stashChangesMenu.Text;
    public static string StashOptionsMenu => _instance.Value._stashOptionsMenu.Text;
    public static string StashOptionsTitle => _instance.Value._stashOptionsTitle.Text;
    public static string IncludeUntrackedFilesInStash => _instance.Value._includeUntrackedFilesInStash.Text;
    public static string KeepIndexWhenStashing => _instance.Value._keepIndexWhenStashing.Text;
    public static string OfferResolveStashConflicts => _instance.Value._offerResolveStashConflicts.Text;
    public static string StashMessagePrompt => _instance.Value._stashMessagePrompt.Text;
    public static string SaveStashOptions => _instance.Value._saveStashOptions.Text;
    public static string StashApplyMenu => _instance.Value._stashApplyMenu.Text;
    public static string StashPopMenu => _instance.Value._stashPopMenu.Text;
    public static string StashDropMenu => _instance.Value._stashDropMenu.Text;
    public static string ConfirmStashDrop => _instance.Value._confirmStashDrop.Text;
    public static string DiscardSelectedFiles => _instance.Value._discardSelectedFiles.Text;
    public static string ResetAllChangesMenu => _instance.Value._resetAllChangesMenu.Text;
    public static string ConfirmDiscardChanges => _instance.Value._confirmDiscardChanges.Text;
    public static string ConfirmDiscardChangesAndDelete => _instance.Value._confirmDiscardChangesAndDelete.Text;
    public static string ConfirmResetAllChanges => _instance.Value._confirmResetAllChanges.Text;
    public static string UndoLastCommitMenu => _instance.Value._undoLastCommitMenu.Text;
    public static string UndoLastCommitConfirmation => _instance.Value._undoLastCommitConfirmation.Text;
    public static string UndoLastCommitCaption => _instance.Value._undoLastCommitCaption.Text;
    public static string ResetCurrentBranchToRevisionMenu => _instance.Value._resetCurrentBranchToRevisionMenu.Text;
    public static string ResetSoftToRevisionMenu => _instance.Value._resetSoftToRevisionMenu.Text;
    public static string ResetMixedToRevisionMenu => _instance.Value._resetMixedToRevisionMenu.Text;
    public static string ResetKeepToRevisionMenu => _instance.Value._resetKeepToRevisionMenu.Text;
    public static string ResetMergeToRevisionMenu => _instance.Value._resetMergeToRevisionMenu.Text;
    public static string ResetHardToRevisionMenu => _instance.Value._resetHardToRevisionMenu.Text;
    public static string ConfirmHardResetToRevision => _instance.Value._confirmHardResetToRevision.Text;
    public static string ResetHardToRevisionTitle => _instance.Value._resetHardToRevisionTitle.Text;
    public static string CherryPickMenu => _instance.Value._cherryPickMenu.Text;
    public static string RevertCommitMenu => _instance.Value._revertCommitMenu.Text;
    public static string MainlineParentPromptTitle => _instance.Value._mainlineParentPromptTitle.Text;
    public static string MainlineParentPrompt => _instance.Value._mainlineParentPrompt.Text;
    public static string InvalidMainlineParentError => _instance.Value._invalidMainlineParentError.Text;
    public static string MergeBranchMenu => _instance.Value._mergeBranchMenu.Text;
    public static string ResolveMergeConflicts => _instance.Value._resolveMergeConflicts.Text;
    public static string OpenMergeTool => _instance.Value._openMergeTool.Text;
    public static string StageConflictResolution => _instance.Value._stageConflictResolution.Text;
    public static string UseOursResolution => _instance.Value._useOursResolution.Text;
    public static string UseTheirsResolution => _instance.Value._useTheirsResolution.Text;
    public static string ContinueAfterResolution => _instance.Value._continueAfterResolution.Text;
    public static string MergeStrategyLabel => _instance.Value._mergeStrategyLabel.Text;
    public static string MergeStrategyDefault => _instance.Value._mergeStrategyDefault.Text;
    public static string MergeMessageLabel => _instance.Value._mergeMessageLabel.Text;
    public static string MergeMessageHint => _instance.Value._mergeMessageHint.Text;
    public static string RefreshConflicts => _instance.Value._refreshConflicts.Text;
    public static string MergeOptionsTitle => _instance.Value._mergeOptionsTitle.Text;
    public static string MergeOptionsBranchPrompt => _instance.Value._mergeOptionsBranchPrompt.Text;
    public static string MergeAllowFastForward => _instance.Value._mergeAllowFastForward.Text;
    public static string MergeSquash => _instance.Value._mergeSquash.Text;
    public static string MergeNoCommit => _instance.Value._mergeNoCommit.Text;
    public static string MergeAllowUnrelatedHistories => _instance.Value._mergeAllowUnrelatedHistories.Text;
    public static string MergeExecute => _instance.Value._mergeExecute.Text;
    public static string RebaseOntoMenu => _instance.Value._rebaseOntoMenu.Text;
    public static string RebaseOptionsTitle => _instance.Value._rebaseOptionsTitle.Text;
    public static string RebaseOptionsBranchPrompt => _instance.Value._rebaseOptionsBranchPrompt.Text;
    public static string RebaseInteractive => _instance.Value._rebaseInteractive.Text;
    public static string RebaseAutosquash => _instance.Value._rebaseAutosquash.Text;
    public static string RebaseSpecificRange => _instance.Value._rebaseSpecificRange.Text;
    public static string RebaseRangeUpstreamPrompt => _instance.Value._rebaseRangeUpstreamPrompt.Text;
    public static string RebaseRangeUpstreamRequired => _instance.Value._rebaseRangeUpstreamRequired.Text;
    public static string RebaseAutoStash => _instance.Value._rebaseAutoStash.Text;
    public static string RebaseExecute => _instance.Value._rebaseExecute.Text;
    public static string EditRebaseTodo => _instance.Value._editRebaseTodo.Text;
    public static string RebaseDetachedHeadError => _instance.Value._rebaseDetachedHeadError.Text;
    public static string RebaseTargetRequired => _instance.Value._rebaseTargetRequired.Text;
    public static string RebaseSameBranchError => _instance.Value._rebaseSameBranchError.Text;
    public static string RebaseOperationInProgressError => _instance.Value._rebaseOperationInProgressError.Text;
    public static string CommitButton => _instance.Value._commitButton.Text;
    public static string CommitMessageWatermark => _instance.Value._commitMessageWatermark.Text;
    public static string CommitMessageHistory => _instance.Value._commitMessageHistory.Text;
    public static string CommitTemplates => _instance.Value._commitTemplates.Text;
    public static string GitCommitTemplate => _instance.Value._gitCommitTemplate.Text;
    public static string CommitAuthor => _instance.Value._commitAuthor.Text;
    public static string CommitGpgSigning => _instance.Value._commitGpgSigning.Text;
    public static string CommitGpgDefault => _instance.Value._commitGpgDefault.Text;
    public static string CommitGpgDisabled => _instance.Value._commitGpgDisabled.Text;
    public static string CommitGpgDefaultKey => _instance.Value._commitGpgDefaultKey.Text;
    public static string CommitGpgSpecificKey => _instance.Value._commitGpgSpecificKey.Text;
    public static string CommitGpgKeyId => _instance.Value._commitGpgKeyId.Text;
    public static string CommitResetAuthor => _instance.Value._commitResetAuthor.Text;
    public static string AmendCommitConfirmation => _instance.Value._amendCommitConfirmation.Text;
    public static string AmendCommitCaption => _instance.Value._amendCommitCaption.Text;
    public static string CommitAndPush => _instance.Value._commitAndPush.Text;
    public static string CommitValidationCaption => _instance.Value._commitValidationCaption.Text;
    public static string CommitMsgFirstLineInvalid => _instance.Value._commitMsgFirstLineInvalid.Text;
    public static string CommitMsgLineInvalid => _instance.Value._commitMsgLineInvalid.Text;
    public static string CommitMsgSecondLineNotEmpty => _instance.Value._commitMsgSecondLineNotEmpty.Text;
    public static string CommitMsgRegExNotMatched => _instance.Value._commitMsgRegExNotMatched.Text;
    public static string AmendCommitOption => _instance.Value._amendCommitOption.Text;
    public static string SignOffCommitOption => _instance.Value._signOffCommitOption.Text;
    public static string NoVerifyCommitOption => _instance.Value._noVerifyCommitOption.Text;
    public static string EnterCommitMessageError => _instance.Value._enterCommitMessageError.Text;
    public static string NoStagedChangesError => _instance.Value._noStagedChangesError.Text;
    public static string UnrecognizedConflictOperation => _instance.Value._unrecognizedConflictOperation.Text;
    public static string UnrecognizedOperationInProgress => _instance.Value._unrecognizedOperationInProgress.Text;
    public static string RecoveryActionStateChanged => _instance.Value._recoveryActionStateChanged.Text;
    public static string ContinueRebase => _instance.Value._continueRebase.Text;
    public static string SkipRebase => _instance.Value._skipRebase.Text;
    public static string AbortRebase => _instance.Value._abortRebase.Text;
    public static string AbortMerge => _instance.Value._abortMerge.Text;

    /// <summary>
    ///  Gets the label for aborting a cherry-pick or revert operation.
    /// </summary>
    public static string AbortOperation => _instance.Value._abortOperation.Text;
    public static string AbortPatch => _instance.Value._abortPatch.Text;
    public static string ResetSelectedLines => _instance.Value._resetSelectedLines.Text;
    public static string ResetSelectedLinesConfirmation => _instance.Value._resetSelectedLinesConfirmation.Text;
    public static string ResetChangesCaption => _instance.Value._resetChangesCaption.Text;

    public static string Inactive => _instance.Value._rotInactive.Text;

    public static string Arguments => _instance.Value._argumentsText.Text;
    public static string Command => _instance.Value._commandText.Text;
    public static string ExitCode => _instance.Value._exitCodeText.Text;
    public static string WorkingDirectory => _instance.Value._workingDirectoryText.Text;
    public static string ReportBug => _instance.Value._reportBugText.Text;
    public static string ReportIssueDescription => _instance.Value._reportIssueDescription.Text;
    public static string ExternalErrorDescription => _instance.Value._externalErrorDescription.Text;

    public static string RemoteInError => _instance.Value._remoteInError.Text;
    public static string NoChanges => _instance.Value._noChanges.Text;

    public static string SettingsTypeToFind => _instance.Value._settingsTypeToFind.Text;

    public static string Since = _instance.Value._since.Text;
    public static string Until = _instance.Value._until.Text;
    public static string Author = _instance.Value._author.Text;
    public static string Committer = _instance.Value._committer.Text;
    public static string Message = _instance.Value._message.Text;
    public static string DiffContent = _instance.Value._diffContent.Text;
    public static string PathFilter = _instance.Value._pathFilter.Text;
    public static string ShowOnlyFirstParent = _instance.Value._showOnlyFirstParent.Text;
    public static string ShowReflog = _instance.Value._showReflog.Text;
    public static string ShowReflogTooltip = _instance.Value._showReflogTooltip.Text;
    public static string ShowReflogMenu = _instance.Value._showReflogMenu.Text;
    public static string ReflogWindowTitle = _instance.Value._reflogWindowTitle.Text;
    public static string ReflogReference = _instance.Value._reflogReference.Text;
    public static string ReflogSha = _instance.Value._reflogSha.Text;
    public static string ReflogAction = _instance.Value._reflogAction.Text;
    public static string CommandLogTitle = _instance.Value._commandLogTitle.Text;
    public static string ShowCurrentBranchOnly = _instance.Value._showCurrentBranchOnly.Text;
    public static string SimplifyByDecoration = _instance.Value._simplifyByDecoration.Text;

    #region Scripts

    private readonly TranslationString _scriptConfirmExecuteText = new("Do you want to execute script");
    private readonly TranslationString _scriptErrorCantFindText = new("Unable to find script");
    private readonly TranslationString _scriptErrorFailedToExecuteText = new("Failed to execute script");
    private readonly TranslationString _scriptErrorOptionWithoutRevisionGridText = new("option is only supported when invoked from the revision grid");
    private readonly TranslationString _scriptErrorOptionWithoutRevisionText = new("A valid revision is required to substitute the argument options");
    private readonly TranslationString _scriptText = new("Script");
    private readonly TranslationString _scriptUserInputCaption = new("User input for script '{0}'");
    private readonly TranslationString _scriptUserCanceledRun = new("Script not started.");

    public static string ScriptConfirmExecute => _instance.Value._scriptConfirmExecuteText.Text;
    public static string ScriptErrorCantFind => _instance.Value._scriptErrorCantFindText.Text;
    public static string ScriptErrorFailedToExecute => _instance.Value._scriptErrorFailedToExecuteText.Text;
    public static string ScriptErrorOptionWithoutRevisionGridText => _instance.Value._scriptErrorOptionWithoutRevisionGridText.Text;
    public static string ScriptErrorOptionWithoutRevisionText => _instance.Value._scriptErrorOptionWithoutRevisionText.Text;
    public static string ScriptText => _instance.Value._scriptText.Text;
    public static string ScriptUserInputCaption => _instance.Value._scriptUserInputCaption.Text;
    public static string ScriptUserCanceledRun => _instance.Value._scriptUserCanceledRun.Text;

    #endregion

    public static string StashDropConfirmTitle => _instance.Value._stashDropConfirmTitle.Text;
    public static string CannotBeUndone => _instance.Value._cannotBeUndone.Text;
    public static string AreYouSure => _instance.Value._areYouSure.Text;

    public static string SwitchWorktreeConfirmation => _instance.Value._switchWorktreeConfirmation.Text;
    public static string SwitchWorktreeCaption => _instance.Value._switchWorktreeCaption.Text;
    public static string DeleteWorktreeConfirmation => _instance.Value._deleteWorktreeConfirmation.Text;
    public static string DeleteWorktreeCaption => _instance.Value._deleteWorktreeCaption.Text;
    public static string DeleteWorktreeFailed => _instance.Value._deleteWorktreeFailed.Text;
    public static string DeleteMainWorktreeRefused => _instance.Value._deleteMainWorktreeRefused.Text;

    public static string CreateWorktree => _instance.Value._createWorktree.Text;
    public static string PruneWorktrees => _instance.Value._pruneWorktrees.Text;
    public static string ManageWorktrees => _instance.Value._manageWorktrees.Text;
    public static string WorktreeDirectoryNotFound => _instance.Value._worktreeDirectoryNotFound.Text;
    public static string NonexistingGitRevision => _instance.Value._nonexistingGitRevision.Text;
    public static string IgnoringReference => _instance.Value._ignoringReference.Text;

    public static string GitSecurityError => _instance.Value._gitSecurityError.Text;
    public static string GitDubiousOwnershipHeader => _instance.Value._gitDubiousOwnershipHeader.Text;
    public static string GitDubiousOwnershipText => _instance.Value._gitDubiousOwnershipText.Text;
    public static string GitDubiousOwnershipTrustRepository => _instance.Value._gitDubiousOwnershipTrustRepository.Text;
    public static string GitDubiousOwnershipOpenRepositoryFolder => _instance.Value._gitDubiousOwnershipOpenRepositoryFolder.Text;
    public static string GitDubiousOwnershipSeeGitCommandOutput => _instance.Value._gitDubiousOwnershipSeeGitCommandOutput.Text;
    public static string GitDubiousOwnershipHideGitCommandOutput => _instance.Value._gitDubiousOwnershipHideGitCommandOutput.Text;

    public static string SeeErrorMessage => _instance.Value._seeErrorMessage.Text;
    public static string HideErrorMessage => _instance.Value._hideErrorMessage.Text;
    public static string ReportIssue => _instance.Value._reportIssue.Text;
    public static string ReportReproducedIssueDescription => _instance.Value._reportReproducedIssueDescription.Text;
    public static string RestartApplication => _instance.Value._restartApplication.Text;
    public static string RestartApplicationDescription => _instance.Value._restartApplicationDescription.Text;
    public static string FailedToLoadAnAssembly => _instance.Value._failedToLoadAnAssembly.Text;
    public static string FailedToLoadFileOrAssemblyFormat => _instance.Value._failedToLoadFileOrAssemblyFormat.Text;
    public static string FailedToLoadFileOrAssemblyText => _instance.Value._failedToLoadFileOrAssemblyText.Text;
    public static string NotConfigured => _instance.Value._notConfigured.Text;
    public static string FailedToLoadPlugin => _instance.Value._failedToLoadPlugin.Text;
    public static string FailedToLoadPluginPopupText => _instance.Value._failedToLoadPluginPopupText.Text;
}
