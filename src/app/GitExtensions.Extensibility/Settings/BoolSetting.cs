namespace GitExtensions.Extensibility.Settings;

public class BoolSetting(string name, string caption, bool defaultValue) : ISetting
{
    public BoolSetting(string name, bool defaultValue)
        : this(name, name, defaultValue)
    {
    }

    public bool DefaultValue { get; } = defaultValue;
    public CheckBox? CustomControl { get; set; }

    public bool? this[SettingsSource settings]
    {
        get => settings.GetBool(Name);

        set => settings.SetBool(Name, value);
    }

    string ISetting.Name { get; } = name;
    string ISetting.Caption { get; } = caption;

    public bool ValueOrDefault(SettingsSource settings)
    {
        return this[settings] ?? DefaultValue;
    }
}
