namespace GitExtUtils.GitUI.Theming;

public class ThemeSettings(
    Theme theme,
    Theme invariantTheme,
    string[] variations,
    bool useSystemVisualStyle)
{
    private static ThemeSettings? _default;

    public static ThemeSettings Default
    {
        get => _default ??= new ThemeSettings(Theme.Default, Theme.Default, ThemeVariations.None, true);
    }

    public Theme Theme { get; } = theme;

    public Theme InvariantTheme { get; } = invariantTheme;

    public string[] Variations { get; } = variations;

    public bool UseSystemVisualStyle { get; } = useSystemVisualStyle;
}
