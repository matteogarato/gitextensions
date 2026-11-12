using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands.Git;
using GitExtensions.Extensibility.Configurations;

namespace GitCommands.Config;

public class ConfigFile : IConfigFile
{
    private static readonly SearchValues<char> _commentChars = SearchValues.Create(';', '#');

    private readonly List<IConfigSection> _configSections = [];

    public ConfigFile(string fileName)
    {
        FileName = fileName;

        try
        {
            if (!string.IsNullOrEmpty(Path.GetFileName(FileName)) && File.Exists(FileName))
            {
                new ConfigFileParser(this).Parse();
            }
        }
        catch (Exception ex)
        {
            throw new GitConfigurationException(fileName, ex);
        }
    }

    string IConfigFile.FileName { get; }

    IReadOnlyList<IConfigSection> ISubmodulesConfigFile.ConfigSections
    {
        get => _configSections;
    }

    IEnumerable<IConfigSection> IConfigFile.GetConfigSections(string sectionName)
    {
        return ConfigSections.Where(section =>
            section.SectionName.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
    }

    void IConfigFile.LoadFromString(string str)
    {
        new ConfigFileParser(this).Parse(str);
    }

    void ISubmodulesConfigFile.Save()
    {
        Save(FileName);
    }

    void IConfigFile.Save(string fileName)
    {
        try
        {
            FileInfoExtensions.MakeFileTemporaryWritable(fileName,
                file => File.WriteAllText(file, GetAsString(), GetEncoding()));
        }
        catch (Exception ex)
        {
            ExceptionUtils.ShowException(ex, false);
        }
    }

    string IConfigFile.GetAsString()
    {
        StringBuilder configFileContent = new();

        foreach (var section in ConfigSections)
        {
            var dic = section.AsDictionary();

            // Skip empty sections
            if (dic.Count == 0)
            {
                continue;
            }

            configFileContent.Append(section);
            configFileContent.Append(Environment.NewLine);

            foreach ((var key, var values) in dic)
            {
                foreach (var value in values)
                {
                    configFileContent.AppendLine(string.Concat("\t", key, " = ", EscapeValue(value)));
                }
            }
        }

        return configFileContent.ToString();
    }

    void IConfigFile.SetValue(string setting, string value)
    {
        SetStringValue(setting, value);
    }

    string IConfigFile.GetValue(string setting, string defaultValue)
    {
        if (string.IsNullOrEmpty(setting))
        {
            throw new ArgumentNullException(nameof(setting));
        }

        var keyIndex = FindAndCheckKeyIndex(setting);

        var configSectionName = setting[..keyIndex];
        var keyName = setting[(keyIndex + 1)..];

        var configSection = FindConfigSection(configSectionName);

        if (configSection is null)
        {
            return defaultValue;
        }

        return configSection.GetValue(keyName, defaultValue);
    }

    string ISubmodulesConfigFile.GetPathValue(string setting)
    {
        return GetStringValue(setting);
    }

    IReadOnlyList<string> IConfigFile.GetValues(string setting)
    {
        var keyIndex = FindAndCheckKeyIndex(setting);

        var configSectionName = setting[..keyIndex];
        var keyName = setting[(keyIndex + 1)..];

        var configSection = FindConfigSection(configSectionName);

        if (configSection is null)
        {
            return [];
        }

        return configSection.GetValues(keyName);
    }

    IConfigSection IConfigFile.FindOrCreateConfigSection(string name)
    {
        var result = FindConfigSection(name);
        if (result is null)
        {
            result = new ConfigSection(name, true);
            _configSections.Add(result);
        }

        return result;
    }

    void IConfigFile.AddConfigSection(IConfigSection configSection)
    {
        if (FindConfigSection(configSection) is not null)
        {
            throw new ArgumentException("Can not add a section that already exists: " + configSection.SectionName);
        }

        _configSections.Add(configSection);
    }

    void ISubmodulesConfigFile.RemoveConfigSection(string configSectionName)
    {
        var configSection = FindConfigSection(configSectionName);

        if (configSection is null)
        {
            return;
        }

        _configSections.Remove(configSection);
    }

    IConfigSection? IConfigFile.FindConfigSection(string name)
    {
        ConfigSection configSectionToFind = new(name, true);

        return FindConfigSection(configSectionToFind);
    }

    private static Encoding GetEncoding()
    {
        return GitModule.SystemEncoding;
    }

    public static string EscapeValue(string value)
    {
        value = value.Replace("\\", "\\\\");
        value = value.Replace("\"", "\\\"");
        value = value.Replace("\n", "\\n");
        value = value.Replace("\t", "\\t");

        if (value.IndexOfAny(_commentChars) != -1 || value.Trim() != value)
        {
            value = value.Quote();
        }

        return value;
    }

    private void SetStringValue(string setting, string value)
    {
        var keyIndex = FindAndCheckKeyIndex(setting);

        var configSectionName = setting[..keyIndex];
        var keyName = setting[(keyIndex + 1)..];

        FindOrCreateConfigSection(configSectionName).SetValue(keyName, value);
    }

    private static int FindAndCheckKeyIndex(string setting)
    {
        var keyIndex = FindKeyIndex(setting);

        if (keyIndex < 0 || keyIndex == setting.Length)
        {
            throw new Exception("Invalid setting name: " + setting);
        }

        return keyIndex;
    }

    private static int FindKeyIndex(string setting)
    {
        return setting.LastIndexOf('.');
    }

    private string GetStringValue(string setting)
    {
        return GetValue(setting, string.Empty);
    }

    private IConfigSection? FindConfigSection(IConfigSection configSectionToFind)
    {
        return ConfigSections.FirstOrDefault(configSectionToFind.Equals);
    }

    #region ConfigFileParser

    private sealed class ConfigFileParser(ConfigFile configFile)
    {
        private readonly StringBuilder _token = new();
        private readonly StringBuilder _valueToken = new();

        private bool _escapedSection;
        private bool _escapedValue;

        private string? _fileContent;
        private string? _key;

        // parsed char
        private int _pos;
        private bool _quotedStringInSection;

        private bool _quotedValue;
        private IConfigSection? _section;

        private string FileName
        {
            get => configFile.FileName;
        }

        public void Parse(string? fileContent = null)
        {
            _fileContent = fileContent ?? File.ReadAllText(FileName, GetEncoding());

            ParsePart parseFunc = ReadUnknown;

            for (_pos = 0; _pos < _fileContent.Length; _pos++)
            {
                parseFunc = parseFunc(_fileContent[_pos]);
            }

            if (_fileContent.Length > 0 && !_fileContent.EndsWith('\n'))
            {
                parseFunc('\n');
            }
        }

        private void NewSection()
        {
            var sectionName = _token.ToString();
            _token.Clear();
            _section = configFile.FindConfigSection(sectionName);
            if (_section is null)
            {
                _section = new ConfigSection(sectionName, false);
                configFile._configSections.Add(_section);
            }
        }

        private void NewKey()
        {
            _key = _token.ToString().Trim();
            _token.Clear();

            if (_section is null)
            {
                throw new Exception($"Key {_key} in config file {FileName} is not in a section.");
            }
        }

        private void NewValue()
        {
            _token.Append(_valueToken.ToString().Trim());
            _valueToken.Clear();

            var value = _token.ToString();

            if (string.IsNullOrEmpty(_key))
            {
                throw new Exception($"Value {value} for empty key in config file {FileName}.");
            }

            _section?.AddValue(_key, value);

            _key = null;
        }

        private ParsePart ReadSection(char c)
        {
            if (_escapedSection)
            {
                switch (c)
                {
                    case '\\':
                    case '"':
                        _token.Append(c);
                        break;
                    case 't':
                        _token.Append('\t');
                        break;
                    default:
                        throw new Exception("Invalid escape character: " + Regex.Escape(c.ToString()));
                }

                _escapedSection = false;
                return ReadSection;
            }

            // closing square bracket not in quoted section lead to start new section
            if (c == ']' && !_quotedStringInSection)
            {
                NewSection();
                return ReadUnknown;
            }

            if (c == '"')
            {
                _quotedStringInSection = !_quotedStringInSection;
            }

            switch (c)
            {
                case '\\':
                    _escapedSection = true;
                    return ReadSection;
                default:
                    _token.Append(c);
                    return ReadSection;
            }
        }

        private ParsePart ReadComment(char c)
        {
            switch (c)
            {
                case '\n':
                    // check for line continuation
                    if (_token.Length > 0 && _token[^1] == '\\')
                    {
                        _token.Remove(_token.Length - 1, 1);
                        return ReadComment;
                    }

                    return ReadUnknown;

                default:
                    _token.Append(c);
                    return ReadComment;
            }
        }

        private ParsePart ReadKey(char c)
        {
            switch (c)
            {
                case '=':
                    NewKey();
                    return ReadValue;
                case '\n':
                    NewKey();
                    _token.Append("true");
                    NewValue();
                    return ReadUnknown;
                default:
                    _token.Append(c);
                    return ReadKey;
            }
        }

        private ParsePart ReadValue(char c)
        {
            if (_escapedValue)
            {
                switch (c)
                {
                    case '\\':
                    case '"':
                        _valueToken.Append(c);
                        break;
                    case 't':
                        _valueToken.Append('\t');
                        break;
                    case 'n':
                        _valueToken.Append('\n');
                        break;
                    case '\r':
                        return ReadValue;
                    case '\n':
                        // line continuation
                        break;

                    default:
                        throw new Exception("Invalid escape character: " + Regex.Escape(c.ToString()));
                }

                _escapedValue = false;
                return ReadValue;
            }

            switch (c)
            {
                case '\\':
                    _escapedValue = true;
                    return ReadValue;
                case '"':
                    _token.Append(_quotedValue
                        ? _valueToken.ToString()
                        : _valueToken.ToString().Trim());
                    _valueToken.Clear();
                    _quotedValue = !_quotedValue;
                    return ReadValue;
                case ';':
                case '#':
                    if (_quotedValue)
                    {
                        _valueToken.Append(c);
                        return ReadValue;
                    }

                    NewValue();
                    return ReadComment;
                case '\r':
                    return ReadValue;
                case '\n':
                    NewValue();
                    return ReadUnknown;
                default:
                    _valueToken.Append(c);
                    return ReadValue;
            }
        }

        private ParsePart ReadUnknown(char c)
        {
            _token.Clear();

            switch (c)
            {
                case '[':
                    return ReadSection;
                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    return ReadUnknown;
                case ';':
                case '#':
                    return ReadComment;
                default:
                    _token.Append(c);
                    return ReadKey;
            }
        }

        private delegate ParsePart ParsePart(char c);
    }

    #endregion
}
