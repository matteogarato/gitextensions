using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Translations;
using GitExtensions.Shims.WinForms;
using GitExtUtils.GitUI.Theming;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.CommitInfo;
using GitUI.Editor;
using GitUI.LeftPanel;
using NSubstitute;

namespace GitExtensionsTests;

public sealed class ViewConstructionTests
{
    [Fact]
    public void Translator_should_find_the_shared_translations()
    {
        Translator.GetAllTranslations().Should().NotBeEmpty();
    }

    [AvaloniaFact]
    public void FormBrowse_should_construct()
    {
        FormBrowse form = new();
        form.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void FormBrowse_refresh_should_notify_repository_changed()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns(Path.GetTempPath());
        module.IsValidGitWorkingDir().Returns(false);

        ILockableNotifier notifier = Substitute.For<ILockableNotifier>();
        IAppTitleGenerator appTitleGenerator = Substitute.For<IAppTitleGenerator>();
        appTitleGenerator.Generate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>()).Returns("Git Extensions");

        IGitUICommands commands = Substitute.For<IGitUICommands>();
        commands.Module.Returns(module);
        commands.RepoChangedNotifier.Returns(notifier);
        commands.GetService(typeof(IAppTitleGenerator)).Returns(appTitleGenerator);

        FormBrowse form = new(commands);
        MenuItem refresh = form.FindControl<MenuItem>("refreshToolStripMenuItem")
            ?? throw new InvalidOperationException("Refresh menu item was not created.");

        refresh.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        notifier.Received(1).Notify();
    }

    [AvaloniaFact]
    public void FormBrowse_should_use_existing_translation_keys_for_menu_headers()
    {
        FormBrowse form = new();
        ITranslation translation = Substitute.For<ITranslation>();
        translation.TranslateItem(
                nameof(FormBrowse),
                "fetchAllToolStripMenuItem",
                "Text",
                Arg.Any<Func<string?>>())
            .Returns("&Fetch translated");

        form.AddTranslationItems(translation);
        form.TranslateItems(translation);

        translation.Received(1).AddTranslationItem(
            nameof(FormBrowse),
            "fetchAllToolStripMenuItem",
            "Text",
            "Fetch &all");
        MenuItem fetchAll = form.FindControl<MenuItem>("fetchAllToolStripMenuItem")
            ?? throw new InvalidOperationException("Fetch-all menu item was not created.");
        fetchAll.Header.Should().Be("_Fetch translated");
    }

    [AvaloniaFact]
    public void FormBrowse_theme_menu_should_apply_and_store_the_selected_theme()
    {
        ThemeId originalTheme = AppSettings.ThemeId;

        try
        {
            IGitModule module = Substitute.For<IGitModule>();
            module.WorkingDir.Returns(Path.GetTempPath());
            module.IsValidGitWorkingDir().Returns(false);

            IAppTitleGenerator appTitleGenerator = Substitute.For<IAppTitleGenerator>();
            appTitleGenerator.Generate(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>()).Returns("Git Extensions");

            IGitUICommands commands = Substitute.For<IGitUICommands>();
            commands.Module.Returns(module);
            commands.GetService(typeof(IAppTitleGenerator)).Returns(appTitleGenerator);

            FormBrowse form = new(commands);
            MenuItem darkTheme = form.FindControl<MenuItem>("darkThemeMenuItem")
                ?? throw new InvalidOperationException("Dark theme menu item was not created.");

            darkTheme.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            AppSettings.ThemeId.Should().Be(ThemeId.DefaultDark);
            Avalonia.Application.Current?.RequestedThemeVariant.Should().Be(Avalonia.Styling.ThemeVariant.Dark);
            Application.SystemColorMode.Should().Be(SystemColorMode.Dark);
            darkTheme.IsChecked.Should().BeTrue();
        }
        finally
        {
            AppSettings.ThemeId = originalTheme;
            ThemePreference.Apply(originalTheme);
        }
    }

    [AvaloniaFact]
    public void RevisionGridControl_should_construct()
    {
        RevisionGridControl control = new();
        control.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void FileStatusList_should_construct()
    {
        FileStatusList control = new();
        control.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void FileViewer_should_construct_and_view_a_patch()
    {
        FileViewer control = new();
        control.ViewPatch("@@ -1,1 +1,1 @@\n-old\n+new\n");
        control.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void CommitInfo_should_construct()
    {
        CommitInfo control = new();
        control.Revision.Should().BeNull();
    }

    [AvaloniaFact]
    public void RepoObjectsTree_should_construct()
    {
        RepoObjectsTree control = new();
        control.SetRefs([]);
        control.SelectedRef.Should().BeNull();
    }
}
