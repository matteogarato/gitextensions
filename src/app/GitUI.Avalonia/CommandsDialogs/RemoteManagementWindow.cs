using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using GitExtensions.Shims.WinForms;
using ResourceManager;
using ListBox = Avalonia.Controls.ListBox;
using TextBox = Avalonia.Controls.TextBox;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Adds, edits, activates, deactivates, and removes repository remotes.
/// </summary>
public sealed class RemoteManagementWindow : GitExtensionsFormBase
{
    private readonly IConfigFileRemoteSettingsManager _remotesManager;
    private readonly ListBox _remoteList = new() { MinWidth = 210 };
    private readonly TextBox _name = new();
    private readonly TextBox _fetchUrl = new();
    private readonly TextBox _pushUrl = new();
    private readonly Button _save = new() { IsDefault = true };
    private readonly Button _delete = new();
    private readonly Button _toggleState = new();
    private List<ConfigFileRemote> _remotes = [];
    private ConfigFileRemote? _selectedRemote;
    private bool _hasChanges;

    /// <summary>
    ///  Gets a value indicating whether any remote configuration changed while this window was open.
    /// </summary>
    public bool HasChanges => _hasChanges;

    /// <summary>
    ///  Initializes a new instance of the <see cref="RemoteManagementWindow"/> class.
    /// </summary>
    /// <param name="module">The repository whose remotes are managed.</param>
    public RemoteManagementWindow(IGitModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        _remotesManager = new ConfigFileRemoteSettingsManager(() => module);
        Title = GitUI.TranslatedStrings.RemoteManagementTitle;
        Width = 680;
        Height = 390;
        MinWidth = 560;
        MinHeight = 330;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _remoteList.SelectionChanged += (_, _) => SelectRemote(_remoteList.SelectedIndex);
        _save.Content = GitUI.TranslatedStrings.AddRemote;
        _save.Click += (_, _) => SaveRemote();
        _delete.Content = GitUI.TranslatedStrings.DeleteRemote;
        _delete.Click += (_, _) => DeleteRemote();
        _toggleState.Click += (_, _) => ToggleRemoteState();
        Button add = new() { Content = GitUI.TranslatedStrings.AddRemote };
        add.Click += (_, _) => SelectRemote(index: -1);
        Button close = new() { Content = GitUI.TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close(_hasChanges);

        StackPanel remoteDetails = new()
        {
            Spacing = 8,
            Children =
            {
                CreateField(GitUI.TranslatedStrings.RemoteName, _name),
                CreateField(GitUI.TranslatedStrings.FetchUrl, _fetchUrl),
                CreateField(GitUI.TranslatedStrings.PushUrl, _pushUrl),
            },
        };
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
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
            Margin = new Avalonia.Thickness(12),
            ColumnDefinitions = new ColumnDefinitions("Auto,12,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                _remoteList,
                remoteDetails,
                actions,
            },
        };
        Content = layout;

        ReloadRemotes();
    }

    private static StackPanel CreateField(string label, TextBox input)
        => new()
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label },
                input,
            },
        };

    private void ReloadRemotes(string? selectedName = null)
    {
        _remotes = [.. _remotesManager.LoadRemotes(loadDisabled: true)];
        _remoteList.ItemsSource = _remotes
            .Select(remote => remote.Disabled
                ? string.Format(GitUI.TranslatedStrings.RemoteInactive, remote.Name)
                : remote.Name ?? string.Empty)
            .ToArray();

        int selectedIndex = selectedName is null
            ? -1
            : _remotes.FindIndex(remote => remote.Name == selectedName);
        _remoteList.SelectedIndex = selectedIndex;
        if (selectedIndex < 0)
        {
            SelectRemote(index: -1);
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

        bool isEditable = _selectedRemote?.Disabled != true;
        _name.IsEnabled = isEditable;
        _fetchUrl.IsEnabled = isEditable;
        _pushUrl.IsEnabled = isEditable;
        _save.Content = _selectedRemote is null
            ? GitUI.TranslatedStrings.AddRemote
            : GitUI.TranslatedStrings.SaveRemote;
        _save.IsEnabled = isEditable;
        _delete.IsEnabled = _selectedRemote is not null;
        _toggleState.IsEnabled = _selectedRemote is not null;
        _toggleState.Content = _selectedRemote?.Disabled == true
            ? GitUI.TranslatedStrings.ActivateRemote
            : GitUI.TranslatedStrings.DeactivateRemote;
    }

    private void SaveRemote()
    {
        string name = _name.Text?.Trim() ?? string.Empty;
        string fetchUrl = _fetchUrl.Text?.Trim() ?? string.Empty;
        string pushUrl = _pushUrl.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(this, GitUI.TranslatedStrings.RemoteNameRequiredError);
            return;
        }

        if (string.IsNullOrWhiteSpace(fetchUrl))
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(this, GitUI.TranslatedStrings.RemoteUrlRequiredError);
            return;
        }

        if (!string.Equals(_selectedRemote?.Name, name, StringComparison.Ordinal)
            && (_remotesManager.EnabledRemoteExists(name) || _remotesManager.DisabledRemoteExists(name)))
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(this, string.Format(GitUI.TranslatedStrings.RemoteAlreadyExistsError, name));
            return;
        }

        ConfigFileRemoteSaveResult result = _remotesManager.SaveRemote(
            _selectedRemote,
            name,
            fetchUrl,
            string.IsNullOrWhiteSpace(pushUrl) ? null : pushUrl,
            _selectedRemote?.PuttySshKey ?? string.Empty,
            _selectedRemote?.Color,
            _selectedRemote?.Prefix);

        if (!string.IsNullOrWhiteSpace(result.UserMessage))
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(this, result.UserMessage);
            return;
        }

        _hasChanges = true;
        ReloadRemotes(name);
    }

    private void DeleteRemote()
    {
        if (_selectedRemote is null)
        {
            return;
        }

        string name = _selectedRemote.Name ?? string.Empty;
        if (GitExtensions.Extensibility.MessageBoxes.Show(
                this,
                string.Format(GitUI.TranslatedStrings.ConfirmDeleteRemote, name),
                GitUI.TranslatedStrings.DeleteRemote,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        string output = _remotesManager.RemoveRemote(_selectedRemote);
        if (!string.IsNullOrWhiteSpace(output))
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(this, output);
            return;
        }

        _hasChanges = true;
        ReloadRemotes();
    }

    private void ToggleRemoteState()
    {
        if (_selectedRemote?.Name is not string remoteName)
        {
            return;
        }

        string selectedName = remoteName;
        _remotesManager.ToggleRemoteState(remoteName, disabled: !_selectedRemote.Disabled);
        _hasChanges = true;
        ReloadRemotes(selectedName);
    }
}
