using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using ResourceManager;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Previews and removes untracked files from a repository.
/// </summary>
public sealed class CleanupRepositoryWindow : GitExtensionsFormBase
{
    private readonly IGitUICommands _uiCommands;
    private readonly RadioButton _rbRemoveAll = new() { GroupName = "CleanupMode" };
    private readonly RadioButton _rbRemoveNonIgnored = new() { GroupName = "CleanupMode" };
    private readonly RadioButton _rbRemoveIgnored = new() { GroupName = "CleanupMode" };
    private readonly CheckBox _chkRemoveDirectories = new();
    private readonly CheckBox _chkCleanSubmodules = new();
    private readonly CheckBox _chkIncludePaths = new();
    private readonly CheckBox _chkExcludePaths = new();
    private readonly TextBox _txtIncludePaths = new()
    {
        AcceptsReturn = true,
        MinHeight = 72,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        IsEnabled = false,
    };
    private readonly TextBox _txtExcludePaths = new()
    {
        AcceptsReturn = true,
        MinHeight = 72,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        IsEnabled = false,
    };
    private readonly TextBox _txtPreviewOutput = new()
    {
        AcceptsReturn = true,
        IsReadOnly = true,
        MinHeight = 140,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
    };
    private readonly Button _btnBrowseInclude = new() { Content = GitUI.TranslatedStrings.BrowseForDirectory };
    private readonly Button _btnBrowseExclude = new() { Content = GitUI.TranslatedStrings.BrowseForDirectory };
    private readonly Button _btnPreview = new() { IsDefault = true };
    private readonly Button _btnCleanup = new();
    private readonly Button _btnClose = new() { IsCancel = true };

    /// <summary>
    ///  Initializes a new instance of the <see cref="CleanupRepositoryWindow"/> class.
    /// </summary>
    /// <param name="uiCommands">Commands used to run Git with process feedback.</param>
    /// <param name="path">An optional path to use for both path filters.</param>
    public CleanupRepositoryWindow(IGitUICommands uiCommands, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);
        _uiCommands = uiCommands;

        Title = GitUI.TranslatedStrings.CleanupRepositoryTitle;
        Width = 680;
        Height = 760;
        MinWidth = 560;
        MinHeight = 620;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _rbRemoveAll.Content = GitUI.TranslatedStrings.CleanupRemoveAllFiles;
        _rbRemoveAll.IsChecked = true;
        _rbRemoveNonIgnored.Content = GitUI.TranslatedStrings.CleanupRemoveNonIgnoredFiles;
        _rbRemoveIgnored.Content = GitUI.TranslatedStrings.CleanupRemoveIgnoredFiles;
        _chkRemoveDirectories.Content = GitUI.TranslatedStrings.CleanupRemoveDirectories;
        _chkRemoveDirectories.IsChecked = true;
        _chkCleanSubmodules.Content = GitUI.TranslatedStrings.CleanupSubmodulesRecursive;

        _chkIncludePaths.Content = GitUI.TranslatedStrings.CleanupIncludePaths;
        _chkIncludePaths.IsCheckedChanged += (_, _) => _txtIncludePaths.IsEnabled = _chkIncludePaths.IsChecked == true;
        _chkExcludePaths.Content = GitUI.TranslatedStrings.CleanupExcludePaths;
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

        _btnPreview.Content = GitUI.TranslatedStrings.CleanupPreview;
        _btnPreview.Click += (_, _) => ThreadHelper.FileAndForget(PreviewAsync);
        _btnCleanup.Content = GitUI.TranslatedStrings.CleanupAction;
        _btnCleanup.Click += (_, _) => ThreadHelper.FileAndForget(CleanupAsync);
        _btnClose.Content = GitUI.TranslatedStrings.Close;
        _btnClose.Click += (_, _) => Close(false);

        Grid includePathPicker = CreatePathPicker(_txtIncludePaths, _btnBrowseInclude);
        Grid excludePathPicker = CreatePathPicker(_txtExcludePaths, _btnBrowseExclude);
        StackPanel options = new()
        {
            Spacing = 8,
            Children =
            {
                new GroupBox
                {
                    Header = GitUI.TranslatedStrings.CleanupRemoveUntrackedGroup,
                    Content = new StackPanel
                    {
                        Spacing = 6,
                        Children = { _rbRemoveAll, _rbRemoveNonIgnored, _rbRemoveIgnored },
                    },
                },
                _chkRemoveDirectories,
                _chkCleanSubmodules,
                _chkIncludePaths,
                includePathPicker,
                new TextBlock { Text = GitUI.TranslatedStrings.CleanupOnePathPerLine },
                _chkExcludePaths,
                excludePathPicker,
                new TextBlock { Text = GitUI.TranslatedStrings.CleanupOnePathPerLine },
                new TextBlock { Text = GitUI.TranslatedStrings.CleanupLog },
                _txtPreviewOutput,
            },
        };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
            Children = { _btnPreview, _btnCleanup, _btnClose },
        };

        Content = new DockPanel
        {
            Margin = new Avalonia.Thickness(12),
            Children =
            {
                buttons,
                new ScrollViewer
                {
                    Content = options,
                },
            },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
    }

    private static Grid CreatePathPicker(TextBox paths, Button browse)
    {
        Grid picker = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children = { paths, browse },
        };
        Grid.SetColumn(browse, 1);
        return picker;
    }

    private async Task BrowseIncludePathsAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = true,
        });

        AddSelectedPaths(_txtIncludePaths, folders.Select(folder => folder.TryGetLocalPath()));
        _chkIncludePaths.IsChecked = true;
    }

    private async Task BrowseExcludePathsAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
        });

        AddSelectedPaths(_txtExcludePaths, files.Select(file => file.TryGetLocalPath()));
        _chkExcludePaths.IsChecked = true;
    }

    private void AddSelectedPaths(TextBox target, IEnumerable<string?> selectedPaths)
    {
        try
        {
            string[] paths = [.. selectedPaths
                .OfType<string>()
                .Select(path => CleanupWorkflow.ToRepositoryPath(_uiCommands.Module.WorkingDir, path))];
            if (paths.Length == 0)
            {
                return;
            }

            string existingText = target.Text ?? string.Empty;
            string separator = string.IsNullOrEmpty(existingText) ? string.Empty : Environment.NewLine;
            target.Text = existingText + separator + string.Join(Environment.NewLine, paths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
    }

    private async Task PreviewAsync()
    {
        if (!TryBuildCommands(dryRun: true, out ArgumentString cleanCommand, out ArgumentString? submoduleCommand))
        {
            return;
        }

        SetRunning(true);
        try
        {
            List<string> output = [];
            ExecutionResult cleanResult = await Task.Run(
                () => _uiCommands.Module.GitExecutable.Execute(cleanCommand));
            output.Add(cleanResult.AllOutput);
            if (!cleanResult.ExitedSuccessfully)
            {
                MessageBoxes.ShowError(this, cleanResult.AllOutput);
                _txtPreviewOutput.Text = cleanResult.AllOutput;
                return;
            }

            if (submoduleCommand is ArgumentString submoduleArguments)
            {
                ExecutionResult submoduleResult = await Task.Run(
                    () => _uiCommands.Module.GitExecutable.Execute(submoduleArguments));
                output.Add(submoduleResult.AllOutput);
                if (!submoduleResult.ExitedSuccessfully)
                {
                    MessageBoxes.ShowError(this, submoduleResult.AllOutput);
                    _txtPreviewOutput.Text = string.Concat(output);
                    return;
                }
            }

            string previewOutput = string.Concat(output);
            _txtPreviewOutput.Text = string.IsNullOrWhiteSpace(previewOutput)
                ? GitUI.TranslatedStrings.CleanupNoOutput
                : previewOutput;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
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
        if (!TryBuildCommands(dryRun: false, out ArgumentString cleanCommand, out ArgumentString? submoduleCommand))
        {
            return Task.CompletedTask;
        }

        if (!MessageBoxes.Confirm(
            this,
            GitUI.TranslatedStrings.CleanupConfirmQuestion,
            GitUI.TranslatedStrings.CleanupConfirmCaption,
            MessageBoxIcon.Warning,
            GitExtensions.Shims.WinForms.MessageBoxDefaultButton.Button2))
        {
            return Task.CompletedTask;
        }

        SetRunning(true);
        bool commandStarted = false;
        try
        {
            commandStarted = true;
            if (!_uiCommands.StartGitCommandProcessDialog(this, cleanCommand))
            {
                return Task.CompletedTask;
            }

            if (submoduleCommand is ArgumentString submoduleArguments)
            {
                _uiCommands.StartGitCommandProcessDialog(this, submoduleArguments);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
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
        IReadOnlyList<string>? paths = _chkIncludePaths.IsChecked == true
            ? CleanupWorkflow.ParsePathLines(_txtIncludePaths.Text)
            : null;
        IReadOnlyList<string>? excludes = _chkExcludePaths.IsChecked == true
            ? CleanupWorkflow.ParsePathLines(_txtExcludePaths.Text)
            : null;

        if (paths is { Count: 0 } || excludes is { Count: 0 })
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.CleanupPathRequired);
            cleanCommand = default;
            submoduleCommand = null;
            return false;
        }

        CleanMode mode = _rbRemoveIgnored.IsChecked == true
            ? CleanMode.OnlyIgnored
            : _rbRemoveNonIgnored.IsChecked == true
                ? CleanMode.OnlyNonIgnored
                : CleanMode.All;
        bool directories = _chkRemoveDirectories.IsChecked == true;
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
