using System.ComponentModel.Design;
using GitCommands.Config;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.ScriptsEngine;
using NSubstitute;
using GitModule = GitCommands.Git.GitModule;

namespace GitUITests.Script;

[Culture("en-US")]
public class ScriptOptionsParserTests
{
    private readonly IGitModule _module = null!;
    private readonly IScriptOptionsProvider _scriptOptionsProvider = null!;
    private IGitUiCommands _commands = null!;

    public ScriptOptionsParserTests()
    {
        _module = Substitute.For<IGitModule>();
        _scriptOptionsProvider = Substitute.For<IScriptOptionsProvider>();

        _commands = Substitute.For<IGitUiCommands>();
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

        var result = ScriptOptionsParser.GetTestAccessor()
            .GetCurrentRevision(_module, null!, null!, null!, null!, false);

        result.Should().Be(null);
    }

    [Fact]
    public void GetCurrentRevision_should_return_expected_if_current_revision_has_no_refs()
    {
        GitRevision revision = new(ObjectId.IndexId);
        _module.GetRevision(shortFormat: true, loadRefs: true).Returns(x => revision);

        var result = ScriptOptionsParser.GetTestAccessor()
            .GetCurrentRevision(_module, null!, null!, null!, null!, false);

        result.Should().Be(revision);
    }

    [Fact]
    public void Parse_should_throw_if_module_null()
    {
        ((Action)(() => ScriptOptionsParser.Parse("bla", null!, null!, _scriptOptionsProvider))).Should()
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
        var result =
            ScriptOptionsParser.Parse(arguments, null!, null!, _scriptOptionsProvider);

        result.arguments.Should().Be(arguments);
        result.abort.Should().Be(false);
    }

    [Fact]
    public void Parse_should_return_unmodified_arguments_if_no_options_matched()
    {
        const string arguments = "{openUrl} https://gitlab.com{zeDefaultRemotePathFromUrl}/tree/{zeBranch}";

        var result =
            ScriptOptionsParser.Parse(arguments, _commands, null!, _scriptOptionsProvider);

        result.arguments.Should().Be(arguments);
        result.abort.Should().Be(false);
    }

    [Fact]
    public void Parse_should_parse_c_arguments()
    {
        const string subject = "line1";
        GitRevision revision = new(ObjectId.IndexId) { Subject = subject, Body = $"{subject}\n\nline3" };
        _module.GetRevision(shortFormat: false, loadRefs: true).Returns(x => revision);

        var expectedMessage = $"{subject}\\n\\nline3";

        var result = ScriptOptionsParser.Parse("echo {{cSubject}} {{cMessage}}", _commands,
            null!, _scriptOptionsProvider);

        result.arguments.Should().Be($"echo \"{revision.Subject}\" \"{expectedMessage}\"");
        result.abort.Should().Be(false);
    }

    [Fact]
    public void Parse_should_parse_s_arguments()
    {
        const string subject = "line1";
        GitRevision revision = new(ObjectId.IndexId) { Subject = subject, Body = $"{subject}\n\nline3" };
        var browseRepo = Substitute.For<IBrowseRepo>();
        browseRepo.GetLatestSelectedRevision().Returns(x => revision);
        _commands.BrowseRepo.Returns(browseRepo);

        var expectedMessage = $"{subject}\\n\\nline3";

        var result = ScriptOptionsParser.Parse("echo {{sSubject}} {{sMessage}}", _commands,
            null!, _scriptOptionsProvider);

        result.arguments.Should().Be($"echo \"{revision.Subject}\" \"{expectedMessage}\"");
        result.abort.Should().Be(false);
    }

    [Fact]
    public void ParseScriptArguments_resolves_cDefaultRemotePathFromUrl_currentRemote_unset()
    {
        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{openUrl} https://gitlab.com{cDefaultRemotePathFromUrl}/tree/{sBranch}", "cDefaultRemotePathFromUrl",
            null!, _scriptOptionsProvider, null!, null!, null!,
            null!, null!, null!, null!, null!,
            null!,
            null!, null!, null!, null!, null!);

        result.Should().Be("{openUrl} https://gitlab.com/tree/{sBranch}");
    }

    [Fact]
    public void ParseScriptArguments_resolve_cDefaultRemotePathFromUrl_currentRemote_set()
    {
        var currentRemote = "myRemote";
        _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, currentRemote))
            .Returns("https://gitlab.com/gitlabhq/gitlabhq.git");

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{openUrl} https://gitlab.com{cDefaultRemotePathFromUrl}/tree/{sBranch}", "cDefaultRemotePathFromUrl",
            null!, _scriptOptionsProvider, _commands, null!, null!,
            null!, null!, null!, null!, null!,
            null!,
            null!, null!, null!, null!, currentRemote);

        result.Should().Be("{openUrl} https://gitlab.com/gitlabhq/gitlabhq/tree/{sBranch}");
    }

    [Fact]
    public void ParseScriptArguments_resolve_sRemotePathFromUrl_selectedRemotes_empty()
    {
        List<string> noSelectedRemotes = [];

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{openUrl} https://gitlab.com{sRemotePathFromUrl}/tree/{sBranch}", "sRemotePathFromUrl",
            null!, _scriptOptionsProvider, null!, null!, null!,
            null!, null!, null!, noSelectedRemotes, null!,
            null!,
            null!, null!, null!, null!, null!);

        result.Should().Be("{openUrl} https://gitlab.com/tree/{sBranch}");
    }

    [Fact]
    public void ParseScriptArguments_resolve_sRemotePathFromUrl_currentRemote_set()
    {
        var currentRemote = "myRemote";
        List<string> selectedRemotes = [currentRemote];
        _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, currentRemote))
            .Returns("https://gitlab.com/gitlabhq/gitlabhq.git");

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{openUrl} https://gitlab.com{sRemotePathFromUrl}/tree/{sBranch}", "sRemotePathFromUrl",
            null!, _scriptOptionsProvider, _commands, null!, null!,
            null!, null!, null!, selectedRemotes, null!,
            null!,
            null!, null!, null!, null!, null!);

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
        var option = "sRemoteBranch";
        var branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{" + option + "}", option,
            null!, _scriptOptionsProvider, null!, null!, null!,
            null!, null!, remoteBranches, null!, null!,
            null!,
            null!, null!, null!, null!, null!);

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
        var option = "sRemoteBranchName";
        var branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{" + option + "}", option,
            null!, _scriptOptionsProvider, null!, null!, null!,
            null!, null!, remoteBranches, null!, null!,
            null!,
            null!, null!, null!, null!, null!);

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
        var option = "cRemoteBranch";
        var branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{" + option + "}", option,
            null!, _scriptOptionsProvider, null!, null!, null!,
            null!, null!, null!, null!, null!,
            null!,
            null!, null!, remoteBranches, null!, null!);

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
        var option = "cRemoteBranchName";
        var branch = remoteName + '/' + branchName;
        List<IGitRef> remoteBranches = [new GitRef(null!, default, branch)];

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{" + option + "}", option,
            null!, _scriptOptionsProvider, null!, null!, null!,
            null!, null!, null!, null!, null!,
            null!,
            null!, null!, remoteBranches, null!, null!);

        result.Should().Be(branchName);
    }

    [Fact]
    public void ParseScriptArguments_resolve_RepoName()
    {
        const string option = "RepoName";
        const string dirName = "Windows"; // chose one which will never contain a repo

        var repositoryDescriptionProvider = Substitute.For<IRepositoryDescriptionProvider>();
        repositoryDescriptionProvider.Get(Arg.Any<string>()).Returns(dirName);

        ServiceContainer serviceContainer = new();
        serviceContainer.AddService(repositoryDescriptionProvider);

        _commands = new GitUICommands(serviceContainer,
            new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), ""));

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{" + option + "}", option,
            null!, _scriptOptionsProvider, _commands, null!, null!,
            null!, null!, null!, null!, null!,
            null!,
            null!, null!, null!, null!, null!);

        result.Should().Be(dirName);
    }

    [Fact]
    public void ParseScriptArguments_resolve_HEAD_with_checkedout_branch_name()
    {
        var option = "HEAD";
        var branchName = "this_is_my_branch_name";
        _module.GetSelectedBranch(true).Returns(branchName);

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{" + option + "}", option,
            null!, _scriptOptionsProvider, _commands, null!, null!,
            null!, null!, null!, null!, null!,
            null!,
            null!, null!, null!, null!, null!);

        result.Should().Be(branchName);
    }

    [Fact]
    public void ParseScriptArguments_resolve_HEAD_with_detached_head_hash()
    {
        var option = "HEAD";
        var detachedHeadHash = "d54e5cf78a403d5ace3299549be0f6cabee50a63";
        _module.GetSelectedBranch(true).Returns(string.Empty);

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments(
            "{" + option + "}", option,
            null!, _scriptOptionsProvider, _commands, null!, null!,
            null!, null!, null!, null!, null!,
            null!, null!, null!, null!,
            new GitRevision(ObjectId.Parse(detachedHeadHash)), null!);

        result.Should().Be(detachedHeadHash);
    }

    [Fact]
    public void ParseScriptArguments_resolve_QuotedWithBackslashAtEnd()
    {
        _module.WorkingDir.Returns("C:\\test path with whitespaces\\");

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments("{{WorkingDir}} \"{WorkingDir}\"",
            "WorkingDir", null!, null!, _commands, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!);

        result.Should().Be("\"C:\\test path with whitespaces\\\\\" \"C:\\test path with whitespaces\\\"");
    }

    [Fact]
    public void ParseScriptArguments_resolve_StringWithDoubleQuotes()
    {
        GitRevision gitRevision = new(ObjectId.Random())
        {
            Subject = "test string with \"double quotes\" and escaped \\\"double quotes\\\""
        };

        var result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments("{{sMessage}}", "sMessage", null!,
            null!, null!, null!, null!, null!, null!, null!, null!, gitRevision, null!, null!, null!, null!, null!,
            null!);
        result.Should().Be("\"test string with \\\"double quotes\\\" and escaped \\\"double quotes\\\"\"");

        result = ScriptOptionsParser.GetTestAccessor().ParseScriptArguments("{sMessage}", "sMessage", null!, null!,
            null!, null!, null!, null!, null!, null!, null!, gitRevision, null!, null!, null!, null!, null!, null!);
        result.Should().Be("test string with \"double quotes\" and escaped \\\"double quotes\\\"");
    }

    [Fact]
    public void Parse_should_resolve_IScriptOptionsProvider_options()
    {
        const string option1 = nameof(option1);
        const string option2 = nameof(option2);
        const string value1A = nameof(value1A);
        const string value1B = nameof(value1B);
        const string value2 = nameof(value2);
        _scriptOptionsProvider.Options.Returns(new[] { option1, option2 });
        _scriptOptionsProvider.GetValues(option1).Returns([value1A, value1B]);
        _scriptOptionsProvider.GetValues(option2).Returns([value2]);

        (var arguments, var abort) = ScriptOptionsParser.Parse("foo {{" + option1 + "}} bar {" + option2 + "}",
            Substitute.For<IGitUiCommands>(), null!, _scriptOptionsProvider);

        arguments.Should().Be($"foo \"{value1A}\" \"{value1B}\" bar {value2}");
        abort.Should().BeFalse();
    }
}
