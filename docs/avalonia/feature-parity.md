# Avalonia feature parity

## Scope and evidence

This inventory compares the checked-out WinForms UI with the Avalonia port at
`813f48be0d91699124f61a1fa6c0fd5e376d3b81`. Avalonia work is still uncommitted in this
worktree. The Windows Forms app is the behavioral reference; shared Git services are not
evidence that a workflow is exposed by Avalonia.

Evidence references are source and test locations, not assumptions based on a view class name.
The headless integration suite uses temporary local Git repositories. Linux evidence refers to
Ubuntu 26.04 x64 with .NET SDK 10.0.112. Windows UI runtime evidence is not available here.

### Status definitions

| Status | Meaning |
| --- | --- |
| `VERIFIED_PARITY` | The WinForms workflow and equivalent Avalonia workflow both work with materially equivalent safety and behavior, with executed tests on supported target platforms. |
| `VERIFIED_PARTIAL` | An Avalonia subset is exercised, but material options, workflows, safeguards, or platform coverage differ from WinForms. |
| `IMPLEMENTED_UNVERIFIED` | Relevant Avalonia behavior is present, but its operation/result or required platform has not been verified. |
| `MISSING` | The WinForms user-facing workflow exists, but no functional Avalonia path was found, or a required platform alternative has not been implemented. |
| `BLOCKED` | A platform or build dependency prevents the workflow from being used or validated as expected. |
| `INTENTIONALLY_PLATFORM_SPECIFIC` | A supported integration is deliberately limited to a platform and is not an Avalonia feature-parity gap for other platforms. |
| `NOT_APPLICABLE` | No equivalent user-facing behavior is expected on the target platform. |

No workflow currently meets `VERIFIED_PARITY`: the Avalonia UI is deliberately a partial port.
“Risk” describes consequences of an incorrect or incomplete implementation: High includes
index/data changes and history rewrites; Medium includes incorrect displayed state; Low is
primarily usability or presentation impact.

The 47 inventoried workflows currently comprise 15 `VERIFIED_PARTIAL`, 26 `MISSING`,
2 `IMPLEMENTED_UNVERIFIED`, 3 `BLOCKED`, and 1 `INTENTIONALLY_PLATFORM_SPECIFIC`.
No feature meets `VERIFIED_PARITY`.

## Feature matrix

| ID | Feature | WinForms UI / shared implementation | Avalonia implementation and executed evidence | Linux / Windows evidence | Risk | Accessibility / known difference | Priority | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| APP-01 | Startup and main repository window | `GitExtensions/Program.cs`, `GitUI/CommandsDialogs/FormBrowse.cs` | `GitExtensions.Avalonia/Program.cs`, `App.axaml.cs`, `GitUI.Avalonia/FormBrowse.axaml`; published executable launched outside checkout in Phase 6; headless construction tests | Linux Ubuntu 26.04 x64 only; Windows Forms root build passes, no Windows runtime test | Medium | Main browse shell has no complete WinForms menu/toolbar set | P1 | `VERIFIED_PARTIAL` |
| REPO-01 | Open repository by path / picker | `FormBrowse.cs`, `FormOpenDirectory.cs`, `GitModule.TryFindGitWorkingDir` | `FormBrowse.OpenRepository`; integration test opens parent from nested directory and uses a Unicode path | Real local Git repo in Linux headless test; published startup with spaced/Unicode path | Medium | Keyboard and native picker behavior not fully exercised | P1 | `VERIFIED_PARTIAL` |
| REPO-02 | Recent repositories / dashboard | `BrowseDialog/DashboardControl/*`, `UserRepositoriesListController` | Shared settings load, but no Avalonia dashboard/recent-repo UI was found | No restart/recent-history workflow test | Medium | No recent-repo selection surface in Avalonia | P1 | `MISSING` |
| HIST-01 | Revision history and refresh | `FormBrowse.InitRevisionGrid.cs`, `RevisionGridControl.cs`, `RevisionReader` | Avalonia `RevisionGridControl`; real local-repo integration verifies revision count and reload on notification | Linux headless test passes; Windows root build only | Medium | Search/filter/navigation history absent | P1 | `VERIFIED_PARTIAL` |
| GRAPH-01 | Revision graph and merge lanes | `RevisionDataGridView`, shared graph model/renderers | Avalonia graph renderer; three golden-image tests for synthetic chain/merge/crossing lanes | Headless rendering tests pass; not verified on real large history or Windows | Medium | High-DPI, scaling, real-history graph comparison outstanding | P1 | `VERIFIED_PARTIAL` |
| REF-01 | Branch/tag/reference list and selection | `RepoObjectsTree`, revision-grid ref renderers | Avalonia `RepoObjectsTree`; integration loads refs and checks selected head indirectly | Real repository refs loaded in Linux integration test | Medium | Create/rename/delete and context actions absent | P1 | `VERIFIED_PARTIAL` |
| REV-01 | Commit details | `CommitInfo` control and `FormBrowse.InitCommitDetails.cs` | Avalonia `CommitInfo`; integration selects a real commit and verifies its object ID | Linux headless test; no Windows runtime test | Low | Signature/GPG and all metadata variants not assessed | P2 | `VERIFIED_PARTIAL` |
| FILE-01 | Changed files and revision selection | `FileStatusList`, `RevisionDiffController` | Avalonia `FileStatusList`; real selected commit/file covered by integration test | Linux headless test | Medium | Context menus and broad diff modes absent | P1 | `VERIFIED_PARTIAL` |
| DIFF-01 | Text diff display | `RevisionDiffControl`, `FormViewPatch`, shared diff services | Avalonia `FileViewer`; integration verifies tracked and untracked text content; a control construction test exists | Linux headless test; binary/large/rename error cases not covered | Medium | Read-only text viewer; no complete upstream diff modes/context actions | P1 | `VERIFIED_PARTIAL` |
| WORK-01 | Working-tree status | `FormBrowse`, `GitModule.GetAllChangedFilesWithSubmodulesStatus` | Avalonia status panel; test asserts staged, unstaged, untracked, Unicode, and conflicted status | Linux real Git integration | High | Status text is dense; accessibility review outstanding | P1 | `VERIFIED_PARTIAL` |
| WORK-02 | Stage / unstage whole files | WinForms `FormCommit` and `FileStatusList` operations | Avalonia working-tree panel calls shared `IGitModule` stage/unstage; test verifies Git index state and unrelated staged content remains | Linux real Git integration | High | No commit UI follows staging | P1 | `VERIFIED_PARTIAL` |
| WORK-03 | Partial/hunk/line staging | WinForms commit/diff controls and patch selection | No Avalonia partial-stage UI or handlers found | Not tested | High | Missing selective staging safety/use case | P1 | `MISSING` |
| COMMIT-01 | Commit creation and validation | `FormCommit`, `CommitMessageManager`, `Commands.Commit`, scripts/hooks | No Avalonia commit dialog; `GitUICommands` reports several methods as not ported | Not tested | High | No UI validation, message persistence, amend/sign-off/GPG, hook scripts | P0 | `MISSING` |
| COMMIT-02 | Amend / commit options / commit-and-push | `FormCommit`, `Commands.Commit` | No Avalonia UI | Not tested | High | Amend can rewrite history; no feature | P1 | `MISSING` |
| BRANCH-01 | Create/rename/delete branch | `FormCreateBranch`, `FormRenameBranch`, `FormDeleteBranch`, `Commands` | No corresponding Avalonia dialog or menu action found | Not tested | High | Current-branch/worktree/force-delete safeguards unavailable in Avalonia | P1 | `MISSING` |
| BRANCH-02 | Checkout branch or revision | `FormCheckoutBranch`, `FormCheckoutRevision` | No Avalonia checkout UI | Not tested | High | Dirty-tree confirmation and recovery not ported | P1 | `MISSING` |
| REF-02 | Create/delete tags | `FormCreateTag`, `FormDeleteTag` | No Avalonia tag UI | Not tested | Medium | Missing | P2 | `MISSING` |
| REMOTE-01 | Configure remotes | `FormRemotes`, `FormRemotesController` | No Avalonia remote-management UI | Not tested | Medium | Existing Git config remains usable outside UI | P2 | `MISSING` |
| REMOTE-02 | Fetch all | Browse command menu calls Git fetch through `FormProcess` | Avalonia `fetchAllToolStripMenuItem_Click`; no remote integration test | Handler exists; network/local-remote result not asserted | Medium | Progress/error path not independently verified | P2 | `IMPLEMENTED_UNVERIFIED` |
| REMOTE-03 | Pull / merge / rebase pull policies | `FormPull`, toolbar defaults and structured commands | No Avalonia pull UI | Not tested | High | Fetch is not pull parity | P1 | `MISSING` |
| REMOTE-04 | Push / force-push / tracking | `FormPush`, structured commands and confirmations | No Avalonia push UI | Not tested | High | Force options and tracking safeguards absent | P1 | `MISSING` |
| MERGE-01 | Start/continue merge | `FormMergeBranch`, `FormResolveConflicts`, shared commands | Recovery status/actions include continue merge only; no merge setup UI and no execution test | Linux status detection test only | High | Recovery command behavior not tested | P1 | `VERIFIED_PARTIAL` |
| MERGE-02 | Conflict resolution and configured mergetool | `FormResolveConflicts`, `MergeConflictHandler`, `Module.RunMergeTool` | No Avalonia conflict-resolution UI | Not tested | High | Cannot resolve conflict through a full Avalonia workflow | P1 | `MISSING` |
| REBASE-01 | Rebase setup / continue / skip / abort | `FormRebase`, structured commands | Avalonia displays an interrupted rebase and shows constrained actions; test verifies detection/UI only | Linux test for real conflicted rebase state; no recovery execution | High | No setup dialog; commands and history outcome untested | P1 | `VERIFIED_PARTIAL` |
| REBASE-02 | Interactive rebase / todo plan | `FormRebase`, editor/process integration | No Avalonia rebase-plan UI | Not tested | High | History-rewriting workflow absent | P1 | `MISSING` |
| GIT-01 | Cherry-pick / sequence | `FormCherryPick`, shared commands | No Avalonia UI | Not tested | High | Missing | P1 | `MISSING` |
| GIT-02 | Revert commit | `FormRevertCommit`, shared commands | No Avalonia UI | Not tested | High | Missing | P1 | `MISSING` |
| GIT-03 | Reset / discard changes | `FormResetChanges`, `FormBrowse` safeguards | No Avalonia UI | Not tested | High | Destructive action intentionally not exposed | P1 | `MISSING` |
| STASH-01 | Stash save/list/apply/pop/drop | `FormStash`, shared Git operations | No Avalonia stash UI | Not tested | High | Pop/drop recovery and conflict safeguards absent | P1 | `MISSING` |
| SEARCH-01 | Revision search/filter | `SearchWindow`, `SearchControl`, revision filter UI | No Avalonia search/filter control | Not tested | Medium | No search surface | P2 | `MISSING` |
| FILE-02 | File history / blame | `FormFileHistory`, `FormBlame`, shared parsers | No Avalonia file history/blame UI | Not tested | Medium | Missing | P2 | `MISSING` |
| CONFIG-01 | Application and repository settings UI | `FormSettings` and settings pages; `AppSettings`/Git config backends | Shared app settings load/save and theme choice only; no settings dialog | Theme test passed; broader persistence not tested | Medium | Most preferences are not configurable through Avalonia | P1 | `VERIFIED_PARTIAL` |
| THEME-01 | Theme selection | `ThemeModule`, `ThemeId`, settings page | Built-in light/dark preference applies Avalonia variant and shim; test verifies dark selection | Headless test; system following/custom themes not verified | Low | Only two built-in variants | P2 | `VERIFIED_PARTIAL` |
| LOC-01 | Translation/localization | `Translator`, `TranslationApp`, XLF catalogs | Translation infrastructure is referenced and translator test sees entries | Catalog generation blocked on Windows WinExe generator in Linux; new phase strings none | Medium | Full catalog parity/coverage unknown | P2 | `BLOCKED` |
| KEY-01 | Hotkeys and configurable shortcuts | `GitUI/Hotkey`, `FormSettings` hotkey page | No Avalonia hotkey registration/dispatch or settings UI found | Not tested | Medium | Keyboard support is default control navigation only | P1 | `MISSING` |
| PLUG-01 | Plugin discovery and UI hosting | MEF `ManagedExtensibility`, `PluginRegistry`, built-in plugins | Avalonia startup does not initialize the WinForms plugin host; no Avalonia plugin UI | Not tested | High | Existing plugin UI contracts are WinForms-coupled | P1 | `BLOCKED` |
| SCRIPT-01 | User scripts/custom commands/hooks | `ScriptsEngine`, menu/toolbars, commit script hooks | No Avalonia script menu or lifecycle integration found | Not tested | Medium | Commit hooks also unavailable because commit UI is absent | P2 | `MISSING` |
| TOOL-01 | External editor/diff/merge tools | `GitModule` and tool configuration classes | Shared operations exist; no complete Avalonia tool UI or invocation verification | Linux executable/permission scenarios untested | High | Windows paths/tools are not portable by default | P1 | `IMPLEMENTED_UNVERIFIED` |
| AUTH-01 | Credential helpers / SSH integration | Windows credential manager and shared Git SSH configuration | Non-Windows credential stub throws for writes; SSH path/process exists in shared code | Linux auth/agent/helper not tested | High | Git Extensions-managed credential writes blocked | P1 | `BLOCKED` |
| HOOK-01 | Git hooks and commit scripts | Git hooks plus `ScriptsEngine` before/after commit | No Avalonia commit entry point / script integration | Not tested | High | Hooks remain Git-controlled, but app script parity absent | P2 | `MISSING` |
| SUB-01 | Submodule operations | `FormSubmodules`, `SubmoduleHelpers` | Shared model references exist; no Avalonia submodule UI | Not tested | Medium | Missing user-facing operations | P2 | `MISSING` |
| PATCH-01 | Apply/format/view patches | `FormApplyPatch`, `FormFormatPatch`, `FormViewPatch` | Patch operation state recovery only; patch workflows not ported | Not tested | Medium | No patch workflow UI | P2 | `MISSING` |
| REPO-03 | Clone/init/archive/cleanup/verify | `FormClone`, `FormInit`, `FormArchive`, `FormCleanupRepository`, `FormVerify` | No corresponding Avalonia workflows | Not tested | Medium | No clone/init or maintenance UI | P2 | `MISSING` |
| DIAG-01 | Git command log/reflog/lost-object tools | `FormGitCommandLog`, `FormReflog`, `FormVerify` | Process logging infrastructure exists; no Avalonia command-log/reflog UI | No persisted Avalonia log or command-log workflow verified | Medium | Limited troubleshooting surface | P2 | `MISSING` |
| WIN-01 | Windows Explorer shell integration | Native `GitExtensionsShellEx` and installer | Not part of Avalonia/Linux project | Windows-native build/runtime not exercised here | Low on Linux | Intentionally Windows-only; Avalonia app has no shell extension | P3 | `INTENTIONALLY_PLATFORM_SPECIFIC` |
| DESK-01 | Linux desktop entry/file association | Existing Windows installer/shell integration | No desktop entry, icon installation, URI/file association in the port | No package installation test | Low | Not implemented | P3 | `MISSING` |
| DIST-01 | Linux publish/install/update | Windows MSI/portable pipeline | Framework-dependent `linux-x64` publish and CI artifact workflow configured; local artifact started outside checkout | Linux publish and launch verified locally; hosted CI step and install/uninstall not run | Medium | Requires .NET 10 runtime, Git, native desktop dependencies | P1 | `VERIFIED_PARTIAL` |

## Architecture and behavioral assessment

- `GitCommands`, `GitExtensions.Extensibility`, `GitExtUtils`, and selected framework-neutral
  `GitUI` sources are shared. `GitUI.Avalonia` links portable sources and owns Avalonia controls.
- Git status, staging, untracked-file diff generation, and recovery command construction reuse
  existing `IGitModule`/`Commands` mechanisms. Avalonia-specific async orchestration remains in
  `FormBrowse` and is not equivalent to the full `FormBrowse`/`GitUICommands` workflow suite.
- The Avalonia browse code directly coordinates status retrieval, operation-state classification,
  stage/unstage, selection, and status/error display. This is a small presentation-layer
  orchestration, but it is duplicated relative to WinForms and is not backed by the complete
  command-dialog and validation workflows.
- `GitUICommands` deliberately throws `NotImplementedException` for unported APIs; callers must
  not assume a method is available just because the interface is implemented.
- The non-Windows credential implementation deliberately refuses writes. Windows Explorer
  shell/askpass components remain intentionally platform-specific.

## Prioritized findings

| Priority | Finding | Reproduction / expected / actual | Correction and coverage |
| --- | --- | --- | --- |
| P0 | No P0 data-loss defect was reproduced in the exercised paths. | Review of exposed Avalonia actions found no reset, force-push, branch deletion, stash drop, or discard action; this absence is not a claim that unported actions are safe. | Continue to gate destructive features on their WinForms safety checks and add isolated Git-state tests before exposing them. |
| P1 | Core workflow parity is absent: commit, checkout/branch, pull/push, conflict resolution, stash and reset are missing. | Open a repo and stage a file: there is no Avalonia commit action to finish the normal first-commit workflow. | Implement one complete workflow at a time using existing shared commands; do not label the application a parity release before these work. |
| P1 | Credential writes fail on non-Windows. | Configure a Git Extensions-managed credential through the shared non-Windows backend; `SetCredentials` throws `PlatformNotSupportedException`. | Use system Git credential helpers as an interim configuration path; validate before claiming authenticated operations. |
| P2 fixed in this phase | Browse windows subscribed to `PostRepositoryChanged` without close-time cleanup, and outstanding browse loads were not invalidated at close. | Close a browse window, then notify the same `GitUICommands`; pre-fix window could reload after close and remain retained by the event publisher. | Unsubscribe, invalidate load version, and dispose the five browse request sequences on `Closed`; added `FormBrowse_should_detach_repository_notifications_when_closed`. |
| P2 | Linux settings-save failures are swallowed in shared `AppSettings`; there is no persistent Avalonia diagnostic log identified. | Settings save can fail without a user-visible diagnostic. | Separate shared settings error-policy work; define a supported log location and exercise permission failures before release. |
| P2 | Plugin host and most settings/hotkeys/custom tools are not wired to Avalonia. | Avalonia startup constructs the browser and services but does not initialize the WinForms `PluginRegistry`/settings dialog host. | Port compatible host surfaces or explicitly exclude plugins from a beta and fail visibly when a plugin-dependent action is requested. |
| P3 | No Linux desktop launcher or installable package exists. | Publish folder can be launched explicitly; no menu/desktop integration installed. | Decide on a package target after core workflow and auth gates pass. |

## Release conclusion

The application is a useful internal Avalonia browser prototype with tested Linux repository
reading and stage/unstage slices. It is not functionally equivalent to WinForms and should not
be described as production-ready. See [`release-readiness.md`](release-readiness.md) for gate
outcomes and [`testing-strategy.md`](testing-strategy.md) for required follow-up coverage.
