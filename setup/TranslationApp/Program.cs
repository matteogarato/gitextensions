using BugReporter;
using GitCommands.Settings;
using GitExtensions.Extensibility.Translations;
using GitExtensions.Extensibility.Translations.Xliff;
using GitExtUtils.GitUI;
using GitUI;
using GitUI.Plugin;
using GitUIPluginInterfaces;
using Microsoft.VisualStudio.Threading;

namespace TranslationApp;

internal static class Program
{
    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // This form created for obtain UI synchronization context only
        using (new Form())
        {
            // Store the shared JoinableTaskContext
            ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        }

        // Force load into the appdomain
        using (BugReportForm dummy = new())
        {
        }

        ManagedExtensibility.Initialise();

        // Required for translation
        PluginRegistry.InitializeAll();

        // We will be instantiating a number of forms using their default constructors.
        // This would lead to InvalidOperationException thrown in GitModuleForm().
        // Set the flag that will stop this from happening.
        GitModuleForm.IsUnitTestActive = true;

        AppSettings.Font = SystemFonts.MessageBoxFont!;

        var neutralItems = TranslationHelpers.LoadNeutralItems();
        var filename = Path.Combine(Translator.GetTranslationDir(), "English.xlf");
        TranslationHelpers.SaveTranslation(null, neutralItems, filename);

        var translationsNames = Translator.GetAllTranslations();
        foreach (var name in translationsNames)
        {
            var translation = Translator.GetTranslation(name);
            var translateItems =
                TranslationHelpers.LoadTranslation(translation, neutralItems);
            filename = Path.Combine(Translator.GetTranslationDir(), name + ".xlf");
            TranslationHelpers.SaveTranslation(translation.First().Value.TargetLanguage!, translateItems, filename);
        }
    }
}
