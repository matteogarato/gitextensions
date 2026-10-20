# Production-code coverage inventory

This inventory tracks managed and native production projects in the checked-out solution. The
solution contains 29 projects under `src/`, 10 projects under `externals/`, one build tool under
`eng/tools/`, and the translation generator under `setup/`. Test projects are listed separately;
their source is not part of the production coverage denominator.

Coverage is not yet measured for the whole solution. `Avalonia suite` below means the project was
loaded by the Avalonia test run and appears in that run's Cobertura report; it does not mean the
assembly has sufficient coverage. All other values are explicitly pending measurement.

## Application and shared production projects

| Project | Target framework(s) | Existing test project(s) | Coverage status |
| --- | --- | --- | --- |
| `BugReporter` | `net10.0-windows` | `BugReporter.Tests`, `BugReporter.IntegrationTests` | Pending Windows run |
| `GitCommands` | `net10.0`, `net10.0-windows` | `GitCommands.Tests` | Measured in Avalonia suite; partial |
| `GitExtensions.Analyzers.CSharp` | `netstandard2.0` | None found | Pending; analyzer testing strategy needed |
| `GitExtensions.Avalonia` | `net10.0` | `GitUI.Avalonia.Tests` | Measured in Avalonia suite; partial |
| `GitExtensions.Extensibility` | `net10.0`, `net10.0-windows` | `GitExtensions.Extensibility.Tests` | Measured in Avalonia suite; partial |
| `GitExtensions` (WinForms app) | `net10.0-windows` | `UI.IntegrationTests` | Pending Windows run |
| `GitExtensions.Shims.WinForms` | `net10.0` | No dedicated project found | Measured in Avalonia suite; partial |
| `GitExtUtils` | `net10.0`, `net10.0-windows` | `GitExtUtils.Tests` | Measured in Avalonia suite; partial |
| `GitUI.Avalonia` | `net10.0` | `GitUI.Avalonia.Tests` | Measured in Avalonia suite; partial |
| `GitUI` (WinForms UI) | `net10.0-windows` | `GitUI.Tests`, `UI.IntegrationTests` | Pending Windows run |
| `ResourceManager` | `net10.0`, `net10.0-windows` | `ResourceManager.Tests` | Measured in Avalonia suite; partial |

## Plugin production projects

| Project | Target framework | Existing test project(s) | Coverage status |
| --- | --- | --- | --- |
| `AutoCompileSubmodules` | `net10.0-windows` | None found | Pending |
| `BackgroundFetch` | `net10.0-windows` | None found | Pending |
| `AppVeyorIntegration` | `net10.0-windows` | `AppVeyorIntegration.Tests` | Pending Windows run |
| `AzureDevOpsIntegration` | `net10.0-windows` | `AzureDevOpsIntegration.Tests` | Pending Windows run |
| `GitHubActionsIntegration` | `net10.0-windows` | `GitHubActionsIntegration.Tests` | Pending Windows run |
| `GitlabIntegration` | `net10.0-windows` | `GitlabIntegration.Tests` | Pending Windows run |
| `JenkinsIntegration` | `net10.0-windows` | `JenkinsIntegration.Tests` | Pending Windows run |
| `TeamCityIntegration` | `net10.0-windows` | No dedicated project found | Pending |
| `CreateLocalBranches` | `net10.0-windows` | No dedicated project found | Pending |
| `DeleteUnusedBranches` | `net10.0-windows` | `DeleteUnusedBranches.Tests` | Pending Windows run |
| `FindLargeFiles` | `net10.0-windows` | `FindLargeFiles.Tests` | Pending Windows run |
| `GitHub3` | `net10.0-windows` | No dedicated project found | Pending |
| `GitUIPluginInterfaces` | `net10.0`, `net10.0-windows` | `GitUIPluginInterfaces.Tests` | Measured in Avalonia suite; partial |
| `Gource` | `net10.0-windows` | No dedicated project found | Pending |
| `ProxySwitcher` | `net10.0-windows` | No dedicated project found | Pending |
| `ReleaseNotesGenerator` | `net10.0-windows` | `ReleaseNotesGenerator.Tests` | Pending Windows run |
| `GitImpact` | `net10.0-windows` | No dedicated project found | Pending |
| `GitStatistics` | `net10.0-windows` | No dedicated project found | Pending |

## External, build-time, and native projects

| Project | Target framework | Project role | Coverage treatment |
| --- | --- | --- | --- |
| `ConEmuInside` | .NET Framework 4.0 Client | External executable | Not yet measured; Windows-only legacy project |
| `ConEmuWinForms` | `net10.0-windows` | External UI library | Not yet measured; production dependency |
| `ConsoleUtilityShowcase` | .NET Framework 4.5 | External sample | Development sample, not a shipped production assembly |
| `ControlDllTestbed` | .NET Framework 4.5.2 | External testbed | Development/test utility, not a shipped production assembly |
| `Git.hub` | `net10.0-windows` | External library | Not yet measured; production dependency |
| `Git.hub.Demo` | .NET Framework 4.0 Client | External sample | Development sample, not a shipped production assembly |
| `ICSharpCode.TextEditor` | `net10.0-windows` | External UI library | Not yet measured; production dependency |
| `ICSharpCode.TextEditor.Sample` | `net10.0-windows` | External sample | Development sample, not a shipped production assembly |
| `ICSharpCode.TextEditor.Tests` | `net10.0-windows` | External test project | Not in the main solution's test run; coverage pending |
| `NetSpell.SpellChecker` | `net10.0-windows` | External library | Not yet measured; production dependency |
| `setup/TranslationApp` | `net10.0-windows` | Build-time localization tool | Pending decision on executable-code coverage scope |
| `eng/tools/WinFormsToAxaml` | `net10.0` | Developer tool | Pending decision on executable-code coverage scope |
| Native C++ and WiX installer | Native C++ solutions under `src/native/`; WiX project under `setup/installer/` | Non-managed production/build code | Not instrumented by Coverlet; requires separate Windows-native validation and an explicit coverage strategy. |

No production assembly is currently excluded by a coverage filter. Generated source, if any, has
not yet been reviewed for narrowly justified exclusions.

## Test projects discovered

The checked-out tree contains `GitCommands.Tests`, `GitUI.Tests`, `GitUI.Avalonia.Tests`,
`GitExtUtils.Tests`, `GitExtensions.Extensibility.Tests`, `BugReporter.Tests`,
`ResourceManager.Tests`, `UI.IntegrationTests`, `BugReporter.IntegrationTests`,
`GitUIPluginInterfaces.Tests`, `DeleteUnusedBranches.Tests`, `FindLargeFiles.Tests`,
`ReleaseNotesGenerator.Tests`, `AppVeyorIntegration.Tests`, `AzureDevOpsIntegration.Tests`,
`GitHubActionsIntegration.Tests`, `GitlabIntegration.Tests`, and `JenkinsIntegration.Tests`.
The `tests/CommonTestUtils` project is a test-support library, not a
test project. Test-project existence is not evidence that tests pass or cover the associated
production project. All first-party test projects target `net10.0-windows` except
`GitUI.Avalonia.Tests`, which targets `net10.0`.

## Next inventory updates

1. Run the instrumented full Windows test matrix and generate an aggregate report.
2. Record line and branch coverage for every assembly, including projects with no dedicated tests.
3. Add focused tests for uncovered behavior and assign uncovered projects to a test layer.
4. Track native, installer, translation, and developer-tool scope separately rather than silently
   treating them as covered or removing them from the denominator.
