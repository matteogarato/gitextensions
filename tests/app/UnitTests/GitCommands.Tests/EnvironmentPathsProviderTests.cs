using GitCommands;
using NSubstitute;

namespace GitCommandsTests;

public class EnvironmentPathsProviderTests
{
    private readonly IEnvironmentAbstraction _environment = null!;
    private readonly IEnvironmentPathsProvider _provider = null!;
    private readonly string _separator = null!;

    public EnvironmentPathsProviderTests()
    {
        _separator = Path.PathSeparator.ToString();

        _environment = Substitute.For<IEnvironmentAbstraction>();
        _provider = new EnvironmentPathsProvider(_environment);
    }

    [Fact]
    public void GetEnvironmentValidPaths()
    {
        var pathVariable = string.Join(_separator, GetValidPaths().Concat(GetInvalidPaths()));
        _environment.GetEnvironmentVariable("PATH").Returns(_ => pathVariable);

        var validPaths = _provider.GetEnvironmentValidPaths();

        validPaths.ToArray().Should().Equal(GetValidPaths().ToArray());
    }

    [Fact]
    public void GetEnvironmentValidPaths_quoted()
    {
        var paths = GetValidPaths().Concat(GetInvalidPaths());
        var quotedPaths = paths.Select(path => path.Quote());
        var pathVariable = string.Join(_separator, quotedPaths);
        _environment.GetEnvironmentVariable("PATH").Returns(pathVariable);

        var validPaths = _provider.GetEnvironmentValidPaths();

        validPaths.ToArray().Should().Equal(GetValidPaths().ToArray());
    }

    [Theory(SkipUnless = nameof(PlatformTestConditions.IsWindows), SkipType = typeof(PlatformTestConditions))]
    [InlineData("\\\\my-pc\\Work\\GitExtensions\\", true)]
    [InlineData("C:\\Work\\GitExtensions\\", true)]
    [InlineData("C:\\Work\\", true)]
    [InlineData("C:\\", true)]
    [InlineData("C:", true)]
    [InlineData("", false)]
    [InlineData("\"C:\\Work\\GitExtensions\\", false)]
    public void IsValidPath(string given, bool expected)
    {
        EnvironmentPathsProvider.IsValidPath(given).Should().Be(expected);
    }

    private static IEnumerable<string> GetInvalidPaths()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            yield return @"c::\word";
            yield return "\"c:\\word\t\\\"";
            yield return @".c:\Programs\";
        }
        // I am not able to figure out any invalid (giving exception) path under mono
    }

    private static IEnumerable<string> GetValidPaths()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            yield return @"c:\work";
            yield return @"c:\work\";
            yield return @"c:\Program Files(86)\";
            yield return @"c:\Program Files(86)\Git";
        }
        else
        {
            yield return "/etc/init.d/xvfb";
            yield return "/var";
            yield return "/";
        }
    }
}
