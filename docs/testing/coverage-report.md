# Coverage baseline

## Whole-solution status

**BELOW TARGET / PARTIAL.** A solution-wide baseline has not yet been measured. The complete test
suite requires Windows Desktop runtime projects that cannot execute on the current Linux host.
Do not interpret the cross-platform subset below as solution-wide coverage.

Revision: `813f48be0d91699124f61a1fa6c0fd5e376d3b81` (`master`); the working tree also contained
uncommitted Avalonia and platform-port changes during the measurement.

## Cross-platform Avalonia test subset

Re-measured after the xUnit v3 migration with Coverlet collector and Cobertura:

```shell
dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj \
  -p:RuntimeIdentifier=linux-x64 \
  -p:SelfContained=false \
  --collect:"XPlat Code Coverage" \
  --settings:tests/coverlet.runsettings \
  --results-directory artifacts/Debug/CoverageValidation/graph-tests-final
```

The run passed 99/99 tests. Its generated Cobertura report is
`artifacts/Debug/CoverageValidation/graph-tests-final/2ef67a7e-a10f-4121-9b9b-f2a57ae3399a/coverage.cobertura.xml`.
The report is a build artifact and is not committed.

| Scope in this run | Line coverage | Branch coverage |
| --- | ---: | ---: |
| All production assemblies loaded by this test run (7,543 / 28,174 lines; 1,979 / 9,415 branches) | 26.77% | 21.01% |
| `GitExtensions.Avalonia` | 14.10% | 8.33% |
| `GitUI.Avalonia` | 70.44% | 58.30% |
| `GitExtensions.Shims.WinForms` | 7.48% | 12.19% |
| `GitCommands` | 17.55% | 13.79% |
| `GitExtUtils` | 37.25% | 24.00% |
| `GitUIPluginInterfaces` | 0.00% | 0.00% |
| `ResourceManager` | 0.49% | 0.00% |
| `GitExtensions.Extensibility` | 28.54% | 22.98% |

`LaneNodeLocator` reached 100% line and branch coverage. `LaneInfoProvider` reached 100% line and
90% branch coverage. `RevisionGraphRow` reached 98.59% line and 96.29% branch coverage. The
existing test sources for these shared graph classes are linked into the cross-platform Avalonia
test project, so the same cases also run on Linux rather than only in the Windows-targeted test
project.

The percentages and counts above come directly from the Cobertura report produced by this run.
Do not mix them with counts from OpenCover or other coverage formats, which may represent branch
points differently.

The subset report contains no test assembly, but does include the shared production dependencies
loaded by these tests. It does not include WinForms, plugin, external-library, native, or installer
coverage. The aggregate for this subset is therefore diagnostic only and is not a valid final
solution percentage.

## Environment and outstanding measurements

- Full `dotnet build GitExtensions.slnx -m:1 /v:q`: blocked on this Linux host when Windows
  apphost files were missing (`MSB3030`). Disabling apphosts globally fails because xUnit v3 test
  projects require them; see the architecture assessment for the exact constraints.
- Root `dotnet test`: Windows-targeted test hosts could not start because
  `Microsoft.WindowsDesktop.App` 10.0.0 is unavailable on Linux.
- Avalonia-focused build: succeeded with an apphost and `RuntimeIdentifier=linux-x64`,
  `SelfContained=false`.
- Avalonia headless tests after migration and cross-platform graph-test wiring: 99/99 passed,
  including the coverage run.
- Whole-solution line and branch coverage, coverage per Windows/plugin assembly, and coverage
  thresholds: **not yet measured**.
- Native C++, installer, and Windows runtime tests: **not tested in this environment**.

Use the CI-published Cobertura reports and aggregate HTML report for the next full-solution
measurement. Do not claim the 90% line or 85% branch goals until those reports meet them for every
eligible production assembly.
