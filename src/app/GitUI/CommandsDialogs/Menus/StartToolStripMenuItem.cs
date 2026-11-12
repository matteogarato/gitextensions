using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.CommandsDialogs.BrowseDialog;
using ResourceManager.Hotkey;

namespace GitUI.CommandsDialogs.Menus;

internal partial class StartToolStripMenuItem : ToolStripMenuItemEx
{
    private IRepositoryHistoryUiService? _repositoryHistoryUiService;

    public StartToolStripMenuItem()
    {
        InitializeComponent();
    }

    internal ToolStripMenuItem OpenRepositoryMenuItem
    {
        get => openToolStripMenuItem;
    }

    internal ToolStripMenuItem FavouriteRepositoriesMenuItem
    {
        get => tsmiFavouriteRepositories;
    }

    public event EventHandler<GitModuleEventArgs>? GitModuleChanged;
    public event EventHandler? RecentRepositoriesCleared;

    /// <summary>
    ///     Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();

            _repositoryHistoryUiService?.GitModuleChanged -= repositoryHistoryUIService_GitModuleChanged;
        }

        base.Dispose(disposing);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _repositoryHistoryUiService = UiCommands.GetRequiredService<IRepositoryHistoryUiService>();
        _repositoryHistoryUiService.GitModuleChanged += repositoryHistoryUIService_GitModuleChanged;
    }

    public override void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
    {
        openToolStripMenuItem.ShortcutKeyDisplayString = hotkeys.GetShortcutDisplay(FormBrowse.Command.OpenRepo);

        base.RefreshShortcutKeys(hotkeys);
    }

    private void CloneToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartCloneDialog(OwnerForm, string.Empty, false, GitModuleChanged);
    }

    private void ExitToolStripMenuItemClick(object sender, EventArgs e)
    {
        OwnerForm?.Close();
    }

    private void InitNewRepositoryToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartInitializeDialog(OwnerForm, gitModuleChanged: GitModuleChanged);
    }

    private void OpenToolStripMenuItemClick(object sender, EventArgs e)
    {
        var module = FormOpenDirectory.OpenModule(OwnerForm!,
            UiCommands.GetRequiredService<IGitExecutorProvider>(), UiCommands.Module);
        if (module is not null)
        {
            GitModuleChanged?.Invoke(OwnerForm, new GitModuleEventArgs(module));
        }
    }

    private void repositoryHistoryUIService_GitModuleChanged(object? sender, GitModuleEventArgs e)
    {
        GitModuleChanged?.Invoke(this, e);
    }

    private void tsmiFavouriteRepositories_DropDownOpening(object sender, EventArgs e)
    {
        tsmiFavouriteRepositories.DropDown.SuspendLayout();
        tsmiFavouriteRepositories.DropDownItems.Clear();
        _repositoryHistoryUiService!.PopulateFavouriteRepositoriesMenu(tsmiFavouriteRepositories);
        tsmiFavouriteRepositories.DropDown.ResumeLayout();
    }

    private void tsmiRecentRepositories_DropDownOpening(object sender, EventArgs e)
    {
        // Note: repo-branch name cache is shared with the dashboard, no update needed
        tsmiRecentRepositories.DropDown.SuspendLayout();
        tsmiRecentRepositories.DropDownItems.Clear();
        _repositoryHistoryUiService!.PopulateRecentRepositoriesMenu(tsmiRecentRepositories);
        if (tsmiRecentRepositories.DropDownItems.Count < 1)
        {
            return;
        }

        tsmiRecentRepositories.DropDownItems.Add(clearRecentRepositoriesListToolStripMenuItem);
        tsmiRecentRepositories.DropDownItems.Add(tsmiRecentRepositoriesClear);
        tsmiRecentRepositories.DropDown.ResumeLayout();
    }

    private void tsmiRecentRepositoriesClear_Click(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ThreadHelper.JoinableTaskFactory.Run(() => RepositoryHistoryManager.Locals.SaveRecentHistoryAsync([]));
        RecentRepositoriesCleared?.Invoke(sender, e);
    }
}
