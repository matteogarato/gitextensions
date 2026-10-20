using GitExtUtils;

namespace GitExtUtilsTests;
public sealed class DisplayWithSuffixUpdaterTests
{
    [Fact]
    public void FromInitialValue()
    {
        "a\u00A0(b)".Should().Be(DisplayWithSuffixUpdater.UpdateSuffixWithinParenthesis("a", "b"));
    }

    [Fact]
    public void FromValueContainingAlreadyTheSuffix()
    {
        "a\u00A0(c)".Should().Be(DisplayWithSuffixUpdater.UpdateSuffixWithinParenthesis("a (b)", "c"));
    }
}
