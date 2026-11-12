using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs.BrowseDialog;

namespace UITests.CommandsDialogs;

public class FormGoToCommitTests : IDisposable
{
    /// <summary>
    ///     The tab stops of the dialog, in tab order.
    /// </summary>
    private static readonly string[] _tabStops =
    [
        "textboxCommitExpression",
        "comboBoxTags",
        "comboBoxBranches",
        "goButton",
        "linkGitRevParse"
    ];

    // Created once for each test
    private readonly GitUiCommands _commands = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormGoToCommitTests()
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
    public void Focus_should_visit_every_tab_stop_when_tabbing_forwards()
    {
        RunFormTest(form =>
            WalkFocusFromFirstTabStop(form, true)
                .Should().Equal(_tabStops.Skip(1).Append(_tabStops[0])));
    }

    [WinFormsFact]
    public void Focus_should_visit_every_tab_stop_when_tabbing_backwards()
    {
        RunFormTest(form =>
            WalkFocusFromFirstTabStop(form, false)
                .Should().Equal(_tabStops.Skip(1).Reverse().Append(_tabStops[0])));
    }

    /// <summary>
    ///     Moves the focus around the whole dialog, starting at the first tab stop, and reports the
    ///     names of the controls which received the focus.
    /// </summary>
    private static List<string> WalkFocusFromFirstTabStop(Form form, bool forward)
    {
        form.Controls.Find(_tabStops[0], true).Single().Focus();

        List<string> focused = [];
        for (var step = 0; step < _tabStops.Length; ++step)
        {
            form.SelectNextControl(form.ActiveControl, forward, true, true, true);
            focused.Add(form.ActiveControl?.Name ?? "<none>");
        }

        return focused;
    }

    private void RunFormTest(Action<FormGoToCommit> testDriver)
    {
        UiTest.RunForm<FormGoToCommit>(
            () =>
            {
                using FormGoToCommit form = new(_commands);
                form.ShowDialog();
            },
            form =>
            {
                testDriver(form);
                form.Close();
                return Task.CompletedTask;
            });
    }
}
