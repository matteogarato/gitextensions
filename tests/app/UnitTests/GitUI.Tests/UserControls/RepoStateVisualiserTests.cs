using GitExtensions.Extensibility.Git;
using GitUI.UserControls;

namespace GitUITests.UserControls;
public sealed class RepoStateVisualiserTests
{
    public RepoStateVisualiserTests()
    {
        _repoStateVisualiser = new RepoStateVisualiser();
    }

    private RepoStateVisualiser _repoStateVisualiser = null!;

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
        (Image image, Brush brush) commitIcon = _repoStateVisualiser.Invoke([]);

        commitIcon.Should().Be(RepoStateVisualiser.Clean);
    }

    [Fact]
    public void ReturnsIconDirtySubmodulesWhenThereAreOnlyWorkTreeSubmodules()
    {
        (Image image, Brush brush) commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isSubmodule: true),
            CreateGitItemStatus(isSubmodule: true)
        });

        commitIcon.Should().Be(RepoStateVisualiser.DirtySubmodules);
    }

    [Fact]
    public void ReturnsIconDirtyWhenThereAreWorkTreeChanges()
    {
        (Image image, Brush brush) commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isSubmodule: true),
            CreateGitItemStatus()
        });

        commitIcon.Should().Be(RepoStateVisualiser.Dirty);
    }

    [Fact]
    public void ReturnsIconMixedWhenThereAreIndexAndWorkTreeFiles()
    {
        (Image image, Brush brush) commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isStaged: true),
            CreateGitItemStatus()
        });

        commitIcon.Should().Be(RepoStateVisualiser.Mixed);
    }

    [Fact]
    public void ReturnsIconStagedWhenThereAreOnlyIndexFiles()
    {
        (Image image, Brush brush) commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isStaged: true),
            CreateGitItemStatus(isStaged: true)
        });

        commitIcon.Should().Be(RepoStateVisualiser.Staged);
    }

    [Fact]
    public void ReturnsIconUntrackedOnlyWhenThereAreUntrackedFilesOnly()
    {
        (Image image, Brush brush) commitIcon = _repoStateVisualiser.Invoke(new[]
        {
            CreateGitItemStatus(isTracked: false),
            CreateGitItemStatus(isTracked: false)
        });

        commitIcon.Should().Be(RepoStateVisualiser.UntrackedOnly);
    }

    [Fact]
    public void ReturnsIconUnknownWhenNull()
    {
        (Image image, Brush brush) commitIcon = _repoStateVisualiser.Invoke(null);

        commitIcon.Should().Be(RepoStateVisualiser.Unknown);
    }
}
