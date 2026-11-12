using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitUI.Compat;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Displays the in-memory Git Extensions command log.
/// </summary>
public sealed class CommandLogWindow : GitExtensionsFormBase
{
    private const string _monospaceFontFamily = "monospace";

    private static CommandLogWindow? _instance;

    private readonly TextBox _details = new()
    {
        AcceptsReturn = true,
        FontFamily = FontFamily.Parse(_monospaceFontFamily),
        IsReadOnly = true,
        TextWrapping = TextWrapping.NoWrap
    };

    private readonly ListBox _entries = new();
    private bool _isClosed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandLogWindow" /> class.
    /// </summary>
    public CommandLogWindow()
    {
        Title = TranslatedStrings.CommandLogTitle;
        Width = 1000;
        Height = 650;
        MinWidth = 640;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _entries.ItemTemplate = new FuncDataTemplate<CommandLogEntry>(
            (entry, _) => new TextBlock
            {
                FontFamily = FontFamily.Parse(_monospaceFontFamily), Text = entry.ColumnLine
            },
            false);
        _entries.SelectionChanged += (_, _) => UpdateDetails();

        Button clear = new()
        {
            Content = TranslatedStrings.ClearFilter, HorizontalAlignment = HorizontalAlignment.Right
        };
        clear.Click += (_, _) =>
        {
            CommandLog.Clear();
            RefreshEntries();
        };

        Button copy = new() { Content = TranslatedStrings.CommandLogCopy };
        copy.Click += (_, _) => ThreadHelper.FileAndForget(CopyAsync);
        Button export = new() { Content = TranslatedStrings.CommandLogExport };
        export.Click += (_, _) => ThreadHelper.FileAndForget(ExportAsync);
        StackPanel actions = new()
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { copy, export, clear }
        };

        Grid.SetRow(_details, 1);
        Grid.SetRow(actions, 2);
        Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("3*,2*,Auto"),
            RowSpacing = 8,
            Children = { _entries, _details, actions }
        };

        CommandLog.CommandsChanged += OnCommandsChanged;
        Closed += (_, _) =>
        {
            _isClosed = true;
            CommandLog.CommandsChanged -= OnCommandsChanged;
            if (_instance == this)
            {
                _instance = null;
            }
        };

        RefreshEntries();
    }

    /// <summary>
    ///     Opens the command log or activates its existing window.
    /// </summary>
    /// <param name="owner">The owning browse window.</param>
    public static void ShowOrActivate(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_instance is { IsVisible: true } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        _instance = new CommandLogWindow();
        _instance.Show(owner);
    }

    /// <summary>
    ///     Formats log entries as plain text, one block per command, for copying or saving.
    /// </summary>
    /// <param name="entries">The entries in chronological order.</param>
    /// <returns>The report text.</returns>
    public static string BuildReport(IEnumerable<CommandLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            entries.Select(entry => $"{entry.ColumnLine}{Environment.NewLine}{entry.Detail}"));
    }

    private async Task CopyAsync()
    {
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(BuildReport(CommandLog.Commands));
        }
    }

    private async Task ExportAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = TranslatedStrings.CommandLogExport,
            SuggestedFileName = "git-command-log.txt",
            DefaultExtension = "txt"
        });
        if (file?.TryGetLocalPath() is not { } path)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(path, BuildReport(CommandLog.Commands));
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
    }

    private void OnCommandsChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isClosed)
            {
                RefreshEntries();
            }
        });
    }

    private void RefreshEntries()
    {
        var selectedEntry = _entries.SelectedItem as CommandLogEntry;
        var entries = CommandLog.Commands.ToArray();
        _entries.ItemsSource = entries;
        _entries.SelectedItem = selectedEntry is not null && entries.Contains(selectedEntry)
            ? selectedEntry
            : entries.LastOrDefault();
        UpdateDetails();
    }

    private void UpdateDetails()
    {
        _details.Text = _entries.SelectedItem is CommandLogEntry selectedEntry
            ? selectedEntry.Detail
            : string.Empty;
    }
}
