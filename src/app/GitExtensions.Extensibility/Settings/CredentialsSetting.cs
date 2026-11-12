using System.Net;
using GitExtensions.Extensibility.Settings.UserControls;

namespace GitExtensions.Extensibility.Settings;

public class CredentialsSetting(string name, string caption, Func<string?> getWorkingDir)
    : ICredentialsManager, ISetting
{
    private readonly CredentialsManager _credentialsManager = new(getWorkingDir);
    private readonly NetworkCredential _defaultValue = new();
    public CredentialsControl? CustomControl { get; set; }

    void ICredentialsManager.Save()
    {
        _credentialsManager.Save();
    }

    string ISetting.Name { get; } = name;
    string ISetting.Caption { get; } = caption;

    public NetworkCredential GetValueOrDefault(SettingsSource settings)
    {
        return _credentialsManager.GetCredentialOrDefault(settings.SettingLevel, Name, _defaultValue);
    }

    public void SaveValue(SettingsSource settings, string userName, string password)
    {
        if (settings.SettingLevel == SettingLevel.Effective)
        {
            var currentCredentials = GetValueOrDefault(settings);
            if (currentCredentials.UserName == userName && currentCredentials.Password == password)
            {
                return;
            }
        }

        var newCredentials = string.IsNullOrWhiteSpace(userName)
            ? null
            : new NetworkCredential(userName, password);
        _credentialsManager.SetCredentials(settings.SettingLevel, Name, newCredentials);
    }
}
