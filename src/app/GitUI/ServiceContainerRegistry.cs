using System.ComponentModel.Design;
using GitCommands.Settings;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;
using GitUI.ConsoleEmulation;
using GitUI.ConsoleEmulation.ConEmu;
using GitUI.ConsoleEmulation.Mintty;
using GitUI.Hotkey;
using GitUI.Models;
using GitUI.ScriptsEngine;
using GitUI.Shells;
using ResourceManager;

namespace GitUI;

public static class ServiceContainerRegistry
{
    public static void RegisterServices(ServiceContainer serviceContainer)
    {
        ScriptsManager scriptsManager = new();
        HotkeySettingsManager hotkeySettingsManager = new(scriptsManager);

        OutputHistoryModel outputHistoryModel = new(AppSettings.OutputHistoryDepth.Value);
        serviceContainer.GetRequiredService<ISubscribableTraceListener>().TraceReceived += (in message) =>
        {
            // In release builds, all Trace.Write* output is recorded.
            // In debug builds, forward only exceptions and DebugHelper.Trace messages but not all the noisy Debug.Write* output.
#if DEBUG
            const char noBreakSpace = '\u00a0';
            if (message.Contains("Exception") || message.Contains($":{noBreakSpace}"))
#endif
            {
                outputHistoryModel.RecordHistory(message);
            }
        };

        serviceContainer.AddService<IWindowsJumpListManager>(
            new WindowsJumpListManager(serviceContainer.GetRequiredService<IRepositoryDescriptionProvider>()));
        serviceContainer.AddService<IScriptsManager>(scriptsManager);
        serviceContainer.AddService<IScriptsRunner>(scriptsManager);
        serviceContainer.AddService<IHotkeySettingsManager>(hotkeySettingsManager);
        serviceContainer.AddService<IHotkeySettingsLoader>(hotkeySettingsManager);
        serviceContainer.AddService<ISimplePromptCreator>(new SimplePromptCreator());
        serviceContainer.AddService<IFilePromptCreator>(new FilePromptCreator());
        serviceContainer.AddService<IOutputHistoryProvider>(outputHistoryModel);
        serviceContainer.AddService<IOutputHistoryRecorder>(outputHistoryModel);

        IRepositoryCurrentBranchNameCache branchNameCache = new RepositoryCurrentBranchNameCache(
            new RepositoryCurrentBranchNameProvider(serviceContainer.GetRequiredService<IGitExecutorProvider>()));
        InvalidRepositoryRemover invalidRepositoryRemover = new();
        serviceContainer.AddService(branchNameCache);
        serviceContainer.AddService<IInvalidRepositoryRemover>(invalidRepositoryRemover);
        serviceContainer.AddService<IRepositoryHistoryUiService>(new RepositoryHistoryUiService(
            serviceContainer.GetRequiredService<IGitExecutorProvider>(), branchNameCache, invalidRepositoryRemover));

        serviceContainer.AddService<IShellProvider>(new ShellProvider());

        serviceContainer.AddService<IConsoleEmulatorsRegistry>(
            new ConsoleEmulatorsRegistry(
                [
                    new ConEmuConsoleEmulator(serviceContainer.GetRequiredService<IShellProvider>()),
                    new MinttyConsoleEmulator()
                ],
                AppSettings.UseConsoleEmulatorForCommands,
                AppSettings.ConsoleEmulatorName,
                AppSettings.ConEmuStyle,
                () => AppSettings.ConEmuConsoleFont));
    }
}
