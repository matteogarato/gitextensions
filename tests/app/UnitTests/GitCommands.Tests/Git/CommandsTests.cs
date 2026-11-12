using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitCommands.Utils;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitCommandsTests.Git;

public partial class CommandsTests
{
    public static IEnumerable<object?[]> GetRefsTestData
    {
        get
        {
            foreach (var sortBy in EnumHelper.GetValues<GitRefsSortBy>())
            {
                foreach (var sortOrder in EnumHelper.GetValues<GitRefsSortOrder>())
                {
                    string sortCondition;
                    string sortConditionRef;
                    var format =
                        @" --format=""%(if)%(authordate)%(then)%(objectname) %(refname)%(else)%(*objectname) %(*refname)%(end)""";
                    var formatNoTag = @" --format=""%(objectname) %(refname)""";
                    if (sortBy == GitRefsSortBy.Default)
                    {
                        sortCondition = sortConditionRef = string.Empty;
                    }
                    else
                    {
                        const string gitRefsSortByVersion = "version:refname";
                        var sortKey = sortBy == GitRefsSortBy.VersionRefname
                            ? gitRefsSortByVersion
                            : sortBy.ToString();
                        var derefSortKey = (sortBy == GitRefsSortBy.VersionRefname ? GitRefsSortBy.Refname : sortBy)
                            .ToString();

                        var order = sortOrder == GitRefsSortOrder.Ascending ? string.Empty : "-";
                        sortCondition = $@" --sort=""{order}{sortKey}""";
                        sortConditionRef = $@" --sort=""{order}*{derefSortKey}""";
                    }

                    yield return new object?[]
                    {
                        RefsFilter.Tags | RefsFilter.Heads | RefsFilter.Remotes, /* noLocks */ false, sortBy, sortOrder,
                        0,
                        /* expected */
                        $@"for-each-ref{sortConditionRef}{sortCondition}{format} refs/heads/ refs/remotes/ refs/tags/"
                    };
                    yield return new object?[]
                    {
                        RefsFilter.Tags, /* noLocks */ false, sortBy, sortOrder, 0,
                        /* expected */ $@"for-each-ref{sortConditionRef}{sortCondition}{format} refs/tags/"
                    };
                    yield return new object?[]
                    {
                        RefsFilter.Heads, /* noLocks */ false, sortBy, sortOrder, 0,
                        /* expected */ $@"for-each-ref{sortCondition}{formatNoTag} refs/heads/"
                    };
                    yield return new object?[]
                    {
                        RefsFilter.Heads, /* noLocks */ false, sortBy, sortOrder, 100,
                        /* expected */ $@"for-each-ref{sortCondition}{formatNoTag} --count=100 refs/heads/"
                    };
                    yield return new object?[]
                    {
                        RefsFilter.Heads, /* noLocks */ true, sortBy, sortOrder, 0,
                        /* expected */ $@"--no-optional-locks for-each-ref{sortCondition}{formatNoTag} refs/heads/"
                    };
                    yield return new object?[]
                    {
                        RefsFilter.Remotes, /* noLocks */ false, sortBy, sortOrder, 0,
                        /* expected */ $@"for-each-ref{sortCondition}{formatNoTag} refs/remotes/"
                    };

                    yield return new object?[]
                    {
                        RefsFilter.NoFilter, /* noLocks */ true, sortBy, sortOrder, 0,
                        /* expected */ $@"--no-optional-locks for-each-ref{sortConditionRef}{sortCondition}{format}"
                    };
                    yield return new object?[]
                    {
                        RefsFilter.Tags | RefsFilter.Heads | RefsFilter.Remotes | RefsFilter.NoFilter, /* noLocks */
                        true, sortBy, sortOrder, 0,
                        /* expected */
                        $@"--no-optional-locks for-each-ref{sortConditionRef}{sortCondition}{format} refs/heads/ refs/remotes/ refs/tags/"
                    };
                }
            }
        }
    }

    [Fact]
    public void AbortMergeCmd()
    {
        GitCommands.Git.Commands.AbortMerge().Arguments.Should().Be("merge --abort");
    }

    public static IEnumerable<object?[]> AddSubmoduleTestCases()
    {
        yield return new object?[] { "", null };
        yield return new object?[] { "-c protocol.file.allow=always ", GitCommands.Git.Commands.GetAllowFileConfig() };
    }

    [Theory]
    [MemberData(nameof(AddSubmoduleTestCases))]
    public void AddSubmoduleCmd(string config, IEnumerable<GitConfigItem> configs)
    {
        GitCommands.Git.Commands.AddSubmodule("remotepath", "localpath", "branch", false, configs).Arguments.Should()
            .Be($"{config}submodule add -b \"branch\" \"remotepath\" \"localpath\"");

        GitCommands.Git.Commands.AddSubmodule("remotepath", "localpath", null!, false, configs).Arguments.Should()
            .Be($"{config}submodule add \"remotepath\" \"localpath\"");

        GitCommands.Git.Commands.AddSubmodule("remotepath", "localpath", "branch", true, configs).Arguments.Should()
            .Be($"{config}submodule add -f -b \"branch\" \"remotepath\" \"localpath\"");

        GitCommands.Git.Commands.AddSubmodule("remote\\path", "local\\path", "branch", true, configs).Arguments.Should()
            .Be($"{config}submodule add -f -b \"branch\" \"remote/path\" \"local/path\"");
    }

    [Fact]
    public void ApplyDiffPatchCmd()
    {
        GitCommands.Git.Commands.ApplyDiffPatch(false, "hello\\world.patch", PathUtil.ToPosixPath).Arguments.Should()
            .Be("apply \"hello/world.patch\"");
        GitCommands.Git.Commands.ApplyDiffPatch(true, "hello\\world.patch", PathUtil.ToPosixPath).Arguments.Should()
            .Be("apply --ignore-whitespace \"hello/world.patch\"");
    }

    [Theory]
    [InlineData(false, false, "hello\\world.patch", "am --3way \"hello/world.patch\"")]
    [InlineData(false, true, "hello\\world.patch", "am --3way --ignore-whitespace \"hello/world.patch\"")]
    [InlineData(true, false, "hello\\world.patch", "am --3way --signoff \"hello/world.patch\"")]
    [InlineData(true, true, "hello\\world.patch", "am --3way --signoff --ignore-whitespace \"hello/world.patch\"")]
    [InlineData(true, true, null, "am --3way --signoff --ignore-whitespace")]
    public void ApplyMailboxPatchCmd(bool signOff, bool ignoreWhitespace, string? patchFile, string expected)
    {
        GitCommands.Git.Commands.ApplyMailboxPatch(signOff, ignoreWhitespace, patchFile, PathUtil.ToPosixPath).Arguments
            .Should().Be(expected);
    }

    [Fact]
    public void BranchCmd()
    {
        // TODO split this into BranchCmd and CheckoutCmd
        var objectId = ObjectId.Random();
        GitCommands.Git.Commands.Branch("branch", objectId, true).Arguments.Should()
            .Be($"checkout -b \"branch\" {objectId}");
        GitCommands.Git.Commands.Branch("branch", objectId, false).Arguments.Should()
            .Be($"branch \"branch\" {objectId}");
        GitCommands.Git.Commands.Branch("branch", default, true).Arguments.Should().Be("checkout -b \"branch\"");
    }

    [Fact]
    public void CheckoutCmd()
    {
        GitCommands.Git.Commands.Checkout("branch", LocalChangesAction.DontChange).Arguments.Should()
            .Be("checkout \"branch\"");
        GitCommands.Git.Commands.Checkout("branch", LocalChangesAction.Merge).Arguments.Should()
            .Be("checkout --merge \"branch\"");
        GitCommands.Git.Commands.Checkout("branch", LocalChangesAction.Reset).Arguments.Should()
            .Be("checkout --force \"branch\"");
        GitCommands.Git.Commands.Checkout("branch", LocalChangesAction.Stash).Arguments.Should()
            .Be("checkout \"branch\"");
    }

    [Theory]
    [InlineData(CleanMode.OnlyNonIgnored, true, false, null, null, "clean --dry-run")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, null, null, "clean -f")]
    [InlineData(CleanMode.OnlyNonIgnored, false, true, null, null, "clean -d -f")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, "\"path1\"", null, "clean -f \"path1\"")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, "\"path1\"", "--exclude=excludes",
        "clean -f \"path1\" --exclude=excludes")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, null, "excludes", "clean -f excludes")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, "\"path1\" \"path2\"", null, "clean -f \"path1\" \"path2\"")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, "\"path1\" \"path2\"", "--exclude=exclude1 --exclude=exclude2",
        "clean -f \"path1\" \"path2\" --exclude=exclude1 --exclude=exclude2")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, null, "--exclude=exclude1 --exclude=exclude2",
        "clean -f --exclude=exclude1 --exclude=exclude2")]
    [InlineData(CleanMode.OnlyIgnored, false, false, null, null, "clean -X -f")]
    [InlineData(CleanMode.All, false, false, null, null, "clean -x -f")]
    public void CleanCmd(CleanMode mode, bool dryRun, bool directories, string? paths, string? excludes,
        string expected)
    {
        GitCommands.Git.Commands.Clean(mode, dryRun, directories, paths, excludes).Arguments.Should().Be(expected);
    }

    [Theory]
    [InlineData(CleanMode.OnlyNonIgnored, true, false, null, "clean --dry-run")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, null, "clean -f")]
    [InlineData(CleanMode.OnlyNonIgnored, false, true, null, "clean -d -f")]
    [InlineData(CleanMode.OnlyNonIgnored, false, false, "paths", "clean -f paths")]
    [InlineData(CleanMode.OnlyIgnored, false, false, null, "clean -X -f")]
    [InlineData(CleanMode.All, false, false, null, "clean -x -f")]
    public void CleanupSubmoduleCommand(CleanMode mode, bool dryRun, bool directories, string? paths, string expected)
    {
        var subExpected = "submodule foreach --recursive git " + expected;
        GitCommands.Git.Commands.CleanSubmodules(mode, dryRun, directories, paths).Arguments.Should().Be(subExpected);
    }

    [Fact]
    public void CloneCmd()
    {
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath).Arguments.Should()
            .Be("clone -v --progress \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from/path", "to/path", PathUtil.ToPosixPath).Arguments.Should()
            .Be("clone -v --progress \"from/path\" \"to/path\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, true).Arguments.Should()
            .Be("clone -v --bare --progress \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, initSubmodules: true).Arguments.Should()
            .Be("clone -v --recurse-submodules --progress \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, initSubmodules: true).Arguments.Should()
            .Be("clone -v --recurse-submodules --progress \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, depth: 2).Arguments.Should()
            .Be("clone -v --depth 2 --progress \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, isSingleBranch: true).Arguments.Should()
            .Be("clone -v --single-branch --progress \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, isSingleBranch: false).Arguments.Should()
            .Be("clone -v --no-single-branch --progress \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, branch: "branch").Arguments.Should()
            .Be("clone -v --progress --branch branch \"from\" \"to\"");
        GitCommands.Git.Commands.Clone("from", "to", PathUtil.ToPosixPath, branch: null).Arguments.Should()
            .Be("clone -v --progress --no-checkout \"from\" \"to\"");
    }

    [Fact]
    public void CommitCmd()
    {
        GitCommands.Git.Commands.Commit(false, false, "", true, "COMMITMESSAGE", PathUtil.ToPosixPath, gpgSign: null)
            .Arguments.Should().Be("commit -F \"COMMITMESSAGE\"");
        GitCommands.Git.Commands
            .Commit(false, false, "", true, "COMMITMESSAGE", path => "adapted_commit_message_path", gpgSign: null)
            .Arguments.Should().Be("commit -F \"adapted_commit_message_path\"");
        GitCommands.Git.Commands.Commit(true, false, "", true, "COMMITMESSAGE", PathUtil.ToPosixPath, gpgSign: null)
            .Arguments.Should().Be("commit --amend -F \"COMMITMESSAGE\"");
        GitCommands.Git.Commands.Commit(false, true, "", true, "COMMITMESSAGE", PathUtil.ToPosixPath, gpgSign: null)
            .Arguments.Should().Be("commit --signoff -F \"COMMITMESSAGE\"");
        GitCommands.Git.Commands.Commit(false, false, "foo", true, "COMMITMESSAGE", PathUtil.ToPosixPath, gpgSign: null)
            .Arguments.Should().Be("commit --author=\"foo\" -F \"COMMITMESSAGE\"");
        GitCommands.Git.Commands.Commit(false, false, "", false, null, PathUtil.ToPosixPath, gpgSign: null).Arguments
            .Should().Be("commit");
        GitCommands.Git.Commands.Commit(false, false, "", true, "COMMITMESSAGE", PathUtil.ToPosixPath, true).Arguments
            .Should().Be("commit --no-verify -F \"COMMITMESSAGE\"");
        GitCommands.Git.Commands.Commit(false, false, "", true, "COMMITMESSAGE", PathUtil.ToPosixPath, gpgSign: false)
            .Arguments.Should().Be("commit --no-gpg-sign -F \"COMMITMESSAGE\"");
        GitCommands.Git.Commands.Commit(false, false, "", true, "COMMITMESSAGE", PathUtil.ToPosixPath, gpgSign: true)
            .Arguments.Should().Be("commit --gpg-sign -F \"COMMITMESSAGE\"");
        GitCommands.Git.Commands
            .Commit(false, false, "", true, "COMMITMESSAGE", PathUtil.ToPosixPath, gpgSign: true, gpgKeyId: "key")
            .Arguments.Should().Be("commit --gpg-sign=key -F \"COMMITMESSAGE\"");
    }

    [Theory]
    [InlineData(@"  ""author <author@mail.com>""  ",
        @"commit --author=""author <author@mail.com>"" -F ""COMMITMESSAGE""")]
    [InlineData(@"""author <author@mail.com>""", @"commit --author=""author <author@mail.com>"" -F ""COMMITMESSAGE""")]
    [InlineData(@"author <author@mail.com>", @"commit --author=""author <author@mail.com>"" -F ""COMMITMESSAGE""")]
    public void CommitCmdShouldTrimAuthor(string input, string expected)
    {
        var actual =
            GitCommands.Git.Commands.Commit(false, false, input, true, "COMMITMESSAGE", PathUtil.ToPosixPath);
        actual.Arguments.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, @"", false, false, null, @"", @"commit")]
    [InlineData(false, false, @"", false, false, false, @"", @"commit --no-gpg-sign")]
    [InlineData(true, false, @"", false, false, false, @"", @"commit --amend --no-gpg-sign")]
    [InlineData(false, true, @"", false, false, false, @"", @"commit --signoff --no-gpg-sign")]
    [InlineData(false, false, @"", true, false, false, @"", @"commit --no-gpg-sign -F ""COMMITMESSAGE""")]
    [InlineData(false, false, @"", false, true, false, @"", @"commit --no-verify --no-gpg-sign")]
    [InlineData(false, false, @"", false, false, false, @"12345678", @"commit --no-gpg-sign")]
    [InlineData(false, false, @"", false, false, true, @"", @"commit --gpg-sign")]
    [InlineData(false, false, @"", false, false, true, @"      ", @"commit --gpg-sign")]
    [InlineData(false, false, @"", false, false, true, null, @"commit --gpg-sign")]
    [InlineData(false, false, @"", false, false, true, @"12345678", @"commit --gpg-sign=12345678")]
    [InlineData(true, true, @"", true, true, true, @"12345678",
        @"commit --amend --no-verify --signoff --gpg-sign=12345678 -F ""COMMITMESSAGE""")]
    public void CommitCmdTests(bool amend, bool signOff, string author, bool useExplicitCommitMessage, bool noVerify,
        bool? gpgSign, string? gpgKeyId, string expected)
    {
        var actual = GitCommands.Git.Commands.Commit(amend, signOff, author, useExplicitCommitMessage,
            "COMMITMESSAGE", PathUtil.ToPosixPath, noVerify, gpgSign, gpgKeyId!);
        actual.Arguments.Should().Be(expected);
    }

    [Fact]
    public void ContinueBisectCmd()
    {
        var id1 = ObjectId.Random();
        var id2 = ObjectId.Random();

        GitCommands.Git.Commands.ContinueBisect(GitBisectOption.Good).Arguments.Should().Be("bisect good");
        GitCommands.Git.Commands.ContinueBisect(GitBisectOption.Bad).Arguments.Should().Be("bisect bad");
        GitCommands.Git.Commands.ContinueBisect(GitBisectOption.Skip).Arguments.Should().Be("bisect skip");
        GitCommands.Git.Commands.ContinueBisect(GitBisectOption.Good, id1, id2).Arguments.Should()
            .Be($"bisect good {id1} {id2}");
    }

    [Fact]
    public void ContinueMergeCmd()
    {
        GitCommands.Git.Commands.ContinueMerge().Arguments.Should().Be("merge --continue");
    }

    [Fact]
    public void GetAllChangedFilesCmd()
    {
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.Default, IgnoreSubmodulesMode.Default)
            .Arguments.Should()
            .Be("-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files --ignore-submodules");
        GitCommands.Git.Commands.GetAllChangedFiles(false, UntrackedFilesMode.Default, IgnoreSubmodulesMode.Default)
            .Arguments.Should()
            .Be(
                "-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files --ignored --ignore-submodules");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.No, IgnoreSubmodulesMode.Default).Arguments
            .Should().Be(
                "-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files=no --ignore-submodules");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.Normal, IgnoreSubmodulesMode.Default)
            .Arguments.Should()
            .Be("-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files=normal --ignore-submodules");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.All, IgnoreSubmodulesMode.Default)
            .Arguments.Should()
            .Be("-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files=all --ignore-submodules");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.Default).Arguments.Should()
            .Be("-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.Default).Arguments.Should()
            .Be("-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.Default, IgnoreSubmodulesMode.Untracked)
            .Arguments.Should()
            .Be(
                "-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files --ignore-submodules=untracked");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.Default, IgnoreSubmodulesMode.Dirty)
            .Arguments.Should()
            .Be("-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files --ignore-submodules=dirty");
        GitCommands.Git.Commands.GetAllChangedFiles(true, UntrackedFilesMode.Default, IgnoreSubmodulesMode.All)
            .Arguments.Should()
            .Be("-c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files --ignore-submodules=all");
        GitCommands.Git.Commands
            .GetAllChangedFiles(true, UntrackedFilesMode.Default, IgnoreSubmodulesMode.Default, true).Arguments.Should()
            .Be(
                "--no-optional-locks -c diff.ignoresubmodules=none status --porcelain=2 -z --untracked-files --ignore-submodules");
    }

    [Theory]
    [InlineData(
        @"-c color.ui=never -c diff.submodule=short -c diff.noprefix=false -c diff.mnemonicprefix=false -c diff.ignoresubmodules=none -c core.safecrlf=false diff --no-ext-diff --find-renames --find-copies extra --cached -- ""new"" ""old""",
        "new", "old", true, "extra", false)]
    [InlineData(
        @"-c color.ui=never -c diff.submodule=short -c diff.noprefix=false -c diff.mnemonicprefix=false -c diff.ignoresubmodules=none -c core.safecrlf=false diff --no-ext-diff --find-renames --find-copies extra -- ""new""",
        "new", "old", false, "extra", false)]
    [InlineData(
        @"--no-optional-locks -c color.ui=never -c diff.submodule=short -c diff.noprefix=false -c diff.mnemonicprefix=false -c diff.ignoresubmodules=none -c core.safecrlf=false diff --no-ext-diff --find-renames --find-copies extra --cached -- ""new"" ""old""",
        "new", "old", true, "extra", true)]
    public void GetCurrentChangesCmd(string expected, string fileName, string oldFileName, bool staged,
        string extraDiffArguments, bool noLocks)
    {
        GitCommands.Git.Commands.GetCurrentChanges(fileName, oldFileName, staged,
            extraDiffArguments, noLocks).ToString().Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(GetRefsTestData))]
    public void GetRefsCmd(RefsFilter getRefs, bool noLocks, GitRefsSortBy sortBy, GitRefsSortOrder sortOrder,
        int count, string expected)
    {
        GitCommands.Git.Commands.GetRefs(getRefs, noLocks, sortBy, sortOrder, count).ToString().Should().Be(expected);
    }

    // Don't care about permutations because the args aren't correlated
    [Theory]
    [InlineData(false, false, false, null, false, null, null, "merge --no-ff --no-edit branch")]
    [InlineData(true, true, true, null, true, null, null,
        "merge --ff --squash --no-commit --allow-unrelated-histories --no-edit branch")]

    // Fast forward is requested explicitly, so that merge.ff=false does not override the choice
    [InlineData(true, false, false, null, false, null, null, "merge --ff --no-edit branch")]

    // mergeCommitFilePath parameter
    [InlineData(false, true, false, null, false, "", null, "merge --no-ff --squash --no-edit branch")]
    [InlineData(false, true, false, null, false, "   ", null, "merge --no-ff --squash --no-edit branch")]
    [InlineData(false, true, false, null, false, "\t", null, "merge --no-ff --squash --no-edit branch")]
    [InlineData(false, true, false, null, false, "\n", null, "merge --no-ff --squash --no-edit branch")]
    [InlineData(false, true, false, null, false, "foo", null, "merge --no-ff --squash -F \"foo\" --no-edit branch")]
    [InlineData(false, true, false, null, false, "D:\\myrepo\\.git\\file", null,
        "merge --no-ff --squash -F \"D:/myrepo/.git/file\" --no-edit branch")]

    // log parameter
    [InlineData(true, true, false, null, false, null, -1, "merge --ff --squash --no-edit branch")]
    [InlineData(true, true, false, null, false, null, 0, "merge --ff --squash --no-edit branch")]
    [InlineData(true, true, false, null, false, null, 5, "merge --ff --squash --log=5 --no-edit branch")]
    public void MergeBranchCmd(bool allowFastForward, bool squash, bool noCommit, string? strategy,
        bool allowUnrelatedHistories, string? mergeCommitFilePath, int? log, string expected)
    {
        GitCommands.Git.Commands
            .MergeBranch("branch", allowFastForward, squash, noCommit, strategy!, allowUnrelatedHistories,
                mergeCommitFilePath, PathUtil.ToPosixPath, log).Arguments.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, null)]
    [InlineData(true, true, "")]
    [InlineData(true, true, "HEAD")]
    [InlineData(true, true, "1234567890")]
    [InlineData(true, false, null)]
    [InlineData(true, false, "")]
    [InlineData(true, false, "HEAD")]
    [InlineData(true, false, "1234567890")]
    [InlineData(false, true, null)]
    [InlineData(false, true, "")]
    [InlineData(false, true, "HEAD")]
    [InlineData(false, true, "1234567890")]
    [InlineData(false, false, null)]
    [InlineData(false, false, "")]
    [InlineData(false, false, "HEAD")]
    [InlineData(false, false, "1234567890")]
    public void MergedBranchesCmd(bool includeRemote, bool fullRefname, string? commit)
    {
        var formatArg = fullRefname ? @" --format=""%(refname)""" : string.Empty;
        var remoteArg = includeRemote ? " -a" : string.Empty;
        var commitArg = string.IsNullOrWhiteSpace(commit) ? string.Empty : $" {commit.Quote()}";
        var expected = $"branch{formatArg}{remoteArg} --merged{commitArg}";

        GitCommands.Git.Commands.MergedBranches(includeRemote, fullRefname, commit).Arguments.Should().Be(expected);
    }

    [Fact]
    public void PushAllCmd()
    {
        GitCommands.Git.Commands.PushAll("remote", ForcePushOptions.DoNotForce, false, 0).Arguments.Should()
            .Be("push --progress --all \"remote\"");
        GitCommands.Git.Commands.PushAll("remote", ForcePushOptions.Force, false, 0).Arguments.Should()
            .Be("push -f --progress --all \"remote\"");
        GitCommands.Git.Commands.PushAll("remote", ForcePushOptions.ForceWithLease, false, 0).Arguments.Should()
            .Be("push --force-with-lease --progress --all \"remote\"");
        GitCommands.Git.Commands.PushAll("remote", ForcePushOptions.DoNotForce, true, 0).Arguments.Should()
            .Be("push -u --progress --all \"remote\"");
        GitCommands.Git.Commands.PushAll("remote", ForcePushOptions.DoNotForce, false, 1).Arguments.Should()
            .Be("push --recurse-submodules=check --progress --all \"remote\"");
        GitCommands.Git.Commands.PushAll("remote", ForcePushOptions.DoNotForce, false, 2).Arguments.Should()
            .Be("push --recurse-submodules=on-demand --progress --all \"remote\"");
    }

    [Fact]
    public void PushCmd()
    {
        GitCommands.Git.Commands.Push("remote", "from-branch", null, ForcePushOptions.DoNotForce, false, 0).Arguments
            .Should().Be("push --progress \"remote\" from-branch");

        GitCommands.Git.Commands.Push("remote", "from-branch", "to-branch", ForcePushOptions.DoNotForce, false, 0)
            .Arguments.Should().Be("push --progress \"remote\" from-branch:refs/heads/to-branch");

        GitCommands.Git.Commands.Push("remote", "from-branch", "to-branch", ForcePushOptions.Force, false, 0).Arguments
            .Should().Be("push -f --progress \"remote\" from-branch:refs/heads/to-branch");

        GitCommands.Git.Commands.Push("remote", "from-branch", "to-branch", ForcePushOptions.ForceWithLease, false, 0)
            .Arguments.Should().Be("push --force-with-lease --progress \"remote\" from-branch:refs/heads/to-branch");

        GitCommands.Git.Commands.Push("remote", "from-branch", "to-branch", ForcePushOptions.DoNotForce, true, 0)
            .Arguments.Should().Be("push -u --progress \"remote\" from-branch:refs/heads/to-branch");

        GitCommands.Git.Commands.Push("remote", "from-branch", "to-branch", ForcePushOptions.DoNotForce, false, 1)
            .Arguments.Should()
            .Be("push --recurse-submodules=check --progress \"remote\" from-branch:refs/heads/to-branch");

        GitCommands.Git.Commands.Push("remote", "from-branch", "to-branch", ForcePushOptions.DoNotForce, false, 2)
            .Arguments.Should()
            .Be("push --recurse-submodules=on-demand --progress \"remote\" from-branch:refs/heads/to-branch");
    }

    [Fact]
    public void PushTagCmd()
    {
        GitCommands.Git.Commands.PushTag("path", "tag", false).Arguments.Should()
            .Be("push --progress \"path\" tag tag");
        GitCommands.Git.Commands.PushTag("path", " tag ", false).Arguments.Should()
            .Be("push --progress \"path\" tag tag");
        GitCommands.Git.Commands.PushTag("path\\path", " tag ", false).Arguments.Should()
            .Be("push --progress \"path/path\" tag tag");
        GitCommands.Git.Commands.PushTag("path", "tag", true).Arguments.Should().Be("push --progress \"path\" --tags");
        GitCommands.Git.Commands.PushTag("path", "tag", true, ForcePushOptions.Force).Arguments.Should()
            .Be("push -f --progress \"path\" --tags");
        GitCommands.Git.Commands.PushTag("path", "tag", true, ForcePushOptions.ForceWithLease).Arguments.Should()
            .Be("push --force-with-lease --progress \"path\" --tags");

        // TODO this should probably throw rather than return an empty string
        GitCommands.Git.Commands.PushTag("path", "", false).Arguments.Should().Be("");
    }

    [Theory]
    [InlineData(null, "onto")]
    [InlineData("from", null)]
    public void RebaseCmd_throws_ArgumentException_if_from_or_onto_null(string? from, string? onto)
    {
        GitCommands.Git.Commands.RebaseOptions rebaseOptions = new()
        {
            BranchName = "branch", From = from, OnTo = onto
        };

        ((Action)(() => GitCommands.Git.Commands.Rebase(rebaseOptions))).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, true, null, "-c rebase.autosquash=false rebase \"branch\"")]
    [InlineData(true, false, false, false, false, false, true, null,
        "-c rebase.autosquash=false rebase -i --no-autosquash \"branch\"")]
    [InlineData(false, true, false, false, false, false, true, null,
        "-c rebase.autosquash=false rebase --rebase-merges \"branch\"")]
    [InlineData(false, false, true, false, false, false, true, null, "-c rebase.autosquash=false rebase \"branch\"")]
    [InlineData(false, false, false, true, false, false, true, null,
        "-c rebase.autosquash=false rebase --autostash \"branch\"")]
    [InlineData(true, false, true, false, false, false, true, null,
        "-c rebase.autosquash=false rebase -i --autosquash \"branch\"")]
    [InlineData(false, false, false, false, true, false, true, null,
        "-c rebase.autosquash=false rebase --ignore-date \"branch\"")]
    [InlineData(false, false, false, false, false, true, true, null,
        "-c rebase.autosquash=false rebase --committer-date-is-author-date \"branch\"")]
    [InlineData(false, false, false, true, true, false, true, null,
        "-c rebase.autosquash=false rebase --ignore-date --autostash \"branch\"")]
    [InlineData(false, false, false, true, false, true, true, null,
        "-c rebase.autosquash=false rebase --committer-date-is-author-date --autostash \"branch\"")]
    [InlineData(true, true, true, true, true, false, true, null,
        "-c rebase.autosquash=false rebase --ignore-date --autostash \"branch\"")]
    [InlineData(true, true, true, true, false, true, true, null,
        "-c rebase.autosquash=false rebase --committer-date-is-author-date --autostash \"branch\"")]
    [InlineData(true, true, true, true, false, false, true, null,
        "-c rebase.autosquash=false rebase -i --autosquash --rebase-merges --autostash \"branch\"")]
    [InlineData(false, false, false, false, false, false, true, false,
        "-c rebase.autosquash=false rebase --no-update-refs \"branch\"")]
    [InlineData(false, false, false, false, false, false, true, true,
        "-c rebase.autosquash=false rebase --update-refs \"branch\"")]
    public void RebaseCmd(bool interactive, bool preserveMerges, bool autosquash, bool autoStash, bool ignoreDate,
        bool committerDateIsAuthorDate, bool supportRebaseMerges, bool? updateRefs, string expected)
    {
        GitCommands.Git.Commands.RebaseOptions rebaseOptions = new()
        {
            BranchName = "branch",
            Interactive = interactive,
            PreserveMerges = preserveMerges,
            AutoSquash = autosquash,
            AutoStash = autoStash,
            IgnoreDate = ignoreDate,
            CommitterDateIsAuthorDate = committerDateIsAuthorDate,
            SupportRebaseMerges = supportRebaseMerges,
            UpdateRefs = updateRefs
        };

        GitCommands.Git.Commands.Rebase(rebaseOptions).Arguments.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, null, "from", "onto",
        "-c rebase.autosquash=false rebase --onto onto \"from\" \"branch\"")]
    [InlineData(false, false, false, false, true, false, null, "from", "onto",
        "-c rebase.autosquash=false rebase --ignore-date --onto onto \"from\" \"branch\"")]
    public void RebaseCmd_specific_range(bool interactive, bool preserveMerges, bool autoSquash, bool autoStash,
        bool ignoreDate, bool committerDateIsAuthorDate, bool? updateRefs, string? from, string? onto, string expected)
    {
        GitCommands.Git.Commands.RebaseOptions rebaseOptions = new()
        {
            BranchName = "branch",
            Interactive = interactive,
            PreserveMerges = preserveMerges,
            AutoSquash = autoSquash,
            AutoStash = autoStash,
            IgnoreDate = ignoreDate,
            CommitterDateIsAuthorDate = committerDateIsAuthorDate,
            UpdateRefs = updateRefs,
            From = from,
            OnTo = onto
        };

        GitCommands.Git.Commands.Rebase(rebaseOptions).Arguments.Should().Be(expected);
    }

    [Fact]
    public void RemoveCmd()
    {
        // TODO file names should be quoted

        GitCommands.Git.Commands.Remove().Arguments.Should().Be("rm --force -r .");
        GitCommands.Git.Commands.Remove(false).Arguments.Should().Be("rm -r .");
        GitCommands.Git.Commands.Remove(isRecursive: false).Arguments.Should().Be("rm --force .");
        GitCommands.Git.Commands.Remove(files: ["a", "b", "c"]).Arguments.Should().Be("rm --force -r a b c");
    }

    [Fact]
    public void RenameBranch()
    {
        const string oldName = "foo";
        const string newName = "far";

        GitCommands.Git.Commands.RenameBranch(oldName, newName).Arguments.Should()
            .Be($"branch -m \"{oldName}\" \"{newName}\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void ResetCmd_should_throw_if_ResetIndex_and_hash_is_null_or_empty(string? hash)
    {
        ((Action)(() => GitCommands.Git.Commands.Reset(ResetMode.ResetIndex, hash, "file.txt"))).Should()
            .Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(ResetMode.ResetIndex, "tree-ish", null, @"reset --quiet ""tree-ish"" --")]
    [InlineData(ResetMode.ResetIndex, "tree-ish", "file.txt", @"reset --quiet ""tree-ish"" -- ""file.txt""")]
    [InlineData(ResetMode.Soft, null, null, @"reset --soft --quiet --")]
    [InlineData(ResetMode.Mixed, null, null, @"reset --mixed --quiet --")]
    [InlineData(ResetMode.Hard, null, null, @"reset --hard --quiet --")]
    [InlineData(ResetMode.Merge, null, null, @"reset --merge --quiet --")]
    [InlineData(ResetMode.Keep, null, null, @"reset --keep --quiet --")]
    [InlineData(ResetMode.Soft, "tree-ish", null, @"reset --soft --quiet ""tree-ish"" --")]
    [InlineData(ResetMode.Mixed, "tree-ish", null, @"reset --mixed --quiet ""tree-ish"" --")]
    [InlineData(ResetMode.Hard, "tree-ish", null, @"reset --hard --quiet ""tree-ish"" --")]
    [InlineData(ResetMode.Merge, "tree-ish", null, @"reset --merge --quiet ""tree-ish"" --")]
    [InlineData(ResetMode.Keep, "tree-ish", null, @"reset --keep --quiet ""tree-ish"" --")]
    [InlineData(ResetMode.Soft, null, "file.txt", @"reset --soft --quiet -- ""file.txt""")]
    [InlineData(ResetMode.Mixed, null, "file.txt", @"reset --mixed --quiet -- ""file.txt""")]
    [InlineData(ResetMode.Hard, null, "file.txt", @"reset --hard --quiet -- ""file.txt""")]
    [InlineData(ResetMode.Merge, null, "file.txt", @"reset --merge --quiet -- ""file.txt""")]
    [InlineData(ResetMode.Keep, null, "file.txt", @"reset --keep --quiet -- ""file.txt""")]
    [InlineData(ResetMode.Soft, "tree-ish", "file.txt", @"reset --soft --quiet ""tree-ish"" -- ""file.txt""")]
    [InlineData(ResetMode.Mixed, "tree-ish", "file.txt", @"reset --mixed --quiet ""tree-ish"" -- ""file.txt""")]
    [InlineData(ResetMode.Hard, "tree-ish", "file.txt", @"reset --hard --quiet ""tree-ish"" -- ""file.txt""")]
    [InlineData(ResetMode.Merge, "tree-ish", "file.txt", @"reset --merge --quiet ""tree-ish"" -- ""file.txt""")]
    [InlineData(ResetMode.Keep, "tree-ish", "file.txt", @"reset --keep --quiet ""tree-ish"" -- ""file.txt""")]
    public void ResetCmd(ResetMode mode, string? commit, string? file, string expected)
    {
        GitCommands.Git.Commands.Reset(mode, commit, file).Arguments.Should().Be(expected);
    }

    [Fact]
    public void RevertCmd()
    {
        var commitId = ObjectId.Random();

        GitCommands.Git.Commands.Revert(commitId, true, 0).Arguments.Should().Be($"revert {commitId}");

        GitCommands.Git.Commands.Revert(commitId, false, 0).Arguments.Should().Be($"revert --no-commit {commitId}");

        GitCommands.Git.Commands.Revert(commitId, true, 1).Arguments.Should().Be($"revert -m 1 {commitId}");
    }

    [Fact]
    public void StashSaveCmd()
    {
        // TODO test case where message string contains quotes
        // TODO test case where message string contains newlines
        // TODO test case where selectedFiles contains whitespaces (not currently quoted)

        GitCommands.Git.Commands.StashSave(false, false, null!, []).Arguments.Should().Be("stash save");

        GitCommands.Git.Commands.StashSave(false, false, null!, null).Arguments.Should().Be("stash save");

        GitCommands.Git.Commands.StashSave(true, false, null!, null).Arguments.Should().Be("stash save -u");

        GitCommands.Git.Commands.StashSave(false, true, null!, null).Arguments.Should().Be("stash save --keep-index");

        GitCommands.Git.Commands.StashSave(false, true, null!, null).Arguments.Should().Be("stash save --keep-index");

        GitCommands.Git.Commands.StashSave(false, false, "message", null).Arguments.Should()
            .Be("stash save \"message\"");

        GitCommands.Git.Commands.StashSave(false, false, null!, new[] { "a", "b" }).Arguments.Should()
            .Be("stash push -- \"a\" \"b\"");
    }

    [Fact]
    public void StashSave_should_add_message_if_provided_partial_stash()
    {
        GitCommands.Git.Commands.StashSave(false, false, "test message", new[] { "a", "b" }).Arguments.Should()
            .Be("stash push -m \"test message\" -- \"a\" \"b\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void StashSave_should_not_add_empty_message_partial_stash(string? theMessage)
    {
        GitCommands.Git.Commands.StashSave(false, false, theMessage!, new[] { "a", "b" }).Arguments.Should()
            .Be("stash push -- \"a\" \"b\"");
    }

    [Fact]
    public void StashSave_should_not_add_null_or_empty_filenames()
    {
        GitCommands.Git.Commands.StashSave(false, false, null!, new[] { null!, "", "a" }).Arguments.Should()
            .Be("stash push -- \"a\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void StashSaveCmd_should_not_add_empty_message_full_stash(string? theMessage)
    {
        GitCommands.Git.Commands.StashSave(false, false, theMessage!, []).Arguments.Should().Be("stash save");
    }

    [Fact]
    public void StashSaveCmd_should_add_message_if_provided_full_stash()
    {
        GitCommands.Git.Commands.StashSave(false, false, "test message", []).Arguments.Should()
            .Be("stash save \"test message\"");
    }

    [Fact]
    public void SubmoduleSyncCmd()
    {
        var config = "";
        GitCommands.Git.Commands.SubmoduleSync("foo").Arguments.Should().Be($"{config}submodule sync \"foo\"");
        GitCommands.Git.Commands.SubmoduleSync("").Arguments.Should().Be($"{config}submodule sync");
        GitCommands.Git.Commands.SubmoduleSync(null).Arguments.Should().Be($"{config}submodule sync");
    }

    [Theory]
    [InlineData("mybranch", "2111111111111111111111111111111111111111",
        @"update-ref ""mybranch"" 2111111111111111111111111111111111111111")]
    public void UpdateRef_should_return_expected_command_arguments(string gitRef, string hash, string expectedArguments)
    {
        GitCommands.Git.Commands.UpdateRef(gitRef, ObjectId.Parse(hash)).Arguments.Should().Be(expectedArguments);
    }
}
