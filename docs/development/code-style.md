# Code style and quality

This guide records the conventions already used by Git Extensions and how to apply them safely.
It is a baseline for incremental cleanup, not a mandate to rewrite legacy code in one pass.

## Source of truth

- The root [`.editorconfig`](../../.editorconfig) defines indentation, line endings, C# formatting,
  naming preferences, and diagnostic severities. Nested editorconfig files may define scoped
  exceptions, especially for external projects.
- [Directory.Build.props](../../Directory.Build.props), [Directory.Build.targets](../../Directory.Build.targets),
  and [eng/](../../eng/) configure the compiler, nullable analysis, package management, and analyzers.
  Project-specific settings may override these defaults.
- Follow established conventions in the owning project when a rule is not configured centrally.
  Do not change public APIs, serialized names, plugin contracts, or framework targets for stylistic
  consistency alone.

## C# conventions

- Use the repository's configured indentation, braces, line endings, using ordering, and namespace
  style; do not reformat generated files or imported external code as part of an unrelated change.
- Use PascalCase for types and public members, `I`-prefixed interface names, and the configured
  underscore prefix for private fields. Keep existing public names stable unless a compatibility
  review justifies a change.
- Nullable reference types and implicit usings are enabled by the shared build defaults, but
  individual projects can differ. Make null contracts explicit; do not suppress warnings without
  understanding the underlying state.
- The shared build currently selects C# 14. Projects may select another language version or target
  framework when required. Do not unify those values without verifying all consuming projects.
- Prefer small, readable methods and clear responsibilities where a concrete maintenance or
  correctness problem exists. Avoid speculative abstractions and cosmetic file moves.
- Use the existing Git command abstractions and service patterns. Shared Git/domain code should
  remain independent of WinForms or Avalonia unless its responsibility explicitly belongs to a UI.
- Handle errors explicitly, preserve useful context, and avoid logging credentials or other
  sensitive data. Follow the established cancellation and UI-threading patterns for the owning
  application.

## Projects, tests, and compatibility

- Organize files by the responsibility and conventions of their project; do not impose one directory
  hierarchy on the WinForms application, Avalonia application, shared libraries, plugins, and tests.
- Keep WinForms and Avalonia presentation code separate while reusing shared Git services and
  models. Do not change plugin-facing types or members without accounting for the plugin interface
  version and downstream compatibility.
- Tests use xUnit v3, NSubstitute, and AwesomeAssertions. Preserve the method-under-test name and add a
  snake_case behavior suffix; isolate Git operations in temporary repositories and avoid external
  network dependencies in the core suite.
- Generated designer code, generated resources, localization catalogs, XAML-generated artifacts,
  and imported external sources must be changed through their source or generation workflow when
  possible. Do not hand-format generated output.

## Analyzer policy

The repository already uses compiler diagnostics, StyleCop, banned API checks, and selected
threading analyzers through shared build configuration. Keep these checks enabled. Rule exceptions
belong in the narrowest appropriate configuration and need a technical reason; do not add analyzer
packages, suppressions, or global severity changes merely to make a baseline appear clean.

Treat compiler and analyzer results as distinct from formatting results. A repository-wide formatter
finding is not automatically a functional defect; address legacy findings incrementally and inspect
each diff.

## Formatting and verification

The supported formatter is `dotnet format` from the SDK selected by `global.json`. Start with a
small, explicit set of files, review the diff, then verify the same scope:

```shell
dotnet format whitespace GitExtensions.slnx \
  --include src/app/GitCommands/Git/AheadBehindData.cs --no-restore
dotnet format whitespace GitExtensions.slnx \
  --include src/app/GitCommands/Git/AheadBehindData.cs --verify-no-changes --no-restore
```

For the Avalonia-specific solution, substitute
`eng/avalonia/GitExtensions.Avalonia.slnx` and include only files in that solution's scope. The
formatter also supports `style` and `analyzers` commands; limit their scope and review all changes
before accepting them.

Useful build and test commands:

```shell
dotnet build /v:q
dotnet test
dotnet build eng/avalonia/GitExtensions.Avalonia.slnx /v:q -p:UseAppHost=false
dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj -p:UseAppHost=false
```

The root solution targets Windows desktop projects and may build on Linux while those tests cannot
run without the Windows Desktop runtime. The Avalonia commands exercise its cross-platform build
and headless tests. Record platform limitations rather than changing target frameworks to bypass
them.

## Incremental cleanup status

The root solution built with the available .NET 10 SDK (0 errors, 66 MSBuild warnings). The
reported warnings were Git notices about CRLF-to-LF normalization in files already modified in
the working tree; no compiler or analyzer warnings were reported. The Avalonia solution also built
with `UseAppHost=false`.

One whitespace discrepancy in
[`AheadBehindData.cs`](../../src/app/GitCommands/Git/AheadBehindData.cs) was corrected and its
scoped formatter verification passed. The Avalonia headless test project passed 24/24 after this
change. The full `dotnet test` command could not launch 17 Windows-targeted test hosts on Linux
because `Microsoft.WindowsDesktop.App` 10.0.0 is not installed.

Repository-wide whitespace verification currently reports existing formatting differences in both
the main and Avalonia solution scopes. Therefore, full-solution formatting is not yet a passing
gate; the selected source file is now clean within its scope. Continue by correcting small,
reviewed scopes, rerunning their verification, and documenting unresolved legacy or
platform-specific findings. Windows runtime, native C++, and installer validation require a
Windows environment. No analyzer, package, target-framework, or shared build-configuration changes
were made in this initial implementation pass.
