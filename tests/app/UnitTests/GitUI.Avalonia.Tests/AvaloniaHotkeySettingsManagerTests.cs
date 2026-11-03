using System.Xml.Serialization;
using Avalonia.Input;
using GitCommands;
using GitExtensions.Shims.WinForms;
using GitUI.Hotkey;
using ResourceManager;

namespace GitExtensionsTests;

public sealed class AvaloniaHotkeySettingsManagerTests
{
    private static readonly XmlSerializer _serializer = new(typeof(HotkeySettings[]), [typeof(HotkeyCommand)]);

    [Fact]
    public void SaveBrowseHotkeys_should_preserve_other_settings_and_unknown_browse_commands()
    {
        string originalSerializedHotkeys = AppSettings.SerializedHotkeys ?? string.Empty;
        try
        {
            AppSettings.SerializedHotkeys = Serialize(
            [
                new HotkeySettings("Commit", new HotkeyCommand(7, "Commit") { KeyData = Keys.F7 }),
                new HotkeySettings("Browse", new HotkeyCommand(999, "Legacy") { KeyData = Keys.F8 }),
            ]);

            AvaloniaHotkeySettingsManager manager = new();
            HotkeyCommand[] hotkeys = [.. manager.LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName)];
            hotkeys.Single(command => command.CommandCode == (int)AvaloniaBrowseHotkeyCommand.OpenRepository).KeyData = Keys.F6;
            manager.SaveBrowseHotkeys(hotkeys);

            HotkeySettings[] savedSettings = Deserialize(AppSettings.SerializedHotkeys ?? string.Empty);
            savedSettings.Single(setting => setting.Name == "Commit").Commands
                .Should().ContainSingle(command => command.CommandCode == 7 && command.KeyData == Keys.F7);
            HotkeyCommand[] savedBrowseCommands = savedSettings.Single(setting => setting.Name == "Browse").Commands!;
            savedBrowseCommands.Should().ContainSingle(command => command.CommandCode == 999 && command.KeyData == Keys.F8);
            savedBrowseCommands.Should().ContainSingle(command =>
                command.CommandCode == (int)AvaloniaBrowseHotkeyCommand.OpenRepository
                && command.KeyData == Keys.F6);
        }
        finally
        {
            AppSettings.SerializedHotkeys = originalSerializedHotkeys;
        }
    }

    [Fact]
    public void SaveBrowseHotkeys_should_reject_duplicate_shortcuts()
    {
        AvaloniaHotkeySettingsManager manager = new();
        HotkeyCommand[] hotkeys = [.. manager.LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName)];
        hotkeys[1].KeyData = hotkeys[0].KeyData;

        Action save = () => manager.SaveBrowseHotkeys(hotkeys);

        save.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryCreateKeyData_should_convert_key_and_modifiers()
    {
        AvaloniaHotkeyKeyConverter.TryCreateKeyData(Key.O, KeyModifiers.Control | KeyModifiers.Shift, out Keys keyData)
            .Should().BeTrue();

        keyData.Should().Be(Keys.Control | Keys.Shift | Keys.O);
        AvaloniaHotkeyKeyConverter.TryCreateKeyData(Key.O, KeyModifiers.Meta, out _).Should().BeFalse();
    }

    private static HotkeySettings[] Deserialize(string serializedSettings)
    {
        using StringReader reader = new(serializedSettings);
        object? deserialized = _serializer.Deserialize(reader);
        return deserialized is HotkeySettings[] settings
            ? settings
            : throw new InvalidOperationException("The saved hotkey configuration could not be deserialized.");
    }

    private static string Serialize(HotkeySettings[] settings)
    {
        using StringWriter writer = new();
        _serializer.Serialize(writer, settings);
        return writer.ToString();
    }
}
