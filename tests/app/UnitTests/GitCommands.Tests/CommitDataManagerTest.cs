using GitCommands;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests;

public class CommitDataManagerTest
{
    private readonly Func<IGitModule> _getModule = null!;
    private readonly IGitModule _module = null!;
    private CommitDataManager _commitDataManager = null!;

    public CommitDataManagerTest()
    {
        _module = Substitute.For<IGitModule>();
        _module.ReEncodeStringFromLossless(Arg.Any<string>()).Returns(x => x[0]);
        _module.ReEncodeCommitMessage(Arg.Any<string>()).Returns(x => x[0]);

        _getModule = () => _module;
        _commitDataManager = new CommitDataManager(_getModule);
    }
}
