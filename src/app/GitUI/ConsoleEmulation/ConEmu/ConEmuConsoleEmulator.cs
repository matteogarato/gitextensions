using GitUI.Shells;

namespace GitUI.ConsoleEmulation.ConEmu;

internal sealed class ConEmuConsoleEmulator(IShellProvider shellProvider) : IConsoleEmulator
{
    private const string _darkThemeFallback = "<Tomorrow Night>";
    private const string _lightThemeFallback = "<Tomorrow>";

    private static readonly Font _defaultFont = new("Consolas", 12);

    string IConsoleEmulator.Name
    {
        get => "conemu";
    }

    string IConsoleEmulator.DisplayName
    {
        get => "ConEmu";
    }

    bool IConsoleEmulator.IsSupportedInCurrentEnvironment
    {
        get => OperatingSystem.IsWindows();
    }

    IReadOnlyCollection<string> IConsoleEmulator.AvailableThemes { get; } =
    [
        "<Default Windows scheme>",
        "<Base16>",
        "<Cobalt2>",
        "<ConEmu>",
        "<Gamma 1>",
        "<Monokai>",
        "<Murena scheme>",
        "<PowerShell>",
        "<Solarized>",
        "<Solarized Git>",
        "<Solarized (Luke Maciak)>",
        "<Solarized (John Doe)>",
        "<Solarized Light>",
        "<SolarMe>",
        "<Standard VGA>",
        "<tc-maxx>",
        "<Terminal.app>",
        "<Tomorrow>",
        "<Tomorrow Night>",
        "<Tomorrow Night Blue>",
        "<Tomorrow Night Bright>",
        "<Tomorrow Night Eighties>",
        "<Twilight>",
        "<Ubuntu>",
        "<xterm>",
        "<Zenburn>"
    ];

    string? IConsoleEmulator.DefaultTheme
    {
        get => null;
    }

    IConsoleCommandRunner IConsoleEmulator.CreateCommandRunner(ConsoleEmulatorSettings settings)
    {
        return new ConEmuConsoleCommandRunner(settings with
        {
            Theme = ResolveTheme(settings.Theme), Font = settings.Font ?? _defaultFont
        });
    }

    IConsoleShellRunner IConsoleEmulator.CreateShellRunner(ConsoleEmulatorSettings settings)
    {
        return new ConEmuConsoleShellRunner(shellProvider,
            settings with { Theme = ResolveTheme(settings.Theme), Font = settings.Font ?? _defaultFont });
    }

    internal static string ResolveTheme(string? configuredTheme)
    {
        return string.IsNullOrEmpty(configuredTheme)
            ? Application.IsDarkModeEnabled ? _darkThemeFallback : _lightThemeFallback
            : configuredTheme;
    }
}
