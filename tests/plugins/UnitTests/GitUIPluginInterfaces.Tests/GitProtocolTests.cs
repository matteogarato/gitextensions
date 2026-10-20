using GitUIPluginInterfaces.RepositoryHosts;
using Xunit;

namespace GitUIPluginInterfacesTests;
public class GitProtocolTests
{
    [Theory]
    [InlineData("https://github.com/gitextensions/gitextensions.git", true)]
    [InlineData("htTps://github.com/gitextensions/gitextensions.git", true)]
    [InlineData("HTTPS://github.com/gitextensions/gitextensions.git", true)]
    [InlineData("http://github.com/gitextensions/gitextensions.git", true)]
    [InlineData("https_github.com/gitextensions/gitextensions.git", false)]
    [InlineData("ssh://git@github.com/gitextensions/gitextensions.git", false)]
    [InlineData("git@github.com/gitextensions/gitextensions.git", false)]
    [InlineData("github.com/gitextensions/gitextensions.git", false)]
    public void IsUrlUsingHttp(string url, bool expected)
    {
        url.IsUrlUsingHttp().Should().Be(expected);
    }
}
