using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests.Git;

public class GitDescribeProviderTests
{
    private readonly GitDescribeProvider _provider = null!;
    private IGitModule _module = null!;

    public GitDescribeProviderTests()
    {
        _module = Substitute.For<IGitModule>();
        _provider = new GitDescribeProvider(() => _module);
    }

    [Fact]
    public void GitDescribeProvider_returns_nulls_for_invalid_revision()
    {
        var commitId = ObjectId.Random();
        _module.GetDescribe(commitId, Arg.Any<CancellationToken>()).Returns(x => null);
        (var precedingTag, var commitCount) = _provider.Get(commitId, TestContext.Current.CancellationToken);

        precedingTag.Should().BeNullOrEmpty();
        commitCount.Should().BeNullOrEmpty();
    }

    [Fact]
    public void GitDescribeProvider_returns_nulls_if_no_preceding_tag()
    {
        // 33f0bc7f021210eb4bf49f770b5fc5952dfd41c2 predates tag 0.90 by 1 commit.
        // GitExecutable.GetOutput returns "fatal: No tags can describe '33f0bc7f021210eb4bf49f770b5fc5952dfd41c2'.\r\nTry --always, or create some tags."
        var commitId = ObjectId.Parse("33f0bc7f021210eb4bf49f770b5fc5952dfd41c2");
        _module.GetDescribe(commitId, Arg.Any<CancellationToken>()).Returns(x => null);
        (var precedingTag, var commitCount) = _provider.Get(commitId, TestContext.Current.CancellationToken);

        precedingTag.Should().BeNullOrEmpty();
        commitCount.Should().BeNullOrEmpty();
    }

    [Fact]
    public void GitDescribeProvider_returns_null_commitCount_at_tag()
    {
        // 943d230ba465d86c3ad2cd00f7e8c508d144d9a5 is the commit at tag 0.90.
        var commitId = ObjectId.Parse("943d230ba465d86c3ad2cd00f7e8c508d144d9a5");
        _module.GetDescribe(commitId, Arg.Any<CancellationToken>()).Returns("0.90");
        (var precedingTag, var commitCount) = _provider.Get(commitId, TestContext.Current.CancellationToken);

        precedingTag.Should().Be("0.90");
        commitCount.Should().BeNullOrEmpty();
    }

    [Fact]
    public void GitDescribeProvider_returns_precedingTag_and_commitCount()
    {
        // 16dc9d22d986f9ca03f6ec24007d65e6c062840e comes after tag 0.90 by 2 commits.
        var commitId = ObjectId.Parse("16dc9d22d986f9ca03f6ec24007d65e6c062840e");
        _module.GetDescribe(commitId, Arg.Any<CancellationToken>())
            .Returns("0.90-2-g16dc9d22d986f9ca03f6ec24007d65e6c062840e");
        (var precedingTag, var commitCount) = _provider.Get(commitId, TestContext.Current.CancellationToken);

        precedingTag.Should().Be("0.90");
        commitCount.Should().Be("2");
    }

    [Fact]
    public void GitDescribeProvider_should_throw_if_module_is_not_provided()
    {
        _module = null!;

        ((Action)(() => _provider.Get(ObjectId.Random()))).Should().Throw<ArgumentException>();
    }
}
