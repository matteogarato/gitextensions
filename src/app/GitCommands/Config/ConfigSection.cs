using System.Diagnostics;
using GitExtensions.Extensibility.Configurations;

namespace GitCommands.Config;

/// <summary>
///     ConfigSection
///     Sections can be defined as:
///     [section "subsection"] (subsection is case sensitive)
///     or
///     [section.subsection] (subsection is case insensitive)
///     Case insensitive sections are deprecated. Dot separated subsections are treated
///     as case insensitive only when loaded from config file. Dot separated subsections
///     added from code, are treated as case sensitive.
/// </summary>
public class ConfigSection : IConfigSection
{
    private readonly IDictionary<string, List<string>> _configKeys;

    internal ConfigSection(string name, bool forceCaseSensitive)
    {
        _configKeys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var slashIndex = name.IndexOf('\"');

        if (slashIndex != -1)
        {
            // [section "subsection"] case sensitive
            SectionName = name[..slashIndex].Trim();
            SubSection = name.Substring(slashIndex + 1, name.LastIndexOf('\"') - slashIndex - 1);
            SubSectionCaseSensitive = true;
        }
        else if (!name.Contains('.'))
        {
            // [section] case sensitive
            SectionName = name.Trim();
            SubSectionCaseSensitive = true;
        }
        else
        {
            // [section.subsection] case insensitive
            var subSectionIndex = name.IndexOf('.');

            if (subSectionIndex < 1)
            {
                throw new Exception("Invalid section name: " + name);
            }

            SectionName = name[..subSectionIndex].Trim();
            SubSection = name[(subSectionIndex + 1)..].Trim();
            SubSectionCaseSensitive = false;
        }

        if (forceCaseSensitive)
        {
            SubSectionCaseSensitive = true;
        }
    }

    public bool SubSectionCaseSensitive { get; }

    string IConfigSection.SectionName { get; set; }
    string? IConfigSection.SubSection { get; set; }

    IDictionary<string, IReadOnlyList<string>> IConfigSection.AsDictionary()
    {
        return _configKeys.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    bool IConfigSection.HasValue(string key)
    {
        return _configKeys.ContainsKey(key);
    }

    void IConfigSection.SetValue(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _configKeys.Remove(key);
        }
        else
        {
            _configKeys[key] = [value];
        }
    }

    void IConfigSection.AddValue(string key, string value)
    {
        if (!_configKeys.TryGetValue(key, out var list))
        {
            list = [];
            _configKeys[key] = list;
        }

        list.Add(value);
    }

    string IConfigSection.GetValue(string key, string defaultValue)
    {
        if (_configKeys.TryGetValue(key, out var list))
        {
            if (list.Count > 0)
            {
                return list[^1];
            }
        }

        return defaultValue;
    }

    IReadOnlyList<string> IConfigSection.GetValues(string key)
    {
        return _configKeys.TryGetValue(key, out var configKey) ? configKey : [];
    }

    bool IConfigSection.Equals(IConfigSection other)
    {
        var sc = SubSectionCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return string.Equals(SectionName, other.SectionName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(SubSection, other.SubSection, sc);
    }

    public override string ToString()
    {
        var result = "[" + SectionName;
        if (!string.IsNullOrEmpty(SubSection))
        {
            var escSubSection = SubSection.Replace("\"", "\\\"");
            escSubSection = escSubSection.Replace("\\", "\\\\");

            if (!SubSectionCaseSensitive)
            {
                escSubSection = escSubSection.ToLower();
            }

            result = result + " \"" + escSubSection + "\"";
        }

        result += "]";
        return result;
    }
}

public static class ConfigSectionExt
{
    public static bool GetValueAsBool(this IConfigSection section, string name, bool defaultValue)
    {
        var result = defaultValue;

        if (section.HasValue(name))
        {
            var value = section.GetValue(name);
            if (!bool.TryParse(value, out result))
            {
                Trace.WriteLine(
                    $"Warning: Could not parse boolean value '{value}' for '{section.SectionName}.{name}'. Using default '{defaultValue}'.");
                result = false;
            }
        }

        return result;
    }

    public static void SetValueAsBool(this IConfigSection section, string name, bool value)
    {
        section.SetValue(name, value.ToString());
    }
}
