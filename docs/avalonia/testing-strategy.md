# Avalonia regression-testing strategy

## Risk tiers

| Tier | Focus | Required assertion |
| --- | --- | --- |
| Tier 1 — data integrity | Git index, commit history, branch/ref changes, merge/rebase recovery, stash, process failure/cancellation | Inspect authoritative Git state after the operation; assert failed/cancelled operations did not report success or destroy unrelated state. |
| Tier 2 — browsing and usability | Repository switching, revisions, refs, graph, details, file selection, diff, status, theme, translation | Assert displayed data corresponds to the selected repository/revision/file and does not come from stale requests. |
| Tier 3 — platform and integration | Linux paths/processes, credentials, tools, plugins, configuration, package, keyboard/accessibility | Execute on the declared OS/runtime and test missing executable, permission, and lifecycle behavior. |

## Existing automated coverage

Run the Avalonia headless test project:

```
dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj
```

On Linux, the project selects a portable Linux runtime identifier for the xUnit v3 test apphost.
Do not pass `UseAppHost=false`: xUnit v3 requires the test host executable.

The suite currently contains 38 tests:

- `FormBrowseTests` exercises repository reload, nested/Unicode repository opening, staged,
  unstaged, untracked and conflict status, tracked/untracked diffs, stage/unstage against real
  temporary repositories, rebase conflict-state presentation, and close-time notification
  cleanup.
- `GraphGoldenImageTests` renders synthetic linear, merge, and crossing-lane graphs. These are
  renderer regressions, not proof of correctness on representative large real histories.
- `ViewConstructionTests` covers view construction, translation discovery, refresh notification,
  and built-in light/dark theme application.
- `ProcessDialogTests` covers dialog construction and process success/failure behavior.

The tests use temporary directories and local Git operations; they do not authenticate or access
external hosting services. xUnit v3, NSubstitute, AwesomeAssertions, and Avalonia Headless are the
test infrastructure.

## Critical workflow coverage gaps

| Workflow | Current status | Recommended next test |
| --- | --- | --- |
| New repository → stage → first commit → clean tree | Stage verified; no Avalonia commit dialog exists | First implement commit UI with WinForms-equivalent message validation/hooks, then assert commit object and clean status. |
| Modify → diff → stage → commit without unrelated staged changes | Diff/staging and preserving unrelated staged paths partly exercised; commit missing | Use mixed staged/unstaged paths; assert committed tree includes only intended index entries. |
| Branch create/checkout/switch with dirty worktree | Not exposed | Add dialog tests with protected dirty state and verify exact HEAD/index/worktree after cancellation/failure. |
| Fetch/pull/push with local bare remote | Fetch handler exists, but local-remote result is not tested; pull/push missing | Use bare remote and two temporary clones; assert refs and trees, including non-fast-forward failures. |
| Merge conflict resolve/continue/abort | Detection display tested; resolution UI and recovery execution absent | Divergent same-line edits; test resolve stage state and compare original/aborted tree. |
| Rebase and interactive rebase | Conflict-state detection tested; setup/continue/abort execution and interactive plan absent | Test conflict continue and abort in isolated repo; assert rewritten history and recovered working tree. |
| Cherry-pick/revert/reset/stash | Not exposed | Add command-level and UI tests before exposing; separately verify stash pop retains stash on conflict and reset targets. |
| Repository navigation and diff | Real local history, selection, tracked/untracked and Unicode path diff tested | Add renamed/deleted/binary/empty/large diff tests and real merge-history lane test. |
| Configuration persistence | Theme preference setter and isolated publish config creation observed; graceful restart not tested | Use isolated XDG home, save/close/reopen and assert theme and unrelated existing settings remain intact. |
| Published Linux application | Framework-dependent publish started from outside checkout in Phase 6; current CI Xvfb smoke is configured, not executed | Repeat publish after source changes; run against isolated repo and verify loaded revision, diff, safe Git operation, config and restart. |
| Windows regression | Root solution builds on Linux; no Windows runtime execution | Run original Windows CI and smoke-test WinForms startup on a Windows runner before release. |

## Test implementation conventions

- Put engine and command-line semantics in shared project tests when possible; use Avalonia
  integration tests only to verify user actions, state propagation, and error presentation.
- Keep repository tests under unique temporary paths; set author/email and required Git config
  per test. Use a local bare remote for remote-operation tests.
- Never use the developer's repository, credentials, or global config as test data.
- Do not rely on network services for the core suite.
- For destructive operations, compare `HEAD`, index, refs, and worktree before and after
  cancellation and failure.
- Use wait conditions with timeouts for async UI state; assert that obsolete repository/revision
  results do not overwrite the current view.
- Run the targeted test, the complete Avalonia suite, then relevant root/shared tests after each
  behavioral change.

## Platform matrix

| Environment | Verified in this phase | Remaining |
| --- | --- | --- |
| Ubuntu 26.04 x64, .NET SDK/runtime 10.0.112 | Root build with `UseAppHost=false` (0 errors, 32 warnings), Avalonia solution build (0 errors, 16 warnings), 24/24 Release headless UI tests, version-stamped Release framework-dependent `linux-x64` publish, copied publish folder launched outside checkout, Unicode/space repository path, isolated first-run config creation, unchanged test-repository HEAD/worktree, `ldd` resolution, SHA-256 manifest verification | Hosted Xvfb workflow, published-window history/diff assertions, graceful shutdown/restart persistence, mutation workflows beyond stage/unstage integration, auth/tool tests |
| Windows x64 | None in this phase beyond root build with the Windows Forms solution under Linux SDK | Windows build/test workflow and runtime smoke |
| macOS | None | No support claim; port/runtime/build review required |
| Linux ARM64 / other distributions | None | RID-specific build, native dependency, and runtime validation |

## Execution record

The Phase 7 baseline was `master` at `813f48be0d91699124f61a1fa6c0fd5e376d3b81`, with
uncommitted Avalonia migration changes already present. After the close-lifecycle fix,
`dotnet build /v:q -p:UseAppHost=false` passed with 0 errors (32 warnings), the Avalonia solution
build passed with 0 errors (16 warnings), and the full Avalonia suite passed 24/24. The first
post-fix suite run exposed a queued selection callback after disposal; a closed-state guard and
event detachment fixed the failure. The Release Linux publish, native dependency check, and
outside-checkout startup/configuration smoke were then rerun successfully.

For Phase 8, the full root build without `UseAppHost=false` failed because the installed Ubuntu
SDK could not restore `Microsoft.NETCore.App.Host.ubuntu.26.04-x64`; the root build succeeded
with that property disabled. The complete Avalonia suite passed again in Release (24/24). The
version-stamped publish was copied to an isolated location outside the checkout and launched
against a temporary local repository; first-run settings-file creation and no repository changes
were verified. SHA-256 checks covered all files in the 144-file publish directory. The process
was terminated by the smoke timeout, so graceful close and preference persistence across restart
remain untested.
