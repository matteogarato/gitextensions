using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.Theming;

public class ThemePathProviderInstalledAppTests : IDisposable
{
    private const string _mockAppInstallPath = "c:\\GitExtensions";
    private const string _mockAppDataPath = "c:\\user\\username\\appdata";
    private readonly Lazy<string?> _originalAppDataPath = null!;
    private readonly string _originalAppExecutablePath = null!;

    public ThemePathProviderInstalledAppTests()
    {
        var testAccessor = AppSettings.GetTestAccessor();
        _originalAppExecutablePath = testAccessor.ApplicationExecutablePath;
        testAccessor.ApplicationExecutablePath = Path.Combine(_mockAppInstallPath, "gitextensions.exe");

        _originalAppDataPath = testAccessor.ApplicationDataPath;
        testAccessor.ApplicationDataPath = new Lazy<string?>(() => _mockAppDataPath);
    }

    void IDisposable.Dispose()
    {
        var testAccessor = AppSettings.GetTestAccessor();
        testAccessor.ApplicationExecutablePath = _originalAppExecutablePath;
        testAccessor.ApplicationDataPath = _originalAppDataPath;
    }

    [Fact]
    public void AppThemesDirectory_should_be_inside_app_install()
    {
        new ThemePathProvider().AppThemesDirectory
            .Should().Be(Path.Combine(_mockAppInstallPath, "Themes"));
    }

    [Fact]
    public void UserThemesDirectory_should_be_inside_AppData()
    {
        new ThemePathProvider().UserThemesDirectory
            .Should().Be(Path.Combine(_mockAppDataPath, "Themes"));
    }

    [Fact]
    public void GetThemePath_should_return_path_inside_app_install_When_theme_is_builtin()
    {
        new ThemePathProvider().GetThemePath(new ThemeId("arbitrary_name", true))
            .Should().Be(Path.Combine(_mockAppInstallPath, "Themes", "arbitrary_name.css"));
    }

    [Fact]
    public void GetThemePath_should_return_path_inside_app_data_When_theme_is_not_builtin()
    {
        new ThemePathProvider().GetThemePath(new ThemeId("arbitrary_name"))
            .Should().Be(Path.Combine(_mockAppDataPath, "Themes", "arbitrary_name.css"));
    }
}
