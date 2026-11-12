using CommonTestUtils;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.CommandsDialogs;
using AppSettings = GitCommands.Settings.AppSettings;

namespace UITests.CommandsDialogs;

public class FormPullTests : IDisposable
{
    // Created once for each test
    private readonly GitUiCommands _commands = null!;
    private readonly bool _originalAutoStash;
    private readonly GitPullAction _originalDefaultPullAction;

    private readonly GitPullAction _originalFormPullAction;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormPullTests()
    {
        _originalDefaultPullAction = AppSettings.DefaultPullAction;
        _originalFormPullAction = AppSettings.FormPullAction;
        _originalAutoStash = AppSettings.AutoStash;

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
        AppSettings.DefaultPullAction = _originalDefaultPullAction;
        AppSettings.FormPullAction = _originalFormPullAction;
        AppSettings.AutoStash = _originalAutoStash;

        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Should_correctly_setup_form_title_for_merge_action()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.Merge.Checked = true;
                var expected = string.Format(accessor.PullTitleText,
                    PathUtil.GetDisplayPath(_referenceRepository.Module.WorkingDir));

                accessor.Title.Should().StartWith(expected);
            },
            null!, null!,
            //// select an action different from Merge
            GitPullAction.FetchAll);
    }

    [WinFormsFact]
    public void Should_correctly_setup_form_title_for_rebase_action()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.Rebase.Checked = true;
                var expected = string.Format(accessor.PullTitleText,
                    PathUtil.GetDisplayPath(_referenceRepository.Module.WorkingDir));

                accessor.Title.Should().StartWith(expected);
            },
            null!, null!,
            //// select an action different from Rebase
            GitPullAction.FetchAll);
    }

    [WinFormsFact]
    public void Should_correctly_setup_form_title_for_fetch_action()
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.Fetch.Checked = true;
                var expected = string.Format(accessor.FetchTitleText,
                    PathUtil.GetDisplayPath(_referenceRepository.Module.WorkingDir));

                accessor.Title.Should().StartWith(expected);
            },
            null!, null!,
            //// select an action different from None/fetch
            GitPullAction.Merge);
    }

    [WinFormsTheory]
    [InlineData(GitPullAction.None, true, false, false, false, false, false, false, false)]
    [InlineData(GitPullAction.Merge, true, false, false, false, false, false, false, false)]
    [InlineData(GitPullAction.Rebase, false, false, false, true, false, false, false, false)]
    [InlineData(GitPullAction.Fetch, false, false, false, false, true, false, true, true)]
    [InlineData(GitPullAction.FetchAll, false, false, false, false, true, false, true, true)]
    [InlineData(GitPullAction.FetchPruneAll, false, true, false, false, true, false, true, true)]
    public void Should_correctly_setup_for_defined_pull_action(GitPullAction pullAction,
        bool mergeChecked, bool pruneRemoteBranches, bool pruneRemoteBranchesAndTags, bool rebaseChecked,
        bool fetchChecked, bool autoStashChecked, bool pruneRemoteBranchesEnabled,
        bool pruneRemoteBranchesAndTagsEnabled)
    {
        var defaultPullAction = AppSettings.DefaultPullAction;
        AppSettings.DefaultPullAction = GitPullAction.Merge;
        RunFormTest(
            form =>
            {
                try
                {
                    var accessor = form.GetTestAccessor();

                    accessor.Merge.Checked.Should().Be(mergeChecked);
                    accessor.Prune.Checked.Should().Be(pruneRemoteBranches);
                    accessor.PruneTags.Checked.Should().Be(pruneRemoteBranchesAndTags);
                    accessor.Rebase.Checked.Should().Be(rebaseChecked);
                    accessor.Fetch.Checked.Should().Be(fetchChecked);
                    accessor.AutoStash.Checked.Should().Be(autoStashChecked);
                    accessor.Prune.Enabled.Should().Be(pruneRemoteBranchesEnabled);
                    accessor.PruneTags.Enabled.Should().Be(pruneRemoteBranchesAndTagsEnabled);
                    accessor.Remotes.Text.Should().Be("[ All ]");
                }
                finally
                {
                    AppSettings.DefaultPullAction = defaultPullAction;
                }
            },
            null!, null!, pullAction);
    }

    [WinFormsTheory]
    [InlineData(GitPullAction.None, true, false, false, false, false, false, false, false)]
    [InlineData(GitPullAction.Merge, true, false, false, false, false, false, false, false)]
    [InlineData(GitPullAction.Rebase, false, false, false, true, false, false, false, false)]
    [InlineData(GitPullAction.Fetch, false, false, false, false, true, false, true, true)]
    [InlineData(GitPullAction.FetchAll, false, false, false, false, true, false, true, true)]
    [InlineData(GitPullAction.FetchPruneAll, false, true, false, false, true, false, true, true)]
    public void Should_use_user_DefaultPullAction_pull_action_None(GitPullAction pullAction,
        bool mergeChecked, bool pruneRemoteBranches, bool pruneRemoteBranchesAndTags, bool rebaseChecked,
        bool fetchChecked, bool autoStashChecked, bool pruneRemoteBranchesEnabled,
        bool pruneRemoteBranchesAndTagsEnabled)
    {
        AppSettings.DefaultPullAction = pullAction;

        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.Merge.Checked.Should().Be(mergeChecked);
                accessor.Prune.Checked.Should().Be(pruneRemoteBranches);
                accessor.PruneTags.Checked.Should().Be(pruneRemoteBranchesAndTags);
                accessor.Rebase.Checked.Should().Be(rebaseChecked);
                accessor.Fetch.Checked.Should().Be(fetchChecked);
                accessor.AutoStash.Checked.Should().Be(autoStashChecked);
                accessor.Prune.Enabled.Should().Be(pruneRemoteBranchesEnabled);
                accessor.PruneTags.Enabled.Should().Be(pruneRemoteBranchesAndTagsEnabled);
                accessor.Remotes.Text.Should().Be("[ All ]");
            },
            null!, null!, GitPullAction.None);
    }

    [WinFormsTheory]
    [InlineData(true, false, false, GitPullAction.Merge)]
    [InlineData(false, true, false, GitPullAction.Rebase)]
    [InlineData(false, false, true, GitPullAction.Fetch)]
    public void Should_remember_user_choice_upon_pull_action(bool mergeChecked, bool rebaseChecked, bool fetchChecked,
        GitPullAction expectedFormPullAction)
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();

                accessor.Merge.Checked = mergeChecked;
                accessor.Rebase.Checked = rebaseChecked;
                accessor.Fetch.Checked = fetchChecked;

                accessor.UpdateSettingsDuringPull();

                AppSettings.FormPullAction.Should().Be(expectedFormPullAction);
            },
            null!, null!,
            //// select an action different from None/fetch
            GitPullAction.Merge);
    }

    [WinFormsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_remember_user_choice_upon_pull_auto_stash(bool autoStashChecked)
    {
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();
                accessor.AutoStash.Checked = autoStashChecked;

                accessor.UpdateSettingsDuringPull();

                AppSettings.AutoStash.Should().Be(autoStashChecked);
            },
            null!, null!,
            //// select an action different from None/fetch
            GitPullAction.Merge);
    }

    private void RunFormTest(Action<FormPull> testDriver, string? remoteBranch, string? remote,
        GitPullAction pullAction)
    {
        RunFormTest(
            form =>
            {
                testDriver(form);
                return Task.CompletedTask;
            },
            remoteBranch, remote, pullAction);
    }

    private void RunFormTest(Func<FormPull, Task> testDriverAsync, string? remoteBranch, string? remote,
        GitPullAction pullAction)
    {
        UiTest.RunForm(
            () =>
            {
                // False because we haven't performed any actions
                _commands.StartPullDialog(owner: null, remoteBranch: remoteBranch, remote: remote,
                    pullAction: pullAction).Should().BeFalse();
            },
            testDriverAsync);
    }
}
