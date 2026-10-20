# Automated testing and coverage strategy

This plan complements the repository's [testing guide](../../.github/copilot-docs/L2-core-platform/testing-guide.md)
and Avalonia-specific [regression strategy](../avalonia/testing-strategy.md). It tracks the measured
coverage goal without treating tests from one UI or platform as proof for the whole solution. The
broader architecture findings and deferred work are in the
[refactoring assessment](../architecture/refactoring-assessment.md).

## Test layers

- **Unit:** xUnit v3 tests with NSubstitute and AwesomeAssertions, in the existing project that owns
  the production contract whenever it can execute on the intended target.
- **Integration:** isolated temporary repositories, real Git where Git semantics are under test,
  and controlled filesystems/processes at external boundaries.
- **UI:** existing WinForms integration infrastructure on Windows and Avalonia Headless tests for
  the cross-platform UI.
- **Platform/native:** platform-specific CI for Windows-only services, native C++, and installer
  behavior; do not count a compile-only check as runtime coverage.

Prefer meaningful assertions for outcomes, failure behavior, cancellation, state preservation, and
boundary conditions. Keep tests isolated from developer repositories, global configuration,
credentials, and external network services.

## Coverage collection

Coverlet's collector is pinned centrally and referenced by test projects through
[`eng/Tests.targets`](../../eng/Tests.targets). The local ReportGenerator tool is pinned in
[`dotnet-tools.json`](../../dotnet-tools.json). [`tests/coverlet.runsettings`](../../tests/coverlet.runsettings)
requests both OpenCover and Cobertura output. CI merges the OpenCover inputs into HTML and summary
reports; the original per-test Cobertura files remain available as machine-readable artifacts.
Test platform attachment copies under `In/` are excluded from the merge to avoid counting a run
twice.

Run the cross-platform test suite with coverage:

```shell
dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj \
  -p:RuntimeIdentifier=linux-x64 \
  -p:SelfContained=false \
  --collect:"XPlat Code Coverage" \
  --settings:tests/coverlet.runsettings \
  --results-directory artifacts/Debug/Coverage
```

Restore and invoke the report tool on primary OpenCover reports (not the duplicate copies below
`In/`):

```shell
dotnet tool restore
dotnet tool run reportgenerator -- \
  "-reports:artifacts/Debug/Coverage/<run-id>/coverage.opencover.xml" \
  "-targetdir:artifacts/Debug/CoverageSummary" \
  "-reporttypes:Html;Cobertura;TextSummary"
```

On Windows CI, the main test matrix collects the complete test suite. Its reports and merged
summary are uploaded as artifacts. On Linux, the Avalonia test workflow collects and publishes its
cross-platform subset. The root Windows Desktop test suite cannot be executed on Linux without the
Windows Desktop runtime.

## Denominator and thresholds

The [coverage inventory](coverage-inventory.md) records every production project found under `src/`,
plus external, build-time, and native areas. No broad assembly exclusion is configured. Test
assemblies do not appear in the observed Coverlet Cobertura output. Generated code exclusions must
be evaluated narrowly and recorded before adding them.

The requested target is at least 90% line coverage per eligible production assembly and overall,
with branch coverage reported separately. The current measured Avalonia-test subset is below that
target and is not representative of the full suite. No threshold gate is enabled yet: the complete
Windows CI baseline and per-assembly report must be measured first, otherwise a provisional gate
could either fail without a remediation baseline or conceal unmeasured projects.

Track actual counts and remaining platform gaps in the
[coverage report](coverage-report.md). Do not claim target completion until all eligible production
assemblies have measured results and any exception is explicit, temporary, and justified.
