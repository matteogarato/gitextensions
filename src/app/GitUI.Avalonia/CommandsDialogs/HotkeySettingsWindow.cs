using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using GitExtensions.Shims.WinForms;
using GitUI.Hotkey;
using ResourceManager;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Edits keyboard shortcuts supported by the Avalonia browse window.
/// </summary>
public sealed class HotkeySettingsWindow : Window
{
    private readonly AvaloniaHotkeySettingsManager _settingsManager;
    private readonly HotkeyCommand[] _hotkeys;
    private readonly TextBlock _error = new();

    /// <summary>
    ///  Initializes a new instance of the <see cref="HotkeySettingsWindow"/> class.
    /// </summary>
    /// <param name="settingsManager">The settings manager used to load and save shortcuts.</param>
    public HotkeySettingsWindow(AvaloniaHotkeySettingsManager settingsManager)
    {
        ArgumentNullException.ThrowIfNull(settingsManager);
        _settingsManager = settingsManager;
        _hotkeys = [.. settingsManager.LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName)];

        Title = ResourceManager.TranslatedStrings.KeyboardShortcutsTitle;
        Width = 560;
        MinWidth = 500;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        StackPanel rows = new() { Spacing = 8 };
        foreach (HotkeyCommand hotkey in _hotkeys)
        {
            rows.Children.Add(CreateHotkeyRow(hotkey));
        }

        Button save = new() { Content = ResourceManager.TranslatedStrings.Save, IsDefault = true };
        save.Click += (_, _) => SaveHotkeys();
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = ResourceManager.TranslatedStrings.KeyboardShortcutsHelp },
                rows,
                _error,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { save, cancel },
                },
            },
        };
    }

    private static string GetCommandLabel(int commandCode)
    {
        return (AvaloniaBrowseHotkeyCommand)commandCode switch
        {
            AvaloniaBrowseHotkeyCommand.OpenRepository => ResourceManager.TranslatedStrings.OpenRepositoryShortcut,
            AvaloniaBrowseHotkeyCommand.ToggleDashboard => ResourceManager.TranslatedStrings.ToggleDashboardShortcut,
            AvaloniaBrowseHotkeyCommand.Refresh => ResourceManager.TranslatedStrings.RefreshShortcut,
            AvaloniaBrowseHotkeyCommand.Pull => ResourceManager.TranslatedStrings.PullShortcut,
            AvaloniaBrowseHotkeyCommand.Push => ResourceManager.TranslatedStrings.PushShortcut,
            _ => throw new ArgumentOutOfRangeException(nameof(commandCode), commandCode, "Unknown Avalonia browse hotkey."),
        };
    }

    private static string GetDisplayString(Keys keyData)
    {
        return keyData == Keys.None
            ? string.Empty
            : new KeysConverter().ConvertToString(null, CultureInfo.CurrentCulture, keyData) ?? string.Empty;
    }

    private StackPanel CreateHotkeyRow(HotkeyCommand hotkey)
    {
        Avalonia.Controls.TextBox keyField = new()
        {
            Text = GetDisplayString(hotkey.KeyData),
            IsReadOnly = true,
            MinWidth = 180,
        };
        keyField.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None)
            {
                return;
            }

            if (!AvaloniaHotkeyKeyConverter.TryCreateKeyData(e.Key, e.KeyModifiers, out Keys keyData))
            {
                _error.Text = ResourceManager.TranslatedStrings.UnsupportedShortcutKey;
                return;
            }

            hotkey.KeyData = keyData;
            keyField.Text = GetDisplayString(keyData);
            _error.Text = string.Empty;
            e.Handled = true;
        };

        Button clear = new() { Content = ResourceManager.TranslatedStrings.ClearShortcut };
        clear.Click += (_, _) =>
        {
            hotkey.KeyData = Keys.None;
            keyField.Text = string.Empty;
            _error.Text = string.Empty;
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = GetCommandLabel(hotkey.CommandCode),
                    Width = 190,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                },
                keyField,
                clear,
            },
        };
    }

    private void SaveHotkeys()
    {
        Keys[] assignedKeys = _hotkeys
            .Where(hotkey => hotkey.KeyData != Keys.None)
            .Select(hotkey => hotkey.KeyData)
            .ToArray();
        if (assignedKeys.Distinct().Count() != assignedKeys.Length)
        {
            _error.Text = ResourceManager.TranslatedStrings.HotkeyDuplicateError;
            return;
        }

        _settingsManager.SaveBrowseHotkeys(_hotkeys);
        Close(true);
    }
}
