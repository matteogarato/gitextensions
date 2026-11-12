using CommonTestUtils;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.Theming;

public class AppColorDefaultsTests : IDisposable
{
    private readonly string _originalPath = null!;

    public AppColorDefaultsTests()
    {
        var testAccessor = AppSettings.GetTestAccessor();
        _originalPath = testAccessor.ApplicationExecutablePath;
        testAccessor.ApplicationExecutablePath = Path.Combine(AppContext.BaseDirectory, "gitextensions.exe");
    }

    void IDisposable.Dispose()
    {
        var testAccessor = AppSettings.GetTestAccessor();
        testAccessor.ApplicationExecutablePath = _originalPath;
    }

    [Fact]
    public void Default_values_are_defined_in_AppColorDefaults()
    {
        foreach (var name in Enum.GetValues<AppColor>())
        {
            var value = AppColorDefaults.GetBy(name);
            value.Should().NotBe(AppColorDefaults.FallbackColor);
        }
    }

    public static IEnumerable<object[]> AppColorCases
    {
        get => Enum.GetValues<AppColor>().Select(value => new object[] { value });
    }

    [Theory]
    [MemberData(nameof(AppColorCases))]
    public void Default_values_are_used_when_not_overridden_in_settings(AppColor name)
    {
        // This only applies when migrating colors from AppSettings to a theme.
        // Fixes https://github.com/gitextensions/gitextensions/issues/11629

        MemorySettings emptySettings = new();

        var defaultColor = AppColorDefaults.GetBy(name);
        var settingsColor = emptySettings.GetColor(name.ToString(), defaultColor);

        settingsColor.Should().Be(defaultColor);
    }

#if SUPPORT_THEMES
    [Fact]
    public void Default_values_are_specified_in_invariant_theme()
    {
        Theme invariantTheme = GetInvariantTheme();
        invariantTheme.Should().NotBeNull();
        foreach (AppColor name in Enum.GetValues(typeof(AppColor)))
        {
            Color value = invariantTheme.GetColor(name);
            value.Should().NotBe(Color.Empty);

            Color defaultValue = AppColorDefaults.GetBy(name);
            value.ToArgb().Should().Be(defaultValue.ToArgb());
        }
    }

    [Fact]
    public void Invariant_theme_colors_match_AppColorDefaults()
    {
        Theme invariantTheme = GetInvariantTheme();
        foreach (AppColor name in Enum.GetValues(typeof(AppColor)))
        {
            Color value = invariantTheme.GetColor(name);
            Color defaultValue = AppColorDefaults.GetBy(name);
            value.ToArgb().Should().Be(defaultValue.ToArgb());
        }
    }
#endif

    private static Theme GetInvariantTheme()
    {
        ThemePathProvider themePathProvider = new();
        ThemeLoader themeLoader = new(new ThemeCssUrlResolver(themePathProvider), new ThemeFileReader());
        ThemeRepository repository = new(new ThemePersistence(themeLoader), themePathProvider);
        return repository.GetInvariantTheme();
    }
}
