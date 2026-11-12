using System.Net;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Extensibility.Settings.UserControls;

namespace GitUI.SettingControlBindings;

internal sealed class CredentialsSettingControlBinding(CredentialsSetting setting, CredentialsControl? control)
    : SettingControlBinding<CredentialsSetting, CredentialsControl>(setting, control)
{
    public override CredentialsControl CreateControl()
    {
        Setting.CustomControl = new CredentialsControl();
        return Setting.CustomControl;
    }

    public override void LoadSetting(SettingsSource settings, CredentialsControl control)
    {
        if (SettingLevelSupported(settings.SettingLevel))
        {
            var credentials = Setting.GetValueOrDefault(settings);
            control.UserName = credentials.UserName;
            control.Password = credentials.Password;
            control.Enabled = true;
        }
        else
        {
            control.UserName = string.Empty;
            control.Password = string.Empty;
            control.Enabled = false;
        }
    }

    public override void SaveSetting(SettingsSource settings, CredentialsControl control)
    {
        if (SettingLevelSupported(settings.SettingLevel))
        {
            Setting.SaveValue(settings, control.UserName, control.Password);

            // Reload actual settings.
            LoadSetting(settings, control);
        }
    }

    private static bool SettingLevelSupported(SettingLevel settingLevel)
    {
        return settingLevel switch
        {
            SettingLevel.Global or SettingLevel.Local or SettingLevel.Effective => true,
            _ => false
        };
    }
}
