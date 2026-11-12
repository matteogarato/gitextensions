using System.ComponentModel.Design;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using NSubstitute;
using ResourceManager;
using UITests;
using AppSettings = GitCommands.Settings.AppSettings;
using GitModule = GitCommands.Git.GitModule;

namespace GitExtensions.UITests.UserControls.CommitInfo;

public class CommitInfoTests : IDisposable
{
    private readonly MockLinkFactory _mockLinkFactory = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;
    private GitUiCommands _commands = null!;

    // Created once for each test
    private MockExecutable _gitExecutable = null!;

    public CommitInfoTests()
    {
        _mockLinkFactory = new MockLinkFactory();
        var serviceContainer = GlobalServiceContainer.CreateDefaultMockServiceContainer();
        serviceContainer.RemoveService<ILinkFactory>();
        serviceContainer.AddService<ILinkFactory>(_mockLinkFactory);

        AppSettings.ShowGitNotes = false;
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUiCommands(serviceContainer, _referenceRepository.Module);

        // mock git executable
        _gitExecutable = new MockExecutable();
        var executorAccessor = _referenceRepository.Module.GetTestAccessor().Executor;
        executorAccessor.GitExecutable = _gitExecutable;
        executorAccessor.GitWindowsExecutable = _gitExecutable;
        executorAccessor.GitCommandRunner = new GitCommandRunner(_gitExecutable, () => GitModule.SystemEncoding);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _gitExecutable.Verify();
        _gitExecutable = null!;
        _commands = null!;
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void GetSortedTags_should_throw_on_git_warning()
    {
        RunCommitInfoTest(commitInfo =>
        {
            _gitExecutable.StageOutput(@"for-each-ref --sort=""-taggerdate"" --format=""%(refname)"" refs/tags/",
                "refs/heads/master\nwarning: message");

            ((Action)(() => commitInfo.GetTestAccessor().GetSortedTags())).Should().Throw<RefsWarningException>();

            return Task.CompletedTask;
        });
    }

    [WinFormsFact]
    public void GetSortedTags_should_split_output_if_no_warning()
    {
        RunCommitInfoTest(commitInfo =>
        {
            _gitExecutable.StageOutput(@"for-each-ref --sort=""-taggerdate"" --format=""%(refname)"" refs/tags/",
                "refs/remotes/origin/master\nrefs/heads/master\nrefs/heads/warning"); // does not contain "warning:"

            Dictionary<string, int> expected = new()
            {
                ["refs/remotes/origin/master"] = 0, ["refs/heads/master"] = 1, ["refs/heads/warning"] = 2
            };

            var refs = commitInfo.GetTestAccessor().GetSortedTags();

            refs.Should().HaveCount(3);
            refs.Should().BeEquivalentTo(expected);

            return Task.CompletedTask;
        });
    }

    [WinFormsFact]
    public void GetSortedTags_should_load_ref_different_in_case()
    {
        RunCommitInfoTest(commitInfo =>
        {
            _gitExecutable.StageOutput(@"for-each-ref --sort=""-taggerdate"" --format=""%(refname)"" refs/tags/",
                "refs/remotes/origin/master\nrefs/heads/master\nrefs/remotes/origin/bugfix/YS-38651-test-twist-changes-r100-on-s375\nrefs/remotes/origin/bugfix/ys-38651-test-twist-changes-r100-on-s375"); // case sensitive duplicates

            Dictionary<string, int> expected = new()
            {
                ["refs/remotes/origin/master"] = 0,
                ["refs/heads/master"] = 1,
                ["refs/remotes/origin/bugfix/YS-38651-test-twist-changes-r100-on-s375"] = 2,
                ["refs/remotes/origin/bugfix/ys-38651-test-twist-changes-r100-on-s375"] = 3
            };

            var refs = commitInfo.GetTestAccessor().GetSortedTags();

            refs.Should().HaveCount(4);
            refs.Should().BeEquivalentTo(expected);

            return Task.CompletedTask;
        });
    }

    [WinFormsFact]
    public void GetSortedTags_should_load_ref_with_extra_spaces()
    {
        RunCommitInfoTest(commitInfo =>
        {
            _gitExecutable.StageOutput(@"for-each-ref --sort=""-taggerdate"" --format=""%(refname)"" refs/tags/",
                "refs/remotes/origin/master\nrefs/heads/master\nrefs/tags/v3.1\nrefs/tags/v3.1 \n refs/tags/v3.1"); // have leading and trailing spaces

            Dictionary<string, int> expected = new()
            {
                ["refs/remotes/origin/master"] = 0,
                ["refs/heads/master"] = 1,
                ["refs/tags/v3.1"] = 2,
                ["refs/tags/v3.1 "] = 3,
                [" refs/tags/v3.1"] = 4
            };

            var refs = commitInfo.GetTestAccessor().GetSortedTags();

            refs.Should().HaveCount(5);
            refs.Should().BeEquivalentTo(expected);

            return Task.CompletedTask;
        });
    }

    [WinFormsFact]
    public void GetSortedTags_should_remove_duplicate_refs()
    {
        RunCommitInfoTest(commitInfo =>
        {
            _gitExecutable.StageOutput(@"for-each-ref --sort=""-taggerdate"" --format=""%(refname)"" refs/tags/",
                "refs/remotes/origin/master\nrefs/remotes/foo/duplicate\nrefs/remotes/foo/bar\nrefs/remotes/foo/duplicate\nrefs/remotes/foo/last"); // exact duplicates

            Dictionary<string, int> expected = new()
            {
                ["refs/remotes/origin/master"] = 0,
                ["refs/remotes/foo/duplicate"] = 1,
                ["refs/remotes/foo/bar"] = 2,
                ["refs/remotes/foo/last"] = 3
            };

            var refs = commitInfo.GetTestAccessor().GetSortedTags();

            refs.Should().HaveCount(4);
            refs.Should().BeEquivalentTo(expected);

            return Task.CompletedTask;
        });
    }

    [WinFormsFact]
    public void ReloadCommitInfo_should_render_links_correctly()
    {
        var hash = "a48da1aba59a65b2a7f0df7e3512817caf16819f";

        _gitExecutable.StageOutput("rev-parse --git-common-dir", ".git");

        // Generate branches: branch01...branch15
        _gitExecutable.StageOutput($"branch --contains {hash}",
            string.Join('\n', Enumerable.Range(1, 15).Select(i => $"branch{i:00}")));

        // Generate tags: v1.0...v1.15
        _gitExecutable.StageOutput($"tag --contains {hash}",
            string.Join('\n', Enumerable.Range(0, 15).Select(i => $"v1.{i}")));

        _gitExecutable.StageOutput($"describe --tags --first-parent --abbrev=40 {hash}", "");

        var realCommitObjectId = ObjectId.Parse(hash);
        GitRevision revision = new(realCommitObjectId)
        {
            Author = "John Doe",
            AuthorUnixTime = DateTimeUtils.ToUnixTime(DateTime.Parse("2010-03-24 13:37:12")),
            AuthorEmail = "j.doe@some.email.dotcom",
            Subject = "fix: bugs",
            Body = "fix: bugs\r\n\r\nall bugs fixed"
        };

        RunCommitInfoTest(async commitInfo =>
        {
            commitInfo.SetRevisionWithChildren(revision, null);

            // Wait for pending operations so the Control is loaded completely before testing it
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            await Verify(commitInfo.GetTestAccessor().RevisionInfo.Text);
        });
    }

    // Link start numbers are obtained manually.
    [WinFormsTheory]
    [InlineData("branch05", 183, "gitext://gotobranch/branch05")]
    [InlineData("v1.9", 745, "gitext://gototag/v1.9")]
    public void ReloadCommitInfo_should_extract_links_correctly(string refText, int linkStart, string expectedUri)
    {
        var hash = "a48da1aba59a65b2a7f0df7e3512817caf16819f";
        var hashInBody = "a48da1aba59a65b2a7f0df7e3512817caf16819a";
        var hashLink = $"gitext://gotocommit/{hashInBody}";

        _gitExecutable.StageOutput("rev-parse --git-common-dir", ".git");

        // Generate branches: branch01...branch15
        _gitExecutable.StageOutput($"branch --contains {hash}",
            string.Join('\n', Enumerable.Range(1, 15).Select(i => $"branch{i:00}")));

        // Generate tags: v1.0...v1.15
        _gitExecutable.StageOutput($"tag --contains {hash}",
            string.Join('\n', Enumerable.Range(0, 15).Select(i => $"v1.{i}")));

        _gitExecutable.StageOutput($"describe --tags --first-parent --abbrev=40 {hash}", "");

        var realCommitObjectId = ObjectId.Parse(hash);
        GitRevision revision = new(realCommitObjectId)
        {
            Author = "John Doe",
            AuthorUnixTime = DateTimeUtils.ToUnixTime(DateTime.Parse("2010-03-24 13:37:12")),
            AuthorEmail = "j.doe@some.email.dotcom",
            Subject = "fix: bugs",
            Body = $"fix: bugs\r\n\r\nall bugs from {hashInBody} fixed"
        };

        RunCommitInfoTest(async commitInfo =>
        {
            object? commandClickedSender = null;
            commitInfo.CommandClicked += (s, e) => commandClickedSender = s;
            commitInfo.SetRevisionWithChildren(revision, null);

            // Wait for pending operations so the Control is loaded completely before testing it
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            var ta = commitInfo.GetTestAccessor();

            // simulate a click on refText link
            ta.LinkClicked(ta.RevisionInfo, new LinkClickedEventArgs(refText, linkStart, refText.Length));
            _mockLinkFactory.LastExecutedLinkUri.Should().Be(expectedUri);

            // simulate a click on hash link
            ta.LinkClicked(ta.CommitMessage, new LinkClickedEventArgs(hashInBody, 25, hashInBody.Length));
            _mockLinkFactory.LastExecutedLinkUri.Should().Be(hashLink);
            commandClickedSender.Should().Be(ta.CommitMessage);
        });
    }

    [WinFormsFact]
    public void ReloadCommitInfo_should_handle_ShowAll_branches_correctly()
    {
        var hash = "a48da1aba59a65b2a7f0df7e3512817caf16819f";

        _gitExecutable.StageOutput("rev-parse --git-common-dir", ".git");

        // Generate branches: branch01...branch15
        _gitExecutable.StageOutput($"branch --contains {hash}",
            string.Join('\n', Enumerable.Range(1, 15).Select(i => $"branch{i:00}")));

        // Generate tags: v1.0...v1.15
        _gitExecutable.StageOutput($"tag --contains {hash}",
            string.Join('\n', Enumerable.Range(0, 15).Select(i => $"v1.{i}")));

        _gitExecutable.StageOutput($"describe --tags --first-parent --abbrev=40 {hash}", "");

        var realCommitObjectId = ObjectId.Parse(hash);
        GitRevision revision = new(realCommitObjectId)
        {
            Author = "John Doe",
            AuthorUnixTime = DateTimeUtils.ToUnixTime(DateTime.Parse("2010-03-24 13:37:12")),
            AuthorEmail = "j.doe@some.email.dotcom",
            Subject = "fix: bugs",
            Body = "fix: bugs\r\n\r\nall bugs fixed"
        };

        RunCommitInfoTest(async commitInfo =>
        {
            commitInfo.SetRevisionWithChildren(revision, null);

            // Wait for pending operations so the Control is loaded completely before testing it
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            // simulate a click on refText link
            var ta = commitInfo.GetTestAccessor();
            ta.LinkClicked(ta.RevisionInfo, new LinkClickedEventArgs("not important", 423, 0));
            _mockLinkFactory.LastExecutedLinkUri.Should().Be("gitext://showall/branches");

            await Verify(commitInfo.GetTestAccessor().RevisionInfo.Text);
        });
    }

    [WinFormsFact]
    public void ReloadCommitInfo_should_handle_ShowAll_tags_correctly()
    {
        var hash = "a48da1aba59a65b2a7f0df7e3512817caf16819f";

        _gitExecutable.StageOutput("rev-parse --git-common-dir", ".git");

        // Generate branches: branch01...branch15
        _gitExecutable.StageOutput($"branch --contains {hash}",
            string.Join('\n', Enumerable.Range(1, 15).Select(i => $"branch{i:00}")));

        // Generate tags: v1.0...v1.15
        _gitExecutable.StageOutput($"tag --contains {hash}",
            string.Join('\n', Enumerable.Range(0, 15).Select(i => $"v1.{i}")));

        _gitExecutable.StageOutput($"describe --tags --first-parent --abbrev=40 {hash}", "");

        var realCommitObjectId = ObjectId.Parse(hash);
        GitRevision revision = new(realCommitObjectId)
        {
            Author = "John Doe",
            AuthorUnixTime = DateTimeUtils.ToUnixTime(DateTime.Parse("2010-03-24 13:37:12")),
            AuthorEmail = "j.doe@some.email.dotcom",
            Subject = "fix: bugs",
            Body = "fix: bugs\r\n\r\nall bugs fixed"
        };

        RunCommitInfoTest(async commitInfo =>
        {
            commitInfo.SetRevisionWithChildren(revision, null);

            // Wait for pending operations so the Control is loaded completely before testing it
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            // simulate a click on refText link
            var ta = commitInfo.GetTestAccessor();
            ta.LinkClicked(ta.RevisionInfo, new LinkClickedEventArgs("not important", 774, 0));
            _mockLinkFactory.LastExecutedLinkUri.Should().Be("gitext://showall/tags");

            await Verify(commitInfo.GetTestAccessor().RevisionInfo.Text);
        });
    }

    private void RunCommitInfoTest(Func<GitUI.CommitInfo.CommitInfo, Task> runTestAsync)
    {
        UiTest.RunControl(
            form =>
            {
                var uiCommandsSource = Substitute.For<IGitUiCommandsSource>();
                uiCommandsSource.UiCommands.Returns(x => _commands);

                // the following assignment of CommitInfo.UICommandsSource will already call this command
                _gitExecutable.StageOutput(@"for-each-ref --sort=""-taggerdate"" --format=""%(refname)"" refs/tags/",
                    "");

                form.Size = new Size(600, 480);

                return new GitUI.CommitInfo.CommitInfo
                {
                    Dock = DockStyle.Fill,
                    Parent = form,
                    ShowBranchesAsLinks = true,
                    UiCommandsSource = uiCommandsSource
                };
            },
            async commitInfo =>
            {
                // Wait for pending operations so the Control is loaded completely before testing it
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

                await runTestAsync(commitInfo);
            });
    }

    private class MockLinkFactory : ILinkFactory
    {
        private readonly ILinkFactory _linkFactory = new LinkFactory();

        public string? LastExecutedLinkUri { get; private set; }

        string ILinkFactory.CreateBranchLink(string noPrefixBranch)
        {
            return _linkFactory.CreateBranchLink(noPrefixBranch);
        }

        string ILinkFactory.CreateCommitLink(ObjectId objectId, string? linkText = null,
            bool preserveGuidInLinkText = false)
        {
            return _linkFactory.CreateCommitLink(objectId, linkText, preserveGuidInLinkText);
        }

        string ILinkFactory.CreateLink(string? caption, string uri)
        {
            return _linkFactory.CreateLink(caption, uri);
        }

        string ILinkFactory.CreateShowAllLink(string what)
        {
            return _linkFactory.CreateShowAllLink(what);
        }

        string ILinkFactory.CreateTagLink(string tag)
        {
            return _linkFactory.CreateTagLink(tag);
        }

        void ILinkFactory.ExecuteLink(string? linkUri, Action<CommandEventArgs>? handleInternalLink = null,
            Action<string?>? showAll = null)
        {
            LastExecutedLinkUri = linkUri;

            _linkFactory.ExecuteLink(linkUri, handleInternalLink, showAll);
        }
    }
}
