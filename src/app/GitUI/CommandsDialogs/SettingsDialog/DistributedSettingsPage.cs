using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using GitUI.SettingControlBindings;
using ResourceManager;

namespace GitUI.CommandsDialogs.SettingsDialog;

public partial class DistributedSettingsPage(IServiceProvider serviceProvider)
    : SettingsPageWithHeader(serviceProvider), IDistributedSettingsPage
{
    private static readonly TranslationString _numberSettingPlaceholder = new(@"no value set");

    private static readonly TranslationString _stringSettingPlaceholder =
        new(@"no value set; for empty string, enter ""{0}"" without the double quotes");

    static DistributedSettingsPage()
    {
        NumberSettingControlBinding.PlaceholderText = _numberSettingPlaceholder.Text;
        StringSettingControlBinding.PlaceholderText = _stringSettingPlaceholder.Text;
    }

    protected DistributedSettingsSet DistributedSettingsSet
    {
        get => CommonLogic.DistributedSettingsSet;
    }

    protected DistributedSettings? CurrentSettings { get; private set; }

    void ILocalSettingsPage.SetEffectiveSettings()
    {
        if (DistributedSettingsSet.EffectiveSettings is not null)
        {
            SetCurrentSettings(DistributedSettingsSet.EffectiveSettings);
        }
    }

    void ILocalSettingsPage.SetLocalSettings()
    {
        if (DistributedSettingsSet.LocalSettings is not null)
        {
            SetCurrentSettings(DistributedSettingsSet.LocalSettings);
        }
    }

    void IDistributedSettingsPage.SetDistributedSettings()
    {
        if (DistributedSettingsSet.DistributedSettings is not null)
        {
            SetCurrentSettings(DistributedSettingsSet.DistributedSettings);
        }
    }

    public override void SetGlobalSettings()
    {
        if (DistributedSettingsSet.GlobalSettings is not null)
        {
            SetCurrentSettings(DistributedSettingsSet.GlobalSettings);
        }
    }

    protected override void Init(ISettingsPageHost pageHost)
    {
        base.Init(pageHost);

        CurrentSettings = DistributedSettingsSet.EffectiveSettings;
    }

    protected override SettingsSource GetCurrentSettings()
    {
        Validates.NotNull(CurrentSettings);

        return CurrentSettings;
    }

    private void SetCurrentSettings(DistributedSettings settings)
    {
        if (CurrentSettings is not null)
        {
            SaveSettings();
        }

        CurrentSettings = settings;

        LoadSettings();
    }
}
