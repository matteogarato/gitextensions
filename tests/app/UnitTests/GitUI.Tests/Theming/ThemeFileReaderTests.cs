using GitUI.Theming;

namespace GitUITests.Theming;

public class ThemeFileReaderTests
{
    [Fact]
    public void Should_return_file_text()
    {
        const string mockThemeContent = "test content";

        ThemeFileReader reader = new();

        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, mockThemeContent);
            reader.ReadThemeFile(tempPath).Should().Be(mockThemeContent);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Should_throw_ThemeException_if_file_does_not_exist()
    {
        var nonExistingFile = Path.Combine(AppContext.BaseDirectory, "non_existing_theme.css");

        ThemeFileReader reader = new();
        reader.Invoking(_ => _.ReadThemeFile(nonExistingFile))
            .Should().Throw<ThemeException>()
            .Which.InnerException.Should().BeOfType<FileNotFoundException>();
    }
}
