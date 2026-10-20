# Refactoring assessment

## Overall status

**PARTIALLY COMPLETED.** The C# test framework migration to xUnit v3 with NSubstitute has been
implemented and targeted tests/builds have been run. A cross-platform coverage subset has been
measured. The requested solution-wide architecture refactor, complete audit, and 90% coverage
target are not complete.

This assessment distinguishes changes made for the test migration from other uncommitted changes
already present in the working tree. It does not attribute the latter to this task.

## Initial state and scope

- The repository was on `master`; the worktree was already substantially modified, including
  Avalonia, platform/build, installer, documentation, and production-source changes. There was no
  clean pre-change worktree from which to attribute all findings or establish a comparable baseline.
- The repository structure and boundaries are described in the
  [project map](../../.github/copilot-docs/L1-conceptual/project-map.md) and
  [architecture overview](../../.github/copilot-docs/L1-conceptual/architecture-overview.md):
  `GitExtensions.Extensibility` provides contracts, `GitCommands` owns Git operations and models,
  `GitUI` provides WinForms, and Avalonia UI projects provide the cross-platform presentation.
  Plugins consume the plugin interfaces; native Windows integrations are separate.
- The coverage inventory lists 29 projects under `src/`, 10 external projects, and the build and
  translation tools. Native C++, WiX, external libraries, and Windows-only projects need
  platform-specific validation and are not represented by the Linux Avalonia coverage subset.

### Phase A verification snapshot (2026-10-20)

- The main solution lists 51 projects. The checkout contains 29 project files under `src/` and
  19 under `tests/` (18 test projects plus the `CommonTestUtils` support library).
- The worktree had 369 changed or untracked paths, including 20 under `src/` and 316 under
  `tests/`; 345 paths had tracked diffs. These changes predate this phase and were left untouched.
- `global.json` requests SDK `10.0.100` with `latestFeature` roll-forward; the active SDK was
  `10.0.113`. `Directory.Build.props` sets C# 14, nullable analysis, and warnings-as-errors, with
  StyleCop and banned-API analyzers configured through the shared build targets.
- `dotnet build /v:q` failed with 17 `MSB3030` apphost-copy errors for Windows-targeted test
  projects. The Linux build did not establish a complete solution compilation baseline.
- The Linux-compatible `GitUI.Avalonia.Tests` run passed 99/99 tests using
  `RuntimeIdentifier=linux-x64` and `SelfContained=false`. This verifies that test subset only;
  it does not validate WinForms, Windows-only plugins, or the full solution.
- No new production-code defect or SOLID violation was confirmed in this inventory. The existing
  evidence is sufficient to prioritize baseline/platform coverage work, but not to justify
  solution-wide restructuring, dead-code removal, or performance claims.

## Findings and changes

| Area | Assessment |
| --- | --- |
| Layering and dependency direction | The documented boundaries are appropriate for the existing WinForms/Avalonia split. No solution-wide dependency-graph audit or dependency inversion change was performed. |
| SOLID | No complete SOLID assessment is claimed. No production class was split or interface added in this test migration; doing so without a clean baseline and bounded evidence would risk unrelated behavior changes. |
| Shared behavior / DRY | `AheadBehindData.ToDisplay` is a shared `GitCommands` behavior used by both UI layers, with targeted examples in the Avalonia headless suite and provider tests. No comprehensive semantic-duplication inventory or consolidation was performed in this phase. |
| Dead code | No production or plugin code was removed. Static references alone would not be sufficient to rule out reflection or plugin use. |
| Test dependencies | C# test projects and shared test helpers were migrated to xUnit v3; package versions are centralized, and NSubstitute and AwesomeAssertions remain the mocking/assertion conventions. The `ICSharpCode.TextEditor` submodule worktree was also migrated without a commit or gitlink update. |
| Cross-platform testability | The existing `LaneNodeLocatorTests.cs`, `LaneInfoProviderTests.cs`, and `RevisionGraphRowTests.cs` sources are linked into the Avalonia test project, reusing Windows test cases for graph source compiled into the Avalonia assembly. Added `DynamicProxyGenAssembly2` friend access in that assembly so NSubstitute can substitute its internal graph interfaces. |
| Test lifecycle | NUnit lifecycle patterns were replaced with xUnit fixtures/attributes for application settings, deferred cleanup, STA tests, and assembly-level setup. |
| Errors, async, resources | No solution-wide review or policy change was performed. These areas remain open for separate evidence-based work. |
| Performance | No performance claim is made. No optimization was undertaken or benchmarked. |
| Microsoft C#/.NET conventions | The repository targets .NET 10 / C# 14 and already has centralized build configuration, nullable analysis, StyleCop, and `.editorconfig` rules. No mass reformat or broad analyzer-policy change was made. |

## Verification results

### Build and tests

- **Passed:** `GitUI.Avalonia.Tests`, 99 passed, 0 failed, on Linux, including linked tests for
  `LaneNodeLocator`, `LaneInfoProvider`, and `RevisionGraphRow`.
- **Passed targeted builds:** `CommonTestUtils`, `GitCommands.Tests`, `GitUI.Tests`,
  `GitExtensions.Extensibility.Tests`, `ResourceManager.Tests`, `GitExtUtils.Tests`,
  `BugReporter.Tests`, `GitUI.Avalonia.Tests`, `BugReporter.IntegrationTests`,
  `UI.IntegrationTests`, and the migrated plugin test projects.
- Targeted Windows-framework projects were built serially with
  `RuntimeIdentifier=linux-x64` and `SelfContained=false` where required.
- **Blocked:** execution of Windows-targeted tests; the Linux host does not have
  `Microsoft.WindowsDesktop.App 10.0.0`. The attempted `GitCommands.Tests` run aborted before
  discovery for this reason; it is not reported as a passing test run.
- **Blocked:** a full solution build on this Linux host. Without a runtime identifier, Windows
  apphost copies fail (`MSB3030`); the SDK rejects applying one runtime identifier to the full
  solution (`NETSDK1134`). Disabling apphosts is not a valid workaround because xUnit v3 requires
  them.

### Coverage

The current Cobertura report records **26.77% line coverage** (7,543 / 28,174) and **21.01%
branch coverage** (1,979 / 9,415) across the production assemblies loaded by that subset.
`LaneNodeLocator` reached 100% line and branch coverage; `LaneInfoProvider` reached 100% line and
90% branch coverage; `RevisionGraphRow` reached 98.59% line and 96.29% branch coverage.
`GitUI.Avalonia` reached 70.44% line coverage; other loaded assemblies remain lower. The 90% line
target is **not achieved**. These figures are not solution-wide; see the
[coverage report](../testing/coverage-report.md) and
[production coverage inventory](../testing/coverage-inventory.md).

### Analyzer, formatting, and compatibility checks

- Targeted project builds passed with no build errors in the recorded successful runs.
- A complete solution analyzer/formatting run and a warning comparison against a clean baseline
  were not possible from the already-dirty worktree.
- Linux Avalonia behavior is verified by the 99 passing headless tests; Windows runtime behavior
  remains unverified in this environment.
- No changes were made to public members under `GitExtensions.Extensibility` as part of the test
  migration. Existing worktree changes in that area are outside this phase and may affect the
  plugin interface version if included in a later commit.

## 25-point outcome

| # | Requested report area | Status | Evidence / limitation |
|---:|---|---|---|
| 1 | Initial solution state | PARTIALLY COMPLETED | Branch and dirty worktree noted; no clean baseline available. |
| 2 | Projects analyzed | PARTIALLY COMPLETED | Project map and coverage inventory reviewed; no exhaustive per-project design audit. |
| 3 | Architectural problems | PARTIALLY COMPLETED | Layer boundaries and platform validation gaps recorded; broader findings remain open. |
| 4 | SOLID changes | PARTIALLY COMPLETED | No risky production changes were made without a bounded demonstrated defect. |
| 5 | Duplications found | PARTIALLY COMPLETED | Shared `AheadBehindData` behavior reviewed; no systematic repository-wide inventory. |
| 6 | Duplications removed | RINVIATO PER RISCHIO O COMPATIBILITÀ | No semantic equivalence audit sufficient for safe consolidation. |
| 7 | Verified dead code removed | RINVIATO PER RISCHIO O COMPATIBILITÀ | No production code removed. |
| 8 | Dependencies simplified | COMPLETATO E VERIFICATO | Test dependencies are centralized on xUnit v3; no production dependency graph changes. |
| 9 | Structural refactoring | PARZIALMENTE COMPLETATO | Test lifecycle/wiring migrated and Windows graph tests reused in the cross-platform suite; no production class redesign. |
| 10 | Microsoft conventions | PARZIALMENTE COMPLETATO | Existing .NET 10/C# 14 and analyzer conventions followed; no mass cleanup. |
| 11 | Analyzer rules | RINVIATO PER RISCHIO O COMPATIBILITÀ | No broad analyzer-rule changes; solution-wide baseline absent. |
| 12 | Error handling | RINVIATO PER RISCHIO O COMPATIBILITÀ | No systematic review or changes. |
| 13 | Async/resource handling | RINVIATO PER RISCHIO O COMPATIBILITÀ | No systematic review or changes. |
| 14 | Measured optimization | RINVIATO PER RISCHIO O COMPATIBILITÀ | No optimization or benchmark. |
| 15 | Files/projects changed | COMPLETATO E VERIFICATO | Test projects/helpers, test build wiring, migration docs, and coverage report; see worktree diff. |
| 16 | Tests added/updated | COMPLETATO E VERIFICATO | First-party C# tests migrated to xUnit v3; 99-test Avalonia suite passes with shared graph tests. |
| 17 | Builds | IMPLEMENTATO MA NON VERIFICATO COMPLETAMENTE | Listed test projects built; full solution blocked by Linux/Windows apphost constraints. |
| 18 | Test results | IMPLEMENTATO MA NON VERIFICATO COMPLETAMENTE | 99 Linux headless tests passed; Windows suites could not execute. |
| 19 | Analyzer results | BLOCCATO DALL'AMBIENTE | No full solution run or comparable clean baseline. |
| 20 | Coverage | IMPLEMENTATO MA NON VERIFICATO COMPLETAMENTE | Linux subset measured at 26.77% line / 21.01% branch; 90% target not met. |
| 21 | Windows compatibility | BLOCCATO DALL'AMBIENTE | Windows Desktop runtime unavailable on Linux. |
| 22 | Avalonia/Linux compatibility | COMPLETATO E VERIFICATO | Avalonia headless tests pass 99/99 on Linux. |
| 23 | Residual issues | COMPLETATO E VERIFICATO | Full-suite, coverage, architecture, and platform gaps listed in this report and linked reports. |
| 24 | Deferred refactors | COMPLETATO E VERIFICATO | Broad production restructuring, dead-code removal, performance work, and Windows-only changes deferred pending baseline/evidence. |
| 25 | Recommended next steps | COMPLETATO E VERIFICATO | See the prioritized sequence below. |

## Recommended next steps

1. Establish a clean, reproducible Windows build/test/coverage baseline without mixing the existing
   Avalonia and platform-port worktree changes.
2. Generate per-assembly line and branch coverage for all eligible Windows production assemblies;
   then prioritize uncovered Git operations, destructive safeguards, parsing, and failure paths.
3. Record the project dependency graph and identify one high-impact, bounded production issue before
   changing shared services or public plugin contracts.
4. Run Windows UI/integration tests and validate the `GitExtensions.Extensibility` plugin-interface
   version impact before any changes there are committed.
5. Keep performance work separate and benchmark only a measured hot path.

The test-framework migration report is available in
[`docs/testing/refactoring-assessment.md`](../testing/refactoring-assessment.md).
