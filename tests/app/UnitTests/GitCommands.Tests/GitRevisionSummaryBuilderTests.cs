using System.Reflection;
using System.Runtime.CompilerServices;
using CommonTestUtils;
using GitCommands;

namespace GitCommandsTests;

public sealed class GitRevisionSummaryBuilderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Should_return_null_When_body_is_null_or_whitespace(string? bodyContent)
    {
        new GitRevisionSummaryBuilder().BuildSummary(bodyContent).Should().Be(null);
    }

    [Theory]
    [InlineData("toto")]
    [InlineData("toto\ntata\ntiti")]
    public void Should_have_same_content_When_no_ellipsis(string? bodyContent)
    {
        new GitRevisionSummaryBuilder().BuildSummary(bodyContent).Should().Be(bodyContent);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Theory]
    [InlineData("Too_many_lines")]
    [InlineData("Too_long_lines")]
    public async Task Should_do_ellipsis(string testName)
    {
        string content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(), $"{GetType().Namespace}.MockData.{testName}.txt");

        await Verifier.Verify(new GitRevisionSummaryBuilder().BuildSummary(content));
    }
}
