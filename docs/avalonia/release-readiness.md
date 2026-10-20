# Avalonia release readiness

## Decision

**Overall status: `INTERNAL_PREVIEW`; release-candidate approval is withheld.**

The tested Linux Avalonia browser can open local repositories, load and render history, show
commit details and text diffs, display staged/unstaged/untracked/conflict state, and stage or
unstage selected files. A framework-dependent Linux publish was launched outside the repository
checkout. The UI still lacks the commit workflow and many standard Git operations, Linux
credential writes are unsupported, and Windows UI runtime behavior was not exercised. Those are
release blockers, not minor parity differences.

Avalonia is now the default solution/debug launch, and the Windows MSI is configured to install
it as the primary shortcut while retaining WinForms as **Git Extensions (Classic)**. The MSI
generation, install, and Windows runtime path have not yet been tested; this source change does
not alter the `INTERNAL_PREVIEW` decision.

See the [Phase 8 candidate assessment](./release-candidate.md) and
[portable installation guide](./installation.md) for the artifact identity, verified commands,
installation limits, workflow results, and release gates.

## Gate outcomes

| Gate | Result | Evidence and remaining condition |
| --- | --- | --- |
| A — Build integrity | `PASS_WITH_DOCUMENTED_LIMITATIONS` | Current `dotnet build GitExtensions.slnx --no-restore -v:minimal` passes with Avalonia included (0 errors, 48 warnings); Avalonia Release tests pass 24/24; a Linux-hosted `win-x64` Avalonia publish produced the apphost. Windows installer build remains untested. |
| B — Core functionality | `FAIL` | Repository opening, history, diff, status, stage/unstage are exercised. Commit, checkout/branch, pull/push, stash, reset and conflict resolution are missing. Fetch and recovery command effects are not verified. |
| C — Data integrity | `PASS_WITH_DOCUMENTED_LIMITATIONS` | Existing stage/unstage integration checks index and unrelated staged content; reset/delete controls are absent. Recovery buttons are present but effects and safeguards are unverified; commit and branch-switch safeguards are missing. |
| D — Linux execution | `PASS_WITH_DOCUMENTED_LIMITATIONS` | Ubuntu 26.04 x64: versioned Release publish copied outside checkout and launched with a temporary Unicode/space-path repository; fresh isolated settings file and unchanged repository state verified; apphost and native-library `ldd` checks passed. Published history/diff interaction and graceful shutdown were not asserted. |
| E — Configuration compatibility | `PASS_WITH_DOCUMENTED_LIMITATIONS` | Existing version-independent settings path/backend is reused; fresh file creation was observed. No prior released Avalonia schema exists; graceful persistence/restart, malformed/read-only settings, and migration were not tested. |
| F — Windows compatibility | `PASS_WITH_DOCUMENTED_LIMITATIONS` | Windows Forms projects compiled in the root build on Linux; no Windows runtime, Windows test suite, or Avalonia-on-Windows validation. |
| G — Packaging | `FAIL` | The Windows MSI source is wired to bundle Avalonia and make it the primary shortcut, but no MSI was built or installed. Linux still has only a portable publish directory; no package or tested uninstall/reinstall lifecycle exists. |
| H — Documentation | `PASS_WITH_DOCUMENTED_LIMITATIONS` | Build/publish and portable launch instructions, requirements, configuration limitations, and troubleshooting are recorded; no public distribution is claimed. |
| I — Security and integrity | `NOT_TESTED` | Published files have a verified SHA-256 manifest. No security review, signature, or provenance attestation was performed. |

`PASS_WITH_DOCUMENTED_LIMITATIONS` means the stated slice ran and the named limitation is
explicit. It is not equivalent to full gate acceptance. A gate with an absent core workflow is
`FAIL`, even if the project builds.

## Verification record

Baseline revision: `813f48be0d91699124f61a1fa6c0fd5e376d3b81` on `master`. The worktree already
contained substantial uncommitted Avalonia work; it was preserved.

Executed during the Phase 6/7/8 sequence:

- Plain `dotnet build /v:q` — failed because the SDK could not resolve
  `Microsoft.NETCore.App.Host.ubuntu.26.04-x64` for Windows Forms projects.
- `dotnet build /v:q -p:UseAppHost=false` — passed, 0 errors (32 warnings).
- `dotnet build eng/avalonia/GitExtensions.Avalonia.slnx /v:q -p:UseAppHost=false` — passed
  after lifecycle changes, 0 errors (16 warnings).
- Avalonia headless tests in Release — 24/24 passed.
- The release version stamper was extended and passed an isolated fixture with the Avalonia
  assembly metadata; the local candidate was stamped `7.3.0-internal.1+813f48b.dirty`.
- Release `linux-x64` framework-dependent publish — produced a 144-file, 50,439,086-byte
  portable folder. A SHA-256 manifest covering all published files verified successfully.
- A copy of that published folder launched outside the checkout on Ubuntu 26.04 x64 against an
  isolated Unicode/space-path repository, created isolated first-run settings, and left the test
  repository unchanged. Native dependencies resolved.
- `git diff --check` passed.

The hosted GitHub Actions/Xvfb path, Windows runtime, visible history/diff interaction in the
published window, graceful shutdown, settings persistence across restart, and an installable
package were not tested.

## Release blockers

1. Implement/test an Avalonia commit workflow with message validation and correct index semantics.
2. Implement/test branch checkout and switching, including dirty worktree safeguards.
3. Implement/test pull/push and local bare-remote integration before authenticated remotes.
4. Implement/test conflict-resolution UI and recovery command execution for merge/rebase.
5. Decide how Linux credentials are supported (system helper integration or an explicit supported
   limitation for the initial internal release).
6. Run the hosted Linux workflow and Windows regression CI; verify the original WinForms app on a
   Windows runner.
7. Validate keyboard accessibility and user-facing error/diagnostic paths.

Until these are resolved, do not use `BETA_CANDIDATE`, `RELEASE_CANDIDATE`, or
`PRODUCTION_READY`. The local artifact is an internal preview only.
