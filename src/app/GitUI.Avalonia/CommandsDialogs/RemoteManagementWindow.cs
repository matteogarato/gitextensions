using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands.Remotes;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Shims.WinForms;
using GitUI.Compat;
using ListBox = Avalonia.Controls.ListBox;
using TextBox = Avalonia.Controls.TextBox;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Adds, edits, activates, deactivates, and removes repository remotes.
/// </summary>
public sealed class RemoteManagementWindow : GitExtensionsFormBase
{
    private readonly Button _delete = new();
    private readonly TextBox _fetchUrl = new();
    private readonly TextBox _name = new();
    private readonly TextBox _pushUrl = new();
    private readonly ListBox _remoteList = new() { MinWidth = 210 };
    private readonly IConfigFileRemoteSettingsManager _remotesManager;
    private readonly Button _save = new() { IsDefault = true };
    private readonly Button _toggleState = new();
    private List<ConfigFileRemote> _remotes = [];
    private ConfigFileRemote? _selectedRemote;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RemoteManagementWindow" /> class.
    /// </summary>
    /// <param name="module">The repository whose remotes are managed.</param>
    public RemoteManagementWindow(IGitModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        _remotesManager = new ConfigFileRemoteSettingsManager(() => module);
        Title = TranslatedStrings.RemoteManagementTitle;
        Width = 680;
        Height = 390;
        MinWidth = 560;
        MinHeight = 330;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _remoteList.SelectionChanged += (_, _) => SelectRemote(_remoteList.SelectedIndex);
        _save.Content = TranslatedStrings.AddRemote;
        _save.Click += (_, _) => SaveRemote();
        _delete.Content = TranslatedStrings.DeleteRemote;
        _delete.Click += (_, _) => DeleteRemote();
        _toggleState.Click += (_, _) => ToggleRemoteState();
        Button add = new() { Content = TranslatedStrings.AddRemote };
        add.Click += (_, _) => SelectRemote(-1);
        Button close = new() { Content = TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close(HasChanges);

        StackPanel remoteDetails = new()
        {
            Spacing = 8,
            Children =
            {
                CreateField(TranslatedStrings.RemoteName, _name),
                CreateField(TranslatedStrings.FetchUrl, _fetchUrl),
                CreateField(TranslatedStrings.PushUrl, _pushUrl)
            }
        };
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };
        actions.Children.Add(add);
        actions.Children.Add(_save);
        actions.Children.Add(_delete);
        actions.Children.Add(_toggleState);
        actions.Children.Add(close);
        Grid.SetColumn(remoteDetails, 2);
        Grid.SetRow(actions, 1);
        Grid.SetColumnSpan(actions, 3);

        Grid layout = new()
        {
            Margin = new Thickness(12),
            ColumnDefinitions = new ColumnDefinitions("Auto,12,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children = { _remoteList, remoteDetails, actions }
        };
        Content = layout;

        ReloadRemotes();
    }

    /// <summary>
    ///     Gets a value indicating whether any remote configuration changed while this window was open.
    /// </summary>
    public bool HasChanges { get; private set; }

    private static StackPanel CreateField(string label, TextBox input)
    {
        return new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label }, input } };
    }

    private void ReloadRemotes(string? selectedName = null)
    {
        _remotes = [.. _remotesManager.LoadRemotes(true)];
        _remoteList.ItemsSource = _remotes
            .Select(remote => remote.Disabled
                ? string.Format(TranslatedStrings.RemoteInactive, remote.Name)
                : remote.Name ?? string.Empty)
            .ToArray();

        var selectedIndex = selectedName is null
            ? -1
            : _remotes.FindIndex(remote => remote.Name == selectedName);
        _remoteList.SelectedIndex = selectedIndex;
        if (selectedIndex < 0)
        {
            SelectRemote(-1);
        }
    }

    private void SelectRemote(int index)
    {
        _selectedRemote = index >= 0 && index < _remotes.Count
            ? _remotes[index]
            : null;

        _name.Text = _selectedRemote?.Name ?? string.Empty;
        _fetchUrl.Text = _selectedRemote?.Url ?? string.Empty;
        _pushUrl.Text = _selectedRemote?.PushUrl ?? string.Empty;

        var isEditable = _selectedRemote?.Disabled != true;
        _name.IsEnabled = isEditable;
        _fetchUrl.IsEnabled = isEditable;
        _pushUrl.IsEnabled = isEditable;
        _save.Content = _selectedRemote is null
            ? TranslatedStrings.AddRemote
            : TranslatedStrings.SaveRemote;
        _save.IsEnabled = isEditable;
        _delete.IsEnabled = _selectedRemote is not null;
        _toggleState.IsEnabled = _selectedRemote is not null;
        _toggleState.Content = _selectedRemote?.Disabled == true
            ? TranslatedStrings.ActivateRemote
            : TranslatedStrings.DeactivateRemote;
    }

    private void SaveRemote()
    {
        var name = _name.Text?.Trim() ?? string.Empty;
        var fetchUrl = _fetchUrl.Text?.Trim() ?? string.Empty;
        var pushUrl = _pushUrl.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.RemoteNameRequiredError);
            return;
        }

        if (string.IsNullOrWhiteSpace(fetchUrl))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.RemoteUrlRequiredError);
            return;
        }

        if (!string.Equals(_selectedRemote?.Name, name, StringComparison.Ordinal)
            && (_remotesManager.EnabledRemoteExists(name) || _remotesManager.DisabledRemoteExists(name)))
        {
            MessageBoxes.ShowError(this, string.Format(TranslatedStrings.RemoteAlreadyExistsError, name));
            return;
        }

        var result = _remotesManager.SaveRemote(
            _selectedRemote,
            name,
            fetchUrl,
            string.IsNullOrWhiteSpace(pushUrl) ? null : pushUrl,
            _selectedRemote?.PuttySshKey ?? string.Empty,
            _selectedRemote?.Color,
            _selectedRemote?.Prefix);

        if (!string.IsNullOrWhiteSpace(result.UserMessage))
        {
            MessageBoxes.ShowError(this, result.UserMessage);
            return;
        }

        HasChanges = true;
        ReloadRemotes(name);
    }

    private void DeleteRemote()
    {
        if (_selectedRemote is null)
        {
            return;
        }

        var name = _selectedRemote.Name ?? string.Empty;
        if (MessageBoxes.Show(
                this,
                string.Format(TranslatedStrings.ConfirmDeleteRemote, name),
                TranslatedStrings.DeleteRemote,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        var output = _remotesManager.RemoveRemote(_selectedRemote);
        if (!string.IsNullOrWhiteSpace(output))
        {
            MessageBoxes.ShowError(this, output);
            return;
        }

        HasChanges = true;
        ReloadRemotes();
    }

    private void ToggleRemoteState()
    {
        if (_selectedRemote?.Name is not { } remoteName)
        {
            return;
        }

        var selectedName = remoteName;
        _remotesManager.ToggleRemoteState(remoteName, !_selectedRemote.Disabled);
        HasChanges = true;
        ReloadRemotes(selectedName);
    }
}
