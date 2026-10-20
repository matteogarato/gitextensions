# Avalonia release-candidate assessment

## Decision

**Status: `INTERNAL_PREVIEW`; no release candidate is approved.**

The existing Avalonia UI is useful for controlled internal evaluation of repository browsing,
history, commit details, diffs, working-tree status, and whole-file stage/unstage. It lacks the
commit workflow and other core Git operations. The Windows MSI source is now configured to
launch Avalonia by default with WinForms retained as a Classic shortcut, but that package path
has not been built or tested. This report records a local internal-preview artifact; it is not a
beta candidate or official Git Extensions release.

## Source and candidate identity

| Field | Value |
| --- | --- |
| Branch / base commit | `master` / `813f48be0d91699124f61a1fa6c0fd5e376d3b81` |
| Source tree | Dirty and uncommitted; artifact metadata records a working-tree patch digest |
| Candidate product | Git Extensions Avalonia internal preview |
| Assembly/file version | `7.3.0.0` |
| Informational version | `7.3.0-internal.1+813f48b.dirty` |
| Target framework / RID | `net10.0` / `linux-x64` |
| Publish model | Framework-dependent; requires the .NET 10 runtime |
| Verified OS / architecture | Ubuntu 26.04 x64 only |
| SDK / runtime / Git observed | .NET SDK 10.0.112 / .NET runtime 10.0.12 / Git 2.53.0 |
| Package format | None; portable publish directory only |

The standard version-stamping script now includes the Avalonia assembly metadata so a CI/release
version is applied consistently. The internal preview version is deliberately marked
pre-release. The shared official version base is `7.3.0`; the `.internal.1` sequence is local
preview metadata, not an official CI run number or release version.

## Reproducing the local Linux publish

Run version stamping only in a clean, disposable worktree: the existing tool also stamps the
WinForms assemblies, external assemblies, and native Windows resource files.

```sh
dotnet build /v:q -p:UseAppHost=false
dotnet build eng/avalonia/GitExtensions.Avalonia.slnx /v:q -p:UseAppHost=false
dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj \
  --configuration Release /v:q -p:UseAppHost=false

cd eng
dotnet run --runtime linux-x64 --no-self-contained --file set_version_to.cs -- \
  -v 7.3.0.0 -t '7.3.0-internal.1+813f48b.dirty'
cd ..

dotnet publish src/app/GitExtensions.Avalonia/GitExtensions.Avalonia.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  -p:UseAppHost=true \
  --output artifacts/avalonia-internal-preview/linux-x64
```

The candidate was built from the existing dirty worktree using a byte-for-byte snapshot/restore
around version stamping. A normal clean-worktree build can run the sequence above directly.
After publishing, verify the portable output with:

```sh
cd artifacts/avalonia-internal-preview
sha256sum --check SHA256SUMS
```

## Local artifact inventory

| Artifact | Version | OS / architecture | Format / configuration | Runtime and dependencies | Verification |
| --- | --- | --- | --- | --- | --- |
| `artifacts/avalonia-internal-preview/linux-x64/` (144 files; 50,439,086 bytes) | `7.3.0-internal.1+813f48b.dirty` | Linux / x64 | Framework-dependent portable publish, Release | .NET 10 runtime, Git on `PATH`, compatible Avalonia/Skia desktop libraries | Copied outside the checkout and launched against a temporary repository; `ldd` checks passed |
| `artifacts/avalonia-internal-preview/SHA256SUMS` | Same candidate | Linux / x64 | SHA-256 manifest for all files in the publish directory | n/a | `sha256sum --check SHA256SUMS` passed |
| `artifacts/avalonia-internal-preview/candidate.json` | Same candidate | Linux / x64 | Local build and integrity metadata | n/a | Generated from observed artifact and environment metadata |

The manifest is an integrity check, not a signature. No code signing, provenance attestation, or
byte-for-byte reproducibility claim is made. The artifact is ignored by Git and was not uploaded.
The apphost (`linux-x64/GitExtensions.Avalonia`) SHA-256 is
`e7d7fe8f22db95c43673bdd11a1ff1900be6705ffd7b8cf6d282e9d5693f7395`.
The source-worktree patch SHA-256 recorded at publish time is
`bb15fe909228f0cb318bf9dab2691af1d834786f3a3d343c2c39cff1ea2cad8c`.

## Build and verification record

The repository was cleaned before this phase. A plain root `dotnet build /v:q` failed because
the installed SDK attempted to restore the unavailable
`Microsoft.NETCore.App.Host.ubuntu.26.04-x64` package for Windows Forms projects. The supported
Linux compile check succeeded with `UseAppHost=false`; the explicit `linux-x64` publish uses the
correct runtime identifier.

Executed:

- `dotnet build /v:q -p:UseAppHost=false` — passed, 0 errors, 32 warnings.
- `dotnet build eng/avalonia/GitExtensions.Avalonia.slnx /v:q -p:UseAppHost=false` — passed,
  0 errors, 16 warnings.
- `dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj
  --configuration Release /v:q -p:UseAppHost=false` — passed, 24/24.
- Version stamping — passed in an isolated fixture; the Avalonia assembly received both numeric
  and informational version values.
- Release `linux-x64` publish — passed. The versioning step was run with a byte-for-byte backup
  and restoration of every source/resource file it stamps, preserving the pre-existing dirty
  worktree.
- Publish contents — translation catalogs present; executable bit preserved after copying; all
  apphost and bundled `.so` dependency checks passed.
- Portable-folder launch — passed from an isolated install-copy outside the source tree, with an
  isolated first-run `XDG_CONFIG_HOME` and repository path containing spaces/Unicode. The
  process remained alive for the smoke interval, settings file creation was observed, and the
  temporary repository's HEAD and working tree were unchanged.
- SHA-256 manifest — all 144 published files verified.
- `git diff --check` — passed.

The launch was time-limited rather than closed through the UI. Graceful shutdown and saved
preference persistence across restart were not verified. The smoke did not assert that the published window visibly loaded each history row or diff;
those behaviors are covered by the headless Avalonia integration suite on the same source.

## Default GUI change after candidate generation

The current source now includes Avalonia in `GitExtensions.slnx`, places Avalonia first in the
VS Code launch configurations, and configures the Windows MSI primary shortcuts and post-install
launch to use Avalonia. The WinForms executable remains installed with a **Git Extensions
(Classic)** Start menu shortcut; existing shell integrations and file/protocol associations
continue to use WinForms.

These changes were made after the local Linux preview artifact above was generated, so they are
not present in that artifact. The Avalonia Windows `win-x64` publish succeeded when cross-built
on Linux, but the generated WiX file list, Windows MSI, actual installation, Windows runtime,
and upgrade/uninstall paths remain untested. The default switch does not mean feature parity;
use the Classic UI for workflows that Avalonia does not yet implement.

## End-to-end workflow disposition

| Workflow | Result | Evidence / blocker |
| --- | --- | --- |
| Startup and repository opening | PASS_WITH_LIMITATIONS | Published portable copy launched with a temporary local repository and isolated config; visual repository loading was not asserted against the published UI. |
| History, graph, commit details, and diffs | PASS_WITH_LIMITATIONS | Covered by the 24-test headless Avalonia suite; no published-build visual assertion. |
| Working-tree status and stage/unstage | PASS_WITH_LIMITATIONS | Real-repository tests assert index behavior and preservation of unrelated staged changes; commit UI is absent. |
| Commit creation | FAIL | No Avalonia commit workflow exists. |
| Branch create/checkout/switch | FAIL | No Avalonia branch-management or checkout UI exists. |
| Fetch/pull/push/local remote | FAIL | Fetch handler exists but remote effects are unverified; pull/push are absent. |
| Merge conflict / rebase recovery | FAIL | Operation-state display is tested; resolution and recovery command execution are incomplete/unverified. |
| Stash/reset/cherry-pick/revert | FAIL | Avalonia UI paths are absent. |
| Configuration persistence and upgrade | NOT_TESTED | Fresh settings-file creation was observed; graceful persistence, malformed/read-only config, and previous-Avalonia migration were not tested. |
| Windows runtime regression | NOT_TESTED | Root Windows Forms projects compile in the Linux build; no Windows runner or runtime test was run. |

## Release gates

| Gate | Result | Evidence and remaining work |
| --- | --- | --- |
| A — Build | PASS_WITH_LIMITATIONS | Root and Avalonia builds and Release publish pass with documented Linux host-pack limitation and warnings. |
| B — Core operation | FAIL | Commit, branch/checkout, pull/push, and complete merge/rebase recovery are missing or unverified. |
| C — Data integrity | PASS_WITH_LIMITATIONS | Stage/unstage tests inspect Git index; smoke startup left HEAD/worktree unchanged. Commit/history rewrite and recovery safeguards lack UI coverage. |
| D — Linux execution | PASS_WITH_LIMITATIONS | Actual published portable copy starts on Ubuntu 26.04 x64 and native dependency checks pass; only x64 Ubuntu is tested. |
| E — Configuration compatibility | PASS_WITH_LIMITATIONS | Existing shared settings path/backend is reused; first-run file creation works. No previous released Avalonia version exists, and restart/malformed/read-only cases remain untested. |
| F — Windows compatibility | PASS_WITH_LIMITATIONS | Existing Windows Forms projects compiled in the Linux root build; Windows CI/runtime regression is not tested. |
| G — Packaging | FAIL | Only the Linux portable publish artifact was generated. Windows MSI source changes are unbuilt and untested; no Linux installable package, desktop entry, or verified install/uninstall path exists. Portable-folder copy is not package installation. |
| H — Documentation | PASS_WITH_LIMITATIONS | Build, portable launch, configuration caveats, platform support, and troubleshooting are documented. No public distribution instructions are claimed. |
| I — Security and integrity | NOT_TESTED | SHA-256 artifact checks passed; no security audit, signing, or provenance generation was performed. |

## Preview notes

This preview adds an Avalonia-based Linux desktop shell that reuses Git Extensions' shared Git
engine. In the exercised slice it supports opening a local repository, browsing history and
references, viewing commit details and text diffs, inspecting working-tree changes, staging and
unstaging whole files, selecting built-in light/dark themes, and viewing interrupted-operation
state.

It is not feature parity. Commit/amend, branch and checkout management, pull/push, merge conflict
resolution, rebase setup, stash/reset/cherry-pick/revert, settings pages, plugins, configurable
hotkeys, Linux credential writes, desktop integration, and installable Linux packages are absent
or unverified. The Windows Forms application remains the established Windows UI.

## Actions before any external beta or release candidate

1. Implement and test commit creation, branch checkout/switching, and dirty-worktree safeguards.
2. Implement/test pull/push and merge/rebase setup, resolution, continue, and abort behavior.
3. Decide and validate Linux credential-helper integration; test error reporting and cancellation.
4. Test fresh settings save/restart, existing-config reuse, malformed/read-only configuration, and
   any supported upgrade path without overwriting user data.
5. Run Windows CI and WinForms runtime smoke tests; execute the Linux workflow on its hosted
   runner; assess keyboard/screen-reader accessibility.
6. Decide whether a package/desktop integration is in scope; do not describe the publish folder as
   an installer.
7. Rebuild from a clean, committed source revision, verify version metadata and checksums, and
   obtain maintainer approval before any publication.

No tag, push, package upload, or public release was made.
