using System.ComponentModel.Design;
using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.ScriptsEngine;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitUITests.Script;

[Culture("en-US")]
public class ScriptOptionsParserTests
{
    private IGitUICommands _commands = null!;
    private IGitModule _module = null!;
    private IScriptOptionsProvider _scriptOptionsProvider = null!;

    public ScriptOptionsParserTests()
    {
        _module = Substitute.For<IGitModule>();
        _scriptOptionsProvider = Substitute.For<IScriptOptionsProvider>();

        _commands = Substitute.For<IGitUICommands>();
        _commands.Module.Returns(_module);
    }

    [Fact]
    public void Contains_should_throw_if_arguments_null()
    {
        ((Action)(() => ScriptOptionsParser.Contains(null!, null!))).Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'arguments')");
    }

    [Fact]
    public void Contains_should_throw_if_option_null()
    {
        ((Action)(() => ScriptOptionsParser.Contains("", null!))).Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'option')");
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("", " ", true)]
    [InlineData(" ", "", true)]
    [InlineData(" ", " ", true)]
    [InlineData("{openUrl} https://gitlab.com{cDefaultRemotePathFromUrl}/tree/{sBranch}", "sBranch", true)]
    [InlineData("{openUrl} https://gitlab.com{cDefaultRemotePathFromUrl}/tree/{sBranch}", "zeBranch", false)]
    public void Contains_should_return_expected(string? arguments, string option, bool expected)
    {
        ScriptOptionsParser.Contains(arguments!, option).Should().Be(expected);
    }

    [Fact]
    public void DependsOnSelectedRevision_should_throw_if_option_null()
    {
        ((Action)(() => ScriptOptionsParser.DependsOnSelectedRevision(null!))).Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'option')");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("\t", false)]
    [InlineData("s", true)]
    [InlineData("sBranch", true)]
    [InlineData("zeBranch", false)]
    public void DependsOnSelectedRevision_should_return_expected(string option, bool expected)
    {
        ScriptOptionsParser.DependsOnSelectedRevision(option).Should().Be(expected);
    }

    [Theory]
    [InlineData("", false, "")]
    [InlineData("", true, "")]
    [InlineData(" ", false, "")]
    [InlineData(" ", true, "")]
    [InlineData("\t", false, "")]
    [InlineData("\t", true, "")]
    [InlineData("a", false, "{a}")]
    [InlineData("a", true, "{{a}}")]
    [InlineData(" a", false, "{a}")]
    [InlineData(" a", true, "{{a}}")]
    [InlineData("a ", false, "{a}")]
    [InlineData("a ", true, "{{a}}")]
    [InlineData(" a ", false, "{a}")]
    [InlineData(" a ", true, "{{a}}")]
    [InlineData(" a\ta ", false, "{a\ta}")]
    [InlineData(" a\ta ", true, "{{a\ta}}")]
    public void CreateOption_returns_expected(string option, bool quoted, string expected)
    {
        ScriptOptionsParser.GetTestAccessor().CreateOption(option, quoted).Should().Be(expected);
    }

    [Fact]
    public void GetCurrentRevision_should_return_null_if_bare_or_empty_repo()
    {
        // bare repo has no current checkout, empty repo has no commits
        _module.GetRevision(shortFormat: true, loadRefs: true).Returns(x => null!);

        GitRevision? result = ScriptOptionsParser.GetTestAccessor()
            .GetCurrentRevision(module: _module, currentTags: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentBranches: null!, loadBody: false);

        result.Should().Be(null);
    }

    [Fact]
    public void GetCurrentRevision_should_return_expected_if_current_revision_has_no_refs()
    {
        GitRevision revision = new(ObjectId.IndexId);
        _module.GetRevision(shortFormat: true, loadRefs: true).Returns(x => revision);

        GitRevision? result = ScriptOptionsParser.GetTestAccessor()
            .GetCurrentRevision(module: _module, currentTags: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentBranches: null!, loadBody: false);

        result.Should().Be(revision);
    }

    [Fact]
    public void Parse_should_throw_if_module_null()
    {
        ((Action)(() => ScriptOptionsParser.Parse(arguments: "bla", uiCommands: null!, owner: null!, _scriptOptionsProvider))).Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'uiCommands')");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Parse_should_return_without_process_if_arguments_unset(string? arguments)
    {
        (string? arguments, bool abort) result = ScriptOptionsParser.Parse(arguments, uiCommands: null!, owner: null!, _scriptOptionsProvider);

        result.arguments.Should().Be(arguments);
        result.abort.Should().Be(false);
    }

    [Fact]
    public void Parse_should_return_unmodified_arguments_if_no_options_matched()
    {
        const string arguments = "{openUrl} https://gitlab.com{zeDefaultRemotePathFromUrl}/tree/{zeBranch}";

        (string? arguments, bool abort) result = ScriptOptionsParser.Parse(arguments: arguments, _commands, owner: null!, _scriptOptionsProvider);

        result.arguments.Should().Be(arguments);
        result.abort.Should().Be(false);
    }

    [Fact]
    public void Parse_should_parse_c_arguments()
    {
        const string Subject = "line1";
        GitRevision revision = new(ObjectId.IndexId)
        {
            Subject = Subject,
            Body = $"{Subject}\n\nline3"
        };
        _module.GetRevision(shortFormat: false, loadRefs: true).Returns(x => revision);

        string expectedMessage = $"{Subject}\\n\\nline3";

        (string? arguments, bool abort) result = ScriptOptionsParser.Parse("echo {{cSubject}} {{cMessage}}", _commands, owner: null!, _scriptOptionsProvider);

        result.arguments.Should().Be($"echo \"{revision.Subject}\" \"{expectedMessage}\"");
        result.abort.Should().Be(false);
    }

    [Fact]
    public void Parse_should_parse_s_arguments()
    {
        const string Subject = "line1";
        GitRevision revision = new(ObjectId.IndexId)
        {
            Subject = Subject,
            Body = $"{Subject}\n\nline3"
        };
        IBrowseRepo browseRepo = Substitute.For<IBrowseRepo>();
        browseRepo.GetLatestSelectedRevision().Returns(x => revision);
        _commands.BrowseRepo.Returns(browseRepo);

        string expectedMessage = $"{Subject}\\n\\nline3";

        (string? arguments, bool abort) result = ScriptOptionsParser.Parse("echo {{sSubject}} {{sMessage}}", _commands, owner: null!, _scriptOptionsProvider);

        result.arguments.Should().Be($"echo \"{revision.Subject}\" \"{expectedMessage}\"");
        result.abort.Should().Be(false);
    }

    [Fact]
    public void ParseScriptArguments_resolves_cDefaultRemotePathFromUrl_currentRemote_unset()
    {
        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{openUrl} https://gitlab.com{cDefaultRemotePathFromUrl}/tree/{sBranch}", option: "cDefaultRemotePathFromUrl",
            owner: null!, _scriptOptionsProvider, uiCommands: null!, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote: null!);

        result.Should().Be("{openUrl} https://gitlab.com/tree/{sBranch}");
    }

    [Fact]
    public void ParseScriptArguments_resolve_cDefaultRemotePathFromUrl_currentRemote_set()
    {
        string currentRemote = "myRemote";
        _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, currentRemote)).Returns("https://gitlab.com/gitlabhq/gitlabhq.git");

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{openUrl} https://gitlab.com{cDefaultRemotePathFromUrl}/tree/{sBranch}", option: "cDefaultRemotePathFromUrl",
            owner: null!, _scriptOptionsProvider, _commands, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote);

        result.Should().Be("{openUrl} https://gitlab.com/gitlabhq/gitlabhq/tree/{sBranch}");
    }

    [Fact]
    public void ParseScriptArguments_resolve_sRemotePathFromUrl_selectedRemotes_empty()
    {
        List<string> noSelectedRemotes = [];

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{openUrl} https://gitlab.com{sRemotePathFromUrl}/tree/{sBranch}", option: "sRemotePathFromUrl",
            owner: null!, _scriptOptionsProvider, uiCommands: null!, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, noSelectedRemotes, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote: null!);

        result.Should().Be("{openUrl} https://gitlab.com/tree/{sBranch}");
    }

    [Fact]
    public void ParseScriptArguments_resolve_sRemotePathFromUrl_currentRemote_set()
    {
        string currentRemote = "myRemote";
        List<string> selectedRemotes = [currentRemote];
        _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, currentRemote)).Returns("https://gitlab.com/gitlabhq/gitlabhq.git");

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{openUrl} https://gitlab.com{sRemotePathFromUrl}/tree/{sBranch}", option: "sRemotePathFromUrl",
            owner: null!, _scriptOptionsProvider, _commands, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote: null!);

        result.Should().Be("{openUrl} https://gitlab.com/gitlabhq/gitlabhq/tree/{sBranch}");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "branch")]
    [InlineData("", "feature/branch")]
    [InlineData("origin", "")]
    [InlineData("origin", "branch")]
    [InlineData("origin", "feature/branch")]
    [InlineData("upstream", "")]
    [InlineData("upstream", "branch")]
    [InlineData("upstream", "feature/branch")]
    [InlineData("remote", "")]
    [InlineData("remote", "branch")]
    [InlineData("remote", "feature/branch")]

    public void ParseScriptArguments_resolve_sRemoteBranch(string remoteName, string branchName)
    {
        string option = "sRemoteBranch";
        string branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{" + option + "}", option,
            owner: null!, _scriptOptionsProvider, uiCommands: null!, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: remoteBranches, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote: null!);

        result.Should().Be(branch);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "branch")]
    [InlineData("", "feature/branch")]
    [InlineData("origin", "")]
    [InlineData("origin", "branch")]
    [InlineData("origin", "feature/branch")]
    [InlineData("upstream", "")]
    [InlineData("upstream", "branch")]
    [InlineData("upstream", "feature/branch")]
    [InlineData("remote", "")]
    [InlineData("remote", "branch")]
    [InlineData("remote", "feature/branch")]

    public void ParseScriptArguments_resolve_sRemoteBranchName(string remoteName, string branchName)
    {
        string option = "sRemoteBranchName";
        string branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{" + option + "}", option,
            owner: null!, _scriptOptionsProvider, uiCommands: null!, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: remoteBranches, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote: null!);

        result.Should().Be(branchName);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "branch")]
    [InlineData("", "feature/branch")]
    [InlineData("origin", "")]
    [InlineData("origin", "branch")]
    [InlineData("origin", "feature/branch")]
    [InlineData("upstream", "")]
    [InlineData("upstream", "branch")]
    [InlineData("upstream", "feature/branch")]
    [InlineData("remote", "")]
    [InlineData("remote", "branch")]
    [InlineData("remote", "feature/branch")]

    public void ParseScriptArguments_resolve_cRemoteBranch(string remoteName, string branchName)
    {
        string option = "cRemoteBranch";
        string branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{" + option + "}", option,
            owner: null!, _scriptOptionsProvider, uiCommands: null!, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: remoteBranches, currentRevision: null!, currentRemote: null!);

        result.Should().Be(branch);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "branch")]
    [InlineData("", "feature/branch")]
    [InlineData("origin", "")]
    [InlineData("origin", "branch")]
    [InlineData("origin", "feature/branch")]
    [InlineData("upstream", "")]
    [InlineData("upstream", "branch")]
    [InlineData("upstream", "feature/branch")]
    [InlineData("remote", "")]
    [InlineData("remote", "branch")]
    [InlineData("remote", "feature/branch")]

    public void ParseScriptArguments_resolve_cRemoteBranchName(string remoteName, string branchName)
    {
        string option = "cRemoteBranchName";
        string branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{" + option + "}", option,
            owner: null!, _scriptOptionsProvider, uiCommands: null!, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: remoteBranches, currentRevision: null!, currentRemote: null!);

        result.Should().Be(branchName);
    }

    [Fact]
    public void ParseScriptArguments_resolve_RepoName()
    {
        const string option = "RepoName";
        const string dirName = "Windows"; // chose one which will never contain a repo

        IRepositoryDescriptionProvider repositoryDescriptionProvider = Substitute.For<IRepositoryDescriptionProvider>();
        repositoryDescriptionProvider.Get(Arg.Any<string>()).Returns(dirName);

        ServiceContainer serviceContainer = new();
        serviceContainer.AddService(repositoryDescriptionProvider);

        _commands = new GitUICommands(serviceContainer, new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""));

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{" + option + "}", option,
            owner: null!, _scriptOptionsProvider, _commands, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote: null!);

        result.Should().Be(dirName);
    }

    [Fact]
    public void ParseScriptArguments_resolve_HEAD_with_checkedout_branch_name()
    {
        string option = "HEAD";
        string branchName = "this_is_my_branch_name";
        _module.GetSelectedBranch(emptyIfDetached: true).Returns(branchName);

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{" + option + "}", option,
            owner: null!, _scriptOptionsProvider, uiCommands: _commands, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!,
            currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!, currentRevision: null!, currentRemote: null!);

        result.Should().Be(branchName);
    }

    [Fact]
    public void ParseScriptArguments_resolve_HEAD_with_detached_head_hash()
    {
        string option = "HEAD";
        string detachedHeadHash = "d54e5cf78a403d5ace3299549be0f6cabee50a63";
        _module.GetSelectedBranch(emptyIfDetached: true).Returns(string.Empty);

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            arguments: "{" + option + "}", option,
            owner: null!, _scriptOptionsProvider, uiCommands: _commands, allSelectedRevisions: null!, selectedTags: null!,
            selectedBranches: null!, selectedLocalBranches: null!, selectedRemoteBranches: null!, selectedRemotes: null!, selectedRevision: null!,
            currentTags: null!, currentBranches: null!, currentLocalBranches: null!, currentRemoteBranches: null!,
            currentRevision: new GitRevision(ObjectId.Parse(detachedHeadHash)), currentRemote: null!);

        result.Should().Be(detachedHeadHash);
    }

    [Fact]
    public void ParseScriptArguments_resolve_QuotedWithBackslashAtEnd()
    {
        _module.WorkingDir.Returns("C:\\test path with whitespaces\\");

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments("{{WorkingDir}} \"{WorkingDir}\"", "WorkingDir", null!, null!, _commands, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        result.Should().Be("\"C:\\test path with whitespaces\\\\\" \"C:\\test path with whitespaces\\\"");
    }

    [Fact]
    public void ParseScriptArguments_resolve_StringWithDoubleQuotes()
    {
        GitRevision gitRevision = new(ObjectId.Random())
        {
            Subject = "test string with \"double quotes\" and escaped \\\"double quotes\\\""
        };

        string? result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments("{{sMessage}}", "sMessage", null!, null!, null!, null!, null!, null!, null!, null!, null!, gitRevision, null!, null!, null!, null!, null!, null!);
        result.Should().Be("\"test string with \\\"double quotes\\\" and escaped \\\"double quotes\\\"\"");

        result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments("{sMessage}", "sMessage", null!, null!, null!, null!, null!, null!, null!, null!, null!, gitRevision, null!, null!, null!, null!, null!, null!);
        result.Should().Be("test string with \"double quotes\" and escaped \\\"double quotes\\\"");
    }

    [Fact]
    public void Parse_should_resolve_IScriptOptionsProvider_options()
    {
        const string option1 = nameof(option1);
        const string option2 = nameof(option2);
        const string value1a = nameof(value1a);
        const string value1b = nameof(value1b);
        const string value2 = nameof(value2);
        _scriptOptionsProvider.Options.Returns(new string[] { option1, option2 });
        _scriptOptionsProvider.GetValues(option1).Returns([value1a, value1b]);
        _scriptOptionsProvider.GetValues(option2).Returns([value2]);

        (string? arguments, bool abort) = ScriptOptionsParser.Parse("foo {{" + option1 + "}} bar {" + option2 + "}", Substitute.For<IGitUICommands>(), owner: null!, _scriptOptionsProvider);

        arguments.Should().Be($"foo \"{value1a}\" \"{value1b}\" bar {value2}");
        abort.Should().BeFalse();
    }
}
