using System.ComponentModel.Design;
using GitExtUtils;
using GitUI.ConsoleEmulation;
using GitUI.ConsoleEmulation.PlainText;
using GitUI.Hotkey;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI;

// Twin of GitUI/ServiceContainerRegistry.cs (reduced): services are added here as their
// implementations get ported. ConEmu and Mintty are Windows-only, so the Avalonia UI always
// uses the plain text console emulation.
public static class ServiceContainerRegistry
{
    public static void RegisterServices(ServiceContainer serviceContainer)
    {
        serviceContainer.AddService<IConsoleEmulatorsRegistry>(PlainTextConsoleEmulatorsRegistry.Instance);
        AvaloniaHotkeySettingsManager hotkeySettingsManager = new();
        serviceContainer.AddService<AvaloniaHotkeySettingsManager>(hotkeySettingsManager);
        serviceContainer.AddService<IHotkeySettingsLoader>(hotkeySettingsManager);
        ScriptsManager scriptsManager = new();
        serviceContainer.AddService<IScriptsManager>(scriptsManager);
        serviceContainer.AddService<IScriptsRunner>(scriptsManager);
        serviceContainer.AddService<ISimplePromptCreator>(new SimplePromptCreator());
        serviceContainer.AddService<IFilePromptCreator>(new FilePromptCreator());
    }
}
