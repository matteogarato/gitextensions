using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtUtils;
using ResourceManager;

namespace GitExtensions.Plugins.ProxySwitcher;

public partial class ProxySwitcherForm : GitExtensionsFormBase
{
    private readonly IGitModule _gitCommands;
    private readonly ProxySwitcherPlugin _plugin;
    private readonly SettingsSource _settings;

    /// <summary>
    ///     Default constructor added to register all strings to be translated
    ///     Use the other constructor:
    ///     ProxySwitcherForm(IGitPluginSettingsContainer settings, GitUIBaseEventArgs gitUiCommands)
    /// </summary>
    public ProxySwitcherForm()
    {
        InitializeComponent();

        _plugin = null!;
        _settings = null!;
        _gitCommands = null!;
    }

    public ProxySwitcherForm(ProxySwitcherPlugin plugin, SettingsSource settings, GitUiEventArgs gitUiCommands)
    {
        InitializeComponent();
        InitializeComplete();

        Text = _pluginDescription.Text;
        _plugin = plugin;
        _settings = settings;
        _gitCommands = gitUiCommands.GitModule;
    }

    [GeneratedRegex(@":(.*)@", RegexOptions.ExplicitCapture)]
    private static partial Regex PasswordRegex { get; }

    private void ProxySwitcherForm_Load(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_plugin.HttpProxy.ValueOrDefault(_settings)))
        {
            MessageBoxes.ShowError(this, _pleaseSetProxy.Text, Text);
            Close();
        }
        else
        {
            RefreshProxy();
        }
    }

    private void RefreshProxy()
    {
        LocalHttpProxy_TextBox.Text = HidePassword(_gitCommands.GetEffectiveSetting("http.proxy"));
        GlobalHttpProxy_TextBox.Text =
            HidePassword(
                new GitConfigSettings(_gitCommands.GitExecutable, GitSettingLevel.Global).GetValue("http.proxy") ?? "");
        ApplyGlobally_CheckBox.Checked = string.Equals(LocalHttpProxy_TextBox.Text, GlobalHttpProxy_TextBox.Text);
    }

    private static string HidePassword(string httpProxy)
    {
        return PasswordRegex.Replace(httpProxy, ":****@");
    }

    private string BuildHttpProxy()
    {
        StringBuilder sb = new();
        sb.Append('"');
        var username = _plugin.Username.ValueOrDefault(_settings);
        if (!string.IsNullOrEmpty(username))
        {
            var password = _plugin.Password.ValueOrDefault(_settings);
            sb.Append(username);
            if (!string.IsNullOrEmpty(password))
            {
                sb.Append(':');
                sb.Append(password);
            }

            sb.Append('@');
        }

        sb.Append(_plugin.HttpProxy.ValueOrDefault(_settings));
        var port = _plugin.HttpProxyPort.ValueOrDefault(_settings);
        if (!string.IsNullOrEmpty(port))
        {
            sb.Append(':');
            sb.Append(port);
        }

        sb.Append('"');
        return sb.ToString();
    }

    private void SetProxy_Button_Click(object sender, EventArgs e)
    {
        var httpProxy = BuildHttpProxy();

        GitArgumentBuilder args = new("config")
        {
            { ApplyGlobally_CheckBox.Checked, "--global" }, "http.proxy", httpProxy
        };
        _gitCommands.GitExecutable.GetOutput(args);

        RefreshProxy();
    }

    private void UnsetProxy_Button_Click(object sender, EventArgs e)
    {
        var arguments = ApplyGlobally_CheckBox.Checked
            ? "config --global --unset http.proxy"
            : "config --unset http.proxy";

        _gitCommands.GitExecutable.GetOutput(arguments);

        RefreshProxy();
    }

    #region Translation

    private readonly TranslationString _pluginDescription = new("Proxy Switcher");

    private readonly TranslationString _pleaseSetProxy =
        new("There is no proxy configured. Please set the proxy host in the plugin settings.");

    #endregion
}
