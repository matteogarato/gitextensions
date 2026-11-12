using GitUI.CommandsDialogs.SettingsDialog.Pages;

namespace GitUITests.CommandsDialogs.SettingsDialog.Pages;

public class GitConfigSettingsPageControllerTests
{
    private readonly GitConfigSettingsPageController _controller = null!;

    public GitConfigSettingsPageControllerTests()
    {
        _controller = new GitConfigSettingsPageController();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData("", null)]
    [InlineData("", "")]
    public void
        GetInitialDirectory_CalculateInitialDirectory_should_return_ProgramFiles_if_path_and_toolPreferredPath_unset(
            string? path, string? toolPreferredPath)
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        _controller.GetInitialDirectory(path!, toolPreferredPath!).Should().Be(expected);
    }

    [Fact]
    public void GetInitialDirectory_CalculateInitialDirectory_should_return_directory_for_supplied_path()
    {
        var tempFolder = @"c:\";
        _controller.GetInitialDirectory(tempFolder, null!).Should().Be(@"c:\");

        tempFolder = @"c:";
        _controller.GetInitialDirectory(tempFolder, null!).Should().Be(@"c:\");

        tempFolder = Path.GetTempPath(); // something like: C:\Users\user\AppData\Local\Temp\
        _controller.GetInitialDirectory(tempFolder, null!).Should().Be(tempFolder);

        _controller.GetInitialDirectory(tempFolder[..^1], null!).Should().Be(tempFolder);

        var tempFile = Path.GetTempFileName(); // something like: C:\Users\user\AppData\Local\Temp\tmp97C5.tmp
        _controller.GetInitialDirectory(tempFile, null!).Should().Be(tempFolder);
    }
}
