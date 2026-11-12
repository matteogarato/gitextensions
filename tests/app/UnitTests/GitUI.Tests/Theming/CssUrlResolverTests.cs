using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using NSubstitute;

namespace GitUITests.Theming;

public class CssUrlResolverTests
{
    private const string _preinstalledThemesMockPath = "pre-installed\\themes";
    private const string _userDefinedThemesMockPath = "app-data\\themes";

    [Fact]
    public void Should_resolve_to_preinstalled_themes_directory_by_default()
    {
        ThemeCssUrlResolver resolver = new(CreateMockThemePathProvider());
        var resolvedPath = resolver.ResolveCssUrl("bright.css");
        resolvedPath.Should().Be(Path.Combine(_preinstalledThemesMockPath, "bright.css"));
    }

    [Fact]
    public void Should_resolve_to_user_defined_themes_directory_When_url_starts_with_macro()
    {
        ThemeCssUrlResolver resolver = new(CreateMockThemePathProvider());
        var resolvedPath = resolver.ResolveCssUrl("{UserAppData}/bright_custom.css");
        resolvedPath.Should().Be(Path.Combine(_userDefinedThemesMockPath, "bright_custom.css"));
    }

    private static IThemePathProvider CreateMockThemePathProvider()
    {
        var pathProvider = Substitute.For<IThemePathProvider>();

        pathProvider.ThemeExtension.Returns(".css");

        pathProvider.GetThemePath(Arg.Any<ThemeId>()).Returns(callInfo =>
            Path.Combine(
                callInfo.Arg<ThemeId>().IsBuiltin
                    ? _preinstalledThemesMockPath
                    : _userDefinedThemesMockPath,
                callInfo.Arg<ThemeId>().Name + ".css"));

        return pathProvider;
    }
}
