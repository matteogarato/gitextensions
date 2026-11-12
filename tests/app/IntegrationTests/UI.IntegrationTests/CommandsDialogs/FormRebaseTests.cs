using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs;

namespace UITests.CommandsDialogs;

public class FormRebaseTests : IDisposable
{
    private readonly GitUiCommands _commands = null!;
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormRebaseTests()
    {
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Interactive_check_enables_autosquash()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.ChkInteractive.Checked = true;

                accessor.ChkAutosquash.Enabled.Should().BeTrue();
            },
            "", null!, null!, false, false);
    }

    [WinFormsFact]
    public void Interactive_uncheck_disables_autosquash()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();
                accessor.ChkInteractive.Checked = true;

                accessor.ChkInteractive.Checked = false;

                accessor.ChkAutosquash.Enabled.Should().BeFalse();
            },
            "", null!, null!, false, false);
    }

    [WinFormsFact]
    public void Ignore_date_check_disables_all_other_options()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.ChkIgnoreDate.Checked = true;

                accessor.ChkInteractive.Enabled.Should().BeFalse();
                accessor.ChkPreserveMerges.Enabled.Should().BeFalse();
                accessor.ChkAutosquash.Enabled.Should().BeFalse();
                accessor.ChkCommitterDateIsAuthorDate.Enabled.Should().BeFalse();
            },
            "", null!, null!, false, false);
    }

    [WinFormsFact]
    public void Ignore_date_uncheck_enables_all_options_if_interactive_checked()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();
                accessor.ChkInteractive.Checked = true;
                accessor.ChkIgnoreDate.Checked = true;

                accessor.ChkIgnoreDate.Checked = false;

                accessor.ChkInteractive.Enabled.Should().BeTrue();
                accessor.ChkPreserveMerges.Enabled.Should().BeTrue();
                accessor.ChkAutosquash.Enabled.Should().BeTrue();
                accessor.ChkCommitterDateIsAuthorDate.Enabled.Should().BeTrue();
            },
            "", null!, null!, false, false);
    }

    [WinFormsFact]
    public void Ignore_date_uncheck_enables_all_options_but_autosquash_if_interactive_not_checked()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();
                accessor.ChkIgnoreDate.Checked = true;

                accessor.ChkIgnoreDate.Checked = false;

                accessor.ChkInteractive.Enabled.Should().BeTrue();
                accessor.ChkPreserveMerges.Enabled.Should().BeTrue();
                accessor.ChkAutosquash.Enabled.Should().BeFalse();
                accessor.ChkCommitterDateIsAuthorDate.Enabled.Should().BeTrue();
            },
            "", null!, null!, false, false);
    }

    [WinFormsFact]
    public void Committer_date_check_disables_all_other_options()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.ChkCommitterDateIsAuthorDate.Checked = true;

                accessor.ChkInteractive.Enabled.Should().BeFalse();
                accessor.ChkPreserveMerges.Enabled.Should().BeFalse();
                accessor.ChkAutosquash.Enabled.Should().BeFalse();
                accessor.ChkIgnoreDate.Enabled.Should().BeFalse();
            },
            "", null!, null!, false, false);
    }

    [WinFormsFact]
    public void Committer_date_uncheck_enables_all_options_if_interactive_is_checked()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();
                accessor.ChkInteractive.Checked = true;
                accessor.ChkCommitterDateIsAuthorDate.Checked = true;

                accessor.ChkCommitterDateIsAuthorDate.Checked = false;

                accessor.ChkInteractive.Enabled.Should().BeTrue();
                accessor.ChkPreserveMerges.Enabled.Should().BeTrue();
                accessor.ChkAutosquash.Enabled.Should().BeTrue();
                accessor.ChkIgnoreDate.Enabled.Should().BeTrue();
            },
            "", null!, null!, false, false);
    }

    [WinFormsFact]
    public void Committer_date_uncheck_enables_all_options_but_autosquash_if_interactive_not_checked()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();
                accessor.ChkCommitterDateIsAuthorDate.Checked = true;

                accessor.ChkCommitterDateIsAuthorDate.Checked = false;

                accessor.ChkInteractive.Enabled.Should().BeTrue();
                accessor.ChkPreserveMerges.Enabled.Should().BeTrue();
                accessor.ChkAutosquash.Enabled.Should().BeFalse();
                accessor.ChkIgnoreDate.Enabled.Should().BeTrue();
            },
            "", null!, null!, false, false);
    }

    private void RunFormTest(Action<FormRebase> testDriver, string from, string? to, string? onto,
        bool interactive, bool startRebaseImmediately)
    {
        RunFormTest(
            form =>
            {
                testDriver(form);
                return Task.CompletedTask;
            },
            from, to, onto, interactive, startRebaseImmediately);
    }

    private void RunFormTest(Func<FormRebase, Task> testDriverAsync, string from, string? to,
        string? onto, bool interactive, bool startRebaseImmediately)
    {
        UiTest.RunForm(
            () =>
            {
                _commands.StartRebaseDialog(owner: null, from, to, onto, interactive, startRebaseImmediately);
            },
            testDriverAsync);
    }
}
