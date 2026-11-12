using GitExtensions.Extensibility.Git;
using GitUI.UserControls;

namespace GitUITests.UserControls;

public sealed class RepoStateVisualiserTests
{
    private readonly RepoStateVisualiser _repoStateVisualiser = null!;

    public RepoStateVisualiserTests()
    {
        _repoStateVisualiser = new RepoStateVisualiser();
    }

    private static GitItemStatus CreateGitItemStatus(
        bool isStaged = false,
        bool isTracked = true,
        bool isSubmodule = false)
    {
        return new GitItemStatus("file1")
        {
            Staged = isStaged ? StagedStatus.Index : StagedStatus.WorkTree,
            IsTracked = isTracked,
            IsSubmodule = isSubmodule
        };
    }

    [Fact]
    public void ReturnsIconCleanWhenThereIsNoChangedFiles()
    {
        var commitIcon = _repoStateVisualiser.Invoke([]);

        commitIcon.Should().Be(RepoStateVisualiser.Clean);
    }

    [Fact]
    public void ReturnsIconDirtySubmodulesWhenThereAreOnlyWorkTreeSubmodules()
    {
        var commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isSubmodule: true), CreateGitItemStatus(isSubmodule: true)
        });

        commitIcon.Should().Be(RepoStateVisualiser.DirtySubmodules);
    }

    [Fact]
    public void ReturnsIconDirtyWhenThereAreWorkTreeChanges()
    {
        var commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isSubmodule: true), CreateGitItemStatus()
        });

        commitIcon.Should().Be(RepoStateVisualiser.Dirty);
    }

    [Fact]
    public void ReturnsIconMixedWhenThereAreIndexAndWorkTreeFiles()
    {
        var commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(true), CreateGitItemStatus()
        });

        commitIcon.Should().Be(RepoStateVisualiser.Mixed);
    }

    [Fact]
    public void ReturnsIconStagedWhenThereAreOnlyIndexFiles()
    {
        var commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(true), CreateGitItemStatus(true)
        });

        commitIcon.Should().Be(RepoStateVisualiser.Staged);
    }

    [Fact]
    public void ReturnsIconUntrackedOnlyWhenThereAreUntrackedFilesOnly()
    {
        var commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isTracked: false), CreateGitItemStatus(isTracked: false)
        });

        commitIcon.Should().Be(RepoStateVisualiser.UntrackedOnly);
    }

    [Fact]
    public void ReturnsIconUnknownWhenNull()
    {
        var commitIcon = _repoStateVisualiser.Invoke(null);

        commitIcon.Should().Be(RepoStateVisualiser.Unknown);
    }
}
