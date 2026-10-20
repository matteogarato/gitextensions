using GitCommands;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;

namespace GitUITests.Theming;

public class ThemePathProviderPortableAppTests : IDisposable
{
    private string _originalAppExecutablePath = null!;
    private Lazy<string?> _originalAppDataPath = null!;

    // appInstall and AppData paths are same for portable app
    private const string MockAppInstallPath = "c:\\portable\\GitExtensions";
    private const string MockAppDataPath = "c:\\portable\\GitExtensions";

    public ThemePathProviderPortableAppTests()
    {
        AppSettings.TestAccessor testAccessor = AppSettings.GetTestAccessor();
        _originalAppExecutablePath = testAccessor.ApplicationExecutablePath;
        testAccessor.ApplicationExecutablePath = Path.Combine(MockAppInstallPath, "gitextensions.exe");

        _originalAppDataPath = testAccessor.ApplicationDataPath;
        testAccessor.ApplicationDataPath = new Lazy<string?>(() => MockAppDataPath);
    }

    public void Dispose()
    {
        AppSettings.TestAccessor testAccessor = AppSettings.GetTestAccessor();
        testAccessor.ApplicationExecutablePath = _originalAppExecutablePath;
        testAccessor.ApplicationDataPath = _originalAppDataPath;
    }

    [Fact]
    public void AppThemesDirectory_should_be_inside_app_install()
    {
        new ThemePathProvider().AppThemesDirectory
            .Should().Be(Path.Combine(MockAppInstallPath, "Themes"));
    }

    [Fact]
    public void UserThemesDirectory_should_be_null()
    {
        new ThemePathProvider().UserThemesDirectory
            .Should().BeNull();
    }

    [Fact]
    public void GetThemePath_should_return_path_inside_app_install_When_theme_is_builtin()
    {
        new ThemePathProvider().GetThemePath(new ThemeId("arbitrary_name", isBuiltin: true))
            .Should().Be(Path.Combine(MockAppInstallPath, "Themes", "arbitrary_name.css"));
    }

    [Fact]
    public void GetThemePath_should_throw_When_theme_is_not_builtin()
    {
        new ThemePathProvider().Invoking(_ =>
                _.GetThemePath(new ThemeId("arbitrary_name", isBuiltin: false)))
            .Should().Throw<InvalidOperationException>()
            .Which.Message.ToLower().Should().Contain("portable");
    }
}
