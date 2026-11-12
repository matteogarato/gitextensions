using GitExtensions.Extensibility.Settings;

namespace GitUI.CommandsDialogs.SettingsDialog;

public partial class GitConfigBaseSettingsPage(IServiceProvider serviceProvider)
    : SettingsPageWithHeader(serviceProvider), IGitConfigSettingsPage
{
    protected GitConfigSettingsSet GitConfigSettingsSet
    {
        get => CommonLogic.GitConfigSettingsSet;
    }

    protected SettingsSource? CurrentSettings { get; private set; }

    void ILocalSettingsPage.SetEffectiveSettings()
    {
        SetCurrentSettings(GitConfigSettingsSet.EffectiveSettings);
    }

    void ILocalSettingsPage.SetLocalSettings()
    {
        SetCurrentSettings(GitConfigSettingsSet.LocalSettings);
    }

    public override void SetGlobalSettings()
    {
        SetCurrentSettings(GitConfigSettingsSet.GlobalSettings);
    }

    void IGitConfigSettingsPage.SetSystemSettings()
    {
        SetCurrentSettings(GitConfigSettingsSet.SystemSettings);
    }

    protected override void Init(ISettingsPageHost pageHost)
    {
        base.Init(pageHost);

        CurrentSettings = GitConfigSettingsSet.EffectiveSettings;
    }

    protected override SettingsSource GetCurrentSettings()
    {
        Validates.NotNull(CurrentSettings);
        return CurrentSettings;
    }

    protected override void PageToSettings()
    {
        base.PageToSettings();
        GitConfigSettingsSet.Save();
    }

    private void SetCurrentSettings(SettingsSource settings)
    {
        if (CurrentSettings is not null)
        {
            SaveSettings();
        }

        CurrentSettings = settings;

        LoadSettings();
    }
}
