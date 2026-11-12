using GitExtensions.Plugins.ReleaseNotesGenerator;

namespace ReleaseNotesGeneratorTests;

public class MainFixture
{
    /// <summary>
    ///     Indicates whether the current operating system can run Windows clipboard tests.
    /// </summary>
    public static bool IsWindows
    {
        get => OperatingSystem.IsWindows();
    }

    [Fact]
    public void FormatStringTest()
    {
        HtmlFragment.To8DigitString(15).Should().Be("00000015");
    }

    [Fact(Skip = "Clipboard data object is available only on Windows.", SkipUnless = nameof(IsWindows))]
    public void CreateHtmlFormatClipboardDataObjectTest()
    {
        var dataObject = HtmlFragment.CreateHtmlFormatClipboardDataObject("<p>Hallo</p>");
        dataObject.GetFormats().Length.Should().Be(2);
        dataObject.GetText().Should().Be("<p>Hallo</p>");
        dataObject.TryGetData("HTML Format", out string? s);
        s.Should().Be(
            "Version:0.9\r\n" +
            "StartHTML:00000097\r\n" +
            "EndHTML:00000177\r\n" +
            "StartFragment:00000131\r\n" +
            "EndFragment:00000143\r\n" +
            "<html><body>\r\n" +
            "<!--StartFragment--><p>Hallo</p><!--EndFragment-->\r\n" +
            "</body></html>");
    }

    [StaFact]
    public void CopyToClipboard()
    {
        HtmlFragment.CopyToClipboard("<p>Hallo</p>");

        // Verify manually that the content can be pasted as text and into MS Word or
        // LibreOffice Writer. There it should create a formatted table.
    }
}
