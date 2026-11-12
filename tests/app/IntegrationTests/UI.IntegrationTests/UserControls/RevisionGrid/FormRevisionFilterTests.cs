using CommonTestUtils;
using GitUI;
using GitUI.UserControls.RevisionGrid;

namespace UITests.UserControls.RevisionGrid;

public class FormRevisionFilterTests : IDisposable
{
    private readonly GitUiCommands _commands = null!;
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormRevisionFilterTests()
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

    [WinFormsTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Simplifying_the_merges_must_be_offered_only_along_with_the_full_history(bool fullHistory)
    {
        using FormRevisionFilter form = new(_commands, new FilterInfo());
        var accessor = form.GetTestAccessor();

        accessor.FullHistoryCheck.Checked = fullHistory;
        accessor.UpdateFilters();

        accessor.SimplifyMergesCheck.Enabled.Should().Be(fullHistory,
            "git ignores --simplify-merges unless --full-history is given as well");
    }
}
