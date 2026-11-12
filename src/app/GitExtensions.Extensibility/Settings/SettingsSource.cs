using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using GitExtensions.Extensibility.Configurations;

namespace GitExtensions.Extensibility.Settings;

public abstract class SettingsSource : IConfigValueStore
{
    public virtual SettingLevel SettingLevel { get; init; } = SettingLevel.Unknown;

    public abstract string? GetValue(string name);

    public abstract void SetValue(string name, string? value);

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public string? GetString(string name, string? defaultValue)
    {
        return GetValue(name) ?? defaultValue;
    }

    public void SetString(string name, string? value)
    {
        SetValue(name, value);
    }

    public bool? GetBool(string name)
    {
        var stringValue = GetValue(name);

        if (string.Equals(stringValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(stringValue, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    public bool GetBool(string name, bool defaultValue)
    {
        return GetBool(name) ?? defaultValue;
    }

    public void SetBool(string name, bool? value)
    {
        var stringValue = value.HasValue ? value.Value ? "true" : "false" : null;

        SetValue(name, stringValue);
    }

    public int? GetInt(string name)
    {
        var stringValue = GetValue(name);

        if (int.TryParse(stringValue, out var result))
        {
            return result;
        }

        return null;
    }

    public int GetInt(string name, int defaultValue)
    {
        return GetInt(name) ?? defaultValue;
    }

    public void SetInt(string name, int? value)
    {
        var stringValue = value.HasValue ? value.ToString() : null;

        SetValue(name, stringValue);
    }

    public float? GetFloat(string name)
    {
        var stringValue = GetValue(name);

        if (float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (float.TryParse(stringValue, out result))
        {
            return result;
        }

        return null;
    }

    public float GetFloat(string name, float defaultValue)
    {
        return GetFloat(name) ?? defaultValue;
    }

    public void SetFloat(string name, float? value)
    {
        var stringValue = value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null;

        SetValue(name, stringValue);
    }

    public DateTime? GetDate(string name)
    {
        var stringValue = GetValue(name);

        if (DateTime.TryParseExact(stringValue, "yyyy/M/dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var result))
        {
            return result;
        }

        return null;
    }

    public DateTime GetDate(string name, DateTime defaultValue)
    {
        return GetDate(name) ?? defaultValue;
    }

    public void SetDate(string name, DateTime? value)
    {
        var stringValue = value?.ToString("yyyy/M/dd", CultureInfo.InvariantCulture);

        SetValue(name, stringValue);
    }

    [return: NotNullIfNotNull("defaultValue")]
    public Font? GetFont(string name, Font? defaultValue)
    {
        return GetValue(name).Parse(defaultValue!);
    }

    public void SetFont(string name, Font? value)
    {
        SetValue(name, value?.AsString());
    }

    public Color GetColor(string name, Color defaultValue)
    {
        var stringValue = GetValue(name);

        if (!string.IsNullOrWhiteSpace(stringValue))
        {
            try
            {
                var result = ColorTranslator.FromHtml(stringValue);
                if (result != Color.Empty)
                {
                    return result;
                }
            }
            catch
            {
                // ignore invalid color values (return the default value)
            }
        }

        return defaultValue;
    }

    public T GetEnum<T>(string name, T defaultValue) where T : struct, Enum
    {
        var stringValue = GetValue(name);

        if (Enum.TryParse(stringValue, true, out T result))
        {
            return result;
        }

        return defaultValue;
    }

    public void SetEnum<T>(string name, T value) where T : Enum
    {
        var stringValue = value.ToString();

        SetValue(name, stringValue);
    }
}
