using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.UserControls;
using GitUI.UserControls.RevisionGrid;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.UserControls;

[Culture("en-US")]
public class FilterToolBarTests : IDisposable
{
    private readonly FilterToolBar _filterToolBar = null!;
    private readonly IGitModule _gitModule = null!;
    private readonly bool _originalShowOnlyFirstParent;
    private readonly bool _originalShowReflogReferences;
    private readonly IRevisionGridFilter _revisionGridFilter = null!;

    public FilterToolBarTests()
    {
        _originalShowOnlyFirstParent = AppSettings.ShowOnlyFirstParent;
        _originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowOnlyFirstParent = false;
        AppSettings.ShowReflogReferences.Value = false;

        _gitModule = Substitute.For<IGitModule>();
        _revisionGridFilter = Substitute.For<IRevisionGridFilter>();

        _filterToolBar = new FilterToolBar();
        _filterToolBar.Bind(() => _gitModule, _revisionGridFilter);
    }

    void IDisposable.Dispose()
    {
        _filterToolBar.Dispose();
        AppSettings.ShowOnlyFirstParent = _originalShowOnlyFirstParent;
        AppSettings.ShowReflogReferences.Value = _originalShowReflogReferences;
    }

    [StaFact]
    public void RevisionGridFilter_should_throw_if_unbound()
    {
        ((Action)(() => _ = new FilterToolBar().GetTestAccessor().RevisionGridFilter))
            .Should().Throw<InvalidOperationException>();
    }

    [StaFact]
    public void GetModule_should_throw_if_unbound()
    {
        ((Action)(() => _ = new FilterToolBar().GetTestAccessor().GetModule()))
            .Should().Throw<InvalidOperationException>();
    }

    [StaFact]
    public void ApplyBranchFilter_should_invoke_RevisionGridFilter_with_no_branches()
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Items.Count.Should().Be(0);

        _filterToolBar.GetTestAccessor().ApplyCustomBranchFilter(true);

        _revisionGridFilter.Received().SetAndApplyBranchFilter(string.Empty);
        _filterToolBar.GetTestAccessor().IsApplyingFilter.Should().BeFalse();
    }

    [StaFact]
    public void ApplyBranchFilter_should_invoke_RevisionGridFilter_with_NoResultsFound()
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Items.AddRange("one", "two");
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text = TranslatedStrings.NoResultsFound;

        _filterToolBar.GetTestAccessor().ApplyCustomBranchFilter(true);

        _revisionGridFilter.Received().SetAndApplyBranchFilter(string.Empty);
        _filterToolBar.GetTestAccessor().IsApplyingFilter.Should().BeFalse();
    }

    [StaFact]
    public void ApplyBranchFilter_should_invoke_RevisionGridFilter_with_branches()
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Items.AddRange("one", "two");
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text = "on";
        _filterToolBar.GetTestAccessor().RefreshRevisionFunction(x =>
            new List<IGitRef>
            {
                new GitRef(_filterToolBar.GetTestAccessor().GetModule(),
                    ObjectId.Random(),
                    GitRefName.GetFullBranchName(_filterToolBar.GetTestAccessor().TscboBranchFilter.Text))
            });

        _filterToolBar.GetTestAccessor().ApplyCustomBranchFilter(true);

        _revisionGridFilter.Received().SetAndApplyBranchFilter("on");
        _filterToolBar.GetTestAccessor().IsApplyingFilter.Should().BeFalse();
    }

    [StaFact]
    public void ApplyBranchFilter_should_allow_multiple_branches()
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Items.AddRange("one", "two");
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text = "one two";
        _filterToolBar.GetTestAccessor().RefreshRevisionFunction(x =>
            new List<IGitRef>
            {
                new GitRef(_filterToolBar.GetTestAccessor().GetModule(),
                    ObjectId.Random(),
                    GitRefName.GetFullBranchName("one")),
                new GitRef(_filterToolBar.GetTestAccessor().GetModule(),
                    ObjectId.Random(),
                    GitRefName.GetFullBranchName("two"))
            });

        _filterToolBar.GetTestAccessor().ApplyCustomBranchFilter(true);

        _revisionGridFilter.Received().SetAndApplyBranchFilter("one two");
        _filterToolBar.GetTestAccessor().IsApplyingFilter.Should().BeFalse();
    }

    [StaFact]
    public void ApplyBranchFilter_should_allow_git_option()
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Items.AddRange("one", "two");
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text = "--some_option";

        // Empty git ref list
        _filterToolBar.GetTestAccessor().RefreshRevisionFunction(x => new List<IGitRef>());

        _filterToolBar.GetTestAccessor().ApplyCustomBranchFilter(true);

        _revisionGridFilter.Received().SetAndApplyBranchFilter("--some_option");
        _filterToolBar.GetTestAccessor().IsApplyingFilter.Should().BeFalse();
    }

    [StaFact]
    public void ApplyBranchFilter_should_allow_wildcard_branch()
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Items.AddRange("one", "two");
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text = "wildcard*";

        // Empty git ref list
        _filterToolBar.GetTestAccessor().RefreshRevisionFunction(x => new List<IGitRef>());

        _filterToolBar.GetTestAccessor().ApplyCustomBranchFilter(true);

        _revisionGridFilter.Received().SetAndApplyBranchFilter("wildcard*");
        _filterToolBar.GetTestAccessor().IsApplyingFilter.Should().BeFalse();
    }

    [StaTheory]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, true, true)]
    public void ApplyRevisionFilter_should_invoke_RevisionGridFilter(bool byCommit, bool byCommitter, bool byAuthor,
        bool byDiffContains)
    {
        _filterToolBar.GetTestAccessor().TstxtRevisionFilter.Text = "on";
        _filterToolBar.GetTestAccessor().TsmiCommitFilter.Checked = byCommit;
        _filterToolBar.GetTestAccessor().TsmiCommitterFilter.Checked = byCommitter;
        _filterToolBar.GetTestAccessor().TsmiAuthorFilter.Checked = byAuthor;
        _filterToolBar.GetTestAccessor().TsmiDiffContainsFilter.Checked = byDiffContains;

        _filterToolBar.GetTestAccessor().ApplyRevisionFilter();

        _revisionGridFilter.Received().SetAndApplyRevisionFilter(
            Arg.Is<RevisionFilter>(revisionFilter =>
                AssertFilter(revisionFilter, "on", byCommit, byCommitter, byAuthor, byDiffContains)));

        _filterToolBar.GetTestAccessor().IsApplyingFilter.Should().BeFalse();
    }

    [StaFact]
    public void SetBranchFilter_should_reset_text()
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text = "bla";

        _filterToolBar.SetBranchFilter(string.Empty);

        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text.Should().Be(string.Empty);
    }

    [StaTheory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("foo", "foo")]
    public void SetBranchFilter(string? given, string expected)
    {
        _filterToolBar.GetTestAccessor().TscboBranchFilter.Items.AddRange("one", "two");

        _filterToolBar.SetBranchFilter(given);

        _filterToolBar.GetTestAccessor().TscboBranchFilter.Text.Should().Be(expected);
        _revisionGridFilter.Received().SetAndApplyBranchFilter(expected);
    }

    [StaTheory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void SetRevisionFilter_when_empty(string? given, string expected)
    {
        _filterToolBar.SetRevisionFilter(given);

        _filterToolBar.GetTestAccessor().TstxtRevisionFilter.Text.Should().Be(expected);
        _revisionGridFilter.DidNotReceive().SetAndApplyRevisionFilter(Arg.Any<RevisionFilter>());
    }

    [StaTheory]
    [InlineData("foo", "foo")]
    public void SetRevisionFilter(string? given, string expected)
    {
        _filterToolBar.SetRevisionFilter(given);

        _filterToolBar.GetTestAccessor().TstxtRevisionFilter.Text.Should().Be(expected);
        _revisionGridFilter.Received().SetAndApplyRevisionFilter(
            Arg.Is<RevisionFilter>(revisionFilter => AssertFilter(revisionFilter, expected,
                _filterToolBar.GetTestAccessor().TsmiCommitFilter.Checked,
                _filterToolBar.GetTestAccessor().TsmiCommitterFilter.Checked,
                _filterToolBar.GetTestAccessor().TsmiAuthorFilter.Checked,
                _filterToolBar.GetTestAccessor().TsmiDiffContainsFilter.Checked)));
    }

    private static bool AssertFilter(RevisionFilter receivedRevisionFilter, string filter, bool byCommit,
        bool byCommitter, bool byAuthor, bool byDiffContains)
    {
        receivedRevisionFilter.Should().NotBeNull();
        receivedRevisionFilter.Text.Should().Be(filter);
        receivedRevisionFilter.FilterByAuthor.Should().Be(byAuthor);
        receivedRevisionFilter.FilterByCommit.Should().Be(byCommit);
        receivedRevisionFilter.FilterByCommitter.Should().Be(byCommitter);
        receivedRevisionFilter.FilterByDiffContent.Should().Be(byDiffContains);
        return true;
    }

    [StaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowOnlyFirstParent_should_be_bound_via_FilterChanged(bool settingValue)
    {
        var original = AppSettings.ShowOnlyFirstParent;
        try
        {
            AppSettings.ShowOnlyFirstParent = settingValue;

            _revisionGridFilter.FilterChanged +=
                Raise.EventWith(_revisionGridFilter, new FilterChangedEventArgs(new FilterInfo()));
            _filterToolBar.GetTestAccessor().TsmiShowOnlyFirstParent.Checked.Should().Be(settingValue);
        }
        finally
        {
            AppSettings.ShowOnlyFirstParent = original;
        }
    }

    [StaFact]
    public void ShowOnlyFirstParent_should_invoke_ToggleShowOnlyFirstParent()
    {
        _filterToolBar.GetTestAccessor().TsmiShowOnlyFirstParent.PerformClick();
        _revisionGridFilter.Received(1).ToggleShowOnlyFirstParent();
    }

    [StaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowReflog_should_be_bound_via_FilterChanged(bool settingValue)
    {
        bool original = AppSettings.ShowReflogReferences;
        try
        {
            AppSettings.ShowReflogReferences.Value = settingValue;

            _revisionGridFilter.FilterChanged +=
                Raise.EventWith(_revisionGridFilter, new FilterChangedEventArgs(new FilterInfo()));
            _filterToolBar.GetTestAccessor().TsbShowReflog.Checked.Should().Be(settingValue);
        }
        finally
        {
            AppSettings.ShowReflogReferences.Value = original;
        }
    }

    [StaFact]
    public void ShowReflogButton_should_invoke_ToggleShowReflogReferences()
    {
        _filterToolBar.GetTestAccessor().TsbShowReflog.PerformClick();
        _revisionGridFilter.Received(1).ToggleShowReflogReferences();
    }

    [StaFact]
    public void ShowBranches_ShowAll_should_invoke_ShowAllBranches()
    {
        _filterToolBar.GetTestAccessor().TsmiShowBranchesAll.PerformClick();
        _revisionGridFilter.Received(1).ShowAllBranches();
    }

    [StaFact]
    public void ShowBranches_ShowCurrent_should_invoke_ShowCurrentBranchOnly()
    {
        _filterToolBar.GetTestAccessor().TsmiShowBranchesCurrent.PerformClick();
        _revisionGridFilter.Received(1).ShowCurrentBranchOnly();
    }

    [StaFact]
    public void ShowBranches_ShowFiltered_should_invoke_ShowFilteredBranches()
    {
        _filterToolBar.GetTestAccessor().TsmiShowBranchesFiltered.PerformClick();
        _revisionGridFilter.Received(1).ShowFilteredBranches();
    }
}
