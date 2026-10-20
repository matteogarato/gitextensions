using GitUI.SpellChecker;

namespace GitUITests.SpellChecker;
public class WordAtCursorExtractorTests
{
    private WordAtCursorExtractor _wordAtCursorExtractor = null!;

    public WordAtCursorExtractorTests()
    {
        _wordAtCursorExtractor = new WordAtCursorExtractor();
    }

    [Theory]
    [InlineData(".git", ".git")]
    [InlineData("Add .g", ".g")]
    [InlineData("Add.g", "g")]
    [InlineData("Add,git", "git")]
    [InlineData("Add (.git", ".git")]
    [InlineData("Introduce Tes", "Tes")]
    [InlineData("Add).git", "git")] // test strings with a closing round bracket confuse the testcase parser a little
    [InlineData("[Add].git", "git")]
    [InlineData("Introduce .babeljs]", "")]
    [InlineData("func().otherFun", "otherFun")]
    [InlineData("func().other_fun", "other_fun")]
    [InlineData("obj._field", "_field")]
    [InlineData("func(type init_", "init_")]
    public void ExtractsMeaningfulWord(string text, string expected)
    {
        _wordAtCursorExtractor.Extract(text, text.Length - 1).Should().Be(expected);
    }

    [Theory]
    [InlineData("", -2, 0, 0)]
    [InlineData("", -1, 0, 0)]
    [InlineData("", 0, 0, 0)]
    [InlineData("", 1, 0, 0)]
    [InlineData("_obj.f", -2, 0, 0)]
    [InlineData("_obj.f", -1, 0, 0)]
    [InlineData("_obj.f", 0, 0, 4)]
    [InlineData("_obj.f", 1, 0, 4)]
    [InlineData("_obj.f", 2, 0, 4)]
    [InlineData("_obj.f", 3, 0, 4)]
    [InlineData("_obj.f", 4, 4, 1)]
    [InlineData("_obj.f", 5, 5, 1)]
    [InlineData("_obj.f", 6, 5, 1)]
    [InlineData("_obj.f", 7, 5, 1)]
    [InlineData("0  3", 0, 0, 1)]
    [InlineData("0  2", 1, 1, 1)]
    [InlineData("0  2", 2, 2, 1)]
    [InlineData("0  2", 3, 3, 1)]
    public void Test_GetWordBounds(string text, int index, int expectedStart, int expectedLength)
    {
        (int start, int length) = _wordAtCursorExtractor.GetWordBounds(text, index);
        (start == expectedStart && length == expectedLength).Should().BeTrue();
    }

    [Theory]
    [InlineData("", -2, -1)]
    [InlineData("", -1, -1)]
    [InlineData("", 0, -1)]
    [InlineData("", 1, -1)]
    [InlineData("012", -2, -1)]
    [InlineData("012", -1, -1)]
    [InlineData("012", 0, 0)]
    [InlineData("012", 1, 0)]
    [InlineData("012", 2, 0)]
    [InlineData("012", 3, 0)]
    [InlineData("012", 4, 0)]
    [InlineData("01A34", 5, 0)]
    [InlineData("01a34", 5, 0)]
    [InlineData("01_34", 5, 0)]
    [InlineData("01 34", 5, 3)]
    [InlineData("01.34", 5, 3)]
    [InlineData("01 .4", 5, 3)]
    [InlineData("01..4", 5, 3)]
    [InlineData("01).4", 5, 4)]
    [InlineData("01].4", 5, 4)]
    [InlineData("012.4", 5, 4)]
    [InlineData("012.4", 4, 4)]
    [InlineData("012.4", 3, 4)] // The result is right from the initial index!
    [InlineData("012/4", 3, 4)] // ditto
    [InlineData("012.4", 2, 0)]
    [InlineData("012.4", 1, 0)]
    [InlineData("012.4", 0, 0)]
    public void Test_FindStartOfWord(string text, int index, int expected)
    {
        _wordAtCursorExtractor.FindStartOfWord(text, index).Should().Be(expected);
    }

    [Theory]
    [InlineData("", -2, -1)]
    [InlineData("", -1, -1)]
    [InlineData("", 0, 0)]
    [InlineData("", 1, 0)]
    [InlineData("012", -2, -1)]
    [InlineData("012", -1, -1)]
    [InlineData("012", 0, 3)]
    [InlineData("012", 1, 3)]
    [InlineData("012", 2, 3)]
    [InlineData("012", 3, 3)]
    [InlineData("012", 4, 3)]
    [InlineData("01A34", 0, 5)]
    [InlineData("01a34", 0, 5)]
    [InlineData("01_34", 0, 5)]
    [InlineData("01 34", 0, 2)]
    [InlineData("01/34", 0, 2)]
    [InlineData("01/34", 1, 2)]
    [InlineData("01/34", 2, 2)]
    [InlineData("01/34", 3, 5)]
    [InlineData("01/34", 4, 5)]
    [InlineData("/12", 0, 0)]
    [InlineData("/12", 1, 3)]
    [InlineData("01/", 0, 2)]
    [InlineData("01/", 1, 2)]
    [InlineData("01/", 2, 2)]
    [InlineData("01/", 3, 3)]
    public void Test_FindEndOfWord(string text, int index, int expected)
    {
        _wordAtCursorExtractor.FindEndOfWord(text, index).Should().Be(expected);
    }
}
