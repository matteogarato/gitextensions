using System.IO.Abstractions;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests.Git;

public class IndexLockManagerTests
{
    private const string _indexLock = "index.lock";
    private readonly DirectoryBase _directory = null!;
    private readonly FileBase _file = null!;
    private readonly IFileSystem _fileSystem = null!;
    private readonly IGitDirectoryResolver _gitDirectoryResolver = null!;
    private readonly string _gitFile = null!;
    private readonly string _gitWorkingDir = null!;
    private readonly string _indexLockFile = null!;
    private readonly IGitModule _module = null!;
    private readonly string _workingDir = @"c:\dev\repo";
    private IIndexLockManager _manager = null!;

    public IndexLockManagerTests()
    {
        _gitFile = Path.Combine(_workingDir, ".git");
        _gitWorkingDir = _gitFile.EnsureTrailingPathSeparator();
        _indexLockFile = Path.Combine(_gitWorkingDir, _indexLock);

        _file = Substitute.For<FileBase>();
        _directory = Substitute.For<DirectoryBase>();
        _fileSystem = Substitute.For<IFileSystem>();
        _fileSystem.Directory.Returns(_directory);
        _fileSystem.File.Returns(_file);

        _module = Substitute.For<IGitModule>();
        _module.WorkingDir.Returns(_workingDir);

        _gitDirectoryResolver = Substitute.For<IGitDirectoryResolver>();
        _gitDirectoryResolver.Resolve(_workingDir).Returns(_gitWorkingDir);

        _manager = new IndexLockManager(_module, _gitDirectoryResolver, _fileSystem);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsIndexLocked(bool fileExists)
    {
        _file.Exists(Arg.Any<string>()).Returns(fileExists);

        _manager.IsIndexLocked().Should().Be(fileExists);
    }

    [Fact]
    public void UnlockIndex_should_only_delete_working_folder_lock_if_requested()
    {
        _manager.UnlockIndex(false);

        _module.DidNotReceive().GetSubmodulesLocalPaths();
    }

    [Fact]
    public void UnlockIndex_should_not_delete_lock_if_absent()
    {
        _file.Exists(_indexLockFile).Returns(false);

        _manager.UnlockIndex(false);

        _module.DidNotReceive().GetSubmodulesLocalPaths();
        _file.DidNotReceive().Delete(Arg.Any<string>());
    }

    [Fact]
    public void UnlockIndex_should_only_delete_lock_if_exists()
    {
        _file.Exists(_indexLockFile).Returns(true);

        _manager.UnlockIndex(false);

        _module.DidNotReceive().GetSubmodulesLocalPaths();
        _file.Received(1).Delete(Arg.Any<string>());
        _file.Received(1).Delete(_indexLockFile);
    }

    [Fact]
    public void UnlockIndex_should_throw_if_delete_fails()
    {
        _file.Exists(_indexLockFile).Returns(true);
        _file.When(x => x.Delete(_indexLockFile)).Throw(new DivideByZeroException("boom"));

        var act = () => _manager.UnlockIndex(false);
        var ex = act.Should().Throw<FileDeleteException>().Which;

        ex!.FileName.Should().Be(_indexLockFile);
        _module.DidNotReceive().GetSubmodulesLocalPaths();
        _file.Received(1).Delete(Arg.Any<string>());
        _file.Received(1).Delete(_indexLockFile);
    }

    [Fact(SkipUnless = nameof(PlatformTestConditions.IsWindows), SkipType = typeof(PlatformTestConditions))]
    public void UnlockIndex_should_delete_submodule_locks_if_requested()
    {
        _file.Exists(_indexLockFile).Returns(true);

        var submoduleGitHubWorkingDir = $@"{_workingDir}\Externals\Git.hub\";
        var submoduleNbugWorkingDir = $@"{_workingDir}\Externals\NBug\";
        var submoduleGitHubWorkingDirGitDir = $@"{_gitWorkingDir}\modules\Externals\Git.hub\";
        var submoduleNBugWorkingDirGitDir = $@"{_gitWorkingDir}\modules\Externals\NBug\";
        var submoduleGitHubIndexLock = $@"{_gitWorkingDir}\modules\Externals\Git.hub\{_indexLock}";
        var submoduleNBugIndexLock = $@"{_gitWorkingDir}\modules\Externals\NBug\{_indexLock}";

        _module.GetSubmodulesLocalPaths().Returns(new[] { "Externals/Git.hub", "Externals/NBug" });
        _module.GetSubmoduleFullPath(Arg.Any<string>())
            .Returns(submoduleGitHubWorkingDir, submoduleNbugWorkingDir);
        _gitDirectoryResolver.Resolve(submoduleGitHubWorkingDir).Returns(submoduleGitHubWorkingDirGitDir);
        _gitDirectoryResolver.Resolve(submoduleNbugWorkingDir).Returns(submoduleNBugWorkingDirGitDir);
        _file.Exists(submoduleGitHubIndexLock).Returns(true);
        _file.Exists(submoduleNBugIndexLock).Returns(false);

        _manager.UnlockIndex();

        _file.Received().Delete(submoduleGitHubIndexLock);
        _file.DidNotReceive().Delete(submoduleNBugIndexLock);
    }

    [Fact]
    public void Resolve_submodule_real_filesystem()
    {
        using GitModuleTestHelper helper = new();
        helper.CreateFile(helper.Module.WorkingDir, ".gitmodules", @"[submodule ""Externals/NBug""]
    path = Externals/NBug
    url = https://github.com/gitextensions/NBug.git
[submodule ""Externals/Git.hub""]
    path = Externals/Git.hub
    url = https://github.com/gitextensions/Git.hub.gitk");

        var submoduleGitHub = Path.Combine(helper.Module.WorkingDir, "Externals", "Git.hub");
        var submoduleNBug = Path.Combine(helper.Module.WorkingDir, "Externals", "NBug");
        var submoduleGitHubWorkingDirGitDir =
            Path.Combine(helper.Module.WorkingDirGitDir, "modules", "Externals", "Git.hub");
        var submoduleNbugWorkingDirGitDir =
            Path.Combine(helper.Module.WorkingDirGitDir, "modules", "Externals", "NBug");
        helper.CreateFile(submoduleGitHub, ".git", "gitdir: ../../.git/modules/Externals/Git.hub");
        helper.CreateFile(submoduleNBug, ".git", "gitdir: ../../.git/modules/Externals/NBug");
        helper.CreateFile(helper.Module.WorkingDirGitDir, _indexLock, "");
        helper.CreateFile(submoduleGitHubWorkingDirGitDir, _indexLock, "");

        _manager = new IndexLockManager(helper.Module);

        var indexLock = Path.Combine(helper.Module.WorkingDirGitDir, _indexLock);
        File.Exists(indexLock).Should().BeTrue();
        var gitHubIndexLock = Path.Combine(submoduleGitHubWorkingDirGitDir, _indexLock);
        File.Exists(gitHubIndexLock).Should().BeTrue();
        var nbugIndexLock = Path.Combine(submoduleNbugWorkingDirGitDir, _indexLock);
        File.Exists(nbugIndexLock).Should().BeFalse();

        _manager.UnlockIndex();

        File.Exists(indexLock).Should().BeFalse();
        File.Exists(gitHubIndexLock).Should().BeFalse();
        File.Exists(nbugIndexLock).Should().BeFalse();
    }
}
