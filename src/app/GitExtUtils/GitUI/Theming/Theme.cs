namespace GitExtUtils.GitUI.Theming;

/// <summary>
///     A set of values for .Net system colors and GitExtensions app-specific colors.
/// </summary>
public class Theme(
    IReadOnlyDictionary<AppColor, Color> appColors,
    IReadOnlyDictionary<KnownColor, Color> sysColors,
    ThemeId id)
    : IThemeSerializationData
{
    private static Theme? _default;

    public static Theme Default
    {
        get => _default ??= CreateDefaultTheme();
    }

    public ThemeId Id { get; } = id;

    /// <summary>
    ///     Get the Windows SystemColorMode for this theme, based on the background color.
    /// </summary>
    public SystemColorMode SystemColorMode
    {
        get => new HslColor(GetColor(AppColor.PanelBackground)).L < 0.5
            ? SystemColorMode.Dark
            : SystemColorMode.Classic;
    }

    /// <summary>
    ///     GitExtension app-specific color identifiers.
    /// </summary>
    public static IReadOnlyCollection<AppColor> AppColorNames { get; } =
        Enum.GetValues<AppColor>();

    /// <summary>
    ///     .Net system color identifiers.
    /// </summary>
    private static IReadOnlyCollection<KnownColor> SysColorNames { get; } =
        [.. Enum.GetValues<KnownColor>().Where(c => IsSystemColor(c))];

    IReadOnlyDictionary<AppColor, Color> IThemeSerializationData.AppColorValues
    {
        get => appColors;
    }

    IReadOnlyDictionary<KnownColor, Color> IThemeSerializationData.SysColorValues
    {
        get => sysColors;
    }

    /// <summary>
    ///     Get GitExtensions app-specific color value as defined by this instance. If not defined,
    ///     returns <see cref="Color.Empty" />.
    /// </summary>
    public Color GetColor(AppColor name)
    {
        return appColors.TryGetValue(name, out var result)
            ? result
            : Color.Empty;
    }

    /// <summary>
    ///     Get .Net system color value as defined by this instance.
    /// </summary>
    private Color GetSysColor(KnownColor name)
    {
        return sysColors.TryGetValue(name, out var result)
            ? result
            : Color.Empty;
    }

    /// <summary>
    ///     Get .Net system color value as defined by this instance. If not defined, returns
    ///     <see cref="Color.Empty" />.
    /// </summary>
    public Color GetColor(KnownColor name)
    {
        if (!IsSystemColor(name))
        {
            throw new ArgumentException($"{name} is not system color");
        }

        return GetSysColor(name);
    }

    /// <summary>
    ///     Get .Net system color value as defined by this instance. If not defined, returns
    ///     actual .Net <see cref="SystemColors" /> color.
    /// </summary>
    public Color GetNonEmptyColor(KnownColor name)
    {
        var result = GetColor(name);
        if (result == Color.Empty)
        {
            return Color.FromKnownColor(name);
        }

        return result;
    }

    public static Theme CreateDefaultTheme(string[]? variations = null)
    {
        var appColors =
            AppColorNames.ToDictionary(name => name, name => AppColorDefaults.GetBy(name, variations));
        var sysColors = SysColorNames.ToDictionary(name => name, GetFixedColor);
        return new Theme(appColors, sysColors, ThemeId.DefaultLight);
    }

    /// <summary>
    ///     Get .Net system color value as defined by system.
    ///     The value is converted to fixed RGB as opposed to <see cref="SystemColors" /> color
    ///     which takes value from theme colors table which may change.
    ///     This method is needed because we trick .Net to assume modified <see cref="SystemColors" />
    ///     values to apply custom color scheme to GitExtensions, but still need access to values as
    ///     before our modifications.
    ///     This method should only be called before our modifications to .Net system colors.
    /// </summary>
    private static Color GetFixedColor(KnownColor systemColor)
    {
        return Color.FromArgb(Color.FromKnownColor(systemColor).ToArgb());
    }

    /// <summary>
    ///     Whether <see cref="KnownColor" /> represents Windows theme - defined color,
    ///     produces same result as <see cref="Color.IsSystemColor" /> without the need to
    ///     create <see cref="Color" /> instance.
    /// </summary>
    private static bool IsSystemColor(KnownColor name)
    {
        return name is (< KnownColor.Transparent or > KnownColor.YellowGreen);
    }

    internal static class TestAccessor
    {
        public static IReadOnlyCollection<KnownColor> SysColorNames
        {
            get => Theme.SysColorNames;
        }
    }
}
