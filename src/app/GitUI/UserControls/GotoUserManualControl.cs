using GitCommands.Git;
using ResourceManager;

namespace GitUI.UserControls;

public partial class GotoUserManualControl : GitExtensionsControl
{
    private readonly TranslationString _gotoUserManualControlTooltip =
        new("Read more about this feature at {0}");

    private bool _isLoaded;

    private string _manualSectionAnchorName = null!;

    private string _manualSectionSubfolder = null!;

    public GotoUserManualControl()
    {
        InitializeComponent();
        InitializeComplete();
    }

    public string ManualSectionAnchorName
    {
        get => _manualSectionAnchorName;
        set
        {
            _manualSectionAnchorName = value;
            if (_isLoaded)
            {
                UpdateTooltip();
            }
        }
    }

    public string ManualSectionSubfolder
    {
        get => _manualSectionSubfolder;
        set
        {
            _manualSectionSubfolder = value;
            if (_isLoaded)
            {
                UpdateTooltip();
            }
        }
    }

    private void GotoUserManualControl_Load(object sender, EventArgs e)
    {
        _isLoaded = true;
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        var caption = string.Format(_gotoUserManualControlTooltip.Text, GetUrl());
        toolTip1.SetToolTip(pictureBoxHelpIcon, caption);
        toolTip1.SetToolTip(linkLabelHelp, caption);
    }

    private void OpenManual()
    {
        var url = GetUrl();
        OsShellUtil.OpenUrlInDefaultBrowser(url);
    }

    private string GetUrl()
    {
        return UserManual.UserManual.UrlFor(ManualSectionSubfolder, ManualSectionAnchorName);
    }

    private void linkLabelHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        OpenManual();
    }
}
