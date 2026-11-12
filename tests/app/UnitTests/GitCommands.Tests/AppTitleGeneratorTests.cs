using GitCommands;
using GitCommands.UserRepositoryHistory;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitCommandsTests;

public class AppTitleGeneratorTests
{
    private const string _shortName = "gitextension";
    private readonly AppTitleGenerator _appTitleGenerator = null!;
    private readonly string _defaultBranchName = "no branch";
    private readonly IRepositoryDescriptionProvider _repositoryDescriptionProvider = null!;

    public AppTitleGeneratorTests()
    {
        _repositoryDescriptionProvider = Substitute.For<IRepositoryDescriptionProvider>();
        _repositoryDescriptionProvider.Get(Arg.Any<string>()).Returns(_shortName);

        _appTitleGenerator = new AppTitleGenerator(_repositoryDescriptionProvider);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Generate_should_return_default_title_if_invalid_working_directory(string? path)
    {
        var title = _appTitleGenerator.Generate(path, false, null);
        title.Should().Be(AppSettings.ApplicationName);
    }

    [Fact]
    public void Generate_should_return_default_title_if_not_valid_working_directory()
    {
        var title = _appTitleGenerator.Generate("a", false, null);
        title.Should().Be(AppSettings.ApplicationName);
    }

    [Fact]
    public void Generate_should_include_no_branch_if_supplied()
    {
        var title = _appTitleGenerator.Generate("a", true, null, defaultBranchName: _defaultBranchName);
        title.Should().StartWith($"{_shortName} ({_defaultBranchName}) - {AppSettings.ApplicationName}");
    }

    [Fact]
    public void Generate_should_include_supplied_branch()
    {
        var branchName = "feature/my_(test)_branch";
        var title =
            _appTitleGenerator.Generate("a", true, branchName: branchName, defaultBranchName: _defaultBranchName);
        title.Should().StartWith($"{_shortName} ({branchName}) - {AppSettings.ApplicationName}");
    }

    [Fact]
    public void Generate_should_include_supplied_pathname()
    {
        var branchName = "feature/my_(test)_branch";
        var pathName = "folder/folder/file";
        var title = _appTitleGenerator.Generate("a", true, branchName: branchName, pathName: pathName);
        title.Should().StartWith($@"""file"" {_shortName} ({branchName}) - {AppSettings.ApplicationName}");
    }

    [Fact]
    public void Generate_should_not_quote_already_quoted_pathname()
    {
        var pathName = @"""folder/folder/file""";
        var title =
            _appTitleGenerator.Generate("a", true, pathName: pathName, defaultBranchName: _defaultBranchName);
        title.Should().StartWith($@"""file"" {_shortName} ({_defaultBranchName}) - {AppSettings.ApplicationName}");
    }

    [Theory]
    [InlineData(@"""folder/folder/""", @"folder/folder/")]
    [InlineData(@"folder/folder/", @"folder/folder/")]
    [InlineData(@"""folder/folder/f*ile extra""", @"f*ile extra")]
    public void Generate_should_quote_paths_where_filenames_cannot_be_resolved(string pathName, string expectedPath)
    {
        var title =
            _appTitleGenerator.Generate("a", true, pathName: pathName, defaultBranchName: _defaultBranchName);
        title.Should()
            .StartWith($@"""{expectedPath}"" {_shortName} ({_defaultBranchName}) - {AppSettings.ApplicationName}");
    }

#if DEBUG
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public void Generate_should_include_debug_suffix(string? buildSha)
    {
        var buildBranch = "build_branch";
        AppTitleGenerator.Initialise(buildSha!, buildBranch);
        var title = _appTitleGenerator.Generate("a", true, null, defaultBranchName: _defaultBranchName);

        title.Should().Be($"{_shortName} ({_defaultBranchName}) - {AppSettings.ApplicationName} [DEBUG]");
    }

    [Fact]
    public void Generate_should_include_build_suffix()
    {
        var buildSha = "1234567812345678123456781234567812345678";
        var buildBranch = "build_branch";
        AppTitleGenerator.Initialise(buildSha, buildBranch);
        var title = _appTitleGenerator.Generate("a", true, null, defaultBranchName: _defaultBranchName);

        title.Should()
            .Be($"{_shortName} ({_defaultBranchName}) - {AppSettings.ApplicationName} {buildSha[..8]} ({buildBranch})");
    }
#endif
}
