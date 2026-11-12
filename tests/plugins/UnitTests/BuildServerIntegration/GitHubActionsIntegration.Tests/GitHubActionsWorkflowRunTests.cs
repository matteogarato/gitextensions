using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Plugins.GitHubActionsIntegration.ApiClient.Models;

namespace GitHubActionsIntegrationTests;

public class GitHubActionsWorkflowRunTests
{
    private const string _testSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ToBuildInfo_should_map_success_correctly()
    {
        var run = CreateRun("completed", "success");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Success);
        result.Id.Should().Be("42");
        result.CommitHashList.Should().HaveCount(1);
        result.CommitHashList[0].Should().Be(ObjectId.Parse(_testSha));
        result.Url.Should().Be("https://github.com/owner/repo/actions/runs/42");
        result.ShowInBuildReportTab.Should().BeFalse();
    }

    [Fact]
    public void ToBuildInfo_should_map_failure_correctly()
    {
        var run = CreateRun("completed", "failure");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Failure);
    }

    [Fact]
    public void ToBuildInfo_should_map_cancelled_to_stopped()
    {
        var run = CreateRun("completed", "cancelled");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Stopped);
    }

    [Fact]
    public void ToBuildInfo_should_map_in_progress_correctly()
    {
        var run = CreateRun("in_progress", null);

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.InProgress);
    }

    [Fact]
    public void ToBuildInfo_should_map_queued_to_in_progress()
    {
        var run = CreateRun("queued", null);

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.InProgress);
    }

    [Fact]
    public void ToBuildInfo_should_map_timed_out_to_failure()
    {
        var run = CreateRun("completed", "timed_out");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Failure);
    }

    [Fact]
    public void ToBuildInfo_should_map_action_required_to_unstable()
    {
        var run = CreateRun("completed", "action_required");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Unstable);
    }

    [Fact]
    public void ToBuildInfo_should_map_skipped_to_success()
    {
        var run = CreateRun("completed", "skipped");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Success);
    }

    [Fact]
    public void ToBuildInfo_should_map_neutral_to_success()
    {
        var run = CreateRun("completed", "neutral");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Success);
    }

    [Fact]
    public void ToBuildInfo_should_map_stale_to_unstable()
    {
        var run = CreateRun("completed", "stale");

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.Unstable);
    }

    [Theory]
    [InlineData("waiting")]
    [InlineData("pending")]
    [InlineData("requested")]
    public void ToBuildInfo_should_map_pending_statuses_to_in_progress(string status)
    {
        var run = CreateRun(status, null);

        var result = run.ToBuildInfo();

        result.Status.Should().Be(BuildStatus.InProgress);
    }

    [Fact]
    public void ToBuildInfo_should_format_description()
    {
        var run = CreateRun("completed", "success");
        run.Name = "CI Build";
        run.RunNumber = 123;

        var result = run.ToBuildInfo();

        result.Description.Should().Be("CI Build #123 (success)");
    }

    private static GitHubActionsWorkflowRun CreateRun(string status, string? conclusion)
    {
        var now = DateTime.UtcNow;
        return new GitHubActionsWorkflowRun
        {
            Id = 42,
            Name = "Build",
            HeadSha = _testSha,
            HeadBranch = "main",
            Status = status,
            Conclusion = conclusion,
            HtmlUrl = "https://github.com/owner/repo/actions/runs/42",
            CreatedAt = now.AddMinutes(-5),
            UpdatedAt = now,
            RunStartedAt = now.AddMinutes(-5),
            RunNumber = 1,
            RunAttempt = 1,
            Event = "push",
            DisplayTitle = "Test commit"
        };
    }
}
