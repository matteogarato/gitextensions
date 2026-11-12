using System.Buffers;

namespace GitExtensions.ExtensibilityTests;

public sealed class StringExtensionsTests
{
    private static readonly SearchValues<char> _separators = SearchValues.Create(',', ';');

    [Theory]
    [InlineData("a,b;c", 0, 1)]
    [InlineData("a,b;c", 1, 1)]
    [InlineData("a,b;c", 2, 3)]
    [InlineData("a,b;c", 3, 3)]
    [InlineData("a,b;c", 4, -1)]
    [InlineData("a,b;c", 5, -1)]
    [InlineData("abc", 0, -1)]
    [InlineData(",", 0, 0)]
    [InlineData(",", 1, -1)]
    [InlineData("", 0, -1)]
    public void IndexOfAny_returns_expected(string input, int startIndex, int expected)
    {
        input.IndexOfAny(_separators, startIndex).Should().Be(expected);
    }

    [Theory]
    [InlineData("abc", 0, 3)] // no line ending → string length
    [InlineData("abc", 2, 3)] // startIndex at last char
    [InlineData("abc", 3, 3)] // startIndex past end
    [InlineData("", 0, 0)] // empty string → length (0)
    [InlineData("a\nb", 0, 1)] // LF found from start
    [InlineData("a\nb", 1, 1)] // startIndex at LF
    [InlineData("a\nb", 2, 3)] // startIndex past LF → no further ending
    [InlineData("a\r\nb", 0, 1)] // CR found before LF
    [InlineData("a\r\nb", 2, 2)] // startIndex at LF
    [InlineData("a\r\nb", 3, 4)] // startIndex past CRLF → no ending
    [InlineData("a\nb\nc", 2, 3)] // second LF
    public void GetLineEnd_returns_expected(string input, int startIndex, int expected)
    {
        input.GetLineEnd(startIndex).Should().Be(expected);
    }
}
