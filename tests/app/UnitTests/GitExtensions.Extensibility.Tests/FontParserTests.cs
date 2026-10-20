using GitExtensions.Extensibility;

namespace GitUIPluginInterfacesTests;
public class FontParserTests : IDisposable
{
    private Font _defaultFont = null!;

    public FontParserTests()
    {
        _defaultFont = new Font("Arial", 9);
    }

    public void Dispose()
    {
        _defaultFont.Dispose();
    }

    [Theory]
    [InlineData(FontStyle.Regular, "Arial;9;_IC_;0;0")]
    [InlineData(FontStyle.Bold, "Arial;9;_IC_;1;0")]
    [InlineData(FontStyle.Italic, "Arial;9;_IC_;0;1")]
    [InlineData(FontStyle.Bold | FontStyle.Italic, "Arial;9;_IC_;1;1")]
    public void AsString_should_persist_font_with_styles(FontStyle fontStyle, string? serialised)
    {
        using Font font = new("Arial", 9, fontStyle);
        font.AsString().Should().Be(serialised);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void Parse_should_return_default_if_null_or_empty(string? serialised)
    {
        Font font = serialised.Parse(_defaultFont);

        font.Should().Be(_defaultFont);
    }

    [Theory]
    [InlineData("Arial")]
    [InlineData("Arial;")]
    public void Parse_should_return_default_if_less_then_two_parts(string? serialised)
    {
        Font font = serialised.Parse(_defaultFont);

        font.Should().Be(_defaultFont);
    }

    [Theory]
    [InlineData("Courier;8.25;", "Courier", 8.25f, FontStyle.Regular)]
    [InlineData("Courier;12;_IC_", "Courier", 12f, FontStyle.Regular)]
    [InlineData("Courier;11,3;", "Courier", 11.3f, FontStyle.Regular)]
    [InlineData("Courier;11,3;ru", "Courier", 11.3f, FontStyle.Regular)]
    [InlineData("Courier;12;_IC_;0;0", "Courier", 12f, FontStyle.Regular)]
    [InlineData("Courier;12;_IC_;1;0", "Courier", 12f, FontStyle.Bold)]
    [InlineData("Courier;12;_IC_;0;1", "Courier", 12f, FontStyle.Italic)]
    [InlineData("Courier;12;_IC_;1;1", "Courier", 12f, FontStyle.Italic | FontStyle.Bold)]
    public void Parse_should_parse(string? serialised, string name, float size, FontStyle style)
    {
        Font font = serialised.Parse(_defaultFont);

        font.Should().NotBe(_defaultFont);
        font.OriginalFontName.Should().Be(name);
        font.Size.Should().Be(size);
        font.Style.Should().Be(style);
    }
}
