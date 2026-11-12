using GitExtensions.Extensibility.Extensions;
using GitExtensions.Extensibility.Settings;

namespace GitUI.SettingControlBindings;

internal sealed class BoolSettingControlBinding(BoolSetting setting, CheckBox? customControl)
    : SettingControlBinding<BoolSetting, CheckBox>(setting, customControl)
{
    public override CheckBox CreateControl()
    {
        Setting.CustomControl = new CheckBox { ThreeState = true };
        return Setting.CustomControl;
    }

    public override void LoadSetting(SettingsSource settings, CheckBox control)
    {
        var settingVal = settings.SettingLevel == SettingLevel.Effective
            ? Setting.ValueOrDefault(settings)
            : Setting[settings];

        control.SetNullableChecked(settingVal);
    }

    public override void SaveSetting(SettingsSource settings, CheckBox control)
    {
        var controlValue = control.GetNullableChecked();
        if (settings.SettingLevel == SettingLevel.Effective)
        {
            if (Setting.ValueOrDefault(settings) == controlValue)
            {
                return;
            }
        }

        Setting[settings] = controlValue;
    }
}
