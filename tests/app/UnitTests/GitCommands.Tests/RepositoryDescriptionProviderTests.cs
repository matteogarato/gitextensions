using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using NSubstitute;

namespace GitCommandsTests;

public class RepositoryDescriptionProviderTests : IDisposable
{
    private readonly string _tempDir = null!;

    public RepositoryDescriptionProviderTests()
    {
        _tempDir = Directory.CreateTempSubdirectory().FullName;
    }

    void IDisposable.Dispose()
    {
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void RepositoryDescriptionProvider_should_handle_subrepos()
    {
        var gitDirectoryResolver = Substitute.For<IGitDirectoryResolver>();
        RepositoryDescriptionProvider repositoryDescriptionProvider = new(gitDirectoryResolver);

        var repo = @$"{_tempDir}\test_repo";
        var submodule = @$"{repo}\submodule";
        var nested = @$"{submodule}\nested";
        var subsubmodule = @$"{nested}\subsubmodule";
        var leafsubmodule = @$"{nested}\leafsubmodule";
        Directory.CreateDirectory(leafsubmodule);

        repositoryDescriptionProvider.Get(repo, IsValidGitWorkingDir)
            .Should().Be("test_repo");

        repositoryDescriptionProvider.Get(submodule, IsValidGitWorkingDir)
            .Should().Be("submodule < test_repo");

        repositoryDescriptionProvider.Get(subsubmodule, IsValidGitWorkingDir)
            .Should().Be("subsubmodule < test_repo");

        repositoryDescriptionProvider.Get(leafsubmodule, IsValidGitWorkingDir)
            .Should().Be("leafsubmodule < test_repo");
    }

    [Theory]
    [InlineData("app")]
    [InlineData("repo")]
    [InlineData("repository")]
    public void RepositoryDescriptionProvider_should_skip_uninformative_submodule_name(string uninformative)
    {
        var gitDirectoryResolver = Substitute.For<IGitDirectoryResolver>();
        RepositoryDescriptionProvider repositoryDescriptionProvider = new(gitDirectoryResolver);

        string rootrepo = nameof(rootrepo);
        string parent = nameof(parent);
        var repo = @$"{_tempDir}\{rootrepo}\{parent}\{uninformative}";
        Directory.CreateDirectory(repo);

        repositoryDescriptionProvider.Get(repo, IsValidGitWorkingDir)
            .Should().Be($"{parent} < {rootrepo}");
    }

    [Theory]
    [InlineData("app")]
    [InlineData("repo")]
    [InlineData("repository")]
    public void RepositoryDescriptionProvider_should_not_skip_uninformative_submodule_name_to_parent_repo(
        string uninformative)
    {
        var gitDirectoryResolver = Substitute.For<IGitDirectoryResolver>();
        RepositoryDescriptionProvider repositoryDescriptionProvider = new(gitDirectoryResolver);

        string parentrepo = nameof(parentrepo);
        var repo = @$"{_tempDir}\{parentrepo}\{uninformative}";
        Directory.CreateDirectory(repo);

        repositoryDescriptionProvider.Get(repo, IsValidGitWorkingDir)
            .Should().Be($"{uninformative} < {parentrepo}");
    }

    [Theory]
    [InlineData("app")]
    [InlineData("repo")]
    [InlineData("repository")]
    public void RepositoryDescriptionProvider_should_not_skip_uninformative_root_repo_name(string uninformative)
    {
        var gitDirectoryResolver = Substitute.For<IGitDirectoryResolver>();
        RepositoryDescriptionProvider repositoryDescriptionProvider = new(gitDirectoryResolver);

        string parent = nameof(parent);
        var repo = @$"{_tempDir}\{parent}\{uninformative}";
        Directory.CreateDirectory(repo);

        repositoryDescriptionProvider.Get(repo, IsValidGitWorkingDir)
            .Should().Be($"{uninformative}");
    }

    private static bool IsValidGitWorkingDir(string path)
    {
        return path.EndsWith("submodule") || path.EndsWith("repo");
    }
}
