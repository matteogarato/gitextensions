using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using NSubstitute;

namespace GitUITests.Theming;

public class ThemeLoaderTests
{
    private const string _mockAppThemesDirectory = "c:\\gitextensions\\themes";
    private const string _mockUserThemesDirectory = "c:\\appdata\\gitextensions\\themes";

    private static readonly IReadOnlyCollection<KnownColor> _themableSystemColorNames =
        Theme.TestAccessor.SysColorNames;

    private static readonly IReadOnlyCollection<Color> _testColorValues =
        new[] { Color.Red, Color.Green };

    private static readonly IReadOnlyCollection<Color> _alternativeTestColorValues =
        new[] { Color.LightCoral, Color.LightGreen };

    private static readonly IReadOnlyCollection<AppColor> _appColorNames = Enum.GetValues<AppColor>();

    public static IEnumerable<object[]> AnyThemableSystemColorCases
    {
        get => _themableSystemColorNames.SelectMany(colorName =>
            _testColorValues.Select(testColorValue => new object[] { colorName, testColorValue }));
    }

    public static IEnumerable<object[]> AnyAppColorCases
    {
        get => _appColorNames.SelectMany(colorName =>
            _testColorValues.Select(color => new object[] { colorName, color }));
    }

    public static IEnumerable<object[]> ColorblindSystemColorCases
    {
        get => _themableSystemColorNames.Select(colorName => new object[] { colorName });
    }

    public static IEnumerable<object[]> ColorblindAppColorCases
    {
        get => _appColorNames.Select(colorName => new object[] { colorName });
    }

    public static IEnumerable<object[]> CssCommentCases
    {
        get => _testColorValues.Select(testColorValue => new object[] { KnownColor.Control, testColorValue });
    }

    public static IEnumerable<object[]> CssImportCases
    {
        get => _testColorValues.Select(baseColor => new object[] { KnownColor.Control, baseColor });
    }

    public static IEnumerable<object[]> ImportedColorOverrideCases
    {
        get => _testColorValues.SelectMany(baseColor =>
            _alternativeTestColorValues.Select(colorOverride =>
                new object[] { KnownColor.Control, baseColor, colorOverride }));
    }

    [Theory]
    [MemberData(nameof(AnyThemableSystemColorCases))]
    public void Should_load_any_themable_system_color(KnownColor colorName, Color testColorValue)
    {
        var mockFileReader = CreateMockFileReader(GetThemeContent(colorName, testColorValue));
        var mockCssUrlResolver = Substitute.For<IThemeCssUrlResolver>();
        ThemeLoader loader = new(mockCssUrlResolver, mockFileReader);

        var theme = LoadTheme(loader);

        theme.GetColor(colorName).ToArgb().Should().Be(testColorValue.ToArgb());
    }

    [Theory]
    [MemberData(nameof(AnyAppColorCases))]
    public void Should_load_any_app_color(AppColor colorName, Color color)
    {
        var mockFileReader = CreateMockFileReader(GetThemeContent(colorName, color));
        var mockCssUrlResolver = Substitute.For<IThemeCssUrlResolver>();
        ThemeLoader loader = new(mockCssUrlResolver, mockFileReader);

        var theme = LoadTheme(loader);

        theme.GetColor(colorName).ToArgb().Should().Be(color.ToArgb());
    }

    [Theory]
    [MemberData(nameof(ColorblindSystemColorCases))]
    public void Should_apply_colorblind_overrides_to_system_colors(KnownColor colorName)
    {
        var regularColor = Color.Red;
        var colorblindColor = Color.Blue;
        var mockFileReader =
            CreateMockFileReader(GetThemeContent(colorName, regularColor, colorblindColor));
        var mockCssUrlResolver = Substitute.For<IThemeCssUrlResolver>();
        ThemeLoader loader = new(mockCssUrlResolver, mockFileReader);

        var regularTheme = LoadTheme(loader);
        regularTheme.GetColor(colorName).ToArgb().Should().Be(regularColor.ToArgb());

        var colorblindTheme = LoadTheme(loader, ThemeVariations.Colorblind);

        colorblindTheme.GetColor(colorName).ToArgb().Should().Be(colorblindColor.ToArgb());
    }

    [Theory]
    [MemberData(nameof(ColorblindAppColorCases))]
    public void Should_apply_colorblind_overrides_to_app_colors(AppColor colorName)
    {
        var regularColor = Color.Red;
        var colorblindColor = Color.Blue;
        var mockFileReader =
            CreateMockFileReader(GetThemeContent(colorName, regularColor, colorblindColor));
        var mockCssUrlResolver = Substitute.For<IThemeCssUrlResolver>();
        ThemeLoader loader = new(mockCssUrlResolver, mockFileReader);

        var regularTheme = LoadTheme(loader);
        regularTheme.GetColor(colorName).ToArgb().Should().Be(regularColor.ToArgb());

        var colorblindTheme = LoadTheme(loader, ThemeVariations.Colorblind);
        colorblindTheme.GetColor(colorName).ToArgb().Should().Be(colorblindColor.ToArgb());
    }

    [Theory]
    [MemberData(nameof(CssCommentCases))]
    public void Should_tolerate_css_comment(KnownColor colorName, Color testColorValue)
    {
        var commentedContent =
            "/* entire first line with comment */" + Environment.NewLine +
            GetThemeContent(colorName, testColorValue) + "/* comment after definition */";

        var mockFileReader = CreateMockFileReader(commentedContent);
        var mockCssUrlResolver = Substitute.For<IThemeCssUrlResolver>();
        ThemeLoader loader = new(mockCssUrlResolver, mockFileReader);

        var theme = LoadTheme(loader);

        theme.GetColor(colorName).ToArgb().Should().Be(testColorValue.ToArgb());
    }

    [Fact]
    public void Should_throw_When_unknown_color_name()
    {
        var mockFileReader = CreateMockFileReader(GetThemeContent("InvalidColorName", Color.Red));
        var mockCssUrlResolver = Substitute.For<IThemeCssUrlResolver>();
        ThemeLoader loader = new(mockCssUrlResolver, mockFileReader);

        loader.Invoking(l => LoadTheme(l))
            .Should().Throw<ThemeException>()
            .Which.Message.Should().Contain("InvalidColorName");
    }

    [Fact(Skip = "ExCSS version >= 3 do not report parsing errors.")]
    public void Should_throw_When_css_syntax_error()
    {
        var mockFileReader = CreateMockFileReader(GetThemeContent(KnownColor.Control, Color.Red) + "}");
        var mockCssUrlResolver = Substitute.For<IThemeCssUrlResolver>();
        ThemeLoader loader = new(mockCssUrlResolver, mockFileReader);

        loader.Invoking(l => LoadTheme(l))
            .Should().Throw<ThemeException>()
            .Which.Message.Should().Contain("Error parsing CSS");
    }

    [Theory]
    [MemberData(nameof(CssImportCases))]
    public void Should_honor_css_import_directive(KnownColor colorName, Color baseColor)
    {
        var pathProvider = CreateMockPathProvider();
        ThemeCssUrlResolver resolver = new(pathProvider);

        var themePath = Path.Combine(pathProvider.AppThemesDirectory, "theme.css");
        var baseThemePath = Path.Combine(pathProvider.AppThemesDirectory, "base.css");

        var mockFileReader = CreateMockFileReader(new Dictionary<string, string>
        {
            [baseThemePath] = GetThemeContent(colorName, baseColor), [themePath] = "@import url(\"base.css\");"
        });

        ThemeLoader loader = new(resolver, mockFileReader);

        var theme = loader.LoadTheme(themePath, new ThemeId("theme", true), allowedClasses: ThemeVariations.None);
        theme.GetColor(colorName).ToArgb().Should().Be(baseColor.ToArgb());
    }

    [Theory]
    [MemberData(nameof(ImportedColorOverrideCases))]
    public void Should_override_imported_color_When_current_theme_redefines_it(KnownColor colorName, Color baseColor,
        Color colorOverride)
    {
        var pathProvider = CreateMockPathProvider();
        ThemeCssUrlResolver resolver = new(pathProvider);

        var themePath = Path.Combine(pathProvider.AppThemesDirectory, "theme.css");
        var baseThemePath = Path.Combine(pathProvider.AppThemesDirectory, "base.css");

        var mockFileReader = CreateMockFileReader(new Dictionary<string, string>
        {
            [baseThemePath] = GetThemeContent(colorName, baseColor),
            [themePath] =
                "@import url(\"base.css\");" + Environment.NewLine +
                GetThemeContent(colorName, colorOverride)
        });

        ThemeLoader loader = new(resolver, mockFileReader);

        var theme = loader.LoadTheme(themePath, new ThemeId("theme", true), allowedClasses: ThemeVariations.None);
        theme.GetColor(colorName).ToArgb().Should().Be(colorOverride.ToArgb());
    }

    [Fact]
    public void Should_throw_When_cyclic_css_imports()
    {
        var pathProvider = CreateMockPathProvider();
        ThemeCssUrlResolver resolver = new(pathProvider);

        var themePath = Path.Combine(pathProvider.AppThemesDirectory, "theme.css");
        var baseThemePath = Path.Combine(pathProvider.AppThemesDirectory, "base.css");

        var mockFileReader = CreateMockFileReader(new Dictionary<string, string>
        {
            [baseThemePath] = "@import url(\"theme.css\");", [themePath] = "@import url(\"base.css\");"
        });

        ThemeLoader loader = new(resolver, mockFileReader);

        loader.Invoking(_ => _.LoadTheme(
                themePath,
                new ThemeId("theme", isBuiltin: true),
                allowedClasses: ThemeVariations.None))
            .Should().Throw<ThemeException>()
            .Which.Message.Should().Contain("Cycling CSS import");
    }

    private static IThemePathProvider CreateMockPathProvider()
    {
        var provider = Substitute.For<IThemePathProvider>();
        provider.ThemeExtension.Returns(".css");
        provider.AppThemesDirectory.Returns(_mockAppThemesDirectory);
        provider.UserThemesDirectory.Returns(_mockUserThemesDirectory);
        provider.GetThemePath(Arg.Any<ThemeId>())
            .Returns(callInfo =>
                Path.Combine(
                    callInfo.Arg<ThemeId>().IsBuiltin
                        ? _mockAppThemesDirectory
                        : _mockUserThemesDirectory,
                    callInfo.Arg<ThemeId>().Name + ".css"));
        return provider;
    }

    private static IThemeFileReader CreateMockFileReader(string content)
    {
        var reader = Substitute.For<IThemeFileReader>();
        reader.ReadThemeFile(Arg.Any<string>()).Returns(content);
        return reader;
    }

    private static IThemeFileReader CreateMockFileReader(IReadOnlyDictionary<string, string> contentByFile)
    {
        var reader = Substitute.For<IThemeFileReader>();
        reader.ReadThemeFile(Arg.Any<string>()).Returns(callInfo => contentByFile[callInfo.Arg<string>()]);
        return reader;
    }

    private static string GetThemeContent(object colorName, Color regularColor, Color colorblindColor)
    {
        return GetThemeContent(
            new Dictionary<object, Color>
            {
                [colorName] = regularColor, [colorName + ".colorblind"] = colorblindColor
            });
    }

    private static string GetThemeContent(object colorName, Color color)
    {
        return GetThemeContent(new Dictionary<object, Color> { [colorName] = color });
    }

    private static string GetThemeContent(IEnumerable<KeyValuePair<object, Color>> colorByName)
    {
        return string.Join(
            Environment.NewLine,
            colorByName.Select(pair =>
                $".{pair.Key} {{ color: {ThemePersistence.TestAccessor.FormatColor(pair.Value)}; }}"));
    }

    private static Theme LoadTheme(ThemeLoader loader, params string[] variations)
    {
        return loader.LoadTheme(
            "arbitrary\\theme-path.css",
            new ThemeId("arbitrary_theme_name", true),
            allowedClasses: variations);
    }
}
