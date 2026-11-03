using System.Xml.Serialization;
using GitCommands;
using GitExtensions.Shims.WinForms;
using ResourceManager;

namespace GitUI.Hotkey;

internal enum AvaloniaBrowseHotkeyCommand
{
    OpenRepository = 45,
    ToggleDashboard = 50,
    Refresh = 51,
    Pull = 39,
    Push = 40,
}

/// <summary>Loads and saves the shortcuts supported by the Avalonia browse window.</summary>
public sealed class AvaloniaHotkeySettingsManager : IHotkeySettingsLoader
{
    /// <summary>The settings group used by the shared browse-window hotkey configuration.</summary>
    public const string BrowseSettingsName = "Browse";

    private static readonly XmlSerializer _serializer = new(typeof(HotkeySettings[]), [typeof(HotkeyCommand)]);

    /// <summary>Loads supported browse shortcuts, applying any saved overrides.</summary>
    /// <param name="hotkeySettingsName">The hotkey settings group to load.</param>
    /// <returns>The configured shortcuts for the browse window, or an empty list for other groups.</returns>
    public IReadOnlyList<HotkeyCommand> LoadHotkeys(string hotkeySettingsName)
    {
        if (hotkeySettingsName != BrowseSettingsName)
        {
            return [];
        }

        HotkeyCommand[] defaults = CreateDefaultBrowseHotkeys();
        HotkeySettings? settings = LoadSerializedSettings()
            .SingleOrDefault(setting => setting.Name == BrowseSettingsName);

        if (settings?.Commands is not null)
        {
            foreach (HotkeyCommand command in defaults)
            {
                HotkeyCommand? savedCommand = settings.Commands
                    .FirstOrDefault(saved => saved.CommandCode == command.CommandCode);
                if (savedCommand is not null)
                {
                    command.KeyData = savedCommand.KeyData;
                }
            }
        }

        return defaults;
    }

    /// <summary>Saves the supported browse shortcuts while preserving other hotkey settings.</summary>
    /// <param name="hotkeys">The browse shortcuts to save.</param>
    public void SaveBrowseHotkeys(IEnumerable<HotkeyCommand> hotkeys)
    {
        HotkeyCommand[] updatedCommands = hotkeys.ToArray();
        int[] supportedCommandCodes = Enum.GetValues<AvaloniaBrowseHotkeyCommand>()
            .Select(command => (int)command)
            .ToArray();

        if (updatedCommands.Length != supportedCommandCodes.Length
            || updatedCommands.Select(command => command.CommandCode).Distinct().Count() != supportedCommandCodes.Length
            || updatedCommands.Any(command => !supportedCommandCodes.Contains(command.CommandCode)))
        {
            throw new ArgumentException("The hotkey list must contain each supported Avalonia browse command exactly once.", nameof(hotkeys));
        }

        Keys[] assignedKeys = updatedCommands
            .Where(command => command.KeyData != Keys.None)
            .Select(command => command.KeyData)
            .ToArray();
        if (assignedKeys.Distinct().Count() != assignedKeys.Length)
        {
            throw new InvalidOperationException(ResourceManager.TranslatedStrings.HotkeyDuplicateError);
        }

        List<HotkeySettings> allSettings = LoadSerializedSettings().ToList();
        HotkeySettings? browseSettings = allSettings.SingleOrDefault(setting => setting.Name == BrowseSettingsName);
        if (browseSettings is null)
        {
            browseSettings = new HotkeySettings(BrowseSettingsName);
            allSettings.Add(browseSettings);
        }

        HashSet<int> supportedCodes = [.. supportedCommandCodes];
        HotkeyCommand[] existingCommands = browseSettings.Commands ?? [];
        browseSettings.Commands = existingCommands
            .Where(command => !supportedCodes.Contains(command.CommandCode))
            .Concat(updatedCommands)
            .ToArray();

        using StringWriter writer = new();
        _serializer.Serialize(writer, allSettings.ToArray());
        AppSettings.SerializedHotkeys = writer.ToString();
    }

    private static List<HotkeySettings> LoadSerializedSettings()
    {
        string? serializedHotkeys = AppSettings.SerializedHotkeys;
        if (string.IsNullOrWhiteSpace(serializedHotkeys))
        {
            return [];
        }

        using StringReader reader = new(serializedHotkeys);
        object? deserialized = _serializer.Deserialize(reader);
        return deserialized is HotkeySettings[] settings
            ? [.. settings]
            : throw new InvalidOperationException("The saved hotkey configuration has an unexpected format.");
    }

    private static HotkeyCommand[] CreateDefaultBrowseHotkeys()
    {
        return
        [
            Create(AvaloniaBrowseHotkeyCommand.OpenRepository, Keys.Control | Keys.O),
            Create(AvaloniaBrowseHotkeyCommand.ToggleDashboard, Keys.Control | Keys.Shift | Keys.O),
            Create(AvaloniaBrowseHotkeyCommand.Refresh, Keys.F5),
            Create(AvaloniaBrowseHotkeyCommand.Pull, Keys.Control | Keys.Down),
            Create(AvaloniaBrowseHotkeyCommand.Push, Keys.Control | Keys.Up),
        ];

        static HotkeyCommand Create(AvaloniaBrowseHotkeyCommand command, Keys keyData)
        {
            return new HotkeyCommand((int)command, command.ToString()) { KeyData = keyData };
        }
    }
}
