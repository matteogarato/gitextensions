using GitCommands.Git;
using GitCommands.Settings;
using GitUI.CommandsDialogs.SettingsDialog.ShellExtension;
using ResourceManager;

namespace GitUI.CommandsDialogs.SettingsDialog.Pages;

public partial class ShellExtensionSettingsPage : SettingsPageWithHeader
{
    private const char _checkedInMenu = '0';
    private const char _indeterminateInSubMenu = '1';
    private const char _uncheckedNotInMenu = '2';

    private readonly TranslationString _menuHelp = new(@"* Checked: at top level for direct access
* Intermediate: in a cascaded context menu
* Unchecked: not added to the menu");

    private readonly TranslationString _noItems = new("no items");

    private bool _isLoading;

    public ShellExtensionSettingsPage(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        InitializeComponent();
        UpdateRegistrationStatus();
        InitializeComplete();

        // when the dock is set in the designer it causes weird visual artifacts in scaled Windows environments
        _NO_TRANSLATE_chlMenuEntries.Dock = DockStyle.Fill;

        toolTip1.SetToolTip(menuHelp, _menuHelp.Text);
    }

    protected override void SettingsToPage()
    {
        _isLoading = true;
        for (var i = 0; i < AppSettings.CascadeShellMenuItems.Length; i++)
        {
            switch (AppSettings.CascadeShellMenuItems[i])
            {
                case _checkedInMenu:
                    _NO_TRANSLATE_chlMenuEntries.SetItemCheckState(i, CheckState.Checked);
                    break;
                case _indeterminateInSubMenu:
                    _NO_TRANSLATE_chlMenuEntries.SetItemCheckState(i, CheckState.Indeterminate);
                    break;
                case _uncheckedNotInMenu:
                    _NO_TRANSLATE_chlMenuEntries.SetItemCheckState(i, CheckState.Unchecked);
                    break;
            }
        }

        _isLoading = false;

        cbAlwaysShowAllCommands.Checked = AppSettings.AlwaysShowAllCommands;

        UpdatePreview();

        base.SettingsToPage();
    }

    protected override void PageToSettings()
    {
        var lCascadeShellMenuItems = "";

        for (var i = 0; i < _NO_TRANSLATE_chlMenuEntries.Items.Count; i++)
        {
            switch (_NO_TRANSLATE_chlMenuEntries.GetItemCheckState(i))
            {
                case CheckState.Indeterminate:
                    lCascadeShellMenuItems += _indeterminateInSubMenu;
                    break;
                case CheckState.Checked:
                    lCascadeShellMenuItems += _checkedInMenu;
                    break;
                case CheckState.Unchecked:
                    lCascadeShellMenuItems += _uncheckedNotInMenu;
                    break;
            }
        }

        AppSettings.CascadeShellMenuItems = lCascadeShellMenuItems;
        AppSettings.AlwaysShowAllCommands = cbAlwaysShowAllCommands.Checked;

        base.PageToSettings();
    }

    private void chlMenuEntries_SelectedValueChanged(object sender, EventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var topLevel = "";
        var cascaded = "";

        for (var i = 0; i < _NO_TRANSLATE_chlMenuEntries.Items.Count; i++)
        {
            switch (_NO_TRANSLATE_chlMenuEntries.GetItemCheckState(i))
            {
                case CheckState.Checked:
                    topLevel += "GitExt " + _NO_TRANSLATE_chlMenuEntries.Items[i] + "\r\n";
                    break;
                case CheckState.Indeterminate:
                    cascaded += "       " + _NO_TRANSLATE_chlMenuEntries.Items[i] + "\r\n";
                    break;
            }
        }

        labelPreview.Text = topLevel;
        if (!string.IsNullOrWhiteSpace(cascaded))
        {
            labelPreview.Text += "Git Extensions > \r\n" + cascaded;
        }
        else if (string.IsNullOrWhiteSpace(topLevel))
        {
            labelPreview.Text += $"({_noItems.Text})";
        }
    }

    private void RegisterButton_Click(object sender, EventArgs e)
    {
        ShellExtensionManager.Register();
        UpdateRegistrationStatus();
    }

    private void UnregisterButton_Click(object sender, EventArgs e)
    {
        ShellExtensionManager.Unregister();
        UpdateRegistrationStatus();
    }

    private void UpdateRegistrationStatus()
    {
        gbExplorerIntegration.Enabled = ShellExtensionManager.FilesExist();
        RegisterButton.Enabled = !ShellExtensionManager.IsRegistered();
    }

    private void chlMenuEntries_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        switch (e.CurrentValue)
        {
            case CheckState.Checked:
                e.NewValue = CheckState.Unchecked;
                break;

            case CheckState.Indeterminate:
                e.NewValue = CheckState.Checked;
                break;

            case CheckState.Unchecked:
                e.NewValue = CheckState.Indeterminate;
                break;
        }
    }

    private void menuHelp_Click(object sender, EventArgs e)
    {
        OsShellUtil.OpenUrlInDefaultBrowser(UserManual.UserManual.UrlFor("settings", "shell-extension"));
    }
}
