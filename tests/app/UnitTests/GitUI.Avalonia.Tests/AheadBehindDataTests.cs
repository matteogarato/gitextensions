using GitCommands.Git;

namespace GitExtensionsTests;

public sealed class AheadBehindDataTests
{
    [InlineData("gone", "", true, false, "✗")]
    [InlineData("", "", true, false, "")]
    [InlineData("0", "", true, false, "0↑↓")]
    [InlineData("0", "", false, false, "↑↓")]
    [InlineData("0", "", true, true, "0↓↑")]
    [InlineData("5", "", true, false, "5↑")]
    [InlineData("5", "", false, false, "↑")]
    [InlineData("5", "", true, true, "5↓")]
    [InlineData("", "3", true, false, "3↓")]
    [InlineData("", "3", false, false, "↓")]
    [InlineData("", "3", true, true, "3↑")]
    [InlineData("5", "3", true, false, "5↑ 3↓")]
    [InlineData("5", "3", false, false, "↑↓")]
    [InlineData("5", "3", true, true, "5↓ 3↑")]
    [Theory]
    public void ToDisplay_should_format_counts_and_directions(string ahead, string behind, bool withCounts, bool reverse, string expected)
    {
        AheadBehindData data = new("", "", ahead, behind);

        data.ToDisplay(withCounts, reverse).Should().Be(expected);
    }
}
