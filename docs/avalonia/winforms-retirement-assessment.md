# WinForms retirement assessment

## Decision

**RETIREMENT GATE: BLOCKED.** Avalonia is not yet a functional replacement for the WinForms
application. Retiring the legacy UI now would remove the only working user-facing implementation
for important Git workflows. The current change preserves the legacy application and integrations;
it does not claim WinForms has been removed.

This assessment uses the source-and-test comparison in
[`feature-parity.md`](./feature-parity.md), the environment results in
[`release-readiness.md`](./release-readiness.md), and the existing Avalonia implementation. A
shared Git service or a constructed Avalonia view is not counted as parity unless the user-facing
operation is exercised and its resulting Git state is asserted.

## Repository and application baseline

- The active branch is `master` at `813f48be0` (`perf: avoid some string allocations by using
  spans`). The worktree was already extensively modified before this retirement task; those
  changes have been preserved.
- The primary solution is `GitExtensions.slnx`. It includes both the WinForms
  `GitExtensions`/`GitUI` application and the Avalonia `GitExtensions.Avalonia`/
  `GitUI.Avalonia` application. `eng/avalonia/GitExtensions.Avalonia.slnx` is the focused
  Avalonia build/test solution.
- The Avalonia executable references `GitUI.Avalonia`, which references shared Git services and
  selected framework-neutral sources. It does not directly reference the legacy `GitUI` project.
- The Avalonia browser can open repositories, display revision history, refs, commit details,
  changed files, text diffs and working-tree state, and stage/unstage whole files. A Linux
  headless test run passed 99/99 tests during this assessment. That is useful slice coverage,
  not complete workflow or platform parity.
- Commit, partial staging, branch checkout/management, pull/push, merge conflict resolution,
  interactive rebase, cherry-pick, revert, reset, stash, settings pages, hotkeys, plugin hosting,
  and other workflows remain missing, partial, or unverified. See the feature matrix for the
  evidence and individual statuses.
- A framework-dependent Linux publish and launch has previously been exercised according to the
  migration records, but a full published interaction smoke test and supported Windows Avalonia
  runtime test remain outstanding.

## Feature-parity status

Statuses below use the required retirement vocabulary; feature-level evidence and caveats remain
in [`feature-parity.md`](./feature-parity.md).

| Feature group | Status | Retirement impact |
| --- | --- | --- |
| Startup, repository open, history/graph, details, changed files, text diffs, working-tree status, whole-file stage/unstage | `VERIFIED_PARTIAL` | Browsing and selected index operations work, but do not complete normal repository workflows. |
| Fetch, merge/rebase recovery display, tools, settings, localization, distribution | `IMPLEMENTED_UNVERIFIED` / `BLOCKED` / `VERIFIED_PARTIAL` as individually listed | Presence is not evidence that operation results, persistence, or platform behavior match WinForms. |
| Commit/amend, partial staging, branch/checkout, tags/remotes, pull/push, conflict resolution, interactive rebase, cherry-pick/revert/reset/stash | `MISSING` | Critical workflows; WinForms is still their functioning implementation. |
| Plugins, credentials, catalog generation, platform tooling | `BLOCKED` or `IMPLEMENTED_UNVERIFIED` | Requires dedicated migration and platform validation; existing plugin contracts are not Avalonia UI contracts. |
| Windows Explorer integration | `INTENTIONALLY_PLATFORM_SPECIFIC` | Preserve and update launch behavior only after Windows packaging/runtime verification. |
| Linux desktop installation and associations | `MISSING` | Not required to retain Git operations but required for a complete installable Linux desktop product. |

No critical workflow currently qualifies as `VERIFIED_PARITY`.

## WinForms dependency-removal inventory

| Component / dependency | Classification | Evidence and action |
| --- | --- | --- |
| `src/app/GitExtensions` and `src/app/GitUI` | `MIGRATION_REQUIRED` | The WinForms app and UI remain in the main solution and are the only functional UI for unported workflows. Keep until critical parity and replacement tests pass. |
| `System.Windows.Forms`, Windows Forms controls, designers and resources in `GitUI` and UI consumers | `MIGRATION_REQUIRED` | The WinForms presentation still owns many commands, dialogs, validation paths and plugin surfaces. Do not delete by namespace or directory sweep. |
| `ResourceManager` and shared tiers' multi-target `net10.0-windows` builds | `MIGRATION_REQUIRED` | Windows flavors are currently built for the legacy application and some Windows-only services. Remove only after all consumers and platform references are traced. |
| `GitExtensions.Shims.WinForms` | `RETAINED_INTENTIONALLY` | This is an in-repository compatibility assembly compiled for the cross-platform port; its project has `UseWindowsForms=false` and is not the Microsoft WinForms runtime. Its name/API still represent migration debt, not proof of a runtime WinForms dependency. |
| `eng/tools/WinFormsToAxaml` | `RETAINED_INTENTIONALLY` | Migration utility, not a runtime UI dependency. Its non-Windows build disables apphost generation and uses `dotnet run`; this avoids restoring a distro-specific apphost pack. |
| `setup/TranslationApp` and `GitUI` reference | `MIGRATION_REQUIRED` | Localization generation currently depends on WinForms UI reflection. Preserve catalog generation until a verified Avalonia/shared translation discovery path replaces it. |
| Plugin implementations and `GitUIPluginInterfaces` | `MIGRATION_REQUIRED` | Several plugins reference `GitUI` directly; other plugins use shared interfaces. Audit and migrate each UI-bearing contract before changing plugin ABI or removing the host. |
| Windows Credential Manager package and Windows credential implementation | `RETAINED_INTENTIONALLY` | Windows credential support is a platform service independent of the WinForms GUI. Keep it; separately implement/test a Linux credential-helper strategy. |
| Windows Explorer shell extension and native askpass helper | `RETAINED_INTENTIONALLY` | Independent Windows integrations. Preserve Windows support and change launch/association targets only with tested Windows packaging. |
| Windows installer/MSI | `MIGRATION_REQUIRED` | Current package source retains the Classic application and includes Windows-specific resources/integrations. Rewire and test it after retirement readiness; do not remove Windows packaging infrastructure. |
| `UseWindowsForms` defaults and framework references | `MIGRATION_REQUIRED` | The root build defaults to WinForms for the legacy solution, while portable Avalonia/shared targets opt out. Keep legacy defaults until the legacy application is removed; the new build guard protects the Avalonia graph. |
| Third-party WinForms controls and packages used by `GitUI` | `MIGRATION_REQUIRED` | References are owned by the active WinForms project. Establish Avalonia replacements and confirm no remaining consumer before package removal. |

## Safe changes in this increment

- Added an MSBuild guard for Avalonia projects and shared projects built with `BuildAvalonia=true`.
  It fails the build if these targets enable `UseWindowsForms`, resolve the
  `System.Windows.Forms` assembly or Windows Forms desktop framework, or directly reference the
  legacy `GitUI`/`GitExtensions` projects.
- The guard does not change the main WinForms app, plugins, Windows credential support, native
  integrations, or MSI. It prevents accidental new coupling while preserving the current
  supported behavior.
- The detailed parity matrix now uses the required retirement status vocabulary for intentionally
  platform-specific Windows Explorer integration and the missing Linux desktop entry.

## Build and test evidence for this increment

- **Passed:** `dotnet build src/app/GitExtensions.Avalonia/GitExtensions.Avalonia.csproj
  -p:UseAppHost=false /v:q` (exit code 0). This proves the Avalonia application can be compiled
  without building the WinForms application.
- **Passed:** the built Avalonia runtime configuration and dependency manifest contain no
  `Microsoft.WindowsDesktop` or `System.Windows.Forms` runtime references.
- **Passed:** invoking the dependency guard with `UseWindowsForms=true` fails with the expected
  diagnostic, confirming that an accidental WinForms opt-in is rejected.
- **Passed:** `dotnet build eng/avalonia/GitExtensions.Avalonia.slnx /v:q`: 0 errors
  (324 warnings).
- **Passed:** `dotnet test` for `GitUI.Avalonia.Tests` with the project-selected Linux runtime:
  99 passed, 0 failed.
- **Fixed:** the focused Avalonia solution no longer requires the unavailable
  `Microsoft.NETCore.App.Host.ubuntu.26.04-x64` package: the migration utility builds without an
  apphost on non-Windows hosts, and the xUnit v3 project selects a portable Linux RID per
  architecture so its required apphost remains enabled.
- **Fixed:** Windows-targeted test projects now build with the `win-x64` RID on non-Windows
  hosts, producing the apphost that MSBuild copies instead of searching for a host-incompatible
  apphost. Their tests still require Windows to execute. Windows build, test, MSI, native
  integration and Avalonia-on-Windows runtime results are not established by this increment.

## Remaining retirement gates

1. Port and test complete core flows, starting with commit (including validation/hooks/index
   semantics), branch/checkout safeguards, pull/push, and merge/rebase recovery.
2. Port or explicitly deprecate settings, hotkeys, search, tools, scripts, submodules, diagnostics,
   clone/init and other supported workflows; preserve configuration compatibility.
3. Define Avalonia plugin extension points and a migration/versioning strategy for existing
   WinForms plugin contracts.
4. Verify Linux credential-helper behavior and external tool execution; retain Windows credential
   and native integration paths.
5. Build, install, and smoke-test the Avalonia application and retained Windows integrations on
   Windows; test the published Linux app against an isolated temporary Git repository.
6. Only after the above, remove WinForms projects, UI-only packages/resources, startup wiring,
   and installer references in separately buildable increments.

Until the critical workflows and platform gates pass, retain the WinForms application as the
functional fallback and describe Avalonia as a partial preview rather than the sole supported UI.
