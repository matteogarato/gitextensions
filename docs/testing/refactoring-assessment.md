# Technical report: test framework migration and architecture assessment

For the wider architecture and coverage assessment, see
[`docs/architecture/refactoring-assessment.md`](../architecture/refactoring-assessment.md).

## Status

The NUnit-to-xUnit migration requested for C# tests is implemented and verified in the projects
listed below. This report does **not** claim that a repository-wide production architecture
refactor or a complete SOLID/DRY audit has been performed.

## Changes completed

- Migrated first-party C# tests to xUnit v3, retaining NSubstitute for substitutes and
  AwesomeAssertions for assertions.
- Centralized xUnit v3 packages and test-project wiring. xUnit v3 3.2.2 is aligned with
  `Avalonia.Headless.XUnit` 12.1.0; the runner is 3.1.5, `Xunit.StaFact` 3.0.13, and
  `Verify.XunitV3` 28.16.0.
- Replaced NUnit lifecycle patterns with xUnit fixtures and test attributes, including shared
  application settings, deferred cleanup, STA UI tests, and snapshot verification.
- Updated the testing guide and run-tests skill to describe the xUnit v3 conventions.
- Migrated the tests in the nested `ICSharpCode.TextEditor` repository. Those changes remain in
  its worktree; no commit or submodule pointer update was made.

## Verification

- **Passed:** `GitUI.Avalonia.Tests` on Linux — 48 passed, 0 failed, including 10 linked
  graph-locator cases.
- **Passed builds:** `CommonTestUtils`, `GitCommands.Tests`, `GitUI.Tests`,
  `GitExtensions.Extensibility.Tests`, `ResourceManager.Tests`, `GitExtUtils.Tests`,
  `BugReporter.Tests`, `GitUI.Avalonia.Tests`, `BugReporter.IntegrationTests`,
  `UI.IntegrationTests`, and the migrated plugin test projects (GitUI plugin interfaces,
  DeleteUnusedBranches, FindLargeFiles, AppVeyor, Azure DevOps, GitHub Actions, GitLab, Jenkins,
  and ReleaseNotesGenerator). Builds were run serially; Windows-targeted projects used
  `RuntimeIdentifier=linux-x64` and `SelfContained=false` where required.
- A full solution build without a runtime identifier fails when it tries to copy Windows
  apphosts unavailable on this Linux host (`MSB3030`). A solution-wide runtime identifier is not
  supported by the SDK for this solution (`NETSDK1134`). Disabling apphosts globally is not a
  valid workaround: xUnit v3 rejects test projects without an apphost.
- Windows-targeted test execution could not be verified here because
  `Microsoft.WindowsDesktop.App 10.0` is unavailable on Linux. No complete suite run or coverage
  collection is claimed.
- NUnit references were searched in source and test infrastructure. The only remaining match is
  historical patch content in `GitCommands.Tests/Patches/testdata/big.patch`, not a live
  dependency or test.

## Architecture and measurement limits

- No production project was changed as part of this migration. The work addresses the explicit
  C# test-framework requirement, not production-layer restructuring.
- The working tree was already dirty when this work began. A reliable pre-change build, test,
  coverage, and performance baseline could not be established, so no coverage percentage,
  performance improvement, or complete SOLID/DRY compliance claim is made.
- A repository-wide architecture audit against the attachment's full checklist remains
  outstanding. Production refactors should follow only after recording a clean baseline and
  selecting a bounded change with its owning subsystem, callers, contracts, and tests identified.
