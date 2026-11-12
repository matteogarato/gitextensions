namespace GitUI.ConsoleEmulation.PlainText;

internal sealed class PlainTextConsoleEmulatorsRegistry : IConsoleEmulatorsRegistry
{
    private PlainTextConsoleEmulatorsRegistry()
    {
    }

    public static PlainTextConsoleEmulatorsRegistry Instance { get; } = new();

    IReadOnlyCollection<IConsoleEmulator> IConsoleEmulatorsRegistry.AvailableConsoleEmulators
    {
        get => [];
    }

    IConsoleCommandRunner IConsoleEmulatorsRegistry.CreateCommandController()
    {
        return new PlainTextConsoleCommandRunner();
    }

    IConsoleShellRunner? IConsoleEmulatorsRegistry.CreateShellRunner()
    {
        return null;
    }
}
