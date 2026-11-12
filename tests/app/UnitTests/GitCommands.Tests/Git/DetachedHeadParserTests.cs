using GitCommands.Git;

namespace GitCommandsTests.Git;

public class DetachedHeadParserTests
{
    [Fact]
    public void ShouldExtractOldVersionOfDetachedHeadOutput()
    {
        DetachedHeadParser.TryParse("(detached from c299581)", out var sha1).Should().BeTrue();
        sha1.Should().Be("c299581");
    }

    [Fact]
    public void ShouldExtractNewVersionOfDetachedHeadOutput()
    {
        DetachedHeadParser.TryParse("(HEAD detached at c299582)", out var sha1).Should().BeTrue();
        sha1.Should().Be("c299582");
    }
}
