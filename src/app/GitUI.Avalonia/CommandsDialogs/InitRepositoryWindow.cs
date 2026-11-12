using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.Compat;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Creates a local working or bare repository and opens it in the browse window.
/// </summary>
public sealed class InitRepositoryWindow : GitExtensionsFormBase
{
    private readonly CheckBox _bareRepository = new();
    private readonly TextBox _directoryPath = new() { MinWidth = 360 };
    private readonly IGitExecutorProvider _gitExecutorProvider;
    private readonly Func<string, bool> _openRepository;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InitRepositoryWindow" /> class.
    /// </summary>
    /// <param name="gitExecutorProvider">Provides Git executors for the new repository.</param>
    /// <param name="openRepository">Opens the initialized repository in the application.</param>
    /// <param name="initialPath">The initial directory shown in the path field.</param>
    public InitRepositoryWindow(
        IGitExecutorProvider gitExecutorProvider,
        Func<string, bool> openRepository,
        string? initialPath)
    {
        ArgumentNullException.ThrowIfNull(gitExecutorProvider);
        ArgumentNullException.ThrowIfNull(openRepository);

        _gitExecutorProvider = gitExecutorProvider;
        _openRepository = openRepository;
        _directoryPath.Text = initialPath;

        Title = TranslatedStrings.InitializeRepository;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        Button browse = new() { Content = TranslatedStrings.BrowseForDirectory };
        browse.Click += (_, _) => ThreadHelper.FileAndForget(BrowseForDirectoryAsync);

        Grid directoryPicker = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _directoryPath, browse }
        };
        Grid.SetColumn(browse, 1);
        browse.Margin = new Thickness(8, 0, 0, 0);

        _bareRepository.Content = TranslatedStrings.BareRepository;

        Button initialize = new() { Content = TranslatedStrings.InitializeRepository, IsDefault = true };
        initialize.Click += (_, _) => Initialize();
        Button cancel = new() { Content = TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { initialize, cancel }
        };

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TranslatedStrings.RepositoryDirectory },
                directoryPicker,
                _bareRepository,
                buttons
            }
        };
    }

    private async Task BrowseForDirectoryAsync()
    {
        var folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            _directoryPath.Text = path;
        }
    }

    private void Initialize()
    {
        var path = _directoryPath.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.ChooseDirectory);
            return;
        }

        if (File.Exists(path))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.CannotInitializeRepositoryOnFile);
            return;
        }

        try
        {
            InitRepositoryWorkflow.Initialize(_gitExecutorProvider, path, _bareRepository.IsChecked == true);
            if (_openRepository(path))
            {
                Close(true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or InvalidOperationException)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
    }
}
