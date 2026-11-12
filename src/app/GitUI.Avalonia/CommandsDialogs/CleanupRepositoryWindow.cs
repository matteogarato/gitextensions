using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Shims.WinForms;
using GitUI.Compat;
using CheckBox = Avalonia.Controls.CheckBox;
using TextBox = Avalonia.Controls.TextBox;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Previews and removes untracked files from a repository.
/// </summary>
public sealed class CleanupRepositoryWindow : GitExtensionsFormBase
{
    private readonly Button _btnBrowseExclude = new() { Content = TranslatedStrings.BrowseForDirectory };
    private readonly Button _btnBrowseInclude = new() { Content = TranslatedStrings.BrowseForDirectory };
    private readonly Button _btnCleanup = new();
    private readonly Button _btnClose = new() { IsCancel = true };
    private readonly Button _btnPreview = new() { IsDefault = true };
    private readonly CheckBox _chkCleanSubmodules = new();
    private readonly CheckBox _chkExcludePaths = new();
    private readonly CheckBox _chkIncludePaths = new();
    private readonly CheckBox _chkRemoveDirectories = new();
    private readonly RadioButton _rbRemoveAll = new() { GroupName = "CleanupMode" };
    private readonly RadioButton _rbRemoveIgnored = new() { GroupName = "CleanupMode" };
    private readonly RadioButton _rbRemoveNonIgnored = new() { GroupName = "CleanupMode" };

    private readonly TextBox _txtExcludePaths = new()
    {
        AcceptsReturn = true, MinHeight = 72, TextWrapping = TextWrapping.Wrap, IsEnabled = false
    };

    private readonly TextBox _txtIncludePaths = new()
    {
        AcceptsReturn = true, MinHeight = 72, TextWrapping = TextWrapping.Wrap, IsEnabled = false
    };

    private readonly TextBox _txtPreviewOutput = new()
    {
        AcceptsReturn = true, IsReadOnly = true, MinHeight = 140, TextWrapping = TextWrapping.NoWrap
    };

    private readonly IGitUiCommands _uiCommands;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CleanupRepositoryWindow" /> class.
    /// </summary>
    /// <param name="uiCommands">Commands used to run Git with process feedback.</param>
    /// <param name="path">An optional path to use for both path filters.</param>
    public CleanupRepositoryWindow(IGitUiCommands uiCommands, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);
        _uiCommands = uiCommands;

        Title = TranslatedStrings.CleanupRepositoryTitle;
        Width = 680;
        Height = 760;
        MinWidth = 560;
        MinHeight = 620;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _rbRemoveAll.Content = TranslatedStrings.CleanupRemoveAllFiles;
        _rbRemoveAll.IsChecked = true;
        _rbRemoveNonIgnored.Content = TranslatedStrings.CleanupRemoveNonIgnoredFiles;
        _rbRemoveIgnored.Content = TranslatedStrings.CleanupRemoveIgnoredFiles;
        _chkRemoveDirectories.Content = TranslatedStrings.CleanupRemoveDirectories;
        _chkRemoveDirectories.IsChecked = true;
        _chkCleanSubmodules.Content = TranslatedStrings.CleanupSubmodulesRecursive;

        _chkIncludePaths.Content = TranslatedStrings.CleanupIncludePaths;
        _chkIncludePaths.IsCheckedChanged += (_, _) => _txtIncludePaths.IsEnabled = _chkIncludePaths.IsChecked == true;
        _chkExcludePaths.Content = TranslatedStrings.CleanupExcludePaths;
        _chkExcludePaths.IsCheckedChanged += (_, _) => _txtExcludePaths.IsEnabled = _chkExcludePaths.IsChecked == true;
        _txtIncludePaths.Text = path;
        _txtExcludePaths.Text = path;
        if (!string.IsNullOrWhiteSpace(path))
        {
            _chkIncludePaths.IsChecked = true;
            _chkExcludePaths.IsChecked = true;
        }

        _btnBrowseInclude.Click += (_, _) => ThreadHelper.FileAndForget(BrowseIncludePathsAsync);
        _btnBrowseExclude.Click += (_, _) => ThreadHelper.FileAndForget(BrowseExcludePathsAsync);

        _btnPreview.Content = TranslatedStrings.CleanupPreview;
        _btnPreview.Click += (_, _) => ThreadHelper.FileAndForget(PreviewAsync);
        _btnCleanup.Content = TranslatedStrings.CleanupAction;
        _btnCleanup.Click += (_, _) => ThreadHelper.FileAndForget(CleanupAsync);
        _btnClose.Content = TranslatedStrings.Close;
        _btnClose.Click += (_, _) => Close(false);

        var includePathPicker = CreatePathPicker(_txtIncludePaths, _btnBrowseInclude);
        var excludePathPicker = CreatePathPicker(_txtExcludePaths, _btnBrowseExclude);
        StackPanel options = new()
        {
            Spacing = 8,
            Children =
            {
                new GroupBox
                {
                    Header = TranslatedStrings.CleanupRemoveUntrackedGroup,
                    Content =
                        new StackPanel
                        {
                            Spacing = 6,
                            Children = { _rbRemoveAll, _rbRemoveNonIgnored, _rbRemoveIgnored }
                        }
                },
                _chkRemoveDirectories,
                _chkCleanSubmodules,
                _chkIncludePaths,
                includePathPicker,
                new TextBlock { Text = TranslatedStrings.CleanupOnePathPerLine },
                _chkExcludePaths,
                excludePathPicker,
                new TextBlock { Text = TranslatedStrings.CleanupOnePathPerLine },
                new TextBlock { Text = TranslatedStrings.CleanupLog },
                _txtPreviewOutput
            }
        };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { _btnPreview, _btnCleanup, _btnClose }
        };

        Content = new DockPanel
        {
            Margin = new Thickness(12), Children = { buttons, new ScrollViewer { Content = options } }
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
    }

    private static Grid CreatePathPicker(TextBox paths, Button browse)
    {
        Grid picker = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Children = { paths, browse }
        };
        Grid.SetColumn(browse, 1);
        return picker;
    }

    private async Task BrowseIncludePathsAsync()
    {
        var folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = true });

        AddSelectedPaths(_txtIncludePaths, folders.Select(folder => folder.TryGetLocalPath()));
        _chkIncludePaths.IsChecked = true;
    }

    private async Task BrowseExcludePathsAsync()
    {
        var files =
            await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true });

        AddSelectedPaths(_txtExcludePaths, files.Select(file => file.TryGetLocalPath()));
        _chkExcludePaths.IsChecked = true;
    }

    private void AddSelectedPaths(TextBox target, IEnumerable<string?> selectedPaths)
    {
        try
        {
            string[] paths =
            [
                .. selectedPaths
                    .OfType<string>()
                    .Select(path => CleanupWorkflow.ToRepositoryPath(_uiCommands.Module.WorkingDir, path))
            ];
            if (paths.Length == 0)
            {
                return;
            }

            var existingText = target.Text ?? string.Empty;
            var separator = string.IsNullOrEmpty(existingText) ? string.Empty : Environment.NewLine;
            target.Text = existingText + separator + string.Join(Environment.NewLine, paths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or NotSupportedException)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
    }

    private async Task PreviewAsync()
    {
        if (!TryBuildCommands(true, out var cleanCommand, out var submoduleCommand))
        {
            return;
        }

        SetRunning(true);
        try
        {
            List<string> output = [];
            var cleanResult = await Task.Run(() => _uiCommands.Module.GitExecutable.Execute(cleanCommand));
            output.Add(cleanResult.AllOutput);
            if (!cleanResult.ExitedSuccessfully)
            {
                MessageBoxes.ShowError(this, cleanResult.AllOutput);
                _txtPreviewOutput.Text = cleanResult.AllOutput;
                return;
            }

            if (submoduleCommand is { } submoduleArguments)
            {
                var submoduleResult =
                    await Task.Run(() => _uiCommands.Module.GitExecutable.Execute(submoduleArguments));
                output.Add(submoduleResult.AllOutput);
                if (!submoduleResult.ExitedSuccessfully)
                {
                    MessageBoxes.ShowError(this, submoduleResult.AllOutput);
                    _txtPreviewOutput.Text = string.Concat(output);
                    return;
                }
            }

            var previewOutput = string.Concat(output);
            _txtPreviewOutput.Text = string.IsNullOrWhiteSpace(previewOutput)
                ? TranslatedStrings.CleanupNoOutput
                : previewOutput;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or InvalidOperationException)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
        finally
        {
            SetRunning(false);
        }
    }

    private Task CleanupAsync()
    {
        if (!TryBuildCommands(false, out var cleanCommand, out var submoduleCommand) ||
            !MessageBoxes.Confirm(
                this,
                TranslatedStrings.CleanupConfirmQuestion,
                TranslatedStrings.CleanupConfirmCaption,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2))
        {
            return Task.CompletedTask;
        }

        SetRunning(true);
        var commandStarted = false;
        try
        {
            commandStarted = true;
            if (!_uiCommands.StartGitCommandProcessDialog(this, cleanCommand))
            {
                return Task.CompletedTask;
            }

            if (submoduleCommand is { } submoduleArguments)
            {
                _uiCommands.StartGitCommandProcessDialog(this, submoduleArguments);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or InvalidOperationException)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
        finally
        {
            if (commandStarted)
            {
                _uiCommands.RepoChangedNotifier.Notify();
            }

            SetRunning(false);
        }

        return Task.CompletedTask;
    }

    private bool TryBuildCommands(bool dryRun, out ArgumentString cleanCommand, out ArgumentString? submoduleCommand)
    {
        var paths = _chkIncludePaths.IsChecked == true
            ? CleanupWorkflow.ParsePathLines(_txtIncludePaths.Text)
            : null;
        var excludes = _chkExcludePaths.IsChecked == true
            ? CleanupWorkflow.ParsePathLines(_txtExcludePaths.Text)
            : null;

        if (paths is { Count: 0 } || excludes is { Count: 0 })
        {
            MessageBoxes.ShowError(this, TranslatedStrings.CleanupPathRequired);
            cleanCommand = default;
            submoduleCommand = null;
            return false;
        }

        var mode = _rbRemoveIgnored.IsChecked == true
            ? CleanMode.OnlyIgnored
            : _rbRemoveNonIgnored.IsChecked == true
                ? CleanMode.OnlyNonIgnored
                : CleanMode.All;
        var directories = _chkRemoveDirectories.IsChecked == true;
        cleanCommand = CleanupWorkflow.BuildClean(mode, dryRun, directories, paths, excludes);
        if (_chkCleanSubmodules.IsChecked == true)
        {
            submoduleCommand = CleanupWorkflow.BuildSubmoduleClean(mode, dryRun, directories, paths);
        }
        else
        {
            submoduleCommand = null;
        }

        return true;
    }

    private void SetRunning(bool isRunning)
    {
        _btnPreview.IsEnabled = !isRunning;
        _btnCleanup.IsEnabled = !isRunning;
        _btnClose.IsEnabled = !isRunning;
        _btnBrowseInclude.IsEnabled = !isRunning;
        _btnBrowseExclude.IsEnabled = !isRunning;
        _rbRemoveAll.IsEnabled = !isRunning;
        _rbRemoveNonIgnored.IsEnabled = !isRunning;
        _rbRemoveIgnored.IsEnabled = !isRunning;
        _chkRemoveDirectories.IsEnabled = !isRunning;
        _chkCleanSubmodules.IsEnabled = !isRunning;
        _chkIncludePaths.IsEnabled = !isRunning;
        _chkExcludePaths.IsEnabled = !isRunning;
        _txtIncludePaths.IsEnabled = !isRunning && _chkIncludePaths.IsChecked == true;
        _txtExcludePaths.IsEnabled = !isRunning && _chkExcludePaths.IsChecked == true;
    }
}
