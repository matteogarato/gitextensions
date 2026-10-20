using GitUI.AutoCompletion;

namespace GitUITests.AutoCompletion;

public class AutoCompleteWordTests
{
    [Theory]
    [InlineData("CamelHump", "CH", true)]
    [InlineData("CamelHump", "ch", true)]
    [InlineData("CamelHump", "cH", true)]
    [InlineData("CamelHump", "Ch", true)]
    [InlineData("CamelHump", "Ca", true)]
    [InlineData("CamelHump", "ca", true)]
    [InlineData("CamelHump", "CamelH", true)]
    [InlineData("CamelHump", "CX", false)]
    [InlineData("MyCamelHump", "MCH", true)]
    [InlineData("MyCamelHump", "mch", true)]
    [InlineData("MyCamelHump", "CH", false)]
    [InlineData("Identifier", "i", true)]
    [InlineData("Identifier", "I", true)]
    [InlineData("Identifier", "id", true)]
    [InlineData("Identifier", "Id", true)]
    public void Matches_should_match_prefix_and_camel_humps_case_insensitive(string word, string typedWord, bool expected)
    {
        var autoCompleteWord = new AutoCompleteWord(word);
        autoCompleteWord.Matches(typedWord).Should().Be(expected);
    }
}
