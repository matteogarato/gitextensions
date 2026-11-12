using System.Diagnostics;
using System.Text;
using CommonTestUtils;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitModule = GitCommands.Git.GitModule;

namespace GitCommandsTests;

public sealed partial class GitModuleTests : IDisposable
{
    private static readonly ObjectId _sha1 = ObjectId.Parse("3183d1e95383c44302d4b25a7c647ee169765bd8");
    private static readonly ObjectId _sha2 = ObjectId.Parse("d12782217535ef00f4f84773d5d33691bbf81d00");
    private static readonly ObjectId _sha3 = ObjectId.Parse("dd678b7160a9a5890c8725e33930947af210c765");
    private readonly MockExecutable _executable = null!;

    private readonly GitModule _gitModule = null!;

    public GitModuleTests()
    {
        _executable = new MockExecutable();

        _gitModule = GetGitModuleWithExecutable(_executable);
    }

    public static IEnumerable<object?[]> StagedStatuses
    {
        get
        {
            var headObjectId = ObjectId.Random();

            yield return new object?[]
            {
                ObjectId.IndexId, ObjectId.WorkTreeId, ObjectId.IndexId, StagedStatus.WorkTree
            };

            yield return new object?[] { headObjectId, ObjectId.IndexId, headObjectId, StagedStatus.Index };

            yield return new object?[] { ObjectId.Random(), ObjectId.Random(), ObjectId.Random(), StagedStatus.None };
            yield return new object?[] { ObjectId.Random(), ObjectId.Random(), null, StagedStatus.None };

            // Situations where staged status is unknown
            yield return new object?[]
            {
                ObjectId.WorkTreeId, ObjectId.Random(), ObjectId.Random(), StagedStatus.Unknown
            };
            yield return new object?[]
            {
                ObjectId.Random(), ObjectId.WorkTreeId, ObjectId.IndexId, StagedStatus.Unknown
            };
            yield return new object?[] { ObjectId.IndexId, ObjectId.Random(), ObjectId.Random(), StagedStatus.Unknown };
            yield return new object?[] { ObjectId.Random(), ObjectId.IndexId, ObjectId.Random(), StagedStatus.Unknown };
            yield return new object?[] { ObjectId.IndexId, ObjectId.Random(), null, StagedStatus.Unknown };
            yield return new object?[] { ObjectId.Random(), ObjectId.IndexId, null, StagedStatus.Unknown };
            yield return new object?[] { ObjectId.Random(), null, ObjectId.Random(), StagedStatus.Unknown };
            yield return new object?[] { null, ObjectId.Random(), ObjectId.Random(), StagedStatus.Unknown };

            // Impossible combinations
            yield return new object?[]
            {
                ObjectId.Random(), ObjectId.WorkTreeId, ObjectId.Random(), StagedStatus.Unknown
            };
            yield return new object?[] { ObjectId.Random(), ObjectId.Random(), ObjectId.WorkTreeId, StagedStatus.None };
            yield return new object?[]
            {
                ObjectId.Random(), ObjectId.IndexId, ObjectId.WorkTreeId, StagedStatus.Unknown
            };
            yield return new object?[] { headObjectId, ObjectId.WorkTreeId, headObjectId, StagedStatus.Unknown };
            yield return new object?[] { ObjectId.Random(), ObjectId.Random(), ObjectId.IndexId, StagedStatus.None };
        }
    }

    public static IEnumerable<object?[]> BatchUnstageFilesTestCases
    {
        get
        {
            yield return new object?[]
            {
                new GitItemStatus[]
                {
                    new("abc2") { IsNew = true }, new("abc2") { IsNew = true, IsDeleted = true },
                    new("abc2") { IsNew = false },
                    new("abc3") { IsNew = false, IsRenamed = true, OldName = "def" }
                },
                new[]
                {
                    "reset --quiet \"HEAD\" -- \"abc2\" \"abc3\" \"def\"", "reset -- \"abc2\"",
                    "update-index --force-remove --stdin"
                },
                false
            };

            yield return new object?[]
            {
                new GitItemStatus[]
                {
                    new("abc2") { IsNew = false }, new("abc3") { IsNew = false, IsDeleted = true }
                },
                new[] { "reset --quiet \"HEAD\" -- \"abc2\" \"abc3\"" }, true
            };
        }
    }

    void IDisposable.Dispose()
    {
        _executable.Verify();
    }

    [Theory]
    [InlineData("file.cs", "LOCAL", 1234, "file_LOCAL_1234.cs")]
    [InlineData("dir/sub.dir/file.min.js", "BASE", 1, "dir/sub.dir/file.min_BASE_1.js")]
    [InlineData("dir.d/README", "REMOTE", 42, "dir.d/README_REMOTE_42")]
    [InlineData(".gitignore", "LOCAL", 7, "_LOCAL_7.gitignore")]
    public void GetConflictPartFileName_should_use_git_mergetool_scheme(string fileName, string side, int processId,
        string expected)
    {
        GitModule.TestAccessor.GetConflictPartFileName(fileName, side, processId).Should().Be(expected);
    }

    [Fact]
    public void GetAvailableFileName_should_insert_index_before_extension()
    {
        HashSet<string> existing = ["file_LOCAL_1234.cs", "file_LOCAL_1234_1.cs"];

        GitModule.TestAccessor.GetAvailableFileName("file_LOCAL_1234.cs", existing.Contains).Should()
            .Be("file_LOCAL_1234_2.cs");
        GitModule.TestAccessor.GetAvailableFileName("other_LOCAL_1234.cs", existing.Contains).Should()
            .Be("other_LOCAL_1234.cs");
        GitModule.TestAccessor.GetAvailableFileName("README_LOCAL_1234", _ => true).Should().Be("README_LOCAL_1234_49");
    }

    [Fact]
    public async Task CheckoutConflictedFiles_should_write_sides_next_to_file_with_extension_kept()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        using GitModuleTestHelper helper = new();
        var module = helper.Module;
        try
        {
            // base: file.cs exists; both branches change it; both branches add new.txt (no base)
            helper.CreateRepoFile("dir", "file.cs", "base");
            Run("add .");
            Run("commit -m base");
            var mainBranch = module.GetSelectedBranch();

            Run("checkout -b other");
            helper.CreateRepoFile("dir", "file.cs", "theirs");
            helper.CreateRepoFile("new.txt", "theirs");
            Run("add .");
            Run("commit -m theirs");

            Run($"checkout {mainBranch}");
            helper.CreateRepoFile("dir", "file.cs", "ours");
            helper.CreateRepoFile("new.txt", "ours");
            Run("add .");
            Run("commit -m ours");

            await module.GitExecutable.ExecuteAsync("merge other", throwOnErrorExit: false,
                cancellationToken: TestContext.Current.CancellationToken);

            var processId = Environment.ProcessId.ToString();

            var conflict = await module.GetConflictAsync("dir/file.cs");
            (var baseFile, var localFile, var remoteFile) = module.CheckoutConflictedFiles(conflict);

            var expectedBaseFile = Path.Combine(module.WorkingDir, $"dir/file_BASE_{processId}.cs");
            var expectedLocalFile = Path.Combine(module.WorkingDir, $"dir/file_LOCAL_{processId}.cs");
            var expectedRemoteFile = Path.Combine(module.WorkingDir, $"dir/file_REMOTE_{processId}.cs");
            baseFile.Should().Be(expectedBaseFile);
            localFile.Should().Be(expectedLocalFile);
            remoteFile.Should().Be(expectedRemoteFile);
            (await File.ReadAllTextAsync(expectedBaseFile, TestContext.Current.CancellationToken)).Should().Be("base");
            (await File.ReadAllTextAsync(expectedLocalFile, TestContext.Current.CancellationToken)).Should().Be("ours");
            (await File.ReadAllTextAsync(expectedRemoteFile, TestContext.Current.CancellationToken)).Should()
                .Be("theirs");

            // the files still exist, so a second checkout must not overwrite them
            var expectedSecondBaseFile = Path.Combine(module.WorkingDir, $"dir/file_BASE_{processId}_1.cs");
            (var baseFile2, _, _) = module.CheckoutConflictedFiles(conflict);
            baseFile2.Should().Be(expectedSecondBaseFile);
            (await File.ReadAllTextAsync(expectedSecondBaseFile, TestContext.Current.CancellationToken)).Should()
                .Be("base");

            // add/add conflict: no base side
            var addAddConflict = await module.GetConflictAsync("new.txt");
            (var noBaseFile, var addedLocalFile, _) = module.CheckoutConflictedFiles(addAddConflict);
            noBaseFile.Should().BeNull();
            addedLocalFile.Should().Be(Path.Combine(module.WorkingDir, $"new_LOCAL_{processId}.txt"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }

        return;

        void Run(string arguments)
        {
            module.GitExecutable.GetOutput(arguments);
        }
    }

    [Fact]
    public void ParseGitBlame()
    {
        using var gitVersion = _executable.StageOutput("--version", "git version 2.46.0");
        using var configList = _executable.StageOutput("config list --includes --null", null!);
        GitVersion.ResetVersion();

        var path = Path.Combine(AppContext.BaseDirectory, @"TestData/README.blame");
        var result = _gitModule.ParseGitBlame(File.ReadAllText(path), Encoding.UTF8);

        result.Lines.Count.Should().Be(80);

        result.Lines[0].Commit.ObjectId.Should().Be(ObjectId.Parse("957ff3ce9193fec3bd2578378e71676841804935"));
        result.Lines[0].Text.Should().Be("# Git Extensions");

        result.Lines[0].OriginLineNumber.Should().Be(1);
        result.Lines[0].FinalLineNumber.Should().Be(1);

        result.Lines[1].Commit.Should().BeSameAs(result.Lines[0].Commit);
        result.Lines[6].Commit.Should().BeSameAs(result.Lines[0].Commit);

        result.Lines[^1].Commit.ObjectId.Should().Be(ObjectId.Parse("e3268019c66da7534414e9562ececdee5d455b1b"));
        result.Lines[^1].Text.Should().Be("");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    [InlineData("Hello, World!", "Hello, World!")]
    [InlineData(@"\353\221\220\353\213\244.txt", "두다.txt")] // escaped octal code points (Korean Hangul in this case)
    [InlineData(@"Invalid byte \777.txt",
        @"Invalid byte \777.txt")] // 777 is an invalid byte, which is omitted from the output
    [InlineData(@"\353\221\220\353\213\244 \777.txt", @"두다 \777.txt")] // valid and invalid in the same string
    [InlineData(@"\353\221\220\353\213\244\777.txt",
        @"\353\221\220\353\213\244\777.txt")] // valid and invalid in the same string
    public void UnescapeOctalCodePoints_handles_octal_codes(string? input, string? expected)
    {
        GitModule.UnescapeOctalCodePoints(input).Should().Be(expected);
    }

    [Fact]
    public void UnescapeOctalCodePoints_returns_same_string_if_nothing_to_escape()
    {
        // If nothing was escaped in the original string, the same string instance is returned.
        const string s = "Hello, World!";

        GitModule.UnescapeOctalCodePoints(s).Should().BeSameAs(s);
    }

    [Fact]
    public void FetchCmd()
    {
        using var gitVersion = _executable.StageOutput("--version", "git version 2.46.0");
        using var configList = _executable.StageOutput("config list --includes --null", null!);
        GitVersion.ResetVersion();

        using (_executable.StageOutput("rev-parse --quiet --verify \"refs/heads/remotebranch~0\"", null!))
        {
            _gitModule.FetchCmd("remote", "remotebranch", "localbranch").Arguments.Should().Be(
                "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"remote\" +remotebranch:refs/heads/localbranch --no-tags");
        }

        using (_executable.StageOutput("rev-parse --quiet --verify \"refs/heads/remotebranch~0\"", null!))
        {
            _gitModule.FetchCmd("remote", "remotebranch", "localbranch", true).Arguments.Should().Be(
                "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"remote\" +remotebranch:refs/heads/localbranch --tags");
        }

        using (_executable.StageOutput("rev-parse --quiet --verify \"refs/heads/remotebranch~0\"", null!))
        {
            _gitModule.FetchCmd("remote", "remotebranch", "localbranch", null).Arguments.Should().Be(
                "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"remote\" +remotebranch:refs/heads/localbranch");
        }

        using (_executable.StageOutput("rev-parse --quiet --verify \"refs/heads/remotebranch~0\"", null!))
        {
            _gitModule.FetchCmd("remote", "remotebranch", "localbranch", isUnshallow: true).Arguments.Should().Be(
                "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"remote\" +remotebranch:refs/heads/localbranch --no-tags --unshallow");
        }

        using (_executable.StageOutput("rev-parse --quiet --verify \"refs/heads/remotebranch~0\"", null!))
        {
            _gitModule.FetchCmd("remote", "remotebranch", "localbranch", pruneRemoteBranches: true).Arguments.Should()
                .Be(
                    "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"remote\" +remotebranch:refs/heads/localbranch --no-tags --prune --force");
        }

        using (_executable.StageOutput("rev-parse --quiet --verify \"refs/heads/remotebranch~0\"", null!))
        {
            _gitModule
                .FetchCmd("remote", "remotebranch", "localbranch", pruneRemoteBranches: true,
                    pruneRemoteBranchesAndTags: true).Arguments.Should()
                .Be(
                    "-c fetch.parallel=0 -c submodule.fetchjobs=0 fetch --progress \"remote\" +remotebranch:refs/heads/localbranch --no-tags --prune --force --prune-tags");
        }
    }

    [Fact]
    public void ParseRefs()
    {
        _gitModule.ParseRefs("").Should().BeEmpty();
        _gitModule.ParseRefs("Foo").Should().BeEmpty();

        const string refList =
            "69a7c7a40230346778e7eebed809773a6bc45268 refs/heads/master\n" +
            "69a7c7a40230346778e7eebed809773a6bc45268 refs/remotes/origin/master\n" +
            "5303e7114f1896c639dea0231fac522752cc44a2\trefs/remotes/upstream/mono\n" +
            "366dfba1abf6cb98d2934455713f3d190df2ba34\trefs/tags/2.51\n";

        var refs = _gitModule.ParseRefs(refList);

        refs.Count.Should().Be(4);

        refs[0].Guid.Should().Be("69a7c7a40230346778e7eebed809773a6bc45268");
        refs[0].CompleteName.Should().Be("refs/heads/master");
        refs[0].LocalName.Should().Be("master");
        refs[0].Remote.Should().Be("");
        refs[0].IsHead.Should().BeTrue();
        refs[0].IsRemote.Should().BeFalse();
        refs[0].IsTag.Should().BeFalse();
        refs[0].Module.Should().BeSameAs(_gitModule);

        refs[1].Guid.Should().Be("69a7c7a40230346778e7eebed809773a6bc45268");
        refs[1].CompleteName.Should().Be("refs/remotes/origin/master");
        refs[1].LocalName.Should().Be("master");
        refs[1].Remote.Should().Be("origin");
        refs[1].IsHead.Should().BeFalse();
        refs[1].IsRemote.Should().BeTrue();
        refs[1].IsTag.Should().BeFalse();
        refs[1].Module.Should().BeSameAs(_gitModule);

        refs[2].Guid.Should().Be("5303e7114f1896c639dea0231fac522752cc44a2");
        refs[2].CompleteName.Should().Be("refs/remotes/upstream/mono");
        refs[2].LocalName.Should().Be("mono");
        refs[2].Remote.Should().Be("upstream");
        refs[2].IsHead.Should().BeFalse();
        refs[2].IsRemote.Should().BeTrue();
        refs[2].IsTag.Should().BeFalse();
        refs[2].Module.Should().BeSameAs(_gitModule);

        refs[3].Guid.Should().Be("366dfba1abf6cb98d2934455713f3d190df2ba34");
        refs[3].CompleteName.Should().Be("refs/tags/2.51");
        refs[3].LocalName.Should().Be("2.51");
        refs[3].Remote.Should().Be("");
        refs[3].IsHead.Should().BeFalse();
        refs[3].IsRemote.Should().BeFalse();
        refs[3].IsTag.Should().BeTrue();
        refs[3].Module.Should().BeSameAs(_gitModule);
    }

    [Theory]
    [InlineData("branch -a --contains",
        true,
        true,
        "  aaa\n* current\n+ feature/worktree\n  feature/zzz_another\n  remotes/origin/master\n  remotes/origin/current\n  remotes/upstream/master\n  a+b",
        new[]
        {
            "aaa", "current", "feature/worktree", "feature/zzz_another", "remotes/origin/master",
            "remotes/origin/current", "remotes/upstream/master", "a+b"
        })]
    [InlineData("branch --contains",
        true,
        false,
        "  aaa\n* current\n+ feature/worktree\n  feature/zzz_another\n",
        new[] { "aaa", "current", "feature/worktree", "feature/zzz_another" })]
    [InlineData("branch -r --contains",
        false,
        true,
        "remotes/origin/master\n  remotes/origin/current\n  remotes/upstream/master\n",
        new[] { "remotes/origin/master", "remotes/origin/current", "remotes/upstream/master" })]
    public void GetAllBranchesWhichContainGivenCommit_wellformed(
        string cmd,
        bool getLocal,
        bool getRemote,
        string output,
        string[] expected)
    {
        using (_executable.StageOutput(cmd + " " + _sha1, output))
        {
            var result = _gitModule.GetAllBranchesWhichContainGivenCommit(_sha1, getLocal, getRemote,
                cancellationToken: TestContext.Current.CancellationToken);
            result.Should().Equal(expected);
        }
    }

    [Theory]
    [InlineData(
        false,
        false,
        new string[] { })]
    public void GetAllBranchesWhichContainGivenCommit_empty(
        bool getLocal,
        bool getRemote,
        string[] expected)
    {
        var result = _gitModule.GetAllBranchesWhichContainGivenCommit(_sha1, getLocal, getRemote,
            cancellationToken: TestContext.Current.CancellationToken);
        result.Should().Equal(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void RevParse_should_return_zero_if_invalid(string? revisionExpression)
    {
        _gitModule.RevParse(revisionExpression).IsZero.Should().BeTrue();
    }

    [Fact]
    public void RevParse_should_return_zero_if_revisionExpression_exceeds_260_symbols()
    {
        string revisionExpression = new('a', 261);
        _gitModule.RevParse(revisionExpression).IsZero.Should().BeTrue();
    }

    [Fact]
    public void RevParse_should_return_ObjectId_if_revisionExpression_is_valid_hash()
    {
        string revisionExpression = new('1', ObjectId.Sha1CharCount);
        _gitModule.RevParse(revisionExpression).Should().Be(ObjectId.WorkTreeId);
    }

    [Fact]
    public void RevParse_should_query_git_and_return_ObjectId_if_get_valid_hash()
    {
        var revisionExpression = "11111";
        using (_executable.StageOutput($"rev-parse --quiet --verify \"{revisionExpression}~0\"",
                   new string('1', ObjectId.Sha1CharCount)))
        {
            _gitModule.RevParse(revisionExpression).Should().Be(ObjectId.WorkTreeId);
        }
    }

    [Fact]
    public void RevParse_should_query_git_and_return_zero_if_invalid_response()
    {
        var revisionExpression = "11111";
        using (_executable.StageOutput($"rev-parse --quiet --verify \"{revisionExpression}~0\"", "foo bar"))
        {
            _gitModule.RevParse(revisionExpression).IsZero.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("fatal: not a git repository:")]
    [InlineData("error: something went wrong")]
    [InlineData("HEAD")]
    [InlineData("master")]
    public void GetCurrentCheckout_should_query_git_and_return_zero_if_response_is_not_sha(string msg)
    {
        using (_executable.StageOutput("rev-parse HEAD", msg))
        {
            _gitModule.GetCurrentCheckout().IsZero.Should().BeTrue();
        }
    }

    [Fact]
    public void GetCurrentCheckout_should_query_git_and_return_sha_for_HEAD()
    {
        ObjectId objectId;
        var headId = "69a7c7a40230346778e7eebed809773a6bc45268";

        using (_executable.StageOutput("rev-parse HEAD", headId))
        {
            objectId = _gitModule.GetCurrentCheckout();
        }

        objectId.ToString().Should().Be(headId);
    }

    [Fact]
    public void GetParents_calls_correct_command_and_parses_response()
    {
        GitArgumentBuilder args = new("rev-parse") { $"{_sha1}^@".Quote() };

        using (_executable.StageOutput(
                   args.ToString(),
                   $"{_sha2}\n{_sha3}\n"))
        {
            var parents = _gitModule.GetParents(_sha1);

            parents.Should().Equal(_sha2, _sha3);
        }
    }

    [Theory]
    [InlineData(null, "reset --hard --quiet --")]
    [InlineData("file.txt", "reset --hard --quiet -- \"file.txt\"")]
    public void Reset_with_Hard_should_issue_correct_command_and_parse_response(string? file, string args)
    {
        using (_executable.StageCommand(args))
        {
            _gitModule.Reset(ResetMode.Hard, file);
        }
    }

    [Theory]
    [MemberData(nameof(StagedStatuses))]
    public void GetStagedStatus(ObjectId firstRevision, ObjectId secondRevision, ObjectId parentToSecond,
        StagedStatus status)
    {
        var stagedStatus =
            _gitModule.GetTestAccessor().GetStagedStatus(firstRevision, secondRevision, parentToSecond);
        stagedStatus.Should().Be(status);
    }

    [Fact]
    public void GetSubmodulesLocalPaths()
    {
        List<GitModuleTestHelper> moduleTestHelpers = [];
        try
        {
            const int numModules = 4;

            for (var i = 0; i < numModules; ++i)
            {
                moduleTestHelpers.Add(new GitModuleTestHelper($"repo{i}"));
                Debug.WriteLine($"Repo[{i}]:{moduleTestHelpers[i].TemporaryPath}");
            }

            foreach (var helper in moduleTestHelpers)
            {
                // Submodules require at least one commit
                helper.Module.GitExecutable.GetOutput(@"commit --allow-empty -m ""Initial commit""");
            }

            for (var i = numModules - 1; i > 0; --i)
            {
                var parent = moduleTestHelpers[i - 1];
                var child = moduleTestHelpers[i];

                // Add child as submodule of parent
                parent.AddSubmodule(child, $"repo{i}");
            }

            // Init all modules of root
            var root = moduleTestHelpers[0];
            var cfgs = Commands.GetAllowFileConfig();

            root.Module.GitExecutable.Execute(Commands.SubmoduleUpdate(name: null, cfgs),
                cancellationToken: TestContext.Current.CancellationToken);

            var paths = root.Module.GetSubmodulesLocalPaths(recursive: true);
            paths.Should().Equal(new[] { "repo1", "repo1/repo2", "repo1/repo2/repo3" },
                $"Modules: {string.Join(" ", paths)}");

            paths = root.Module.GetSubmodulesLocalPaths(recursive: false);
            paths.Should().Equal(new[] { "repo1" }, $"Modules: {string.Join(" ", paths)}");
        }
        finally
        {
            foreach (var helper in moduleTestHelpers)
            {
                helper.Dispose();
            }
        }
    }

    [Fact]
    public void Ignore_empty_gitmodules_path()
    {
        using GitModuleTestHelper helper = new();
        helper.CreateFile(helper.Module.WorkingDir, ".gitmodules", @"[submodule ""Externals/NBug""]
    path = Externals/NBug
    url = https://github.com/gitextensions/NBug.git
[submodule ""Externals/Git.hub""]
    url = https://github.com/gitextensions/Git.hub.gitk");
        helper.Module.GetSubmodulesLocalPaths().Should().Equal("Externals/NBug");
    }

    [Fact]
    public async Task GetSuperprojectCurrentCheckout()
    {
        // Create super and sub repo
        using GitModuleTestHelper moduleTestHelperSuper = new("super repo"),
            moduleTestHelperSub = new("sub repo");

        // Add and init the submodule
        moduleTestHelperSuper.AddSubmodule(moduleTestHelperSub, "sub repo");
        var moduleSub = moduleTestHelperSuper.GetSubmodulesRecursive().ElementAt(0);

        // Commit in submodule
        await moduleSub.GitExecutable.GetOutputAsync(@"commit --allow-empty -am ""First commit""");
        var commitRef = (await moduleSub.GitExecutable.GetOutputAsync("show HEAD")).LazySplit('\n').First()
            .LazySplit(' ').Skip(1).First();

        // Update ref in superproject
        await moduleTestHelperSuper.Module.GitExecutable.GetOutputAsync(@"add ""sub repo""");
        await moduleTestHelperSuper.Module.GitExecutable.GetOutputAsync(@"commit -am ""Update submodule ref""");

        (var code, var commitId) = await moduleSub.GetSuperprojectCurrentCheckoutAsync();
        code.Should().Be(' ');
        commitId.ToString().Should().Be(commitRef);
    }

    [Theory]
    [InlineData(false, @"stash list")]
    [InlineData(true, @"--no-optional-locks stash list")]
    public void GetStashesCmd(bool noLocks, string? expected)
    {
        _gitModule.GetStashesCmd(noLocks).ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("", "")] // empty message
    [InlineData("a\r\nb\r\n\r\nc\r\n\r\n\r\n\r\nd",
        "a\r\nb\r\n\r\nc\r\n\r\nd")] // various amount of new lines between text lines
    [InlineData("\r\n\r\n\r\n\r\na\r\nb\r\n\r\n\r\n\r\n\r\n\r\n", "a\r\nb")] // trimmable message
    [InlineData("a\n\n\nb\r\n\r\nc\n\nd", "a\r\n\r\nb\r\n\r\nc\r\n\r\nd")] // mix of new line types
    [InlineData("Hello, this is a single line message", "Hello, this is a single line message")] // single line message
    [InlineData("1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n13",
        "1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n7\r\n8\r\n9\r\n10\r\n11\r\n12\r\n13")] // message with more than 10 lines (a previous limitation)
    public void GetTagMessage(string tagMessage, string expectedReturnedMessage)
    {
        // add initial tag message
        using ReferenceRepository repo = new();
        repo.CreateAnnotatedTag("test_tag", repo.CommitHash!, tagMessage);

        // execute test look-up
        var actualReturnedMessage =
            repo.Module.GetTagMessage("test_tag", cancellationToken: TestContext.Current.CancellationToken);

        // compare result to expectations
        actualReturnedMessage.Should().Be(expectedReturnedMessage);
    }

    // TODO: add GetTagMessage "sad-path" tests, ones that test what happens if we try to execute it on a non-tag object.
    //       I expect we will need to do some refactoring in order to get such tests to pass (GetTagMessage is not robust at the moment)

    [Theory]
    [InlineData(false, @"-c core.safecrlf=false update-index --add --stdin")]
    [InlineData(true, @"update-index --add --stdin")]
    public void UpdateIndexCmd_should_add_core_safecrlf(bool showErrorsWhenStagingFiles, string? expected)
    {
        var accessor = _gitModule.GetTestAccessor();

        var actual = accessor.UpdateIndexCmd(showErrorsWhenStagingFiles).ToString();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("123", "567", "output.file", null)]
    [InlineData("123", "567", "output.file", 1)]
    [InlineData("123", "567", "output.file", 2)]
    public void Test_FormatPatch(string? from, string to, string outputFile, int? start)
    {
        StringBuilder arguments = new();
        arguments.Append("format-patch --find-renames --find-copies --break-rewrites");
        if (start is not null)
        {
            arguments.AppendFormat(" --start-number {0}", start);
        }

        arguments.AppendFormat(" \"{0}\"..\"{1}\" -o \"{2}\"", from, to, outputFile);

        var dummyCommandOutput = "The answer is 42. Just check that the Git arguments are as expected.";

        _executable.StageOutput(arguments.ToString(), dummyCommandOutput);
        _gitModule.FormatPatch(from!, to, outputFile, start).Should().Be(dummyCommandOutput);
    }

    [Theory]
    [InlineData("", "567", "output.file", null)]
    [InlineData("", "567", "output.file", 1)]
    [InlineData(null, "567", "output.file", 2)]
    public void Test_FormatPatchInRoot(string? from, string to, string outputFile, int? start)
    {
        StringBuilder arguments = new();
        arguments.Append("format-patch --find-renames --find-copies --break-rewrites");
        if (start is not null)
        {
            arguments.AppendFormat(" --start-number {0}", start);
        }

        arguments.AppendFormat(" --root \"{0}\" -o \"{1}\"", to, outputFile);

        var dummyCommandOutput = "The answer is 42. Just check that the Git arguments are as expected.";

        _executable.StageOutput(arguments.ToString(), dummyCommandOutput);
        _gitModule.FormatPatch(from!, to, outputFile, start).Should().Be(dummyCommandOutput);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(new string[] { }, "")]
    public void ResetFiles_should_handle_empty_list(string[]? files, string? expectedOutput)
    {
        _gitModule.CheckoutIndexFiles(files?.ToList()!).Should().Be(expectedOutput);
    }

    [Theory]
    [InlineData(new[] { "abc", "def" }, "checkout-index --index --force -- \"abc\" \"def\"")]
    public void ResetFiles_should_work_as_expected(string[] files, string args)
    {
        // Real GitModule is need to access AppSettings.GitCommand static property, avoid exception with dummy GitModule
        using GitModuleTestHelper moduleTestHelper = new();
        var gitModule = GetGitModuleWithExecutable(_executable, module: moduleTestHelper.Module);
        var dummyCommandOutput = "The answer is 42. Just check that the Git arguments are as expected.";
        _executable.StageOutput(args, dummyCommandOutput);
        var result = gitModule.CheckoutIndexFiles(files.ToList());
        result.Should().Be(dummyCommandOutput);
    }

    [Theory]
    [InlineData(new[] { "abc", "def" }, "rm -- \"abc\" \"def\"")]
    public void RemoveFiles_shouldWorkAsExpected(string[] files, string args)
    {
        // Real GitModule is need to access AppSettings.GitCommand static property, avoid exception with dummy GitModule
        using GitModuleTestHelper moduleTestHelper = new();
        var gitModule = GetGitModuleWithExecutable(_executable, module: moduleTestHelper.Module);
        var dummyCommandOutput = "The answer is 42. Just check that the Git arguments are as expected.";
        _executable.StageOutput(args, dummyCommandOutput);
        var result = gitModule.RemoveFiles(files.ToList(), false);
        result.Should().Be(dummyCommandOutput);
    }

    [Theory]
    [InlineData(new string[] { }, "")]
    public void RemoveFiles_should_handle_empty_list(string[] files, string expectedOutput)
    {
        _gitModule.RemoveFiles(files.ToList(), false).Should().Be(expectedOutput);
    }

    [Theory]
    [MemberData(nameof(BatchUnstageFilesTestCases))]
    public void BatchUnstageFiles_should_work_as_expected(GitItemStatus[] files, string[] args, bool expectedResult)
    {
        // Real GitModule is need to access AppSettings.GitCommand static property, avoid exception with dummy GitModule
        using GitModuleTestHelper moduleTestHelper = new();
        var gitModule = GetGitModuleWithExecutable(_executable, module: moduleTestHelper.Module);

        foreach (var arg in args)
        {
            _executable.StageCommand(arg);
        }

        var result = gitModule.BatchUnstageFiles(files);
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("refs/heads/feature/my-test-branch", "feature/my-test-branch")]
    public void GetSelectedBranch_should_return_simple_branch_name(string gitCommandResult, string expectedResult)
    {
        _executable.StageOutput("symbolic-ref --quiet HEAD", gitCommandResult);

        var result = _gitModule.GetSelectedBranch();

        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSelectedBranch_should_return_requested_result_in_detached_state(bool emptyIfDetached)
    {
        _executable.StageOutput("symbolic-ref --quiet HEAD", "", 1);

        var expectedResult = emptyIfDetached ? "" : DetachedHeadParser.DetachedBranch;
        var result = _gitModule.GetSelectedBranch(emptyIfDetached);

        result.Should().Be(expectedResult);
    }

    /// <summary>
    ///     Create a GitModule with mockable GitExecutable
    /// </summary>
    /// <param name="path">Path to the module</param>
    /// <param name="executable">The mock executable</param>
    /// <returns>The GitModule</returns>
    private static GitModule GetGitModuleWithExecutable(IExecutable executable, string path = "",
        GitModule module = null!)
    {
        module ??= new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), path);

        var executorAccessor = module.GetTestAccessor().Executor;
        executorAccessor.GitExecutable = executable;
        executorAccessor.GitWindowsExecutable = executable;
        executorAccessor.GitCommandRunner = new GitCommandRunner(executable, () => GitModule.SystemEncoding);

        return module;
    }
}
