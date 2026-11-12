using System.Text;
using GitCommands.Git;
using GitExtensions.Extensibility;
using NSubstitute;

namespace GitCommandsTests.Git;

public class AheadBehindDataProviderTests : IDisposable
{
    private readonly string _errorOutput = "";
    private readonly IExecutable _executable = null!;
    private readonly StreamReader _outputStreamReader = null!;
    private readonly IProcess _process = null!;
    private readonly AheadBehindDataProvider _provider = null!;
    private readonly MemoryStream _standardErrorStream = null!;
    private readonly MemoryStream _standardOutputStream = null!;

    public AheadBehindDataProviderTests()
    {
        _standardOutputStream = new MemoryStream();
        _standardErrorStream = new MemoryStream();
        _outputStreamReader = new StreamReader(_standardOutputStream);

        _process = Substitute.For<IProcess>();
        _process.StandardOutput.Returns(x => _outputStreamReader);
        _process.StandardError.Returns(x => _errorOutput);

        _executable = Substitute.For<IExecutable>();
        _executable.Start(Arg.Any<ArgumentString>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<Encoding>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(x => _process);

        _provider = new AheadBehindDataProvider(() => _executable);
    }

    void IDisposable.Dispose()
    {
        _standardOutputStream?.Dispose();
        _standardErrorStream?.Dispose();
        _outputStreamReader?.Dispose();
        _process?.Dispose();
    }

    [Theory]
    [InlineData(null)]
    public void GetData_should_throw_if_branch_null(string? branchName)
    {
        ((Action)(() => _provider.GetTestAccessor().GetData(Encoding.UTF8, branchName!))).Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void GetData_should_return_null_if_detached_head()
    {
        _provider.GetTestAccessor().GetData(Encoding.UTF8, "(no branch)").Should().BeNull();
    }

    [Fact]
    public void GetData_should_return_null_if_git_output_empty()
    {
        _provider.GetTestAccessor().GetData(Encoding.UTF8, "**").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetData_should_return_null_result_if_unable_match_branch(string? result)
    {
        SetResultOfGitCommand(result);

        var data = _provider.GetTestAccessor().GetData(Encoding.UTF8, "**");

        data.Should().BeNull();
    }

    [Theory]
    [InlineData("::::::::my-branch")]
    [InlineData("::gone::::::branch-with-no-more-remote")]
    [InlineData("::::::::")]
    [InlineData("results!")]

    // Cover the case where a push refspec is defined for the remote. When this value is set, for-each-ref shows
    // 'refs/remotes/origin/name' as the default push location for any untracked branch 'name'.
    [InlineData("gone::::refs/remotes/origin/new-branch::::new-branch")]
    public void GetData_should_return_empty_if_git_output_has_no_data(string? result)
    {
        SetResultOfGitCommand(result);

        var data = _provider.GetTestAccessor().GetData(Encoding.UTF8, "**");

        data.Should().BeEmpty();
    }

    [Theory]
    [InlineData("::ahead 1::::::my-branch")]
    [InlineData("results!")]
    public void GetData_should_return_empty_if_no_remote(string? result)
    {
        SetResultOfGitCommand(result);

        var data = _provider.GetTestAccessor().GetData(Encoding.UTF8, "**");

        data.Should().BeEmpty();
    }

    [Fact]
    public void GetData_should_return_empty_string_for_unknown()
    {
        SetResultOfGitCommand("::translated-ahead 3::::refs/remotes/upstream/branch::my-branch");

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be(string.Empty);
        aheadBehindData.BehindCount.Should().Be(string.Empty);
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.RemoteRef.Should().Be("refs/remotes/upstream/branch");
        aheadBehindData.ToDisplay().Should().Be(string.Empty);
        aheadBehindData.ToDisplay(reverse: true).Should().Be(string.Empty);
    }

    [Fact]
    public void GetData_should_return_ahead_for_a_branch()
    {
        SetResultOfGitCommand("::ahead 10::::refs/remotes/upstream/branch::my-branch");

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be("10");
        aheadBehindData.BehindCount.Should().Be(string.Empty);
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.RemoteRef.Should().Be("refs/remotes/upstream/branch");
        aheadBehindData.ToDisplay().Should().Be("10↑");
        aheadBehindData.ToDisplay(reverse: true).Should().Be("10↓");
    }

    [Fact]
    public void GetData_should_return_behind_for_a_branch()
    {
        SetResultOfGitCommand("::behind 2::::refs/remotes/upstream/my-branch::my-branch");

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be("0");
        aheadBehindData.BehindCount.Should().Be("2");
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.RemoteRef.Should().Be("refs/remotes/upstream/my-branch");
        aheadBehindData.ToDisplay().Should().Be("2↓");
        aheadBehindData.ToDisplay(reverse: true).Should().Be("2↑");
    }

    [Fact]
    public void GetData_should_return_zero_for_a_branch()
    {
        SetResultOfGitCommand("::::::refs/remotes/upstream/my-branch::my-branch");

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be("0");
        aheadBehindData.BehindCount.Should().Be("");
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.ToDisplay().Should().Be("0↑↓");
        aheadBehindData.ToDisplay(reverse: true).Should().Be("0↓↑");
    }

    [Theory]
    [InlineData("::ahead 99, behind 3::::refs/remotes/upstream/branch::my-branch")]
    [InlineData("ahead 99, behind 3::::refs/remotes/upstream/branch::::my-branch")]
    public void GetData_should_return_ahead_and_behind_for_a_branch(string? result)
    {
        SetResultOfGitCommand(result);

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be("99");
        aheadBehindData.BehindCount.Should().Be("3");
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.RemoteRef.Should().Be("refs/remotes/upstream/branch");
        aheadBehindData.ToDisplay().Should().Be("99↑ 3↓");
        aheadBehindData.ToDisplay(reverse: true).Should().Be("99↓ 3↑");
    }

    [Theory]
    [InlineData(
        "ahead 99, behind 97::ahead 9, behind 7::refs/remotes/upstream/push-branch::refs/remotes/upstream/upstream-branch::my-branch")]
    public void GetData_should_prefer_push_before_upstream_ahead_behind(string? result)
    {
        SetResultOfGitCommand(result);

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be("99");
        aheadBehindData.BehindCount.Should().Be("97");
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.RemoteRef.Should().Be("refs/remotes/upstream/push-branch");
        aheadBehindData.ToDisplay().Should().Be("99↑ 97↓");
        aheadBehindData.ToDisplay(reverse: true).Should().Be("99↓ 97↑");
    }

    [Theory]
    [InlineData(
        "ahead 99::ahead 9, behind 7::refs/remotes/upstream/push-branch::refs/remotes/upstream/upstream-branch::my-branch")]
    public void GetData_should_prefer_push_before_upstream_ahead(string? result)
    {
        SetResultOfGitCommand(result);

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be("99");
        aheadBehindData.BehindCount.Should().Be("");
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.RemoteRef.Should().Be("refs/remotes/upstream/push-branch");
        aheadBehindData.ToDisplay().Should().Be("99↑");
        aheadBehindData.ToDisplay(reverse: true).Should().Be("99↓");
    }

    [Theory]
    [InlineData(
        "behind 97::ahead 9, behind 7::refs/remotes/upstream/push-branch::refs/remotes/upstream/upstream-branch::my-branch")]
    public void GetData_should_prefer_push_before_upstream_behind(string? result)
    {
        SetResultOfGitCommand(result);

        var data = _provider.GetTestAccessor()
            .GetData(Encoding.UTF8, "my-branch");

        data.Should().HaveCount(1);
        var aheadBehindData = data.Values.First();
        aheadBehindData.AheadCount.Should().Be("");
        aheadBehindData.BehindCount.Should().Be("97");
        aheadBehindData.Branch.Should().Be("my-branch");
        aheadBehindData.RemoteRef.Should().Be("refs/remotes/upstream/push-branch");
        aheadBehindData.ToDisplay().Should().Be("97↓");
        aheadBehindData.ToDisplay(reverse: true).Should().Be("97↑");
    }

    [Fact]
    public void GetData_should_return_ahead_and_behind_for_all_branches()
    {
        SetResultOfGitCommand("::::::::branch-not-changed\n"
                              + "::gone::::refs/remotes/upstream/branch::branch-with-no-more-remote\n"
                              + "::ahead 99, behind 3::::refs/remotes/upstream/branch::ahead-behind-branch\n"
                              + "::ahead 3::::refs/remotes/upstream/branch::ahead-branch\n"
                              + "::behind 4::::refs/remotes/upstream/branch::behind-branch");

        var data = _provider.GetTestAccessor().GetData(Encoding.UTF8, "*");

        data.Should().HaveCount(4);
        data.Should().BeEquivalentTo(
            new Dictionary<string, AheadBehindData>
            {
                {
                    "branch-with-no-more-remote",
                    new AheadBehindData
                    {
                        AheadCount = "gone",
                        BehindCount = "",
                        Branch = "branch-with-no-more-remote",
                        RemoteRef = "refs/remotes/upstream/branch"
                    }
                },
                {
                    "ahead-behind-branch",
                    new AheadBehindData
                    {
                        AheadCount = "99",
                        BehindCount = "3",
                        Branch = "ahead-behind-branch",
                        RemoteRef = "refs/remotes/upstream/branch"
                    }
                },
                {
                    "ahead-branch",
                    new AheadBehindData
                    {
                        AheadCount = "3",
                        BehindCount = string.Empty,
                        Branch = "ahead-branch",
                        RemoteRef = "refs/remotes/upstream/branch"
                    }
                },
                {
                    "behind-branch",
                    new AheadBehindData
                    {
                        AheadCount = "0",
                        BehindCount = "4",
                        Branch = "behind-branch",
                        RemoteRef = "refs/remotes/upstream/branch"
                    }
                }
            });
    }

    private void SetResultOfGitCommand(string? result)
    {
        StreamWriter writer = new(_standardOutputStream);
        writer.Write(result);
        writer.Flush();
        _standardOutputStream.Position = 0;
    }
}
