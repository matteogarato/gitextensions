using System.CodeDom.Compiler;
using System.Globalization;
using System.Text.Json;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitCommandsTests.Settings;

public sealed class SettingTests : IDisposable
{
    private const string _settingsFileContent = @"<?xml version=""1.0"" encoding=""utf-8""?><dictionary />";
    private readonly GitExtSettingsCache _gitExtSettingsCache = null!;
    private readonly DistributedSettings _settingContainer = null!;
    private readonly string _settingFilePath = null!;

    private readonly TempFileCollection _tempFiles = new();

    public SettingTests()
    {
        _settingFilePath = _tempFiles.AddExtension(".settings");
        _tempFiles.AddFile(_settingFilePath + ".backup", false);

        File.WriteAllText(_settingFilePath, _settingsFileContent);

        _gitExtSettingsCache = GitExtSettingsCache.Create(_settingFilePath);
        _settingContainer = new DistributedSettings(null, _gitExtSettingsCache, SettingLevel.Unknown);
    }

    void IDisposable.Dispose()
    {
        _gitExtSettingsCache.Dispose();
        ((IDisposable)_tempFiles).Dispose();
    }

    #region Setting

    [Theory]
    [MemberData(nameof(CreateCases))]
    public void Should_create_setting<T>(T settingDefault)
        where T : struct
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        var setting = CreateTestSetting(settingsPath, settingName, settingDefault);

        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().Be(settingDefault);
        setting.Value.Should().Be(settingDefault);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [MemberData(nameof(SaveCases))]
    public void Should_save_setting<T>(T settingDefault, T value)
        where T : struct
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        T storedValue = default;
        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, settingDefault);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, settingDefault);

            storedValue = setting.Value;
        });
        storedValue.Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(CreateCases))]
    public void Should_return_default_value_for_setting_if_value_not_exist<T>(T settingDefault)
        where T : struct
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        T storedValue = default;
        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, settingDefault);

            storedValue = setting.Value;
        });
        storedValue.Should().Be(settingDefault);
    }

    [Theory]
    [MemberData(nameof(CreateCases))]
    public void Should_return_default_value_for_setting_if_value_is_incorrect<T>(T settingDefault)
        where T : struct
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        T storedValue = default;
        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = Guid.NewGuid().ToString();

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, settingDefault);

            storedValue = setting.Value;
        });
        storedValue.Should().Be(settingDefault);
    }

    #endregion Setting

    #region String Setting

    [Theory]
    [MemberData(nameof(CreateStringCases))]
    public void Should_create_string_setting(string settingDefault)
    {
        settingDefault ??= string.Empty;

        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        var setting = Setting.Create(settingsPath, settingName, settingDefault);

        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().Be(settingDefault);
        setting.Value.Should().Be(settingDefault);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [MemberData(nameof(SaveStringCases))]
    public void Should_save_string_setting(string? settingDefault, string? value)
    {
        settingDefault ??= string.Empty;
        value ??= string.Empty;

        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, settingDefault);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, settingDefault);

            setting.Value.Should().Be(value);
        });
    }

    [Theory]
    [MemberData(nameof(SaveStringCases))]
    public void Should_save_nullable_string_setting(string? settingDefault, string? value)
    {
        _ = settingDefault;
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.CreateNullableString(settingsPath, settingName);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.CreateNullableString(settingsPath, settingName);

            setting.Value.Should().Be(value);
        });
    }

    #endregion String Setting

    #region Bool Setting

    [Fact]
    public void Should_create_nullable_bool_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = Setting.CreateNullableBool(settingsPath, settingName);
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().BeNull();
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_save_nullable_bool_setting(bool? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = Setting.CreateNullableBool(settingsPath, settingName);

        setting.Value = value;
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().Be(value);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_return_default_value_for_bool_setting_if_value_not_exist(bool defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_return_default_value_for_bool_setting_if_value_is_incorrect(bool defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = Guid.NewGuid().ToString();

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_create_bool_setting(bool defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        var setting = Setting.Create(settingsPath, settingName, defaultValue);

        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().Be(defaultValue);
        setting.Value.Should().Be(defaultValue);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Should_save_bool_setting(bool defaultValue, bool value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(value);
        });
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void Should_read_bool_setting_case_insensitively(string storedValue, bool expected)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = storedValue;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, !expected);

            setting.Value.Should().Be(expected);
        });
    }

    [Theory]
    [InlineData("TRUE", false)]
    [InlineData("FALSE", true)]
    [InlineData("1", false)]
    [InlineData("yes", true)]
    public void Should_return_default_for_bool_setting_if_stored_value_is_unrecognized(string storedValue,
        bool defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = storedValue;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData(false, "false")]
    [InlineData(true, "true")]
    public void Should_store_bool_setting_as_lowercase_string(bool value, string expectedStoredString)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, !value);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value.Should().Be(expectedStoredString);
        });
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void Should_read_nullable_bool_setting_case_insensitively(string storedValue, bool? expected)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = storedValue;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.CreateNullableBool(settingsPath, settingName);

            setting.Value.Should().Be(expected);
        });
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    [InlineData("1")]
    [InlineData("yes")]
    public void Should_return_null_for_nullable_bool_setting_if_stored_value_is_unrecognized(string storedValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = storedValue;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.CreateNullableBool(settingsPath, settingName);

            setting.Value.Should().BeNull();
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_save_nullable_bool_setting_as_string(bool? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.CreateNullableBool(settingsPath, settingName);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.CreateNullableBool(settingsPath, settingName);

            setting.Value.Should().Be(value);
        });
    }

    [Theory]
    [InlineData(false, "false")]
    [InlineData(true, "true")]
    [InlineData(null, null)]
    public void Should_store_nullable_bool_setting_as_lowercase_string(bool? value, string? expectedStoredString)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.CreateNullableBool(settingsPath, settingName);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.CreateNullableString(settingsPath, settingName);

            setting.Value.Should().Be(expectedStoredString);
        });
    }

    #endregion Bool Setting

    #region Char Setting

    [Fact]
    public void Should_create_char_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        var setting = CreateTestSetting(settingsPath, settingName, default(char));

        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().Be('\0');
        setting.Value.Should().Be('\0');
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(char.MinValue)]
    [InlineData(' ')]
    [InlineData(char.MaxValue)]
    public void Should_save_nullable_char_setting(char? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = CreateNullableTestSetting<char>(settingsPath, settingName);

        setting.Value = value;
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().Be(value);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData('\0')]
    [InlineData(' ')]
    [InlineData('\uFFFF')]
    public void Should_return_default_value_for_char_setting_if_value_not_exist(char defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData('\0')]
    [InlineData(' ')]
    [InlineData('\uFFFF')]
    public void Should_return_default_value_for_char_setting_if_value_is_incorrect(char defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = Guid.NewGuid().ToString();

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    #endregion Char Setting

    #region Byte Setting

    [Fact]
    public void Should_create_nullable_byte_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        var setting = CreateNullableTestSetting<byte>(settingsPath, settingName);

        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().BeNull();
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(byte.MinValue)]
    [InlineData(byte.MaxValue)]
    [InlineData(0)]
    public void Should_save_nullable_byte_setting(byte? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = CreateNullableTestSetting<byte>(settingsPath, settingName);

        setting.Value = value;
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().Be(value);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(255)]
    public void Should_return_default_value_for_byte_setting_if_value_not_exist(byte defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(255)]
    public void Should_return_default_value_for_byte_setting_if_value_is_incorrect(byte defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = Guid.NewGuid().ToString();

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = CreateTestSetting(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    #endregion Byte Setting

    #region Int Setting

    [Fact]
    public void Should_create_nullable_int_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = CreateNullableTestSetting<int>(settingsPath, settingName);
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().BeNull();
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(0)]
    public void Should_save_nullable_int_setting(int? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = CreateNullableTestSetting<int>(settingsPath, settingName);

        setting.Value = value;
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().Be(value);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Should_return_default_value_for_int_setting_if_value_not_exist(int defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Should_return_default_value_for_int_setting_if_value_is_incorrect(int defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = Guid.NewGuid().ToString();

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    #endregion Int Setting

    #region Float Setting

    [Fact]
    public void Should_create_nullable_float_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = CreateNullableTestSetting<float>(settingsPath, settingName);
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().BeNull();
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(float.MinValue)]
    [InlineData(float.MaxValue)]
    [InlineData(0f)]
    public void Should_save_nullable_float_setting(float? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = CreateNullableTestSetting<float>(settingsPath, settingName);

        setting.Value = value;
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().Be(value);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(.1f)]
    public void Should_return_default_value_for_float_setting_if_value_not_exist(float defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(.1f)]
    public void Should_return_default_value_for_float_setting_if_value_is_incorrect(float defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = Guid.NewGuid().ToString();

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    #endregion Float Setting

    #region Enum Setting

    [Fact]
    public void Should_create_nullable_enum_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        var setting = Setting.CreateNullableEnum<TestEnum>(settingsPath, settingName);

        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().BeNull();
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TestEnum.First)]
    [InlineData(TestEnum.Second)]
    public void Should_save_nullable_enum_setting(TestEnum? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = Setting.CreateNullableEnum<TestEnum>(settingsPath, settingName);

        setting.Value = value;
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().Be(value);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TestEnum.First)]
    [InlineData(TestEnum.Second)]
    public void Should_save_nullable_enum_setting_as_string(TestEnum? value)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.CreateNullableEnum<TestEnum>(settingsPath, settingName);

            setting.Value = value;

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.CreateNullableString(settingsPath, settingName);

            var storedValue = setting.Value;
            storedValue.Should().Be(value?.ToString());
            var isNumber = int.TryParse(storedValue, out _);
            isNumber.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(TestEnum.First)]
    [InlineData(TestEnum.Second)]
    public void Should_return_default_value_for_enum_setting_if_value_not_exist(TestEnum defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    [Theory]
    [InlineData(TestEnum.First)]
    [InlineData(TestEnum.Second)]
    public void Should_return_default_value_for_enum_setting_if_value_is_incorrect(TestEnum defaultValue)
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);

        AppSettings.UsingContainer(_settingContainer, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, string.Empty);

            setting.Value = Guid.NewGuid().ToString();

            AppSettings.SaveSettings();
        });

        using TempFileCollection tempFiles = new();
        var filePath = tempFiles.AddExtension(".settings");

        File.WriteAllText(filePath, File.ReadAllText(_settingFilePath));

        DistributedSettings container = new(null, GitExtSettingsCache.Create(filePath), SettingLevel.Unknown);

        AppSettings.UsingContainer(container, () =>
        {
            var setting = Setting.Create(settingsPath, settingName, defaultValue);

            setting.Value.Should().Be(defaultValue);
        });
    }

    public enum TestEnum
    {
        First,
        Second
    }

    #endregion Enum Setting

    #region Struct Setting

    [Fact]
    public void Should_create_nullable_struct_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        var setting = CreateNullableTestSetting<TestStruct>(settingsPath, settingName);
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().BeNull();
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    [Fact]
    public void Should_save_nullable_struct_setting()
    {
        var pathName = Guid.NewGuid().ToString();
        var settingName = Guid.NewGuid().ToString();
        AppSettingsPath settingsPath = new(pathName);
        TestStruct? value = new TestStruct
        {
            Bool = false,
            Char = ' ',
            Byte = 0,
            Int = 0,
            Float = 0f
        };
        var setting = CreateNullableTestSetting<TestStruct>(settingsPath, settingName);

        setting.Value = value;
        setting.Should().NotBeNull();
        setting.Name.Should().Be(settingName);
        setting.Default.Should().BeNull();
        setting.Value.Should().Be(value);
        setting.FullPath.Should().Be($"{pathName}.{settingName}");
    }

    public struct TestStruct
    {
        public bool Bool { get; set; }

        public char Char { get; set; }

        public byte Byte { get; set; }

        public int Int { get; set; }

        public float Float { get; set; }
    }

    #endregion Struct Setting

    #region Helpers

    private static ISetting<T> CreateTestSetting<T>(SettingsPath path, string name, T defaultValue)
        where T : struct
    {
        var type = typeof(T);
        return
            type == typeof(string) ? (ISetting<T>)Setting.Create(path, name, (string)(object)defaultValue)
            : type == typeof(bool) ? (ISetting<T>)Setting.Create(path, name, (bool)(object)defaultValue)
            : type == typeof(int) ? (ISetting<T>)Setting.Create(path, name, (int)(object)defaultValue)
            : type == typeof(float) ? (ISetting<T>)Setting.Create(path, name, (float)(object)defaultValue)
            : Setting.Create(
                path,
                name,
                defaultValue,
                s =>
                {
                    try
                    {
                        if (typeof(T).IsEnum
                            && Enum.TryParse(typeof(T), s, out var parsed)
                            && parsed is T enumValue)
                        {
                            return (true, enumValue);
                        }

                        if ((typeof(T).IsPrimitive || typeof(T) == typeof(decimal) || typeof(T) == typeof(DateTime))
                            && Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture) is T convertedValue)
                        {
                            return (true, convertedValue);
                        }

                        T? deserialized = JsonSerializer.Deserialize<T>(s);
                        return deserialized.HasValue ? (true, deserialized.Value) : default;
                    }
                    catch
                    {
                        return default;
                    }
                },
                v =>
                {
                    if (typeof(T).IsEnum)
                    {
                        return v.ToString();
                    }

                    if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal) || typeof(T) == typeof(DateTime))
                    {
                        return Convert.ToString(v, CultureInfo.InvariantCulture);
                    }

                    return JsonSerializer.Serialize(v);
                });
    }

    private static ISetting<T?> CreateNullableTestSetting<T>(SettingsPath path, string name)
        where T : struct
    {
        var type = typeof(T);
        return
            type == typeof(string) ? (ISetting<T?>)Setting.CreateNullableString(path, name)
            : type == typeof(bool) ? (ISetting<T?>)Setting.CreateNullableBool(path, name)
            : Setting.Create(
                path,
                name,
                null,
                s =>
                {
                    try
                    {
                        if (typeof(T).IsEnum
                            && Enum.TryParse(typeof(T), s, out var parsed)
                            && parsed is T enumValue)
                        {
                            return (true, enumValue);
                        }

                        if ((typeof(T).IsPrimitive || typeof(T) == typeof(decimal) || typeof(T) == typeof(DateTime))
                            && Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture) is T convertedValue)
                        {
                            return (true, convertedValue);
                        }

                        T? deserialized = JsonSerializer.Deserialize<T>(s);
                        return deserialized.HasValue ? (true, deserialized) : default;
                    }
                    catch
                    {
                        return default;
                    }
                },
                v =>
                {
                    if (!v.HasValue)
                    {
                        return null;
                    }

                    var val = v.Value;

                    if (typeof(T).IsEnum)
                    {
                        return val.ToString();
                    }

                    if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal) || typeof(T) == typeof(DateTime))
                    {
                        return Convert.ToString(val, CultureInfo.InvariantCulture);
                    }

                    return JsonSerializer.Serialize(val);
                });
    }

    #endregion Helpers

    #region Test Cases

    public static IEnumerable<object[]> CreateCases()
    {
        foreach (var value in Values())
        {
            yield return new[] { value };
        }
    }

    public static IEnumerable<object[]> SaveCases()
    {
        foreach (var settingDefault in Values())
        {
            foreach (var value in Values())
            {
                if (settingDefault.GetType() == value.GetType())
                {
                    yield return new[] { settingDefault, value };
                }
            }
        }
    }

    public static IEnumerable<object[]> CreateStringCases()
    {
        yield return new object[] { null! };
        yield return new object[] { string.Empty };
        yield return new object[] { "_" };
    }

    public static IEnumerable<object[]> SaveStringCases()
    {
        yield return new object[] { null!, null! };
        yield return new object[] { null!, string.Empty };
        yield return new object[] { null!, "_" };

        yield return new object[] { string.Empty, null! };
        yield return new object[] { string.Empty, string.Empty };
        yield return new object[] { string.Empty, "_" };

        yield return new object[] { "_", null! };
        yield return new object[] { "_", string.Empty };
        yield return new object[] { "_", "_" };
    }

    private static IEnumerable<object> Values()
    {
        yield return false;
        yield return true;

        yield return char.MinValue;
        yield return '_';
        yield return '0';

        yield return sbyte.MinValue;
        yield return sbyte.MaxValue;
        yield return (sbyte)0;
        yield return (sbyte)1;
        yield return (sbyte)-1;

        yield return byte.MinValue;
        yield return byte.MaxValue;
        yield return (byte)1;

        yield return short.MinValue;
        yield return short.MaxValue;
        yield return (short)0;
        yield return (short)1;
        yield return (short)-1;

        yield return ushort.MinValue;
        yield return ushort.MaxValue;
        yield return (ushort)1;

        yield return int.MinValue;
        yield return int.MaxValue;
        yield return 0;
        yield return 1;
        yield return -1;

        yield return uint.MinValue;
        yield return uint.MaxValue;

        yield return long.MinValue;
        yield return long.MaxValue;
        yield return 0L;
        yield return 1L;
        yield return -1L;

        yield return ulong.MinValue;
        yield return ulong.MaxValue;
        yield return 1UL;

        yield return float.MinValue;
        yield return float.MaxValue;
        yield return float.Epsilon;
        yield return float.PositiveInfinity;
        yield return float.NegativeInfinity;
        yield return float.NaN;
        yield return 0F;
        yield return 1F;
        yield return -1F;

        yield return double.MinValue;
        yield return double.MaxValue;
        yield return double.Epsilon;
        yield return double.PositiveInfinity;
        yield return double.NegativeInfinity;
        yield return double.NaN;
        yield return 0D;
        yield return 1D;
        yield return -1D;

        yield return decimal.MinValue;
        yield return decimal.MaxValue;
        yield return decimal.Zero;
        yield return decimal.One;
        yield return decimal.MinusOne;

        yield return DateTime.MinValue;
        yield return DateTime.MaxValue.Day;
        yield return DateTime.Today;

        yield return TestEnum.First;
        yield return TestEnum.Second;

        yield return new TestStruct
        {
            Bool = false,
            Char = char.MinValue,
            Byte = 0,
            Int = 0,
            Float = 0F
        };

        yield return new TestStruct
        {
            Bool = true,
            Char = '_',
            Byte = 1,
            Int = 1,
            Float = 1F
        };
    }

    #endregion Test Cases
}
