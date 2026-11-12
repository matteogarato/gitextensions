using System.Runtime.InteropServices;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtUtils.GitUI;
using GitUI;
using GitUI.Infrastructure;
using Microsoft.VisualStudio.Threading;

namespace UITests.Infrastructure;

public sealed class PuttyHelpersTests
{
    private const string _plink = "plink.exe";

    [WinFormsFact]
    public async Task StartPageantIfConfigured_throw_if_not_UI_thread()
    {
        await TaskScheduler.Default.SwitchTo();

        ((Action)(() => PuttyHelpers.StartPageantIfConfigured(() => null))).Should().Throw<COMException>();
    }

    [WinFormsFact]
    public Task StartPageantIfConfigured_throw_if_not_Plink()
    {
        return RunTestAsync(
            () => ((Action)(() => PuttyHelpers.StartPageantIfConfigured(() => null)))
                .Should()
                .Throw<UserExternalOperationException>()
                .WithMessage(TranslatedStrings.ErrorSshPuTtyNotConfigured),
            "foo.exe");
    }

    [WinFormsFact]
    public Task StartPageantIfConfigured_throw_if_Pageant_not_found()
    {
        return RunTestAsync(
            () => ((Action)(() => PuttyHelpers.StartPageantIfConfigured(() => null)))
                .Should()
                .Throw<UserExternalOperationException>()
                .WithMessage(TranslatedStrings.ErrorFileNotFound)
                .WithInnerException<FileNotFoundException>(),
            _plink);
    }

    [WinFormsTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public Task StartPageantIfConfigured_returns_false_if_key_empty(string? bogusKey)
    {
        // Copy notepad.exe from %SYSTEM32%\notepad.exe to be our plink.exe
        var notepadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);
        var plinkfile = Path.Combine(tempPath, _plink);
        File.Copy(notepadPath, plinkfile, true);

        try
        {
            return RunTestAsync(
                () => PuttyHelpers.StartPageantIfConfigured(() => bogusKey).Should().BeFalse(),
                plinkfile);
        }
        finally
        {
            try
            {
                Directory.Delete(tempPath, true);
            }
            catch
            {
                // no-op
            }
        }
    }

    [WinFormsFact]
    public Task StartPageantIfConfigured_throws_if_key_not_empty_but_absent()
    {
        // Copy notepad.exe from %SYSTEM32%\notepad.exe to be our plink.exe
        var notepadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);
        var plinkfile = Path.Combine(tempPath, _plink);
        File.Copy(notepadPath, plinkfile, true);

        try
        {
            return RunTestAsync(
                () => ((Action)(() => PuttyHelpers.StartPageantIfConfigured(() => "bogus.cer")))
                    .Should()
                    .Throw<UserExternalOperationException>()
                    .WithMessage(TranslatedStrings.ErrorSshKeyNotFound)
                    .WithInnerException<FileNotFoundException>(),
                plinkfile);
        }
        finally
        {
            try
            {
                Directory.Delete(tempPath, true);
            }
            catch
            {
                // no-op
            }
        }
    }

    private static async Task RunTestAsync(Action action, string pageant)
    {
        Form form = new();
        await form.SwitchToMainThreadAsync();
        form.Dispose();

        var configuredSshPath = AppSettings.SshPath;
        var configuredPageant = AppSettings.Pageant;

        try
        {
            AppSettings.SshPath = pageant;
            AppSettings.Pageant = pageant;
            action();
        }
        finally
        {
            AppSettings.SshPath = configuredSshPath;
            AppSettings.Pageant = configuredPageant;
        }
    }
}
