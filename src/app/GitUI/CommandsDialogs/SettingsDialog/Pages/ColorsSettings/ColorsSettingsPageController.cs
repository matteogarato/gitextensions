using GitCommands.Git;
using GitCommands.Settings;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;

namespace GitUI.CommandsDialogs.SettingsDialog.Pages;

internal sealed class ColorsSettingsPageController(
    IColorsSettingsPage page,
    IThemeRepository themeRepository,
    IThemePathProvider themePathProvider)
{
    private int _updateThemeSettingsCounter;

    private bool IsCurrentWinAppMode
    {
        get => page.SelectedThemeId == ThemeId.WindowsAppColorModeId &&
               ThemeModule.Settings.Theme.Id == ThemeId.ColorModeThemeId;
    }

    public bool SettingsAreModified
    {
        get => !(page.SelectedThemeId == ThemeModule.Settings.Theme.Id || IsCurrentWinAppMode)
               || !(page.UseSystemVisualStyle == ThemeModule.Settings.UseSystemVisualStyle || IsCurrentWinAppMode ||
                    ThemeModule.Settings.Theme.Id == ThemeId.DefaultLight)
               || !page.SelectedThemeVariations.SequenceEqual(AppSettings.ThemeVariations);
    }

    public void ShowThemeSettings()
    {
        BeginUpdateThemeSettings();
        page.PopulateThemeMenu(
            Enumerable.Repeat(ThemeId.WindowsAppColorModeId, 1).Concat(themeRepository.GetThemeIds()));
        page.SelectedThemeId = AppSettings.ThemeId;
        page.SelectedThemeVariations = AppSettings.ThemeVariations;
        page.UseSystemVisualStyle = IsCurrentWinAppMode
            ? ThemeId.DefaultLight == ThemeId.ColorModeThemeId
            : page.SelectedThemeId == ThemeId.DefaultLight
                ? true
                : AppSettings.UseSystemVisualStyle;

        EndUpdateThemeSettings();
    }

    public void ApplyThemeSettings()
    {
        AppSettings.ThemeId = page.SelectedThemeId;
        AppSettings.ThemeVariations = page.SelectedThemeVariations;
        if (page.SelectedThemeId != ThemeId.WindowsAppColorModeId && page.SelectedThemeId != ThemeId.DefaultLight)
        {
            // set only if UseSystemVisualStyle is not overridden
            AppSettings.UseSystemVisualStyle = page.UseSystemVisualStyle;
        }
    }

    public void HandleSelectedThemeChanged()
    {
        if (IsThemeSettingsUpdating())
        {
            return;
        }

        BeginUpdateThemeSettings();

        if (page.SelectedThemeId == ThemeId.WindowsAppColorModeId)
        {
            // Display the value forced in ThemeModule.LoadThemeSettings(),
            page.UseSystemVisualStyle = ThemeId.ColorModeThemeId == ThemeId.DefaultLight ||
                                        page.SelectedThemeId == ThemeId.DefaultLight;
        }
        else if (page.SelectedThemeId == ThemeId.DefaultLight)
        {
            page.UseSystemVisualStyle = true;
            page.SelectedThemeVariations = ThemeVariations.None;
        }
        else
        {
            try
            {
                // override default (at least dark in .NET10 requires overrides).
                var theme = themeRepository.GetTheme(page.SelectedThemeId, page.SelectedThemeVariations);
                page.UseSystemVisualStyle = theme.SystemColorMode == SystemColorMode.Classic;
            }
            catch (Exception)
            {
                // ignore, popup in EndUpdateThemeSettings()
            }
        }

        EndUpdateThemeSettings();
    }

    public void HandleUseSystemVisualStyleChanged()
    {
        UpdateThemeSettings();
    }

    public void HandleUseColorblindVariationChanged()
    {
        UpdateThemeSettings();
    }

    public void UpdateThemeSettings()
    {
        BeginUpdateThemeSettings();
        EndUpdateThemeSettings();
    }

    public void BeginUpdateThemeSettings()
    {
        _updateThemeSettingsCounter++;
    }

    public bool IsThemeSettingsUpdating()
    {
        return _updateThemeSettingsCounter > 0;
    }

    public void EndUpdateThemeSettings()
    {
        var counter = --_updateThemeSettingsCounter;
        if (counter < 0)
        {
            throw new InvalidOperationException(
                $"{nameof(EndUpdateThemeSettings)} must be called after {nameof(BeginUpdateThemeSettings)}");
        }

        var themeId = page.SelectedThemeId == ThemeId.WindowsAppColorModeId
            ? ThemeId.ColorModeThemeId
            : page.SelectedThemeId;

        if (counter == 0)
        {
            page.LabelRestartIsNeededVisible = SettingsAreModified;
            page.IsChoosingVisualStyleEnabled = page.SelectedThemeId != ThemeId.WindowsAppColorModeId &&
                                                themeId != ThemeId.DefaultLight;
        }

        if (themeId == ThemeId.DefaultLight)
        {
            // invariant, already loaded.
            return;
        }

        try
        {
            _ = themeRepository.GetTheme(themeId, page.SelectedThemeVariations);
        }
        catch (Exception ex)
        {
            page.ShowThemeLoadingErrorMessage(themeId, page.SelectedThemeVariations, ex);
        }
    }

    public void ShowAppThemesDirectory()
    {
        OsShellUtil.SelectPathInFileExplorer(themePathProvider.AppThemesDirectory);
    }

    public void ShowUserThemesDirectory()
    {
        if (themePathProvider.UserThemesDirectory is not null)
        {
            // Make sure the directory exists before we try to open it
            Directory.CreateDirectory(themePathProvider.UserThemesDirectory);

            OsShellUtil.SelectPathInFileExplorer(themePathProvider.UserThemesDirectory);
        }
    }
}
