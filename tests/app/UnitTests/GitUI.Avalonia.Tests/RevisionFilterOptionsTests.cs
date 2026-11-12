using GitUI.UserControls.RevisionGrid;

namespace GitUI.AvaloniaTests;

public sealed class RevisionFilterOptionsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("src/a b.cs", "\"src/a b.cs\"")]
    [InlineData("\"src/a.cs\"", "\"src/a.cs\"")]
    public void BuildPathSpec_should_quote_the_path_only_when_needed(string? path, string expected)
    {
        RevisionFilterOptions.BuildPathSpec(path).Should().Be(expected);
    }

    [Fact]
    public void IsActive_should_be_false_for_default_options_and_true_when_any_is_set()
    {
        new RevisionFilterOptions().IsActive.Should().BeFalse();
        new RevisionFilterOptions { HideMergeCommits = true }.IsActive.Should().BeTrue();
        new RevisionFilterOptions { DateFrom = new DateTime(2024, 1, 1) }.IsActive.Should().BeTrue();
    }
}
