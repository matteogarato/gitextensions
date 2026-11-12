using Avalonia.Styling;
using GitExtensions.Shims.WinForms;
using GitExtUtils.GitUI.Theming;
using Application = Avalonia.Application;

namespace GitUI;

/// <summary>
///     Applies the Avalonia theme preference and keeps shared UI color-mode checks in sync.
/// </summary>
public static class ThemePreference
{
    /// <summary>
    ///     Applies the selected built-in theme to the active Avalonia application.
    /// </summary>
    /// <param name="themeId">The saved theme preference.</param>
    /// <exception cref="InvalidOperationException">No Avalonia application is active.</exception>
    public static void Apply(ThemeId themeId)
    {
        if (Application.Current is not { } application)
        {
            throw new InvalidOperationException("The Avalonia application is not initialized.");
        }

        if (themeId == ThemeId.WindowsAppColorModeId)
        {
            // Default makes Avalonia follow the operating system, so the shim mirrors the actual platform variant.
            application.RequestedThemeVariant = ThemeVariant.Default;
            GitExtensions.Shims.WinForms.Application.SystemColorMode =
                application.ActualThemeVariant == ThemeVariant.Dark
                    ? SystemColorMode.Dark
                    : SystemColorMode.Classic;
            return;
        }

        var isDark = themeId == ThemeId.DefaultDark;
        application.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        GitExtensions.Shims.WinForms.Application.SystemColorMode = isDark
            ? SystemColorMode.Dark
            : SystemColorMode.Classic;
    }
}
