using GitCommands.Remotes;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Extensions;
using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.BuildServerIntegration;
using Microsoft.VisualStudio.Threading;
using ResourceManager;

namespace GitUI.CommandsDialogs.SettingsDialog.Pages;

public partial class BuildServerIntegrationSettingsPage : DistributedSettingsPage
{
    private readonly TranslationString _noneItem =
        new("None");

    private JoinableTask<object>? _populateBuildServerTypeTask;
    private IConfigFileRemoteSettingsManager? _remotesManager;

    public BuildServerIntegrationSettingsPage(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        InitializeComponent();
        InitializeComplete();
    }

    public override bool IsInstantSavePage
    {
        get => false;
    }

    protected override void Init(ISettingsPageHost pageHost)
    {
        base.Init(pageHost);

        _remotesManager = new ConfigFileRemoteSettingsManager(() => Module);
        _populateBuildServerTypeTask = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await TaskScheduler.Default.SwitchTo(true);

            var exports =
                ManagedExtensibility.GetExports<IBuildServerAdapter, IBuildServerTypeMetadata>();
            string[] buildServerTypes =
            [
                .. exports.Select(export =>
                {
                    var canBeLoaded = export.Metadata.CanBeLoaded;
                    return export.Metadata.BuildServerType.Combine(" - ", canBeLoaded)!;
                })
            ];

            await this.SwitchToMainThreadAsync();

            checkBoxEnableBuildServerIntegration.Enabled = true;
            checkBoxShowBuildResultPage.Enabled = true;
            BuildServerType.Enabled = true;

            BuildServerType.DataSource = new[] { _noneItem.Text }.Concat(buildServerTypes).ToArray();
            return BuildServerType.DataSource;
        });
    }

    protected override void SettingsToPage()
    {
        ThreadHelper.FileAndForget(async () =>
        {
            Validates.NotNull(_populateBuildServerTypeTask);

            await _populateBuildServerTypeTask.JoinAsync();

            await this.SwitchToMainThreadAsync();

            var currentSettings = GetCurrentSettings();

            checkBoxEnableBuildServerIntegration.SetNullableChecked(
                BuildServerSettings.IntegrationEnabled[currentSettings]);
            checkBoxShowBuildResultPage.SetNullableChecked(BuildServerSettings.ShowBuildResultPage[currentSettings]);

            BuildServerType.SelectedItem = BuildServerSettings.ServerName[currentSettings] ?? _noneItem.Text;
            ActivateBuildServerSettingsControl();

            base.SettingsToPage();
        });
    }

    protected override void PageToSettings()
    {
        var currentSettings = GetCurrentSettings();

        BuildServerSettings.ServerName[currentSettings] = GetSelectedBuildServerType();
        BuildServerSettings.IntegrationEnabled[currentSettings] =
            checkBoxEnableBuildServerIntegration.CheckState == CheckState.Indeterminate
                ? null
                : checkBoxEnableBuildServerIntegration.Checked;
        BuildServerSettings.ShowBuildResultPage[currentSettings] =
            checkBoxShowBuildResultPage.CheckState == CheckState.Indeterminate
                ? null
                : checkBoxShowBuildResultPage.Checked;

        var control = buildServerSettingsPanel.Controls
            .OfType<IBuildServerSettingsUserControl>().SingleOrDefault();
        control?.SaveSettings(BuildServerSettings.GetSettingsSource(currentSettings));

        base.PageToSettings();
    }

    private void ActivateBuildServerSettingsControl()
    {
        var controls = buildServerSettingsPanel.Controls.OfType<IBuildServerSettingsUserControl>()
            .Cast<Control>();
        var previousControl = controls.SingleOrDefault();
        previousControl?.Dispose();

        var control = CreateBuildServerSettingsUserControl();

        buildServerSettingsPanel.Controls.Clear();

        if (control is not null)
        {
            control.LoadSettings(BuildServerSettings.GetSettingsSource(GetCurrentSettings()));

            buildServerSettingsPanel.Controls.Add((Control)control);
            ((Control)control).Dock = DockStyle.Fill;
        }
    }

    private IBuildServerSettingsUserControl? CreateBuildServerSettingsUserControl()
    {
        Validates.NotNull(Module);

        if (BuildServerType.SelectedIndex == 0 || string.IsNullOrEmpty(Module.WorkingDir))
        {
            return null;
        }

        var defaultProjectName =
            Module.WorkingDir.Split(Delimiters.PathSeparators, StringSplitOptions.RemoveEmptyEntries)[^1];

        var exports =
            ManagedExtensibility.GetExports<IBuildServerSettingsUserControl, IBuildServerTypeMetadata>();
        var selectedExport =
            exports.SingleOrDefault(export => export.Metadata.BuildServerType == GetSelectedBuildServerType());
        if (selectedExport is not null)
        {
            var buildServerSettingsUserControl = selectedExport.Value;
            Validates.NotNull(_remotesManager);
            var remoteUrls = _remotesManager.LoadRemotes(false)
                .Select(r => string.IsNullOrEmpty(r.PushUrl) ? r.Url! : r.PushUrl!);

            buildServerSettingsUserControl.Initialize(defaultProjectName, remoteUrls);
            return buildServerSettingsUserControl;
        }

        return null;
    }

    private string? GetSelectedBuildServerType()
    {
        if (BuildServerType.SelectedIndex == 0)
        {
            return null;
        }

        return (string?)BuildServerType.SelectedItem;
    }

    private void BuildServerType_SelectedIndexChanged(object sender, EventArgs e)
    {
        ActivateBuildServerSettingsControl();
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(BuildServerIntegrationSettingsPage form)
    {
        public Panel BuildServerSettingsPanel
        {
            get => form.buildServerSettingsPanel;
        }

        public ComboBox BuildServerType
        {
            get => form.BuildServerType;
        }

        public CheckBox CheckBoxEnableBuildServerIntegration
        {
            get => form.checkBoxEnableBuildServerIntegration;
        }

        public CheckBox CheckBoxShowBuildResultPage
        {
            get => form.checkBoxShowBuildResultPage;
        }
    }
}
