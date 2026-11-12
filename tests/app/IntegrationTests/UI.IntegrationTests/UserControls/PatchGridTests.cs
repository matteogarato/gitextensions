using CommonTestUtils;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.UserControls;
using NSubstitute;
using PatchGrid = GitUI.UserControls.PatchGrid;

namespace UITests.UserControls;

public sealed class PatchGridTests : IDisposable
{
    // Created once for each test
    private readonly GitUICommands _commands = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    public PatchGridTests()
    {
        _referenceRepository = new ReferenceRepository();

        _commands = new GitUICommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void RebasePatchesBuiltAccordingToGitRebaseFiles_EnsureRebaseFileFormatWithGit()
    {
        RunPatchGridTest(patchGrid =>
        {
            var content = "line1" + Environment.NewLine;
            var mainFile = "main_file.txt";
            var initialCommit = CreateCommit("main: initial commit", content, mainFile);
            content += "line2" + Environment.NewLine;
            CreateCommit("main: commit2", content, mainFile);
            content += "line3" + Environment.NewLine;
            var tipOfMainBranchCommit = CreateCommit("main: commit3", content, mainFile);

            // Create a branch with contents that will conflict on rebase
            _referenceRepository.CreateBranch("branch2", initialCommit.Hash);
            _referenceRepository.CheckoutBranch("branch2");
            var otherFile = "other_file.txt";
            var commitDone =
                CreateCommit("branch2: commit that will apply successfully", "other_line1", otherFile);
            var commitApplying = CreateCommit("branch2: commit that will fail to rebase",
                "a content that will create a conflict", mainFile);
            var commitToDo = CreateCommit("branch2: commit todo", "foobar", otherFile);

            // Real git rebase to ensure git rebase file format has not changed
            var execResult =
                _referenceRepository.Module.GitExecutable.Execute($"rebase {tipOfMainBranchCommit.Hash}",
                    throwOnErrorExit: false);
            execResult.ExitCode.Should().NotBe(0); // Expecting conflicts!

            var patches = patchGrid.GetTestAccessor().GetInteractiveRebasePatchFiles();

            patches.Should().HaveCount(3);
            VerifyPatch(patches[0], "pick", commitDone, true);
            VerifyPatch(patches[1], "pick", commitApplying, false, true);
            VerifyPatch(patches[2], "pick", commitToDo, false);

            CommitData CreateCommit(string commitMessage, string content, string filename)
            {
                var hash = _referenceRepository.CreateCommit(commitMessage, content, filename);
                return new CommitData(hash, commitMessage, $"{hash} {commitMessage}", ObjectId.Parse(hash));
            }

            return Task.CompletedTask;
        });
    }

    [WinFormsFact]
    public void RebasePatchesBuiltAccordingToGitRebaseFiles()
    {
        RunPatchGridTest(patchGrid =>
        {
            var commitDonePicked = CreateCommit("commit done: pick");
            var commitDoneDelete = CreateCommit("commit done: drop");
            var commitDoneLineRemoved = CreateCommit("commit done: line removed i.e drop");
            var commitApplying = CreateCommit("commit in progress");
            var commitToDoPicked = CreateCommit("commit todo: picked");
            var commitToDoDelete = CreateCommit("commit todo: drop");
            var commitToDoLineRemoved = CreateCommit("commit todo: line removed i.e drop");

            // Carefully crafted git rebase files to test different actions (pick, drop, line moved, line removed)
            Directory.CreateDirectory(Path.Combine(_referenceRepository.Module.WorkingDirGitDir, "rebase-merge"));
            _referenceRepository.CreateRepoFile(".git/rebase-merge/done", @$"drop {commitDoneDelete.RebaseLine}
pick {commitDonePicked.RebaseLine}
pick {commitApplying.RebaseLine}");
            _referenceRepository.CreateRepoFile(".git/rebase-merge/stopped-sha", commitApplying.Hash);
            _referenceRepository.CreateRepoFile(".git/rebase-merge/git-rebase-todo",
                @$"drop {commitToDoDelete.RebaseLine}
pick {commitToDoPicked.RebaseLine}");

            var patches = patchGrid.GetTestAccessor().GetInteractiveRebasePatchFiles();

            patches.Should().HaveCount(5);
            VerifyPatch(patches[0], "drop", commitDoneDelete, true); // Line moved up "during interactive rebase"
            VerifyPatch(patches[1], "pick", commitDonePicked, true);
            VerifyPatch(patches[2], "pick", commitApplying, false, true);
            VerifyPatch(patches[3], "drop", commitToDoDelete, false); // Line moved up "during interactive rebase"
            VerifyPatch(patches[4], "pick", commitToDoPicked, false);

            CommitData CreateCommit(string commitMessage)
            {
                var hash = _referenceRepository.CreateCommit(commitMessage);
                return new CommitData(hash, commitMessage, $"{hash} {commitMessage}", ObjectId.Parse(hash));
            }

            return Task.CompletedTask;
        });
    }

    private static void VerifyPatch(PatchFile patchFile, string action, CommitData commit, bool isApplied,
        bool isNext = false)
    {
        patchFile.Should().BeEquivalentTo(
            new PatchFile
            {
                Action = action,
                ObjectId = commit.ObjectId,
                IsApplied = isApplied,
                IsNext = isNext,
                Subject = commit.CommitMessage,
                Author = ReferenceRepository.AuthorFullIdentity
            },
            options => options.Excluding(x => x.Date));
    }

    private void RunPatchGridTest(Func<PatchGrid, Task> runTestAsync)
    {
        UiTest.RunControl(
            form =>
            {
                var uiCommandsSource = Substitute.For<IGitUiCommandsSource>();
                uiCommandsSource.UiCommands.Returns(x => _commands);

                form.Size = new Size(600, 480);

                return new PatchGrid { Dock = DockStyle.Fill, Parent = form, UiCommandsSource = uiCommandsSource };
            },
            async patchGrid =>
            {
                // Wait for pending operations so the Control is loaded completely before testing it
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

                await runTestAsync(patchGrid);
            });
    }
}

internal record struct CommitData(string Hash, string CommitMessage, string RebaseLine, ObjectId ObjectId)
{
}
