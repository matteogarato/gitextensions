namespace GitExtensions.Extensibility.Settings;

public class StringSetting(string name, string caption, string defaultValue) : ISetting
{
    public StringSetting(string name, string defaultValue)
        : this(name, name, defaultValue)
    {
    }

    public string DefaultValue { get; } = defaultValue;
    public TextBox? CustomControl { get; set; }

    public string? this[SettingsSource settings]
    {
        get => settings.GetString(Name, null);
        set => settings.SetString(Name, value);
    }

    string ISetting.Name { get; } = name;
    string ISetting.Caption { get; } = caption;

    public string ValueOrDefault(SettingsSource settings)
    {
        return this[settings] ?? DefaultValue;
    }
}
