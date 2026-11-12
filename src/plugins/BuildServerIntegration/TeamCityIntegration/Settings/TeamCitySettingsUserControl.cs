using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI.Theming;
using GitUIPluginInterfaces.BuildServerIntegration;
using ResourceManager;

namespace GitExtensions.Plugins.TeamCityIntegration.Settings;

[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(TeamCityAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class TeamCitySettingsUserControl : GitExtensionsControl, IBuildServerSettingsUserControl
{
    private readonly TranslationString _failToExtractDataFromClipboardCaption = new("Build url not valid");

    private readonly TranslationString _failToExtractDataFromClipboardMessage = new(
        "The clipboard doesn't contain a valid build url." + Environment.NewLine + Environment.NewLine +
        "Please copy in the clipboard the url of the build before retrying." + Environment.NewLine +
        "(Should contain at least the \"buildTypeId\" parameter)");

    private readonly TranslationString
        _failToLoadProjectCaption = new("Error when loading the projects and build list");

    private readonly TranslationString _failToLoadProjectMessage = new("Failed to load the projects and build list." +
                                                                       Environment.NewLine +
                                                                       "Please verify the server url.");

    private readonly TeamCityAdapter _teamCityAdapter = new();
    private string? _defaultProjectName;

    public TeamCitySettingsUserControl()
    {
        InitializeComponent();
        labelRegexError.SetForeColorForBackColor();
        InitializeComplete();

        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
    }

    [GeneratedRegex(@"(\?|\&)(?<buildtypeid>[^=]+)\=(?<buildtype>[^&]+)", RegexOptions.ExplicitCapture)]
    private static partial Regex TeamcityBuildUrl { get; }

    void IBuildServerSettingsUserControl.Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        _defaultProjectName = defaultProjectName;
        SetChooseBuildButtonState();
    }

    void IBuildServerSettingsUserControl.LoadSettings(SettingsSource buildServerConfig)
    {
        TeamCityServerUrl.Text = buildServerConfig.GetString("BuildServerUrl", null);
        TeamCityProjectName.Text = buildServerConfig.GetString("ProjectName", _defaultProjectName);
        TeamCityBuildIdFilter.Text = buildServerConfig.GetString("BuildIdFilter", null);
        CheckBoxLogAsGuest.CheckState = SetNullableChecked(buildServerConfig.GetBool("LogAsGuest", false));
        return;

        static CheckState SetNullableChecked(bool? value)
        {
            return value.HasValue
                ? value.Value ? CheckState.Checked : CheckState.Unchecked
                : CheckState.Indeterminate;
        }
    }

    void IBuildServerSettingsUserControl.SaveSettings(SettingsSource buildServerConfig)
    {
        if (!BuildServerSettingsHelper.IsRegexValid(TeamCityBuildIdFilter.Text))
        {
            return;
        }

        // Empty string is handled as unset, not overriding lower priority levels
        buildServerConfig.SetString("BuildServerUrl", TeamCityServerUrl.Text.NullIfEmpty());
        buildServerConfig.SetString("ProjectName", TeamCityProjectName.Text.NullIfEmpty());
        buildServerConfig.SetString("BuildIdFilter", TeamCityBuildIdFilter.Text.NullIfEmpty());
        buildServerConfig.SetBool("LogAsGuest", NullIfIndeterminate(CheckBoxLogAsGuest));
        return;

        // if the setting is empty, do not set any value (as this could override lower priority levels)
        static bool? NullIfIndeterminate(CheckBox s)
        {
            return s.CheckState == CheckState.Indeterminate ? null : s.Checked;
        }
    }

    private void TeamCityBuildIdFilter_TextChanged(object sender, EventArgs e)
    {
        labelRegexError.Visible = !BuildServerSettingsHelper.IsRegexValid(TeamCityBuildIdFilter.Text);
    }

    private void buttonProjectChooser_Click(object sender, EventArgs e)
    {
        try
        {
            TeamCityBuildChooser teamCityBuildChooser =
                new(TeamCityServerUrl.Text, TeamCityProjectName.Text, TeamCityBuildIdFilter.Text);
            var result = teamCityBuildChooser.ShowDialog(this);

            if (result == DialogResult.OK)
            {
                TeamCityProjectName.Text = teamCityBuildChooser.TeamCityProjectName;
                TeamCityBuildIdFilter.Text = teamCityBuildChooser.TeamCityBuildIdFilter;
            }
        }
        catch
        {
            MessageBoxes.ShowError(this, _failToLoadProjectMessage.Text, _failToLoadProjectCaption.Text);
        }
    }

    private void TeamCityServerUrl_TextChanged(object sender, EventArgs e)
    {
        SetChooseBuildButtonState();
    }

    private void SetChooseBuildButtonState()
    {
        buttonProjectChooser.Enabled = !string.IsNullOrWhiteSpace(TeamCityServerUrl.Text);
    }

    private void lnkExtractDataFromBuildUrlCopiedInTheClipboard_LinkClicked(object sender,
        LinkLabelLinkClickedEventArgs e)
    {
        if (Clipboard.ContainsText() && Clipboard.GetText().Contains("buildTypeId="))
        {
            Uri buildUri = new(Clipboard.GetText());
            var teamCityServerUrl = buildUri.Scheme + "://" + buildUri.Authority;
            TeamCityServerUrl.Text = teamCityServerUrl;
            _teamCityAdapter.InitializeHttpClient(teamCityServerUrl);

            var paramResults = TeamcityBuildUrl.Matches(buildUri.Query);
            foreach (Match paramResult in paramResults)
            {
                if (paramResult.Success)
                {
                    if (paramResult.Groups["buildtypeid"].ValueSpan is "buildTypeId")
                    {
                        var buildType = _teamCityAdapter.GetBuildType(paramResult.Groups["buildtype"].Value);
                        TeamCityProjectName.Text = buildType.ParentProject;
                        TeamCityBuildIdFilter.Text = buildType.Id;
                        return;
                    }
                }
            }
        }

        MessageBoxes.Show(this, _failToExtractDataFromClipboardMessage.Text,
            _failToExtractDataFromClipboardCaption.Text,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
