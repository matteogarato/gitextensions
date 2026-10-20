using Avalonia;
using Avalonia.Styling;
using GitExtUtils.GitUI.Theming;

namespace GitUI;

/// <summary>
///  Applies the Avalonia theme preference and keeps shared UI color-mode checks in sync.
/// </summary>
public static class ThemePreference
{
    /// <summary>
    ///  Applies the selected built-in theme to the active Avalonia application.
    /// </summary>
    /// <param name="themeId">The saved theme preference.</param>
    /// <exception cref="InvalidOperationException">No Avalonia application is active.</exception>
    public static void Apply(ThemeId themeId)
    {
        if (Application.Current is not { } application)
        {
            throw new InvalidOperationException("The Avalonia application is not initialized.");
        }

        bool isDark = themeId == ThemeId.DefaultDark;
        application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        GitExtensions.Shims.WinForms.Application.SystemColorMode = isDark
            ? GitExtensions.Shims.WinForms.SystemColorMode.Dark
            : GitExtensions.Shims.WinForms.SystemColorMode.Classic;
    }
}
