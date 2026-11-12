using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GitCommands;

namespace GitUI.ConsoleEmulation.Mintty;

internal sealed class MinttyConsoleEmulator : IConsoleEmulator
{
    private const string _preferredDefaultTheme = "monokai-dimmed";

    private IReadOnlyCollection<string>? _availableThemes;

    string IConsoleEmulator.Name
    {
        get => "mintty";
    }

    string IConsoleEmulator.DisplayName
    {
        get => "Mintty";
    }

    bool IConsoleEmulator.IsSupportedInCurrentEnvironment
    {
        get => OperatingSystem.IsWindows() && TryResolvePaths(out _, out _);
    }

    IReadOnlyCollection<string> IConsoleEmulator.AvailableThemes
    {
        get => _availableThemes ??= LoadAvailableThemes();
    }

    string? IConsoleEmulator.DefaultTheme
    {
        get => AvailableThemes.Contains(_preferredDefaultTheme, StringComparer.OrdinalIgnoreCase)
            ? _preferredDefaultTheme
            : null;
    }

    IConsoleCommandRunner IConsoleEmulator.CreateCommandRunner(ConsoleEmulatorSettings settings)
    {
        var (minttyPath, bashPath) = ResolvePaths();
        return new MinttyCommandRunner(minttyPath, bashPath, settings);
    }

    IConsoleShellRunner IConsoleEmulator.CreateShellRunner(ConsoleEmulatorSettings settings)
    {
        var (minttyPath, bashPath) = ResolvePaths();
        return new MinttyShellRunner(minttyPath, bashPath, settings);
    }

    private static (string MinttyPath, string BashPath) ResolvePaths()
    {
        if (!TryResolvePaths(out var minttyPath, out var bashPath))
        {
            throw new InvalidOperationException(
                $"mintty or bash not found. Check {nameof(IsSupportedInCurrentEnvironment)} before calling.");
        }

        return (minttyPath, bashPath);
    }

    private static bool TryResolvePaths([NotNullWhen(true)] out string? minttyPath,
        [NotNullWhen(true)] out string? bashPath)
    {
        var foundMintty = PathUtil.TryFindShellPath(@"usr\bin\mintty.exe", out minttyPath);
        var foundBash = PathUtil.TryFindShellPath("bash.exe", out bashPath);
        return foundMintty && foundBash;
    }

    private static IReadOnlyCollection<string> LoadAvailableThemes()
    {
        if (!TryResolvePaths(out var minttyPath, out _))
        {
            return [];
        }

        try
        {
            var themesDir =
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(minttyPath)!, "..", "share", "mintty", "themes"));
            if (!Directory.Exists(themesDir))
            {
                return [];
            }

            return Directory.EnumerateFiles(themesDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            return [];
        }
    }
}
