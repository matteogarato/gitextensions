using GitExtensions.Extensibility.Git;
using GitUI.UserControls;
using NSubstitute;

namespace GitUITests.UserControls;

public class FileStatusDiffCalculatorTests
{
    private readonly FileStatusDiffCalculator _calculator;
    private readonly IGitModule _module;

    public FileStatusDiffCalculatorTests()
    {
        _module = Substitute.For<IGitModule>();
        _calculator = new FileStatusDiffCalculator(() => _module);
        _calculator.SetDiff([new GitRevision(ObjectId.Random())], ObjectId.Random(), false);
    }

    [Fact]
    public void Calculate_must_not_run_git_if_the_repository_was_closed()
    {
        // The module is resolved when the calculation runs, so it may belong to a repository which
        // was closed or switched while the calculation was queued
        _module.IsValidGitWorkingDir().Returns(false);

        ((Action)(() => _calculator.Calculate([], true, false, TestContext.Current.CancellationToken)))
            .Should().Throw<OperationCanceledException>();

        _module.DidNotReceiveWithAnyArgs().GetDiffFilesWithSubmodulesStatus(default, default, default, default, default,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Calculate_must_run_git_for_a_valid_repository()
    {
        _module.IsValidGitWorkingDir().Returns(true);

        ((Action)(() => _calculator.Calculate([], true, false, TestContext.Current.CancellationToken)))
            .Should().NotThrow<OperationCanceledException>();
    }
}
