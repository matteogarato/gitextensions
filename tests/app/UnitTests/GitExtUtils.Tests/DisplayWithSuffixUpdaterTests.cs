using GitExtUtils;

namespace GitExtUtilsTests;

public sealed class DisplayWithSuffixUpdaterTests
{
    [Fact]
    public void FromInitialValue()
    {
        "a\u00A0(b)".Should().Be("a".UpdateSuffixWithinParenthesis("b"));
    }

    [Fact]
    public void FromValueContainingAlreadyTheSuffix()
    {
        "a\u00A0(c)".Should().Be("a (b)".UpdateSuffixWithinParenthesis("c"));
    }
}
