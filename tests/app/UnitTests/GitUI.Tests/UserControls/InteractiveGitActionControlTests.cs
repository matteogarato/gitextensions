using GitUI.UserControls;

namespace GitUITests.UserControls;

public class InteractiveGitActionControlTests : IDisposable
{
    private readonly InteractiveGitActionControl.TestAccessor _accessor;
    private readonly InteractiveGitActionControl _control = null!;

    public InteractiveGitActionControlTests()
    {
        _control = new InteractiveGitActionControl();
        _accessor = _control.GetTestAccessor();
    }

    void IDisposable.Dispose()
    {
        _control.Dispose();
    }

    [StaTheory]
    [InlineData(InteractiveGitActionControl.GitAction.Rebase, false)]
    [InlineData(InteractiveGitActionControl.GitAction.Rebase, true)]
    [InlineData(InteractiveGitActionControl.GitAction.Merge, false)]
    [InlineData(InteractiveGitActionControl.GitAction.Merge, true)]
    [InlineData(InteractiveGitActionControl.GitAction.Patch, false)]
    [InlineData(InteractiveGitActionControl.GitAction.Patch, true)]
    [InlineData(InteractiveGitActionControl.GitAction.Bisect, false)]
    [InlineData(InteractiveGitActionControl.GitAction.None, false)]
    [InlineData(InteractiveGitActionControl.GitAction.None, true)]
    public void SetState(InteractiveGitActionControl.GitAction action, bool conflicts)
    {
        _accessor.SetGitAction(action, conflicts);

        _accessor.Action.Should().Be(action);
        _accessor.HasConflicts.Should().Be(conflicts);

        switch (action)
        {
            case InteractiveGitActionControl.GitAction.None:
                _accessor.Visible.Should().Be(conflicts);
                _accessor.Controls.Contains(_accessor.ResolveButton).Should().Be(conflicts);
                _accessor.Controls.Count.Should().Be(conflicts ? 1 : 0);
                break;
            case InteractiveGitActionControl.GitAction.Bisect:
                _accessor.Visible.Should().BeTrue();
                _accessor.Controls.Contains(_accessor.MoreButton).Should().BeTrue();
                break;
            case InteractiveGitActionControl.GitAction.Rebase:
            case InteractiveGitActionControl.GitAction.Patch:
                _accessor.Visible.Should().BeTrue();
                _accessor.Controls.Contains(_accessor.ResolveButton).Should().Be(conflicts);
                _accessor.Controls.Contains(_accessor.ContinueButton).Should().Be(!conflicts);
                _accessor.Controls.Contains(_accessor.AbortButton).Should().BeTrue();
                _accessor.Controls.Contains(_accessor.MoreButton).Should().BeTrue();
                break;
            case InteractiveGitActionControl.GitAction.Merge:
                _accessor.Visible.Should().BeTrue();
                _accessor.Controls.Contains(_accessor.ResolveButton).Should().Be(conflicts);
                _accessor.Controls.Contains(_accessor.ContinueButton).Should().Be(!conflicts);
                _accessor.Controls.Contains(_accessor.AbortButton).Should().BeTrue();
                break;
        }
    }
}
