using System.Text;
using GitExtUtils;

namespace GitUITests;

public sealed class StringBuilderExtensionsTests
{
    [Theory]
    [InlineData("foo", "foo")]
    [InlineData("\"foo bar\"", "foo bar")]
    [InlineData("\"foo\tbar\"", "foo\tbar")]
    [InlineData("\"foo bar\"", "\"foo bar\"")]
    [InlineData("\"\"", "")]
    [InlineData("\" \"", " ")]
    public void AppendQuoted(string expected, string source)
    {
        new StringBuilder().AppendQuoted(source).ToString().Should().Be(expected);
    }
}
