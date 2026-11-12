using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI.Theming;
using GitUI.SettingControlBindings;
using NSubstitute;

namespace GitUITests.SettingControlBindings;

public sealed class NumberSettingControlBindingTests
{
    private const string _settingName = "TestSetting";
    private const int _defaultValue = 42;

    [Fact]
    public void CreateControlBinding_TextBox_SaveSetting_should_set_null_when_TextBox_is_empty()
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();

        textBox.Text = string.Empty;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(_settingName, null);
    }

    [Fact]
    public void
        CreateControlBinding_TextBox_LoadSetting_should_show_default_value_when_nothing_stored_at_effective_level()
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(_settingName).Returns((string?)null);

        binding.LoadSetting(settingsSource);

        textBox.Text.Should().Be(_defaultValue.ToString());
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]
    public void CreateControlBinding_TextBox_LoadSetting_should_show_empty_when_nothing_stored_at_non_effective_level(
        SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(_settingName).Returns((string?)null);

        binding.LoadSetting(settingsSource);

        textBox.Text.Should().BeEmpty();
    }

    [Fact]
    public void
        CreateControlBinding_TextBox_SaveSetting_should_not_write_when_TextBox_shows_default_value_at_effective_level()
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(_settingName).Returns((string?)null);

        textBox.Text = _defaultValue.ToString();
        binding.SaveSetting(settingsSource);

        settingsSource.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public void CreateControlBinding_TextBox_LoadSetting_should_show_stored_value_at_effective_level(int storedValue)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(_settingName).Returns(storedValue.ToString());

        binding.LoadSetting(settingsSource);

        textBox.Text.Should().Be(storedValue.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public void CreateControlBinding_TextBox_SaveSetting_should_write_valid_number(int value)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();

        textBox.Text = value.ToString();
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(_settingName, value.ToString());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    public void CreateControlBinding_TextBox_should_set_red_background_when_text_changes_to_invalid(string invalidText)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        SettingControlBindingsProvider.CreateControlBinding(setting);

        textBox.Text = invalidText;

        textBox.BackColor.Should().Be(OtherColors.BrightRed);
        textBox.ForeColor.Should().Be(OtherColors.BrightRed.GetTextColor());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    public void CreateControlBinding_TextBox_SaveSetting_should_store_null_on_invalid_input(string invalidText)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();

        textBox.Text = invalidText;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(_settingName, null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public void CreateControlBinding_TextBox_should_restore_background_when_text_changes_to_valid(int value)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new() { BackColor = Color.Red, ForeColor = Color.WhiteSmoke };
        setting.CustomControl = textBox;
        SettingControlBindingsProvider.CreateControlBinding(setting);

        textBox.Text = value.ToString();

        textBox.BackColor.Should().Be(SystemColors.Window);
        textBox.ForeColor.Should().Be(SystemColors.WindowText);
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]
    [InlineData(SettingLevel.Effective)]
    public void CreateControlBinding_TextBox_LoadSetting_should_restore_background(SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new() { BackColor = Color.Red, ForeColor = Color.WhiteSmoke };
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(_settingName).Returns(_defaultValue.ToString());

        binding.LoadSetting(settingsSource);

        textBox.BackColor.Should().Be(SystemColors.Window);
        textBox.ForeColor.Should().Be(SystemColors.WindowText);
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]
    public void CreateControlBinding_TextBox_SaveSetting_should_write_default_value_at_non_effective_level(
        SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);

        textBox.Text = _defaultValue.ToString();
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(_settingName, _defaultValue.ToString());
    }

    [Fact]
    public void
        CreateControlBinding_NumericUpDown_LoadSetting_should_show_default_value_when_nothing_stored_at_effective_level()
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(_settingName).Returns((string?)null);

        binding.LoadSetting(settingsSource);

        numericUpDown.Value.Should().Be(_defaultValue);
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]
    public void
        CreateControlBinding_NumericUpDown_LoadSetting_should_show_empty_when_nothing_stored_at_non_effective_level(
            SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(_settingName).Returns((string?)null);

        binding.LoadSetting(settingsSource);

        numericUpDown.Text.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]
    [InlineData(SettingLevel.Effective)]
    public void CreateControlBinding_NumericUpDown_LoadSetting_should_show_stored_value(SettingLevel settingLevel)
    {
        const int storedValue = 99;
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(_settingName).Returns(storedValue.ToString());

        binding.LoadSetting(settingsSource);

        numericUpDown.Value.Should().Be(storedValue);
    }

    [Fact]
    public void CreateControlBinding_NumericUpDown_SaveSetting_should_set_null_when_control_is_empty()
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();

        numericUpDown.ResetText();
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(_settingName, null);
    }

    [Fact]
    public void
        CreateControlBinding_NumericUpDown_SaveSetting_should_not_write_when_control_shows_default_value_at_effective_level()
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(_settingName).Returns((string?)null);

        numericUpDown.Value = _defaultValue;
        binding.SaveSetting(settingsSource);

        settingsSource.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public void CreateControlBinding_NumericUpDown_SaveSetting_should_write_value_at_effective_level(int value)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        // Store a different value so the effective-level comparison does not skip the write.
        var storedValue = value == _defaultValue ? _defaultValue + 1 : _defaultValue;
        settingsSource.GetValue(_settingName).Returns(storedValue.ToString());

        numericUpDown.Value = value;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(_settingName, value.ToString());
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]
    public void CreateControlBinding_NumericUpDown_SaveSetting_should_write_default_value_at_non_effective_level(
        SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(_settingName, _defaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        var binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        var settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(_settingName).Returns((string?)null);

        numericUpDown.Value = _defaultValue;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(_settingName, _defaultValue.ToString());
    }
}
