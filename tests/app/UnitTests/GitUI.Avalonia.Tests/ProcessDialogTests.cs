using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.ConsoleEmulation.PlainText;
using GitUI.HelperDialogs;
using GitUI.UserControls;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AvaloniaTests;

/// <summary>
///     Tests for the ported process dialogs: construction, and an end-to-end run of a real
///     git command through <see cref="FormProcess" /> with the plain text console emulation.
/// </summary>
public sealed class ProcessDialogTests : IDisposable
{
    private readonly ServiceContainer _serviceContainer = null!;

    public ProcessDialogTests()
    {
        // ThreadHelper is static, while Avalonia's test context can change between cases.
        // Capture the current headless UI thread for every process-dialog test.
        AvaloniaSynchronizationContext.InstallIfNeeded();
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();

        // Like GitExtensions.Avalonia's ServiceContainerRegistry, reduced to what the
        // process dialogs consume.
        _serviceContainer = new ServiceContainer();
        GitExtUtils.ServiceContainerRegistry.RegisterServices(_serviceContainer);
        FileSystem fileSystem = new();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        _serviceContainer.AddService<IFileSystem>(fileSystem);
        _serviceContainer.AddService<IGitDirectoryResolver>(gitDirectoryResolver);
        GitCommands.ServiceContainerRegistry.RegisterServices(_serviceContainer);
        ServiceContainerRegistry.RegisterServices(_serviceContainer);
    }

    void IDisposable.Dispose()
    {
        _serviceContainer.Dispose();
    }

    private GitUiCommands CreateCommands(string workingDir)
    {
        return new(_serviceContainer,
            new GitModule(_serviceContainer.GetRequiredService<IGitExecutorProvider>(), workingDir));
    }

    [AvaloniaFact]
    public void PasswordInput_should_construct()
    {
        PasswordInput control = new();
        control.Password.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void FormStatus_should_construct()
    {
        using FormStatus form = new(CreateCommands(Path.GetTempPath()), PlainTextConsoleEmulatorsRegistry.Instance,
            false);
        form.ErrorOccurred().Should().BeFalse();
        form.KeepDialogOpen.IsVisible.Should().BeFalse();
    }

    [AvaloniaFact]
    public void FormStatus_should_construct_with_the_designer_constructor()
    {
        // The XAML loader needs this constructor. It builds the dialog exactly like the
        // run-time one does — console included — so the designer shows the real thing.
        using FormStatus form = new();

        form.pnlOutput.Children.Should().NotBeEmpty("the console control belongs to the dialog");
        form.KeepDialogOpen.IsVisible.Should().BeFalse();

        // Only the commands are absent, and asking for them says so.
        form.Invoking(f => _ = f.UiCommands).Should().Throw<InvalidOperationException>()
            .WithMessage("*constructed incorrectly*");
    }

    [AvaloniaFact]
    public void FormStatus_should_be_translated_by_the_designer_constructor()
    {
        var original = AppSettings.CurrentTranslation ?? "";
        try
        {
            AppSettings.CurrentTranslation = "German";
            using FormStatus form = new();

            form.Abort.Content.Should()
                .Be("_Abbrechen", "InitializeComplete must translate the dialog the loader built");
        }
        finally
        {
            AppSettings.CurrentTranslation = original;
        }
    }

    [AvaloniaFact]
    public void FormProcess_should_construct()
    {
        using FormProcess form = new(CreateCommands(Path.GetTempPath()), "version", Path.GetTempPath(), null, true);
        form.Title.Should().StartWith("Process");
        form.ProcessString.Should().NotBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void FormProcess_should_run_a_git_command_and_report_success()
    {
        FormProcess form = new(CreateCommands(Path.GetTempPath()), "version", Path.GetTempPath(), null, false);
        try
        {
            form.Show();

            // The command runs in the background; pump the dispatcher until Done enables OK.
            var stopwatch = Stopwatch.StartNew();
            while (!form.Ok.IsEnabled && stopwatch.ElapsedMilliseconds < 30_000)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(25);
            }

            form.Ok.IsEnabled.Should().BeTrue("the git process should finish and enable OK");
            form.ErrorOccurred().Should().BeFalse();
            form.GetOutputString().Should().Contain("git version");
        }
        finally
        {
            form.Close();
        }
    }

    [AvaloniaFact]
    public void FormProcess_should_report_an_error_for_a_failing_command()
    {
        FormProcess form = new(CreateCommands(Path.GetTempPath()), "definitely-not-a-git-command", Path.GetTempPath(),
            null, false);
        try
        {
            form.Show();

            var stopwatch = Stopwatch.StartNew();
            while (!form.Ok.IsEnabled && stopwatch.ElapsedMilliseconds < 30_000)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(25);
            }

            form.Ok.IsEnabled.Should().BeTrue("the git process should finish and enable OK");
            form.ErrorOccurred().Should().BeTrue();
        }
        finally
        {
            form.Close();
        }
    }
}
