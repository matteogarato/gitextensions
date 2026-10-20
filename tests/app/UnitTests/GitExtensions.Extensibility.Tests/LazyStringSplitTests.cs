using GitExtensions.Extensibility;

namespace GitExtUtilsTests;
public sealed class LazyStringSplitTests
{
    [Theory]
    [InlineData("a;b;c", ';', new[] { "a", "b", "c" })]
    [InlineData("a_b_c", '_', new[] { "a", "b", "c" })]
    [InlineData("aa;bb;cc", ';', new[] { "aa", "bb", "cc" })]
    [InlineData("aaa;bbb;ccc", ';', new[] { "aaa", "bbb", "ccc" })]
    [InlineData(";a", ';', new[] { "", "a" })]
    [InlineData("a;", ';', new[] { "a", "" })]
    [InlineData(";a;b;c", ';', new[] { "", "a", "b", "c" })]
    [InlineData("a;b;c;", ';', new[] { "a", "b", "c", "" })]
    [InlineData(";a;b;c;", ';', new[] { "", "a", "b", "c", "" })]
    [InlineData(";;a;;b;;c;;", ';', new[] { "", "", "a", "", "b", "", "c", "", "" })]
    [InlineData("", ';', new[] { "" })]
    [InlineData(";", ';', new[] { "", "" })]
    [InlineData(";;", ';', new[] { "", "", "" })]
    [InlineData(";;;", ';', new[] { "", "", "", "" })]
    [InlineData(";;;a", ';', new[] { "", "", "", "a" })]
    [InlineData("a;;;", ';', new[] { "a", "", "", "" })]
    [InlineData(";a;;", ';', new[] { "", "a", "", "" })]
    [InlineData(";;a;", ';', new[] { "", "", "a", "" })]
    [InlineData("a", ';', new[] { "a" })]
    [InlineData("aa", ';', new[] { "aa" })]
    public void None(string input, char delimiter, string[] expected)
    {
        // This boxes
        IEnumerable<string> actual = new LazyStringSplit(input, delimiter, StringSplitOptions.None);

        actual.Should().Equal(expected);

        // Non boxing foreach
        List<string> list = [.. new LazyStringSplit(input, delimiter, StringSplitOptions.None)];

        list.Should().Equal(expected);

        // Equivalence with string.Split
        input.Split([delimiter], StringSplitOptions.None).Should().Equal(expected);
    }

    [Theory]
    [InlineData("a;b;c", ';', new[] { "a", "b", "c" })]
    [InlineData("a_b_c", '_', new[] { "a", "b", "c" })]
    [InlineData("aa;bb;cc", ';', new[] { "aa", "bb", "cc" })]
    [InlineData("aaa;bbb;ccc", ';', new[] { "aaa", "bbb", "ccc" })]
    [InlineData(";a", ';', new[] { "a" })]
    [InlineData("a;", ';', new[] { "a" })]
    [InlineData(";a;b;c", ';', new[] { "a", "b", "c" })]
    [InlineData("a;b;c;", ';', new[] { "a", "b", "c" })]
    [InlineData(";a;b;c;", ';', new[] { "a", "b", "c" })]
    [InlineData(";;a;;b;;c;;", ';', new[] { "a", "b", "c" })]
    [InlineData("", ';', new string[0])]
    [InlineData(";", ';', new string[0])]
    [InlineData(";;", ';', new string[0])]
    [InlineData(";;;", ';', new string[0])]
    [InlineData(";;;a", ';', new[] { "a" })]
    [InlineData("a;;;", ';', new[] { "a" })]
    [InlineData(";a;;", ';', new[] { "a" })]
    [InlineData(";;a;", ';', new[] { "a" })]
    [InlineData("a", ';', new[] { "a" })]
    [InlineData("aa", ';', new[] { "aa" })]
    public void RemoveEmptyEntries(string input, char delimiter, string[] expected)
    {
        // This boxes
        IEnumerable<string> actual = new LazyStringSplit(input, delimiter, StringSplitOptions.RemoveEmptyEntries);

        actual.Should().Equal(expected);

        // Non boxing foreach
        List<string> list = [.. new LazyStringSplit(input, delimiter, StringSplitOptions.RemoveEmptyEntries)];

        list.Should().Equal(expected);

        // Equivalence with string.Split
        input.Split([delimiter], StringSplitOptions.RemoveEmptyEntries).Should().Equal(expected);
    }

    [Fact]
    public void Constructor_WithNullInput_Throws()
    {
        ((Action)(() => _ = new LazyStringSplit(null!, ';'))).Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("a;b;c", ';', StringSplitOptions.None, new[] { "a", "b", "c" })]
    [InlineData("a_b_\tc", '_', StringSplitOptions.None, new[] { "a", "b", "\tc" })]
    [InlineData("aa;\rbb;cc", ';', StringSplitOptions.None, new[] { "aa", "\rbb", "cc" })]
    [InlineData("\naaa;bbb;ccc", ';', StringSplitOptions.None, new[] { "\naaa", "bbb", "ccc" })]
    [InlineData(";a", ';', StringSplitOptions.None, new[] { "", "a" })]
    [InlineData("a;", ';', StringSplitOptions.None, new[] { "a", "" })]
    [InlineData(";a;b;c", ';', StringSplitOptions.None, new[] { "", "a", "b", "c" })]
    [InlineData("a;b;c;", ';', StringSplitOptions.None, new[] { "a", "b", "c", "" })]
    [InlineData(";a;b;c;", ';', StringSplitOptions.None, new[] { "", "a", "b", "c", "" })]
    [InlineData(";;a;;b;;c;;", ';', StringSplitOptions.None, new[] { "", "", "a", "", "b", "", "c", "", "" })]
    [InlineData("", ';', StringSplitOptions.None, new[] { "" })]
    [InlineData(";", ';', StringSplitOptions.None, new[] { "", "" })]
    [InlineData(";;", ';', StringSplitOptions.None, new[] { "", "", "" })]
    [InlineData(";;;", ';', StringSplitOptions.None, new[] { "", "", "", "" })]
    [InlineData(";;;a", ';', StringSplitOptions.None, new[] { "", "", "", "a" })]
    [InlineData("a;;;", ';', StringSplitOptions.None, new[] { "a", "", "", "" })]
    [InlineData(";a;;", ';', StringSplitOptions.None, new[] { "", "a", "", "" })]
    [InlineData(";;a;", ';', StringSplitOptions.None, new[] { "", "", "a", "" })]
    [InlineData("a", ';', StringSplitOptions.None, new[] { "a" })]
    [InlineData("aa", ';', StringSplitOptions.None, new[] { "aa" })]
    [InlineData("a;b;c", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a", "b", "c" })]
    [InlineData("a_b_c", '_', StringSplitOptions.RemoveEmptyEntries, new[] { "a", "b", "c" })]
    [InlineData("aa;bb;cc", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "aa", "bb", "cc" })]
    [InlineData("aaa;bbb;ccc", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "aaa", "bbb", "ccc" })]
    [InlineData(";a", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a" })]
    [InlineData("a;", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a" })]
    [InlineData(";a;b;c", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a", "b", "c" })]
    [InlineData("a;b;c;", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a", "b", "c" })]
    [InlineData(";a;b;c;", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a", "b", "c" })]
    [InlineData(";;a;;b;;c;;", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a", "b", "c" })]
    [InlineData("", ';', StringSplitOptions.RemoveEmptyEntries, new string[0])]
    [InlineData(";", ';', StringSplitOptions.RemoveEmptyEntries, new string[0])]
    [InlineData(";;", ';', StringSplitOptions.RemoveEmptyEntries, new string[0])]
    [InlineData(";;;", ';', StringSplitOptions.RemoveEmptyEntries, new string[0])]
    [InlineData(";;;a", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a" })]
    [InlineData("a;;;", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a" })]
    [InlineData(";a;;", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a" })]
    [InlineData(";;a;", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a" })]
    [InlineData("a", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "a" })]
    [InlineData("aa", ';', StringSplitOptions.RemoveEmptyEntries, new[] { "aa" })]
    [InlineData("a;b;c", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a", "b", "c" })]
    [InlineData("a_b_c\r", '_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a", "b", "c" })]
    [InlineData("\naa;bb;cc", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "aa", "bb", "cc" })]
    [InlineData("aaa;\rbbb ;ccc", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "aaa", "bbb", "ccc" })]
    [InlineData(";\ta", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a" })]
    [InlineData("a;\n", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a" })]
    [InlineData(";a; b;c", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a", "b", "c" })]
    [InlineData("a;b;\nc;", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a", "b", "c" })]
    [InlineData(";a;b;c;", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a", "b", "c" })]
    [InlineData(";;a;;b;;c;;", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a", "b", "c" })]
    [InlineData("\n", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new string[0])]
    [InlineData("\n;\r", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new string[0])]
    [InlineData("\r;\t;\n", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new string[0])]
    [InlineData("\t;\n; ;", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new string[0])]
    [InlineData(";;;a", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a" })]
    [InlineData("a;;;", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a" })]
    [InlineData("\n;\na\r\n; ;\t", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a" })]
    [InlineData(";\r;a\n;", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a" })]
    [InlineData(" a\t", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "a" })]
    [InlineData("\taa ", ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, new[] { "aa" })]
    public void LazySplitRanges_should_match_string_split(string input, char delimiter, StringSplitOptions options, string[] expected)
    {
        List<string> actual = [];
        foreach ((int beginning, int length) in input.LazySplitRanges(delimiter, options))
        {
            actual.Add(input.Substring(beginning, length));
        }

        actual.Should().Equal(expected);

        // Equivalence with string.Split
        input.Split([delimiter], options).Should().Equal(expected);
    }
}
