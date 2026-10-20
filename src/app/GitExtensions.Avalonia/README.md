# Git Extensions Avalonia

A cross-platform user interface for Git Extensions built with [Avalonia](https://avaloniaui.net/).
It reuses the underlying engine (`GitCommands`, `GitExtensions.Extensibility`, and related
projects). Avalonia is the default solution/debug launch and the configured primary Windows MSI
shortcut; the Windows Forms application remains installed as **Git Extensions (Classic)**.
The Windows MSI and Avalonia Windows runtime have not been built or tested in this environment,
and the Avalonia UI is not at feature parity. The initial Linux distribution target remains a
framework-dependent `linux-x64` publish.

## Status

The Avalonia application opens a repository passed with the `browse <path>` command, the
existing recent-repository setting, or **Repository > Open**. It displays the history graph
and reference labels, selected-commit details and changed files, and diffs for commit and
working-tree files. The working-tree panel separates staged and unstaged files and supports
staging and unstaging selected files; it also supports refresh and Fetch all. History, graph,
commit, diff, reference, and working-tree data reuse the existing GitCommands models and
operations.

| Area | Milestone status |
| --- | --- |
| Avalonia startup and main window | Implemented; a `linux-x64` publish launched outside the checkout on Ubuntu 26.04 x64 with an existing repository path; full desktop interaction was not smoke-tested |
| Repository picker, history, graph, references, commit details, changed files, diff, and working-tree status | Implemented; headless tests cover repository switching, staged/unstaged/untracked/conflict states, commit details, and tracked/untracked diffs |
| Refresh and Fetch all | Implemented; network fetch was not exercised in the smoke test |
| Staged/unstaged file lists and selected-file stage/unstage | Implemented using existing `IGitModule` APIs; integration tests verify Unicode paths and preservation of unrelated staged files |
| Interrupted merge, rebase, patch, and bisect state display | Partially implemented using existing `IGitModule` operation-state checks; tests verify interrupted merge and rebase detection |
| Merge/rebase/patch recovery actions | Partially implemented using existing `Commands` operations; recovery command execution and conflict resolution are not yet covered by Avalonia integration tests |
| Existing settings and appearance | Shared `AppSettings` is used; startup loads and shutdown saves existing settings. View menu now selects built-in light/dark themes using the existing theme setting; other settings pages are not implemented |
| Commit, discard, merge setup, rebase setup, interactive rebase, cherry-pick, revert, reset, stash, checkout, branch/tag management, push/pull, settings pages, and plugin UI | Not implemented |
| Linux portable distribution | Framework-dependent `linux-x64` publish implemented and launched outside the checkout; requires .NET 10 runtime, Git, and Linux desktop libraries |
| Windows default launch | Main solution and VS Code launch configurations default to Avalonia; Windows MSI is configured to install and launch Avalonia by default, with WinForms retained as a Classic shortcut; Windows MSI/runtime behavior is unverified |
| Debian/RPM packages, Flatpak, AppImage, desktop entry, and file associations | Not implemented |
| Linux credential storage, external tools, SSH/authentication, and desktop integrations | App-specific credential storage explicitly fails on non-Windows; external Git credential helpers, SSH, and external tools are not validated |
| Avalonia Windows/macOS build and runtime | Not validated in this environment |
| Existing Windows Forms application | Kept buildable and installed as the Classic UI; integrations not yet ported continue to use it |

Without a repository argument or recent repository, the main window opens empty and a
repository can be selected from **Repository > Open**. This milestone is not full feature
parity.

## Settings and appearance inventory

Configuration remains in the existing `AppSettings`/`SettingsContainer` infrastructure;
the Avalonia application does not introduce a second settings store. Existing settings are
loaded during startup and saved at shutdown. The first Avalonia appearance preference uses
the existing built-in light/dark `ThemeId` values, updates Fluent's requested theme variant,
and synchronizes the shared UI color-mode compatibility shim. Custom CSS themes, system
theme following, settings pages, hotkeys, external-tool configuration, and Avalonia plugin
discovery have not been ported or validated.

## Linux compatibility inventory

The following audit was performed on Ubuntu 26.04 x64 with .NET SDK 10.0.112 and Git 2.53.0.
The desktop session exposed both X11 and Wayland environment variables; the separate CI smoke
test uses Xvfb/X11. These checks do not establish support for other distributions or architectures.

| Priority | Component | Finding and Linux impact | Mitigation / validation |
| --- | --- | --- | --- |
| Fixed | SDK selection (`global.json`) | The previous `"10.0"` SDK version and `"feature"` roll-forward value were invalid; `dotnet` ignored the file, weakening reproducibility. | Pin the 10.0.100 feature band with `latestFeature`; `dotnet --info` now resolves the installed 10.0.112 SDK through the repository file. |
| Fixed | Avalonia publish (`GitExtensions.Avalonia.csproj`) | Repository-wide `IsPublishable=false` caused `dotnet publish` to succeed without creating output. | Enable publishing only for the Avalonia executable; a Release `linux-x64` publish produced an executable and dependency set. |
| Verified with limitations | Git process and paths | Git is executed through the existing `Executable`/`Process` abstraction. A published app started with a repository path and filename containing spaces and Unicode characters. | Framework-dependent publish launch-tested outside the checkout; headless Avalonia tests cover local Git-backed repository workflows. Explicit Git paths, permission-denied cases, external tools, and authenticated remotes are untested. |
| Verified with limitations | User configuration | The compatibility shim maps application data through .NET special folders; an isolated `XDG_CONFIG_HOME` resulted in the existing settings file being created in that tree. | Observed during the isolated publish smoke test. Persistence across a graceful application restart and non-default home-directory permissions still need a dedicated test. |
| High | Credentials | The Windows Credential Manager implementation is replaced on non-Windows by a stub whose credential writes throw `PlatformNotSupportedException`. This blocks Git Extensions-managed credential storage on Linux. | No new credential backend introduced. System Git credential helpers may be configured independently but were not tested. |
| Medium | Diagnostics | Avalonia startup enables `LogToTrace`; this audit found no configured persistent Avalonia log file or documented log location. Settings-save errors are swallowed by the existing shared `AppSettings` implementation. | UI startup remained alive in the publish smoke test; startup-failure and persistence-failure diagnostics remain release work. |
| Medium | Translation generation | The tagged `TranslationApp` is a WinExe referencing the WinForms UI, so the existing catalog generator is not runnable in this Linux environment. | No translation strings were added in this phase. Catalog generation/parity remains part of Windows validation. |
| Medium | Desktop integration and accessibility | No Avalonia desktop entry or explicit automation-name/keyboard-navigation declarations were found in the ported UI. | No file association or full accessibility conformance is claimed; keyboard and screen-reader workflows require assessment. |
| Intentionally platform-specific | Windows integration | The Explorer shell extension and native Windows askpass helper are Windows-specific. The Avalonia solution builds the portable managed target and does not include these components. | Preserve the Windows implementation; Linux alternatives are not provided by this milestone. |

Linux desktop startup also depends on the system .NET 10 runtime, Git on `PATH`, and native
desktop libraries. The CI smoke step installs `xvfb`, `libfontconfig1`, `libice6`, `libsm6`,
`libx11-6`, `libxext6`, and `libxrender1`. The published native libraries resolved successfully
on the local Ubuntu system; the hosted-runner install/smoke steps have not run yet. Package names
and desktop requirements may differ on other distributions.

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) 10.0.100 or later in the .NET 10 feature band (`global.json` uses `latestFeature`)
- [Git](https://git-scm.com/) available on `PATH`
- For a framework-dependent Linux publish: the .NET 10 runtime, Git, and the native desktop
  dependencies listed above
- A graphical Linux session with X11 or Wayland; only Ubuntu 26.04 x64 has been exercised here

## Building

```
dotnet build GitExtensions.slnx
```

`GitExtensions.slnx` includes both UIs. The focused `eng/avalonia/GitExtensions.Avalonia.slnx`
remains available for Avalonia-only builds and tests. Avalonia project references set
`BuildAvalonia=true` so shared projects select the cross-platform target on Windows too.
Non-Windows builds disable apphost generation for the framework-dependent development build;
publishing explicitly enables it for a target RID.

Linux build and startup were verified with .NET SDK 10.0.112 in this environment. Windows
and macOS builds and runtime behavior have not been verified here. Initialize the repository
submodules before building, as for the existing application:

```
git submodule update --init --recursive
```

On Linux, the focused Avalonia solution builds its xUnit test host for a portable host RID
(`linux-x64`, `linux-arm64`, or the corresponding musl RID) and runs the migration utility through
`dotnet` without a generated apphost. This avoids requiring an apphost pack named for the host
distribution.

## Publishing a portable Linux build

The initial distribution is a framework-dependent `linux-x64` publish. It requires a compatible
.NET 10 runtime on the destination system; it does not bundle .NET, Git, or system GUI libraries.
From the repository root:

```
dotnet publish src/app/GitExtensions.Avalonia/GitExtensions.Avalonia.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  -p:UseAppHost=true \
  --output artifacts/Release/publish/avalonia-linux-x64
```

Launch the published app directly:

```
./artifacts/Release/publish/avalonia-linux-x64/GitExtensions.Avalonia browse /path/to/repository
```

The Linux workflow publishes this output, checks its executable and native dependencies, runs it
under Xvfb with a temporary Git repository whose path and filename contain spaces/Unicode, checks
that the expected per-user settings file is created, and uploads the output as a short-lived CI
artifact. A framework-dependent publish is not an installer: no desktop entry, package manager
integration, Flatpak/AppImage, or system-wide install/uninstall procedure is provided.

For the tested internal-preview artifact, its current release decision, installation requirements,
configuration caveats, and verification results, see the [Avalonia installation guide](../../../docs/avalonia/installation.md)
and [release-candidate assessment](../../../docs/avalonia/release-candidate.md). These documents
do not imply approval for beta or public distribution.

## Running

```
dotnet run --project src/app/GitExtensions.Avalonia --no-build -p:UseAppHost=false -- browse /path/to/repository
```

Build the Avalonia solution first. The command-line arguments follow the Windows Forms
application: the first argument is the command (`browse`), the second is the repository path.
Without arguments, the application may open the configured recent repository; otherwise the
window is empty until a repository is selected from **Repository > Open**.

## Testing

```
dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj -p:UseAppHost=false
```

The UI tests run headlessly on Linux and include golden-image tests for known graph
topologies. After an intended rendering change, regenerate the images by running the tests
with `GITEXT_UPDATE_GOLDEN_IMAGES=1`, review the new images, and commit them.

The separate Linux workflow remains non-blocking so this incomplete migration does not gate the
existing Windows Forms CI. It builds the portable target, runs headless tests, performs a
published-output startup smoke test, and compile-checks shared Windows target frameworks.
This Linux workflow has not yet been executed by GitHub Actions for these changes.

## Project layout

The UI lives in `src/app/GitUI.Avalonia`, which mirrors the file structure of the Windows
Forms project `src/app/GitUI`: each ported form or control sits at the same relative path
and keeps the same class and member names, with an `.axaml` file taking the role of the
`.Designer.cs`. Code is written in the same code-behind style used throughout Git
Extensions - named control fields and event handlers, no view models. Framework-neutral
sources are compiled directly from `src/app/GitUI` as links. `src/app/GitExtensions.Shims.WinForms`
provides headless stand-ins for the few Windows Forms types referenced by the shared
engine projects.

## Notes for contributors

- Changes must not break the Windows Forms application; it remains available as the Classic UI.
- The Avalonia default is not a feature-parity claim. Use the Classic UI for workflows not
  implemented in Avalonia.
- Keep the status section aligned with tested functionality; do not mark a workflow complete
  because only its view or control exists.
- New dialogs are scaffolded from their Windows Forms layout:
  `dotnet run --project eng/tools/WinFormsToAxaml -- path/to/Form.Designer.cs` prints an
  `.axaml` starting point (`-o <file>` writes it) with the original control names kept and
  `TODO:WinForms` comments for everything that needs a manual decision.
- Follow the repository coding guidelines (`.github/copilot-instructions.md`) and the
  Windows Forms control naming conventions (`.github/ui_design_guidelines.md`).
- Keep this document up to date when build or runtime requirements change.
