# Avalonia known issues

This list describes actual migration gaps, not a promise that the feature will be available in a
particular release. The full comparison is in [`feature-parity.md`](feature-parity.md).

## P0 — Critical

No P0 data-loss issue was reproduced in the tested code paths. Do not interpret this as safety
approval for unported destructive actions; those controls are currently absent.

## P1 — High

| Issue | Impact | Current behavior / workaround |
| --- | --- | --- |
| No Avalonia commit workflow | A user can stage changes but cannot finish a normal commit in the Avalonia UI. Commit-message validation, amend/signoff/GPG options, commit hooks, and post-commit refresh are absent. | Use the existing WinForms application for commits. |
| No branch/checkout UI | Creating, renaming, deleting, and switching branches are unavailable, including dirty-tree checks and worktree protections. | Use WinForms or Git CLI deliberately. |
| No pull/push/remote-management UI | Fetch all is present, but pull policies, push tracking/force safeguards, and remote configuration are not ported. | Use WinForms or Git CLI; no authenticated remote behavior is claimed for Avalonia. |
| No full merge/conflict-resolution or rebase setup UI | Operation state is detected and a subset of recovery actions is shown, but conflict resolution, configured mergetool flow, operation execution outcomes, and rebase plan editing are incomplete. | Resolve and continue using WinForms; do not assume the Avalonia banner is a complete recovery workflow. |
| No stash/reset/cherry-pick/revert UI | Common working-tree and history workflows are absent. | Use WinForms. |
| Linux credential writes unsupported | The non-Windows `CredentialsManager` throws `PlatformNotSupportedException` when storing credentials. | Configure a system Git credential helper externally; that path was not tested in this environment. |
| Plugin hosting not initialized in Avalonia | Existing MEF plugins and repository-host UI do not load through the Avalonia app. | Plugin-based workflows are unavailable; use WinForms. |

## P2 — Medium

| Issue | Impact | Current behavior / workaround |
| --- | --- | --- |
| Settings UI and hotkeys incomplete | Existing settings backend is used, but Avalonia exposes only built-in light/dark selection; no settings pages or configurable hotkeys are ported. | Set Git configuration outside the app where applicable. |
| Limited fetch/recovery verification | Fetch has a UI handler but remote results are untested; merge/rebase state tests verify display, not command execution. | Treat network/recovery commands as unverified. |
| External tools not verified on Linux | Shared configuration and command machinery exists, but executable lookup, permission errors, editor, diff/merge tools, SSH agent, and desktop opening were not exercised. | Use supported system Git/editor configuration at your own risk; report failures with details. |
| Persistent diagnostics unclear | Avalonia enables trace logging but no supported persistent log path was identified. Existing settings save failures can be swallowed by shared code. | Preserve terminal/trace output when reporting a failure; diagnostics need a dedicated follow-up. |
| Translation catalog generation is Windows-bound | The generator project is a WinExe depending on WinForms assemblies; Linux catalog regeneration could not be performed. | Ensure any UI strings are catalog-verified in Windows CI. |
| Accessibility not assessed | No formal screen-reader, keyboard-only, focus-order, contrast, or scaling assessment is recorded. | No accessibility conformance claim. |
| No file-history/blame/search/submodule/maintenance dialogs | The WinForms features exist but have no Avalonia counterpart. | Use WinForms. |

## P3 — Low / distribution

- No Linux desktop entry, icon installation, file association, Flatpak, AppImage, Debian/RPM
  package, or install/uninstall procedure exists.
- The current publish is framework-dependent `linux-x64`; the destination requires .NET 10 runtime,
  Git, and compatible native desktop libraries.
- Linux runtime evidence is limited to Ubuntu 26.04 x64. Other distributions, Linux ARM64, Windows
  Avalonia runtime, and macOS are unverified.
- Visual parity is incomplete; the main Avalonia browse window intentionally has a small subset
  of WinForms menus and toolbars.

## Recently fixed

- SDK selection now uses a valid .NET 10 feature-band SDK version and roll-forward policy.
- Avalonia executable publishing is enabled locally without changing repository-wide publish
  defaults.
- The shared release version stamper now updates Avalonia assembly metadata. The produced local
  artifact is still an uncommitted internal preview, not an approved release candidate.
- Browse windows detach their repository-change handler and invalidate/dispose outstanding load
  sequences when closed; queued UI callbacks now return without issuing requests after closure.
  The close-notification and interrupted-rebase recovery tests pass as part of the 24-test suite.
