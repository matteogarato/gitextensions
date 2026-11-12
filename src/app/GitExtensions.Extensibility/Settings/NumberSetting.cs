namespace GitExtensions.Extensibility.Settings;

public class NumberSetting<T>(string name, string caption, T defaultValue) : ISetting
{
    public NumberSetting(string name, T defaultValue)
        : this(name, name, defaultValue)
    {
    }

    public T DefaultValue { get; } = defaultValue;
    public Control? CustomControl { get; set; }

    public object? this[SettingsSource settings]
    {
        get
        {
            var stringValue = settings.GetValue(Name);

            if (string.IsNullOrEmpty(stringValue))
            {
                return null;
            }

            _ = TryConvertFromString(stringValue, out var result);
            return result;
        }

        set => settings.SetValue(Name, value?.ToString());
    }

    string ISetting.Name { get; } = name;
    string ISetting.Caption { get; } = caption;

    // TODO: honestly, NumericUpDownBinding might be a better choice than TextBox in general since its internal type is `decimal`.
    //       We would just need to appropriately choose an increment based on NumberSetting's type.
    internal static bool TryConvertFromString(string value, out object? result)
    {
        var type = typeof(T);
        if (type == typeof(int) && int.TryParse(value, out var intResult))
        {
            result = intResult;
            return true;
        }

        if (type == typeof(float) && float.TryParse(value, out var floatResult))
        {
            result = floatResult;
            return true;
        }

        if (type == typeof(double) && double.TryParse(value, out var doubleResult))
        {
            result = doubleResult;
            return true;
        }

        if (type == typeof(long) && long.TryParse(value, out var longResult))
        {
            result = longResult;
            return true;
        }

        result = null;
        return false;
    }

    public T ValueOrDefault(SettingsSource settings)
    {
        var settingVal = this[settings];
        if (settingVal is null)
        {
            return DefaultValue;
        }

        return (T)settingVal;
    }
}
