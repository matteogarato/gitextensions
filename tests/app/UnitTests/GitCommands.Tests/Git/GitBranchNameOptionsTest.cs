using System.Globalization;
using GitCommands.Git;

namespace GitCommandsTests.Git;

public class GitBranchNameOptionsTest : IDisposable
{
    private readonly CultureInfo _currentCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _currentUiCulture = CultureInfo.CurrentUICulture;

    public GitBranchNameOptionsTest()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    void IDisposable.Dispose()
    {
        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentUiCulture;
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void ReplacementToken_can_be_null_or_empty(string? token, string expected)
    {
        GitBranchNameOptions options = new(token);

        options.ReplacementToken.Should().Be(expected);
    }

    [Fact]
    public void ReplacementToken_cant_be_multichar()
    {
        ((Action)(() => new GitBranchNameOptions("aaa"))).Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("Replacement token must be a single character (Parameter 'replacementToken')");
    }

    [Theory]
    [InlineData(" ", ' ')]
    [InlineData("^", '^')]
    [InlineData("~", '~')]
    [InlineData(":", ':')]
    public void ReplacementToken_cant_be_invalid(string? token, char expected)
    {
        ((Action)(() => new GitBranchNameOptions(token))).Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage(string.Format("Replacement token invalid: '{0}' (Parameter 'replacementToken')", expected));
    }
}
