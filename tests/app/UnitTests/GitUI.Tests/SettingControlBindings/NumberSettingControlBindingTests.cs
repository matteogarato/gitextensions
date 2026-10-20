using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI.Theming;
using GitUI.SettingControlBindings;
using NSubstitute;

namespace GitUITests.SettingControlBindings;

public sealed class NumberSettingControlBindingTests
{
    private const string SettingName = "TestSetting";
    private const int DefaultValue = 42;

    [Fact]
    public void CreateControlBinding_TextBox_SaveSetting_should_set_null_when_TextBox_is_empty()
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();

        textBox.Text = string.Empty;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(SettingName, null);
    }

    [Fact]
    public void CreateControlBinding_TextBox_LoadSetting_should_show_default_value_when_nothing_stored_at_effective_level()
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(SettingName).Returns((string?)null);

        binding.LoadSetting(settingsSource);

        textBox.Text.Should().Be(DefaultValue.ToString());
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]

    public void CreateControlBinding_TextBox_LoadSetting_should_show_empty_when_nothing_stored_at_non_effective_level(SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(SettingName).Returns((string?)null);

        binding.LoadSetting(settingsSource);

        textBox.Text.Should().BeEmpty();
    }

    [Fact]
    public void CreateControlBinding_TextBox_SaveSetting_should_not_write_when_TextBox_shows_default_value_at_effective_level()
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(SettingName).Returns((string?)null);

        textBox.Text = DefaultValue.ToString();
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
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(SettingName).Returns(storedValue.ToString());

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
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();

        textBox.Text = value.ToString();
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(SettingName, value.ToString());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1e3")]

    public void CreateControlBinding_TextBox_should_set_red_background_when_text_changes_to_invalid(string invalidText)
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        SettingControlBindingsProvider.CreateControlBinding(setting);

        textBox.Text = invalidText;

        textBox.BackColor.Should().Be(OtherColors.BrightRed);
        textBox.ForeColor.Should().Be(ColorHelper.GetTextColor(OtherColors.BrightRed));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1e3")]

    public void CreateControlBinding_TextBox_SaveSetting_should_store_null_on_invalid_input(string invalidText)
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();

        textBox.Text = invalidText;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(SettingName, null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]

    public void CreateControlBinding_TextBox_should_restore_background_when_text_changes_to_valid(int value)
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
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
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new() { BackColor = Color.Red, ForeColor = Color.WhiteSmoke };
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(SettingName).Returns(DefaultValue.ToString());

        binding.LoadSetting(settingsSource);

        textBox.BackColor.Should().Be(SystemColors.Window);
        textBox.ForeColor.Should().Be(SystemColors.WindowText);
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]

    public void CreateControlBinding_TextBox_SaveSetting_should_write_default_value_at_non_effective_level(SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using TextBox textBox = new();
        setting.CustomControl = textBox;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);

        textBox.Text = DefaultValue.ToString();
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(SettingName, DefaultValue.ToString());
    }

    [Fact]
    public void CreateControlBinding_NumericUpDown_LoadSetting_should_show_default_value_when_nothing_stored_at_effective_level()
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(SettingName).Returns((string?)null);

        binding.LoadSetting(settingsSource);

        numericUpDown.Value.Should().Be(DefaultValue);
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]

    public void CreateControlBinding_NumericUpDown_LoadSetting_should_show_empty_when_nothing_stored_at_non_effective_level(SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(SettingName).Returns((string?)null);

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
        const int StoredValue = 99;
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(SettingName).Returns(StoredValue.ToString());

        binding.LoadSetting(settingsSource);

        numericUpDown.Value.Should().Be(StoredValue);
    }

    [Fact]
    public void CreateControlBinding_NumericUpDown_SaveSetting_should_set_null_when_control_is_empty()
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();

        numericUpDown.ResetText();
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(SettingName, null);
    }

    [Fact]
    public void CreateControlBinding_NumericUpDown_SaveSetting_should_not_write_when_control_shows_default_value_at_effective_level()
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        settingsSource.GetValue(SettingName).Returns((string?)null);

        numericUpDown.Value = DefaultValue;
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
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(SettingLevel.Effective);
        // Store a different value so the effective-level comparison does not skip the write.
        int storedValue = value == DefaultValue ? DefaultValue + 1 : DefaultValue;
        settingsSource.GetValue(SettingName).Returns(storedValue.ToString());

        numericUpDown.Value = value;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(SettingName, value.ToString());
    }

    [Theory]
    [InlineData(SettingLevel.Global)]
    [InlineData(SettingLevel.Local)]
    [InlineData(SettingLevel.Distributed)]

    public void CreateControlBinding_NumericUpDown_SaveSetting_should_write_default_value_at_non_effective_level(SettingLevel settingLevel)
    {
        NumberSetting<int> setting = new(SettingName, defaultValue: DefaultValue);
        using NumericUpDown numericUpDown = new() { Minimum = 0, Maximum = int.MaxValue };
        setting.CustomControl = numericUpDown;
        ISettingControlBinding binding = SettingControlBindingsProvider.CreateControlBinding(setting);
        SettingsSource settingsSource = Substitute.For<SettingsSource>();
        settingsSource.SettingLevel.Returns(settingLevel);
        settingsSource.GetValue(SettingName).Returns((string?)null);

        numericUpDown.Value = DefaultValue;
        binding.SaveSetting(settingsSource);

        settingsSource.Received(1).SetValue(SettingName, DefaultValue.ToString());
    }
}
