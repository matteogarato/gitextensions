namespace GitCommandsTests;

public sealed class StringExtensionTests
{
    [Theory]
    [InlineData(null, 10, "")]
    [InlineData("Hello", 10, "Hello")]
    [InlineData("Hello", 5, "Hello")]
    [InlineData("Hello", 4, "H...")]
    [InlineData("Hello", 3, "...")]
    [InlineData("Hello", 2, "He")]
    public void ShortenTo_works_as_expected(string? s, int length, string expected)
    {
        s.ShortenTo(length).Should().Be(expected);
    }

    [Theory]
    [InlineData("Hello World", "Hello", true)]
    [InlineData("Hello World", " ", true)]
    [InlineData("Hello World", "World", true)]
    [InlineData("Hello World", "", true)]
    [InlineData("Hello World", "Hi", false)]
    [InlineData("Hello World", "ldi", false)]
    public void Contains_works_as_expected(string str, string other, bool expected)
    {
        str.Contains(other, StringComparison.InvariantCultureIgnoreCase).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCDEFG", "ABC", "DEFG")]
    [InlineData("ABCDEFG", "AB", "CDEFG")]
    [InlineData("ABCDEFG", "A", "BCDEFG")]
    [InlineData("ABCDEFG", "", "ABCDEFG")]
    [InlineData("ABCDEFG", "XYZ", "ABCDEFG")]
    [InlineData("AB", "ABCD", "AB")]
    public void RemovePrefix_works_as_expected(string str, string prefix, string expected)
    {
        str.RemovePrefix(prefix).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCDEFG", "EFG", "ABCD")]
    [InlineData("ABCDEFG", "FG", "ABCDE")]
    [InlineData("ABCDEFG", "G", "ABCDEF")]
    [InlineData("ABCDEFG", "", "ABCDEFG")]
    [InlineData("ABCDEFG", "XYZ", "ABCDEFG")]
    [InlineData("CD", "ABCD", "CD")]
    [InlineData("", "ABCD", "")]
    [InlineData("ABCD", "", "ABCD")]
    public void RemoveSuffix_works_as_expected(string str, string prefix, string expected)
    {
        str.RemoveSuffix(prefix).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCDEFG", 'A', "")]
    [InlineData("ABCDEFG", 'B', "A")]
    [InlineData("ABCDEFG", 'C', "AB")]
    [InlineData("ABCDEFG", 'D', "ABC")]
    [InlineData("ABCDEFG", 'E', "ABCD")]
    [InlineData("ABCDEFG", 'Z', "ABCDEFG")]
    [InlineData("", 'Z', "")]
    [InlineData("A", 'A', "")]
    public void SubstringUntil_works_as_expected(string str, char c, string expected)
    {
        str.SubstringUntil(c).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCABC", 'A', "ABC")]
    [InlineData("ABCABC", 'B', "ABCA")]
    [InlineData("ABCABC", 'C', "ABCAB")]
    [InlineData("ABCABC", 'Z', "ABCABC")]
    [InlineData("", 'Z', "")]
    [InlineData("A", 'A', "")]
    [InlineData("AAAA", 'A', "AAA")]
    public void SubstringUntilLast_works_as_expected(string str, char c, string expected)
    {
        str.SubstringUntilLast(c).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCDEFG", 'A', "BCDEFG")]
    [InlineData("ABCDEFG", 'B', "CDEFG")]
    [InlineData("ABCDEFG", 'C', "DEFG")]
    [InlineData("ABCDEFG", 'Z', "ABCDEFG")]
    [InlineData("", 'Z', "")]
    [InlineData("A", 'A', "")]
    [InlineData("ABBA", 'A', "BBA")]
    public void SubstringAfter_char_works_as_expected(string str, char c, string expected)
    {
        str.SubstringAfter(c).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCDEFG", "A", "BCDEFG")]
    [InlineData("ABCDEFG", "AB", "CDEFG")]
    [InlineData("ABCDEFG", "B", "CDEFG")]
    [InlineData("ABCDEFG", "C", "DEFG")]
    [InlineData("ABCDEFG", "Z", "ABCDEFG")]
    [InlineData("", "Z", "")]
    [InlineData("A", "A", "")]
    [InlineData("ABBA", "A", "BBA")]
    public void SubstringAfter_string_works_as_expected(string str, string? s, string expected)
    {
        str.SubstringAfter(s!).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCABC", 'A', "BC")]
    [InlineData("ABCABC", 'B', "C")]
    [InlineData("ABCABC", 'C', "")]
    [InlineData("ABCDEFG", 'A', "BCDEFG")]
    [InlineData("ABCDEFG", 'B', "CDEFG")]
    [InlineData("ABCDEFG", 'C', "DEFG")]
    [InlineData("ABCDEFG", 'Z', "ABCDEFG")]
    [InlineData("", 'Z', "")]
    [InlineData("A", 'A', "")]
    [InlineData("ABBA", 'A', "")]
    public void SubstringAfterLast_char_works_as_expected(string str, char c, string expected)
    {
        str.SubstringAfterLast(c).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABCABC", "A", "BC")]
    [InlineData("ABCABC", "B", "C")]
    [InlineData("ABCABC", "C", "")]
    [InlineData("ABCDEFG", "A", "BCDEFG")]
    [InlineData("ABCDEFG", "AB", "CDEFG")]
    [InlineData("ABCDEFG", "B", "CDEFG")]
    [InlineData("ABCDEFG", "BCD", "EFG")]
    [InlineData("ABCDEFG", "C", "DEFG")]
    [InlineData("ABCDEFG", "Z", "ABCDEFG")]
    [InlineData("", "Z", "")]
    [InlineData("A", "A", "")]
    [InlineData("ABBA", "A", "")]
    public void SubstringAfterLast_string_works_as_expected(string str, string? s, string expected)
    {
        str.SubstringAfterLast(s!).Should().Be(expected);
    }

    [Theory]
    [InlineData("/usr/bin", false, "/usr/bin")]
    [InlineData("/usr/bin", true, "/usr/bin")]
    [InlineData("\\usr\\bin", false, "\\\\usr\\\\bin")]
    [InlineData("\\usr\\bin", true, "\\usr\\bin")]
    [InlineData("C:\\Program Files (x86)\\Microsoft Visual Studio\\Installer\\setup.exe", true,
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\Installer\\setup.exe")]
    [InlineData("echo \"Hello world\"", false, "echo \\\"Hello world\\\"")]
    [InlineData("echo \"Hello world\"", true, "echo \"\"Hello world\"\"")]
    [InlineData("echo \'Hello world\'", false, "echo \\\'Hello world\\\'")]
    [InlineData("echo \'Hello world\'", true, "echo \'Hello world\'")]
    [InlineData("cmd /c \"echo \\\"Hello world\\\"\"", false, "cmd /c \\\"echo \\\\\\\"Hello world\\\\\\\"\\\"")]
    [InlineData("cmd /c \"echo \\\"Hello world\\\"\"", true, "cmd /c \"\"echo \\\"\"Hello world\\\"\"\"\"")]
    [InlineData("cmd /c \"echo \"\"Hello world\"\"\"", false, "cmd /c \\\"echo \\\"\\\"Hello world\\\"\\\"\\\"")]
    [InlineData("cmd /c \"echo \"\"Hello world\"\"\"", true, "cmd /c \"\"echo \"\"\"\"Hello world\"\"\"\"\"\"")]
    public void EscapeForCommandLine_works_as_expected(string? s, bool forWindows, string expected)
    {
        s!.EscapeForCommandLine(forWindows).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" \"", "\" \\\"\"")]
    [InlineData("\"\"", "\"\"")]
    [InlineData("file", "\"file\"")]
    [InlineData("\"file", "\"file")]
    [InlineData("\"file\"", "\"file\"")]
    [InlineData("file with spaces", "\"file with spaces\"")]
    [InlineData("\"file with spaces", "\"file with spaces")]
    [InlineData("\"file with spaces\"", "\"file with spaces\"")]
    [InlineData("wei\"rd", "\"wei\\\"rd\"")]
    [InlineData("\"wei\"rd\"", "\"wei\"rd\"")]
    [InlineData("weird\"", "\"weird\\\"\"")]
    public void QuoteIfUnquotedAndNe(string? str, string? expected)
    {
        str.QuoteIfNotQuotedAndNe().Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("\"", "\"")]
    [InlineData("\"\"", "")]
    [InlineData("\" \"\"", " \"")]
    [InlineData("\" \\\"", " \\")]
    [InlineData("\" \\\"\"", " \\\"")]
    [InlineData("\"file\"", "file")]
    [InlineData("\"file", "\"file")]
    [InlineData("file\"", "file\"")]
    [InlineData("\"file with spaces\"", "file with spaces")]
    public void RemoveQuotes(string? str, string? expected)
    {
        str!.RemoveQuotes().Should().Be(expected);
    }
}
