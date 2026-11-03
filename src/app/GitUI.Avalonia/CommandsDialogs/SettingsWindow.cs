using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Edits application preferences and Git configuration values in one dialog.
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly IGitModule _module;
    private readonly (string Label, Func<bool> Get, Action<bool> Set, CheckBox Box)[] _appSettings;
    private readonly Dictionary<string, TextBox> _textBoxes = [];
    private readonly ComboBox _scope = new() { SelectedIndex = 0 };
    private readonly ComboBox _pullRebase = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    /// <summary>
    ///  Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="module">The repository used for the local configuration level.</param>
    public SettingsWindow(IGitModule module)
    {
        _module = module;
        Title = GitUI.TranslatedStrings.SettingsTitle;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MinWidth = 440;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _appSettings = CreateAppSettings();

        StackPanel appPage = new() { Margin = new Avalonia.Thickness(12), Spacing = 8 };
        foreach ((string label, Func<bool> get, Action<bool> _, CheckBox box) in _appSettings)
        {
            box.Content = label;
            box.IsChecked = get();
            appPage.Children.Add(box);
        }

        _scope.ItemsSource = new[] { GitUI.TranslatedStrings.SettingsScopeGlobal, GitUI.TranslatedStrings.SettingsScopeLocal };
        _scope.SelectionChanged += (_, _) => LoadGitConfig();
        _pullRebase.ItemsSource = new[] { GitUI.TranslatedStrings.SettingsUnset, "true", "false" };

        StackPanel gitPage = new() { Margin = new Avalonia.Thickness(12), Spacing = 8 };
        gitPage.Children.Add(new TextBlock { Text = GitUI.TranslatedStrings.SettingsScopeLabel });
        gitPage.Children.Add(_scope);
        foreach (string key in GitConfigWorkflow.TextKeys)
        {
            TextBox box = new() { PlaceholderText = GitUI.TranslatedStrings.SettingsUnset };
            _textBoxes[key] = box;
            gitPage.Children.Add(new TextBlock { Text = key });
            gitPage.Children.Add(box);
        }

        gitPage.Children.Add(new TextBlock { Text = GitConfigWorkflow.PullRebaseKey });
        gitPage.Children.Add(_pullRebase);

        Button save = new() { Content = GitUI.TranslatedStrings.SettingsSave, IsDefault = true };
        save.Click += (_, _) => ThreadHelper.FileAndForget(SaveAsync);
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                new TabControl
                {
                    ItemsSource = new[]
                    {
                        new TabItem { Header = GitUI.TranslatedStrings.SettingsApplicationTab, Content = appPage },
                        new TabItem { Header = GitUI.TranslatedStrings.SettingsGitTab, Content = new ScrollViewer { MaxHeight = 460, Content = gitPage } },
                    },
                },
                _status,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { save, cancel },
                },
            },
        };

        LoadGitConfig();
    }

    private GitConfigScope Scope => _scope.SelectedIndex == 1 ? GitConfigScope.Local : GitConfigScope.Global;

    private static (string Label, Func<bool> Get, Action<bool> Set, CheckBox Box)[] CreateAppSettings() =>
    [
        (GitUI.TranslatedStrings.SettingsRebaseAutoStash, () => AppSettings.RebaseAutoStash, value => AppSettings.RebaseAutoStash = value, new CheckBox()),
        (GitUI.TranslatedStrings.SettingsCloseProcessDialog, () => AppSettings.CloseProcessDialog, value => AppSettings.CloseProcessDialog = value, new CheckBox()),
        (GitUI.TranslatedStrings.SettingsNormaliseBranchName, () => AppSettings.AutoNormaliseBranchName, value => AppSettings.AutoNormaliseBranchName = value, new CheckBox()),
        (GitUI.TranslatedStrings.SettingsShowAuthorDate, () => AppSettings.ShowAuthorDate, value => AppSettings.ShowAuthorDate = value, new CheckBox()),
        (GitUI.TranslatedStrings.SettingsShowGitStatus, () => AppSettings.ShowGitStatusInBrowseToolbar, value => AppSettings.ShowGitStatusInBrowseToolbar = value, new CheckBox()),
    ];

    private void LoadGitConfig()
    {
        GitConfigScope scope = Scope;
        foreach ((string key, TextBox box) in _textBoxes)
        {
            box.Text = GitConfigWorkflow.Read(_module, scope, key);
        }

        string pullRebase = GitConfigWorkflow.Read(_module, scope, GitConfigWorkflow.PullRebaseKey);
        _pullRebase.SelectedIndex = pullRebase switch
        {
            "true" => 1,
            "false" => 2,
            _ => 0,
        };
    }

    private async Task SaveAsync()
    {
        foreach ((_, _, Action<bool> set, CheckBox box) in _appSettings)
        {
            set(box.IsChecked == true);
        }

        GitConfigScope scope = Scope;
        Dictionary<string, string> values = _textBoxes.ToDictionary(pair => pair.Key, pair => pair.Value.Text?.Trim() ?? string.Empty);
        values[GitConfigWorkflow.PullRebaseKey] = _pullRebase.SelectedIndex switch
        {
            1 => "true",
            2 => "false",
            _ => string.Empty,
        };

        string? error = await Task.Run(() =>
        {
            foreach ((string key, string value) in values)
            {
                if (GitConfigWorkflow.Write(_module, scope, key, value) is string failure)
                {
                    return failure;
                }
            }

            return null;
        });

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (error is not null)
        {
            _status.Text = error;
            return;
        }

        Close(true);
    }
}
