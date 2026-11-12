using CommonTestUtils;
using GitCommands.Git;
using GitUI;
using GitUI.CommandsDialogs;

namespace UITests.CommandsDialogs;

public class FormPushTests : IDisposable
{
    // Created once for each test
    private readonly GitUiCommands _commands = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormPushTests()
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

    // Note: the DataBindings between ForcePushTags and ForcePushBranches or ckForceWithLease (depending on Git version) do not function in this test environment
    [WinFormsTheory]
    [InlineData(false, false, false, ForcePushOptions.DoNotForce)]
    [InlineData(false, true, false, ForcePushOptions.Force)]
    [InlineData(false, false, true,
        ForcePushOptions.Force)] // ForcePushTag requires normal force as with-lease is not allowed for tags
    [InlineData(false, true, true, ForcePushOptions.Force)]
    [InlineData(true, false, false,
        ForcePushOptions.ForceWithLease)] // would be ForcePushOptions.DoNotForce if DataBindings were working
    [InlineData(true, true, false, ForcePushOptions.Force)]
    [InlineData(true, false, true,
        ForcePushOptions.Force)] // ForcePushBranches and ForcePushTags take precedence over ckForceWithLease
    [InlineData(true, true, true,
        ForcePushOptions.Force)] // ForcePushBranches and ForcePushTags take precedence over ckForceWithLease
    public void Should_choose_correct_force_push_option_for_checkbox_state(
        bool forcePushBranchWithLeaseChecked, bool forcePushBranchChecked, bool forcePushTagChecked,
        ForcePushOptions forcePushOption)
    {
        RunFormTest(form =>
        {
            var accessor = form.GetTestAccessor();

            accessor.ForcePushTags.Checked = forcePushTagChecked;
            accessor.CkForceWithLease.Checked = forcePushBranchWithLeaseChecked;
            accessor.ForcePushBranches.Checked = forcePushBranchChecked;

            accessor.GetForcePushOption().Should().Be(forcePushOption);
        });
    }

    private void RunFormTest(Action<FormPush> testDriver)
    {
        RunFormTest(form =>
        {
            testDriver(form);
            return Task.CompletedTask;
        });
    }

    private void RunFormTest(Func<FormPush, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () =>
            {
                // False because we haven't performed any actions
                _commands.StartPushDialog(owner: null, pushOnShow: false, forceWithLease: false, out _).Should()
                    .BeFalse();
            },
            testDriverAsync);
    }
}
