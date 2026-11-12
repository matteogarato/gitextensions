using GitUI.Hotkey;
using GitUI.ScriptsEngine;
using NSubstitute;
using ResourceManager.Hotkey;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.Hotkey;

public class HotkeySettingsManagerTests
{
    private readonly HotkeySettingsManager _settingsManager = null!;

    public HotkeySettingsManagerTests()
    {
        var scriptsManager = Substitute.For<IScriptsManager>();
        scriptsManager.GetScripts().Returns([]);

        _settingsManager = new HotkeySettingsManager(scriptsManager);
    }

    [Fact]
    public void MergeEqualSettings()
    {
        // arrange

        var defaultHotkeySettingsArray = CreateHotkeySettings(2);
        var loadedHotkeySettingsArray = CreateHotkeySettings(2);

        HotkeySettingsManager.MergeIntoDefaultSettings(defaultHotkeySettingsArray, loadedHotkeySettingsArray);
        var expected = CreateHotkeySettings(2);

        defaultHotkeySettingsArray.SequenceEqual(expected).Should().BeTrue();
    }

    internal void SequenceEqualOnDifferentSettings()
    {
        var defaultHotkeySettingsArray = CreateHotkeySettings(2);
        var loadedHotkeySettingsArray = CreateHotkeySettings(2);
        loadedHotkeySettingsArray[0].Commands![0].KeyData = Keys.C;

        defaultHotkeySettingsArray.SequenceEqual(loadedHotkeySettingsArray).Should().BeFalse();
    }

    [Fact]
    public void SequenceEqualOnEqualSettings()
    {
        var defaultHotkeySettingsArray = CreateHotkeySettings(2);
        var loadedHotkeySettingsArray = CreateHotkeySettings(2);

        defaultHotkeySettingsArray.SequenceEqual(loadedHotkeySettingsArray).Should().BeTrue();
    }

    [Fact]
    public void MergeLoadedSettings()
    {
        // arrange

        var defaultHotkeySettingsArray = CreateHotkeySettings(2);
        var loadedHotkeySettingsArray = CreateHotkeySettings(2);
        loadedHotkeySettingsArray[0].Commands![0].KeyData = Keys.C;

        HotkeySettingsManager.MergeIntoDefaultSettings(defaultHotkeySettingsArray, loadedHotkeySettingsArray);

        defaultHotkeySettingsArray.SequenceEqual(loadedHotkeySettingsArray).Should().BeTrue();
    }

    [Fact]
    public void MergeLoadedDiffSizeSettings()
    {
        // arrange

        var defaultHotkeySettingsArray = CreateHotkeySettings(3);
        var loadedHotkeySettingsArray = CreateHotkeySettings(2);
        loadedHotkeySettingsArray[1].Commands![1].KeyData = Keys.C;

        HotkeySettingsManager.MergeIntoDefaultSettings(defaultHotkeySettingsArray, loadedHotkeySettingsArray);
        var expected = CreateHotkeySettings(3);
        expected[1].Commands![1].KeyData = loadedHotkeySettingsArray[1].Commands![1].KeyData;

        defaultHotkeySettingsArray.SequenceEqual(expected).Should().BeTrue();
    }

    [Fact]
    public async Task Can_save_settings()
    {
        var originalHotkeys = AppSettings.SerializedHotkeys;

        try
        {
            _settingsManager.SaveSettings(CreateHotkeySettings(2));

            // Verify as a string, as the xml verifier ignores line breaks.
            await Verify(AppSettings.SerializedHotkeys);
        }
        finally
        {
            AppSettings.SerializedHotkeys = originalHotkeys!;
        }
    }

    private static HotkeySettings[] CreateHotkeySettings(int count)
    {
        return
        [
            .. Enumerable.Range(1, count).Select(i =>
                new HotkeySettings("settings" + i,
                    new HotkeyCommand(1, "C1") { KeyData = Keys.A },
                    new HotkeyCommand(2, "C2") { KeyData = Keys.B }))
        ];
    }
}
