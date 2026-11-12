using GitCommands.Git;
using GitCommands.Settings;
using GitExtUtils;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.Infrastructure;
using GitUI.Shells;
using ResourceManager.Hotkey;

namespace GitUI.CommandsDialogs.Menus;

internal partial class ToolsToolStripMenuItem : ToolStripMenuItemEx
{
    public ToolsToolStripMenuItem()
    {
        InitializeComponent();

        if (!OperatingSystem.IsWindows())
        {
            toolStripSeparator6.Visible = false;
            PuTTYToolStripMenuItem.Visible = false;
        }
    }

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        gitBashToolStripMenuItem.Tag = UiCommands.GetRequiredService<IShellProvider>().GetShell(BashShell.ShellName);
    }

    public override void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
    {
        gitBashToolStripMenuItem.ShortcutKeyDisplayString = hotkeys.GetShortcutDisplay(FormBrowse.Command.GitBash);
        gitGUIToolStripMenuItem.ShortcutKeyDisplayString = hotkeys.GetShortcutDisplay(FormBrowse.Command.GitGui);
        kGitToolStripMenuItem.ShortcutKeyDisplayString = hotkeys.GetShortcutDisplay(FormBrowse.Command.GitGitK);
        settingsToolStripMenuItem.ShortcutKeyDisplayString =
            hotkeys.GetShortcutDisplay(FormBrowse.Command.OpenSettings);

        base.RefreshShortcutKeys(hotkeys);
    }

    public override void RefreshState(bool bareRepository)
    {
        gitGUIToolStripMenuItem.Enabled = !bareRepository;

        base.RefreshState(bareRepository);
    }

    private void GitcommandLogToolStripMenuItemClick(object sender, EventArgs e)
    {
        FormGitCommandLog.ShowOrActivate(OwnerForm!);
    }

    private void GitGuiToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.Module.RunGui();
    }

    private void KGitToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.Module.RunGitK();
    }

    private void StartAuthenticationAgentToolStripMenuItemClick(object sender, EventArgs e)
    {
        PuttyHelpers.StartPageant(UiCommands.Module.WorkingDir);
    }

    private void GenerateOrImportKeyToolStripMenuItemClick(object sender, EventArgs e)
    {
        PuttyHelpers.StartPuttygen(UiCommands.Module.WorkingDir);
    }

    private void OnShowSettingsClick(object sender, EventArgs e)
    {
        var translation = AppSettings.Translation;
        var commitInfoPosition = AppSettings.CommitInfoPosition;

        UiCommands.StartSettingsDialog(OwnerForm);

        SettingsChanged?.Invoke(sender, new SettingsChangedEventArgs(translation, commitInfoPosition));
    }

    private void gitBashToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (gitBashToolStripMenuItem.Tag is not IShellDescriptor shell)
        {
            return;
        }

        try
        {
            Validates.NotNull(shell.ExecutablePath);

            Executable executable = new(shell.ExecutablePath, UiCommands.Module.WorkingDir);
            executable.Start(createWindow: true, throwOnErrorExit: false); // throwOnErrorExit would redirect the output
        }
        catch (Exception exception)
        {
            MessageBoxes.FailedToRunShell(OwnerForm, shell.Name, exception);
        }
    }
}
