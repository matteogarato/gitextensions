namespace GitExtensions.Shims.WinForms;

/// <summary>
///     Stand-in for <c>System.Drawing.SystemFonts</c>. The Avalonia application overwrites
///     <see cref="MessageBoxFont" /> at startup with the actual system font; the initial value is
///     a data-only default that the UI framework resolves with its own fallback rules.
/// </summary>
/// <remarks>
///     Consumed by: <c>GitCommands/Settings/AppSettings.cs</c> (default commit/application fonts).
/// </remarks>
public static class SystemFonts
{
    private const string _defaultFamilyName = "Segoe UI";
    private const float _defaultSize = 9F;

    /// <summary>
    ///     Gets or sets the font used for message boxes and as the general UI default.
    ///     Nullable to match the WinForms signature; never actually <see langword="null" /> here.
    /// </summary>
    public static Font? MessageBoxFont { get; set; } = new(_defaultFamilyName, _defaultSize);

    /// <summary>
    ///     Gets or sets the default UI font.
    ///     Nullable to match the WinForms signature; never actually <see langword="null" /> here.
    /// </summary>
    public static Font? DefaultFont { get; set; } = new(_defaultFamilyName, _defaultSize);
}
