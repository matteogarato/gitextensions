using GitExtensions.Extensibility.Settings;

namespace GitCommands.Settings;

internal sealed class DetachedSettings(SettingsSource settingsSource) : IDetachedSettings
{
    private const string _dictionaryDefault = "en-US";
    private const bool _noFastForwardMergeDefault = false;

    string IDetachedSettings.Dictionary
    {
        get => settingsSource.GetString(nameof(Dictionary).ToLower(), _dictionaryDefault);
        set
        {
            if (Dictionary == value)
            {
                return;
            }

            settingsSource.SetString(nameof(Dictionary).ToLower(), value);
        }
    }

    bool IDetachedSettings.NoFastForwardMerge
    {
        get => settingsSource.GetBool(nameof(NoFastForwardMerge), _noFastForwardMergeDefault);
        set
        {
            if (NoFastForwardMerge == value)
            {
                return;
            }

            settingsSource.SetBool(nameof(NoFastForwardMerge), value);
        }
    }
}
