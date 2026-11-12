using GitCommands.Git.Extensions;
using GitExtensions.Extensibility.Git;

namespace GitCommandsTests.Git.Extensions;

public class GitRevisionExtensionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("0000", false)]
    [InlineData(GitRevision.WorkTreeGuid, true)]
    [InlineData(GitRevision.IndexGuid, true)]
    [InlineData(GitRevision.CombinedDiffGuid, true)]
    public void IsArtificial_tests(string? sha1, bool expected)
    {
        sha1.IsArtificial().Should().Be(expected);
    }
}
