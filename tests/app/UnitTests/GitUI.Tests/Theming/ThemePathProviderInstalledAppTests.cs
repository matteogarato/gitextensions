using GitCommands;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;

namespace GitUITests.Theming;
public class ThemePathProviderInstalledAppTests : IDisposable
{
    private string _originalAppExecutablePath = null!;
    private Lazy<string?> _originalAppDataPath = null!;

    private const string MockAppInstallPath = "c:\\GitExtensions";
    private const string MockAppDataPath = "c:\\user\\username\\appdata";

    public ThemePathProviderInstalledAppTests()
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
    public void UserThemesDirectory_should_be_inside_AppData()
    {
        new ThemePathProvider().UserThemesDirectory
            .Should().Be(Path.Combine(MockAppDataPath, "Themes"));
    }

    [Fact]
    public void GetThemePath_should_return_path_inside_app_install_When_theme_is_builtin()
    {
        new ThemePathProvider().GetThemePath(new ThemeId("arbitrary_name", isBuiltin: true))
            .Should().Be(Path.Combine(MockAppInstallPath, "Themes", "arbitrary_name.css"));
    }

    [Fact]
    public void GetThemePath_should_return_path_inside_app_data_When_theme_is_not_builtin()
    {
        new ThemePathProvider().GetThemePath(new ThemeId("arbitrary_name", isBuiltin: false))
            .Should().Be(Path.Combine(MockAppDataPath, "Themes", "arbitrary_name.css"));
    }
}
