namespace GitExtUtils.GitUI.Theming;

public readonly struct ThemeId(string name, bool isBuiltin = false)
{
    private static readonly string _defaultDarkThemeName = new("dark");
    private static readonly string _defaultLightThemeName = new("light");
    private static readonly string _windowsAppColorModeName = new("Windows app color mode");

    /// <summary>
    ///     The filename for the default invariant theme, identifying as "light".
    /// </summary>
    public static string InvariantThemeFileName { get; } = "invariant";

    // DefaultLight is the default invariant theme
    public static ThemeId DefaultDark { get; } = new(_defaultDarkThemeName, true);
    public static ThemeId DefaultLight { get; } = new(_defaultLightThemeName, true);
    public static ThemeId WindowsAppColorModeId { get; } = new(_windowsAppColorModeName, true);

    public string Name { get; } = string.IsNullOrWhiteSpace(name) ||
                                  (isBuiltin && name.Equals(InvariantThemeFileName, StringComparison.OrdinalIgnoreCase))
        ? _defaultLightThemeName
        : name;

    public bool IsBuiltin { get; } = isBuiltin;

    /// <summary>
    ///     Get the default ThemeId for the current Windows SystemColorMode
    /// </summary>
    public static ThemeId ColorModeThemeId
    {
        get => Application.SystemColorMode == SystemColorMode.Dark
            ? DefaultDark
            : DefaultLight;
    }

    public override bool Equals(object? obj)
    {
        return obj is ThemeId other && Equals(other);
    }

    public override int GetHashCode()
    {
        // Name can be null because of default struct constructor
        var nameHashCode = Name is null
            ? 0
            : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        return nameHashCode ^ IsBuiltin.GetHashCode();
    }

    public static bool operator ==(ThemeId left, ThemeId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ThemeId left, ThemeId right)
    {
        return !left.Equals(right);
    }

    private bool Equals(ThemeId other)
    {
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
               IsBuiltin == other.IsBuiltin;
    }

    public override string ToString()
    {
        return Name;
    }
}
