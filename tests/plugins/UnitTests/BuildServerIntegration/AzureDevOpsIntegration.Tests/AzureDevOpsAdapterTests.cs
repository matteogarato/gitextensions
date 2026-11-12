using GitExtensions.Plugins.AzureDevOpsIntegration;

namespace AzureDevOpsIntegrationTests;

public class AzureDevOpsAdapterTests
{
    private readonly AzureDevOpsAdapter.TestAccessor _sut;

    public AzureDevOpsAdapterTests()
    {
        AzureDevOpsAdapter adapter = new();
        _sut = adapter.GetTestAccessor();
    }

    [Fact]
    public void Should_not_filter_running_builds_When_only_one_returned()
    {
        Build myBuild = new() { SourceVersion = "ACommitHash" };
        IList<Build> runningBuilds = [myBuild];

        var filteredRunningBuilds = _sut.FilterRunningBuilds(runningBuilds);

        filteredRunningBuilds.Should().ContainSingle().And.Contain(myBuild);
    }

    [Fact]
    public void Should_not_filter_running_builds_When_builds_are_on_different_commits()
    {
        Build buildOnOneCommit = new() { SourceVersion = "a_commit_Hash" };
        Build buildOnAnotherCommit = new() { SourceVersion = "another_commit_Hash" };
        IList<Build> runningBuilds = [buildOnOneCommit, buildOnAnotherCommit];

        var filteredRunningBuilds = _sut.FilterRunningBuilds(runningBuilds);

        filteredRunningBuilds.Should().ContainInOrder(buildOnOneCommit, buildOnAnotherCommit);
    }

    [Fact]
    public void Should_take_only_the_first_started_running_builds_When_multiple_builds_on_same_commit()
    {
        Build firstBuildStartedOnACommit =
            new() { SourceVersion = "a_commit_Hash", StartTime = new DateTime(2010, 1, 1) };
        Build buildAlsoStartedOnSameCommitButAfter =
            new() { SourceVersion = "a_commit_Hash", StartTime = new DateTime(2010, 1, 2) };
        Build buildAlsoStartedOnSameCommitButAfterAlso =
            new() { SourceVersion = "a_commit_Hash", StartTime = new DateTime(2010, 1, 3) };
        IList<Build> runningBuilds =
        [
            firstBuildStartedOnACommit, buildAlsoStartedOnSameCommitButAfter, buildAlsoStartedOnSameCommitButAfterAlso
        ];

        var filteredRunningBuilds = _sut.FilterRunningBuilds(runningBuilds);

        filteredRunningBuilds.Should().ContainSingle().And.ContainInOrder(firstBuildStartedOnACommit);
    }

    [Fact]
    public void Should_take_whatever_build_When_multiple_not_yet_started_builds_on_same_commit()
    {
        Build firstBuildStartedOnACommit = new() { SourceVersion = "a_commit_Hash" };
        Build buildAlsoStartedOnSameCommitButAfter = new() { SourceVersion = "a_commit_Hash" };
        Build buildAlsoStartedOnSameCommitButAfterAlso = new() { SourceVersion = "a_commit_Hash" };
        IList<Build> runningBuilds =
        [
            firstBuildStartedOnACommit, buildAlsoStartedOnSameCommitButAfter, buildAlsoStartedOnSameCommitButAfterAlso
        ];

        var filteredRunningBuilds = _sut.FilterRunningBuilds(runningBuilds);

        filteredRunningBuilds.Should().ContainSingle();
    }

    [Fact]
    public void Should_take_only_a_started_running_builds_When_multiple_builds_on_same_commit()
    {
        Build notStartedBuildOnACommit = new() { SourceVersion = "a_commit_Hash" };
        Build startedBuild = new() { SourceVersion = "a_commit_Hash", StartTime = new DateTime(2010, 1, 2) };
        Build anotherNotStartedBuildOnACommit = new() { SourceVersion = "a_commit_Hash" };
        IList<Build> runningBuilds = [notStartedBuildOnACommit, startedBuild, anotherNotStartedBuildOnACommit];

        var filteredRunningBuilds = _sut.FilterRunningBuilds(runningBuilds);

        filteredRunningBuilds.Should().ContainSingle().And.ContainInOrder(startedBuild);
    }
}
