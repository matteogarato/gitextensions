using CommonTestUtils;
using GitExtensions.Extensibility.Git;
using GitModule = GitCommands.Git.GitModule;

namespace UITests.GitCommands;

public sealed class GitModuleTests : IDisposable
{
    private readonly GitModule _gitModule = null!;
    private readonly ReferenceRepository _refRepo = null!;

    public GitModuleTests()
    {
        _refRepo = new ReferenceRepository();
        _gitModule = _refRepo.Module;
        Console.WriteLine("Repo path: " + _gitModule.WorkingDir);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _refRepo.Dispose();
    }

    [Fact]
    public void GetCommitCount_Should_manage_ambiguous_argument()
    {
        const string ambiguousName = "script";
        var initialCommit = CreateCommitWithAmbiguousFolder("commit1");
        CreateCommitWithAmbiguousFolder("commit2");
        var tipOfMainBranchCommit = CreateCommitWithAmbiguousFolder("commit3");

        // Create a branch with same name than a folder inside repo
        _refRepo.CreateBranch(ambiguousName, tipOfMainBranchCommit.Hash);

        var exitCode = _gitModule.GetCommitCount(initialCommit.Hash, ambiguousName);

        exitCode.Should().Be(0);

        CommitData CreateCommitWithAmbiguousFolder(string commitMessage)
        {
            var hash = _refRepo.CreateCommitRelative(ambiguousName, "main_file.txt", commitMessage, commitMessage);
            return new CommitData(hash, commitMessage, $"{hash} {commitMessage}", ObjectId.Parse(hash));
        }
    }
}

internal record struct CommitData(string Hash, string CommitMessage, string RebaseLine, ObjectId ObjectId)
{
}
