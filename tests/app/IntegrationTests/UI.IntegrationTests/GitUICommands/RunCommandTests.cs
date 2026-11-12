using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitUI;
using GitUI.CommandsDialogs;
using AppSettings = GitCommands.Settings.AppSettings;

namespace UITests.GitUICommands;

public sealed class RunCommandTests : IDisposable, IClassFixture<RunCommandTestsFixture>
{
    // Created once for each test
    private GitUiCommands _commands = null!;

    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;

    public RunCommandTests(RunCommandTestsFixture fixture)
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

        var cmdPath = (Environment.GetEnvironmentVariable("COMSPEC") ?? "C:/WINDOWS/system32/cmd.exe").ToPosixPath()
            .QuoteNe();
        _referenceRepository.Module.GitExecutable.RunCommand($"config --local difftool.cmd.path {cmdPath}").Should()
            .BeTrue();
        _referenceRepository.Module.GitExecutable.RunCommand($"config --local mergetool.cmd.path {cmdPath}").Should()
            .BeTrue();
        _referenceRepository.Module.GitExecutable.RunCommand("config --local diff.guitool cmd").Should().BeTrue();
        _referenceRepository.Module.GitExecutable.RunCommand("config --local merge.guitool cmd").Should().BeTrue();

        AppSettings.UseConsoleEmulatorForCommands.Value = false;
        AppSettings.CloseProcessDialog = true;
        AppSettings.UseBrowseForFileHistory.Value = false;

        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_should_throw_on_null_args()
    {
        ((Action)(() => _commands.GetTestAccessor().RunCommandBasedOnArgument(null!)))
            .Should().Throw<NullReferenceException>();
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_should_throw_on_empty_args()
    {
        ((Action)(() => _commands.GetTestAccessor().RunCommandBasedOnArgument([])))
            .Should().Throw<ArgumentOutOfRangeException>();

        ((Action)(() => _commands.GetTestAccessor().RunCommandBasedOnArgument(["ge.exe"])))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_about()
    {
        UserEnvironmentInformation.Initialise("1234567", true);
        RunCommandBasedOnArgument<FormAbout>(["ge.exe", "about"]);
    }

    [WinFormsTheory]
    [InlineData("add")]
    [InlineData("addfiles")]
    public void RunCommandBasedOnArgument_add(string command)
    {
        RunCommandBasedOnArgument<FormAddFiles>(["ge.exe", command]);
    }

    [WinFormsTheory]
    [InlineData("apply")]
    [InlineData("applypatch")]
    public void RunCommandBasedOnArgument_apply(string command)
    {
        RunCommandBasedOnArgument<FormApplyPatch>(["ge.exe", command]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_blame_throws()
    {
        ((Action)(() => _commands.GetTestAccessor().RunCommandBasedOnArgument(["ge.exe", "blame"])))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_blame()
    {
        RunCommandBasedOnArgument<FormBlame>(["ge.exe", "blame", "filename"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_blame_line()
    {
        RunCommandBasedOnArgument<FormBlame>(["ge.exe", "blame", "filename", "42"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_branch()
    {
        var branchName = _referenceRepository.Module.GetCurrentBranchName();

        var newBranchName = $"{branchName}-subbranch";

        _referenceRepository.CheckoutBranch(branchName);
        RunCommandBasedOnArgument<FormCreateBranch>(["ge.exe", "branch"], runTest: form =>
        {
            SetText(form, "BranchNameTextBox", newBranchName);
            ClickButton(form, "cmdOk");
        });
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_browse()
    {
        var selected = ObjectId.Random();
        var first = ObjectId.Random();
        var otherIgnored = ObjectId.Random();
        RunCommandBasedOnArgument<FormBrowse>(["ge.exe", "browse", $"-commit={selected},{first},,{otherIgnored}"]);
    }

    [WinFormsTheory]
    [InlineData("checkout")]
    [InlineData("checkoutbranch")]
    public void RunCommandBasedOnArgument_checkout(string command)
    {
        RunCommandBasedOnArgument<FormCheckoutBranch>(["ge.exe", command], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_checkoutrevision()
    {
        RunCommandBasedOnArgument<FormCheckoutRevision>(["ge.exe", "checkoutrevision"], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_cherry()
    {
        RunCommandBasedOnArgument<FormCherryPick>(["ge.exe", "cherry"], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_cleanup()
    {
        RunCommandBasedOnArgument<FormCleanupRepository>(["ge.exe", "cleanup"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_clone()
    {
        RunCommandBasedOnArgument<FormClone>(["ge.exe", "clone"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_commit()
    {
        RunCommandBasedOnArgument<FormCommit>(["ge.exe", "commit"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_difftool_returns_false_on_missing_argument()
    {
        _commands.GetTestAccessor().RunCommandBasedOnArgument(["ge.exe", "difftool"])
            .Should().BeFalse();
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_difftool()
    {
        _commands.GetTestAccessor().RunCommandBasedOnArgument(["ge.exe", "difftool", "filename"]).Should().BeTrue();
    }

    [WinFormsTheory]
    [InlineData("blamehistory")]
    [InlineData("filehistory")]
    public void RunCommandBasedOnArgument_history_throws(
        string command)
    {
        ((Action)(() => _commands.GetTestAccessor().RunCommandBasedOnArgument(["ge.exe", command])))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [WinFormsTheory]
    [InlineData("blamehistory", false, false)]
    [InlineData("blamehistory", false, true)]
    [InlineData("blamehistory", true, false)]
    [InlineData("blamehistory", true, true)]
    [InlineData("filehistory", false, false)]
    [InlineData("filehistory", false, true)]
    [InlineData("filehistory", true, false)]
    [InlineData("filehistory", true, true)]
    public void RunCommandBasedOnArgument_history(
        string command,
        bool commit,
        bool filter)
    {
        var expectedTab = command.Contains("blame") ? "Blame" : "Diff";

        List<string> args = ["ge.exe", command, "filename"];
        if (commit)
        {
            args.Add(_referenceRepository.CommitHash!);
            if (filter)
            {
                args.Add("--filter-by-revision");
            }
        }

        RunCommandBasedOnArgument<FormFileHistory>([.. args],
            runTest: form => form.FindDescendantOfType<FullBleedTabControl>(_ => true)!.SelectedTab!.Text.Should()
                .Be(expectedTab));
    }

    [WinFormsTheory]
    [InlineData("blamehistory", 0)]
    [InlineData("blamehistory", 1)]
    [InlineData("blamehistory", 2)]
    [InlineData("blamehistory", 3)]
    [InlineData("filehistory", 0)]
    [InlineData("filehistory", 1)]
    [InlineData("filehistory", 2)]
    [InlineData("filehistory", 3)]
    public void RunCommandBasedOnArgument_history_returns_false(
        string command,
        int invalidVariant)
    {
        const bool ignored = false;
        (bool commitValid, bool filter, bool filterValid)[] invalidVariants =
        [
            (false, false, ignored),
            (false, true, false),
            (false, true, true),
            (true, true, false)
        ];
        (var commitValid, var filter, var filterValid) = invalidVariants[invalidVariant];

        List<string> args =
            ["ge.exe", command, "filename", commitValid ? _referenceRepository.CommitHash! : "no-commit"];
        if (filter)
        {
            args.Add(filterValid ? "--filter-by-revision" : "invalid");
        }

        _commands.GetTestAccessor().RunCommandBasedOnArgument([.. args]).Should().BeFalse();
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_fileeditor_throws()
    {
        ((Action)(() => _commands.GetTestAccessor().RunCommandBasedOnArgument(["ge.exe", "fileeditor"])))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_fileeditor()
    {
        RunCommandBasedOnArgument<FormEditor>(["ge.exe", "fileeditor", "filename"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_formatpatch()
    {
        RunCommandBasedOnArgument<FormFormatPatch>(["ge.exe", "formatpatch"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_gitignore()
    {
        RunCommandBasedOnArgument<FormGitIgnore>(["ge.exe", "gitignore"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_init()
    {
        RunCommandBasedOnArgument<FormInit>(["ge.exe", "init"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_merge()
    {
        RunCommandBasedOnArgument<FormMergeBranch>(["ge.exe", "merge"]);
    }

    [WinFormsTheory]
    [InlineData("mergeconflicts")]
    [InlineData("mergetool")]
    public void RunCommandBasedOnArgument_mergeconflicts(string command)
    {
        RunCommandBasedOnArgument<FormResolveConflicts>(["ge.exe", command]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_openrepo()
    {
        RunCommandBasedOnArgument<FormBrowse>(["ge.exe", "openrepo"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_pull()
    {
        RunCommandBasedOnArgument<FormPull>(["ge.exe", "pull"], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_push()
    {
        RunCommandBasedOnArgument<FormPush>(["ge.exe", "push"], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_rebase()
    {
        RunCommandBasedOnArgument<FormRebase>(["ge.exe", "rebase"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_remotes()
    {
        RunCommandBasedOnArgument<FormRemotes>(["ge.exe", "remotes"]);
    }

    [WinFormsTheory]
    [InlineData("revert")]
    [InlineData("reset")]
    public void RunCommandBasedOnArgument_reset(string command)
    {
        RunCommandBasedOnArgument<FormResetChanges>(["ge.exe", command], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_searchfile()
    {
        RunCommandBasedOnArgument<SearchWindow<string>>(["ge.exe", "searchfile"], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_settings()
    {
        RunCommandBasedOnArgument<FormSettings>(["ge.exe", "settings"],
            false /* because dialog is not closed using OK button */);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_stash()
    {
        RunCommandBasedOnArgument<FormStash>(["ge.exe", "stash"]);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_synchronize()
    {
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            while (true)
            {
                await UiTest.WaitForIdleAsync();
                var formPull = Application.OpenForms.OfType<FormPull>().FirstOrDefault();
                if (formPull is not null)
                {
                    formPull.Close();
                    break;
                }
            }

            while (true)
            {
                await UiTest.WaitForIdleAsync();
                var formPush = Application.OpenForms.OfType<FormPush>().FirstOrDefault();
                if (formPush is not null)
                {
                    formPush.Close();
                    break;
                }
            }
        });

        RunCommandBasedOnArgument<FormCommit>(["ge.exe", "synchronize"], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_tag()
    {
        RunCommandBasedOnArgument<FormCreateTag>(["ge.exe", "tag"], false);
    }

    [WinFormsFact]
    public void RunCommandBasedOnArgument_viewdiff()
    {
        RunCommandBasedOnArgument<FormLog>(["ge.exe", "viewdiff"], false);
    }

    [WinFormsTheory]
    [InlineData("git://")]
    [InlineData("http://")]
    [InlineData("https://")]
    [InlineData("github-windows://openRepo/")]
    [InlineData("github-mac://openRepo/")]
    public void RunCommandBasedOnArgument_Url(string url)
    {
        RunCommandBasedOnArgument<FormClone>(["ge.exe", url]);
    }

    [WinFormsTheory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("help")]
    [InlineData("nonsense")]
    public void RunCommandBasedOnArgument_unsupported(string command)
    {
        RunCommandBasedOnArgument<FormCommandlineHelp>(["ge.exe", command]);
    }

    private void RunCommandBasedOnArgument<TForm>(string[] args, bool expectedResult = true,
        Action<TForm>? runTest = null) where TForm : Form
    {
        UiTest.RunForm<TForm>(
            () => _commands.GetTestAccessor().RunCommandBasedOnArgument(args).Should().Be(expectedResult),
            form =>
            {
                runTest?.Invoke(form);

                return Task.CompletedTask;
            });
    }

    private static void ClickButton(Form form, string buttonName)
    {
        form.FindDescendantOfType<Button>(button => button.Name == buttonName)!.PerformClick();
    }

    private static void SetText(Form form, string controlName, string value)
    {
        form.FindDescendantOfType<Control>(control => control.Name == controlName)!.Text = value;
    }
}

public sealed class RunCommandTestsFixture
{
    public RunCommandTestsFixture()
    {
        AppSettings.SetDocumentationBaseUrl("33.33.33");
    }
}
