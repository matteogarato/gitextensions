using CommonTestUtils;
using GitCommands.Settings;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls.RevisionGrid;

namespace UITests.UserControls.RevisionGrid;

public class RevisionGridToolTipProviderTests : IDisposable, IClassFixture<RevisionGridToolTipProviderTestsFixture>
{
    private GitUiCommands _commands = null!;

    // Created once for each test
    private ReferenceRepository _referenceRepository = null!;

    public RevisionGridToolTipProviderTests(RevisionGridToolTipProviderTestsFixture fixture)
    {
        InitializeTest();
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void InitializeTest()
    {
        _referenceRepository = new ReferenceRepository();
        _referenceRepository.CreateCommit("Commit1", "Commit1");
        _referenceRepository.CreateCommit("Commit2", "Commit2");

        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Hide_should_not_shorten_or_extend_the_display_duration()
    {
        RunProviderTest(provider =>
        {
            var accessor = provider.GetTestAccessor();
            var autoPopDelay = accessor.ToolTip.AutoPopDelay;

            provider.Hide();

            accessor.ToolTip.AutoPopDelay.Should().Be(autoPopDelay,
                "the display duration belongs to the tooltip, so hiding must not redefine it for all later tooltips");
        });
    }

    [WinFormsFact]
    public void Clear_should_forget_the_cell_the_tooltip_was_built_for()
    {
        RunProviderTest(provider =>
        {
            provider.ShowRevisionGridTooltips = true;
            const int rowIndex = 1;
            provider.OnCellMouseMove(new DataGridViewCellMouseEventArgs(1, rowIndex, 0, 0,
                new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0)), null);

            var accessor = provider.GetTestAccessor();
            accessor.PreviousRowIndex.Should().Be(rowIndex);

            provider.Clear();

            accessor.PreviousRowIndex.Should().Be(-1,
                "a row index no longer identifies the same revision after the revisions have been reloaded");
        });
    }

    private void RunProviderTest(Action<RevisionGridToolTipProvider> testDriver)
    {
        UiTest.RunForm<FormBrowse>(
            () => _commands.StartBrowseDialog(owner: null).Should().BeTrue(),
            async formBrowse =>
            {
                var revisionGrid = formBrowse.RevisionGridControl;
                UiTest.ProcessUntil("Loading Revisions", () => revisionGrid.GetTestAccessor().IsDataLoadComplete);
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

                testDriver(revisionGrid.GetTestAccessor().ToolTipProvider);
            });
    }
}

public sealed class RevisionGridToolTipProviderTestsFixture
{
    public RevisionGridToolTipProviderTestsFixture()
    {
        // We don't want avatars during tests, otherwise we will be attempting to download them from gravatar.
        AppSettings.ShowAuthorAvatarColumn = false;
    }
}
