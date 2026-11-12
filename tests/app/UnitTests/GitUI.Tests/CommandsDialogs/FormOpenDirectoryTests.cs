using CommonTestUtils;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs.BrowseDialog;
using NSubstitute;

namespace GitUITests.CommandsDialogs;

public class FormOpenDirectoryTests : IDisposable
{
    // Created once for the fixture
    private static readonly IGitExecutorProvider
        _executorProvider = new GitExecutorProvider(new GitDirectoryResolver());

    private readonly ILocalRepositoryManager _localRepositoryManager = null!;
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormOpenDirectoryTests()
    {
        _referenceRepository = new ReferenceRepository();
        _localRepositoryManager = Substitute.For<ILocalRepositoryManager>();
    }

    void IDisposable.Dispose()
    {
        _referenceRepository.Dispose();
    }

    [StaFact]
    public void OpenGitRepository_should_return_null_if_directory_not_exist()
    {
        var path = @"C:\some\directory\that\does\not\exist\at\all";

        FormOpenDirectory.TestAccessor.OpenGitRepository(_executorProvider, path, _localRepositoryManager).Should()
            .BeNull();
        _localRepositoryManager.DidNotReceive().AddAsMostRecentAsync(Arg.Any<string>());
    }

    [StaFact]
    public void OpenGitRepository_should_return_null_if_not_valid_git_repo()
    {
        // Path.GetTempPath returns a path with the trailing slash
        var path = Path.GetTempPath();
        path[^1].Should().Be(Path.DirectorySeparatorChar);

        FormOpenDirectory.TestAccessor.OpenGitRepository(_executorProvider, path, _localRepositoryManager).Should()
            .BeNull();
        _localRepositoryManager.DidNotReceive().AddAsMostRecentAsync(Arg.Any<string>());
    }

    [StaFact]
    public void OpenGitRepository_should_not_throw_if_path_not_end_with_slash()
    {
        // Path.GetTempPath returns a path with the trailing slash
        var path = Path.GetTempPath();
        path[^1].Should().Be(Path.DirectorySeparatorChar);

        // ensure absence of the trailing slash isn't a problem
        path = path[..^1];
        ((Action)(() =>
                FormOpenDirectory.TestAccessor.OpenGitRepository(_executorProvider, path, _localRepositoryManager)))
            .Should().NotThrow();
    }

    [StaFact]
    public void OpenGitRepository_should_return_module_For_valid_git_repo()
    {
        // Path.GetTempPath returns a path with the trailing slash
        var path = Path.GetTempPath();
        path[^1].Should().Be(Path.DirectorySeparatorChar);

        var module = FormOpenDirectory.TestAccessor.OpenGitRepository(_executorProvider,
            _referenceRepository.Module.WorkingDir, _localRepositoryManager);

        module.Should().NotBeNull();
        module.WorkingDir.Should().Be(_referenceRepository.Module.WorkingDir);
        _localRepositoryManager.Received(1).AddAsMostRecentAsync(_referenceRepository.Module.WorkingDir);
    }
}
