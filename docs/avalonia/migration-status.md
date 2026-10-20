# Avalonia migration status

## Summary

The Avalonia work is an incremental UI shell sharing GitExtensions' Git engine, settings backend,
models, translation infrastructure and selected framework-neutral sources. It is not a
feature-complete replacement for WinForms, despite now being the default solution/debug launch
and configured default Windows MSI shortcut. WinForms remains installed as the Classic UI. The checked-out revision is
`813f48be0d91699124f61a1fa6c0fd5e376d3b81`; migration changes are uncommitted.
The feature matrix inventories 47 workflows; none currently meets `VERIFIED_PARITY`.

## Implemented and exercised

- Avalonia startup and main browse window; portable `linux-x64` publish and an outside-checkout
  launch on Ubuntu 26.04 x64.
- Repository open/switch, revision loading, refs, commit details, changed-file selection and
  tracked/untracked text diff display through shared Git modules.
- Working-tree status, file staging and unstaging; integration coverage includes Unicode names,
  conflicts and preserving unrelated staged content.
- Interrupted merge/rebase state display; detection is tested, recovery execution is not.
- Built-in light/dark theme selection backed by existing `AppSettings.ThemeId`.
- Headless graph renderer golden tests.
- Browse-window close cleanup detaches repository notifications, disposes load sequences, and
  guards queued UI callbacks; the latest Linux headless test run passed 99/99 Avalonia tests.
- Release version stamping now includes the Avalonia assembly metadata; a versioned
  framework-dependent `linux-x64` internal-preview publish was tested from a copied folder.
- The main solution and VS Code launch configurations default to Avalonia. The Windows MSI
  source is configured to install Avalonia under `Avalonia\` and use it for primary shortcuts;
  WinForms remains available through the Classic shortcut. Neither the MSI nor Windows runtime
  has been validated.

## Implemented but incomplete or unverified

- Fetch all handler exists, but local remote/network effects are unverified.
- Merge/rebase/patch recovery buttons exist, but command success/failure, refreshed repository
  state and data preservation are untested.
- Translation infrastructure is included; catalog parity/generation could not run in Linux.
- Shared process and external tool machinery is present, but Linux executable/editor/diff/merge
  scenarios are untested.
- Existing settings are loaded/saved, but no Avalonia settings UI exists beyond theme selection.
- Framework-dependent Linux publish is verified locally; graceful shutdown, settings persistence
  across restart, CI Xvfb smoke, and installable packaging are not verified.

## Missing or blocked

Commit/amend, partial staging, checkout/branch/tag management, pull/push/remote configuration,
merge setup and conflict resolution, rebase setup/interactive plans, cherry-pick, revert, reset,
stash, search/filter, file history/blame, settings pages, configurable hotkeys, plugin hosting,
scripts/custom commands, submodules, patch workflows, repository maintenance, Linux credential
writes, desktop integration and installation packages are absent or blocked. The WinForms
implementation remains the complete fallback for Windows workflows not yet ported. Shell
integrations and file/protocol associations still route to WinForms for compatibility.

## Platform statement

Verified platform: Ubuntu 26.04 x64, .NET SDK/runtime 10.0.112. The app uses .NET special-folder
paths for configuration and native Avalonia/Skia libraries. Other Linux distributions,
architectures, Avalonia on Windows, macOS, SSH/credential-helper workflows and external tools are
not validated. Windows Explorer shell integration and the native askpass helper are intentionally
Windows-specific.

See [`feature-parity.md`](feature-parity.md), [`known-issues.md`](known-issues.md),
[`testing-strategy.md`](testing-strategy.md), and [`release-readiness.md`](release-readiness.md)
for detailed evidence and release criteria.
