using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using ResourceManager;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Creates a local working or bare repository and opens it in the browse window.
/// </summary>
public sealed class InitRepositoryWindow : GitExtensionsFormBase
{
    private readonly IGitExecutorProvider _gitExecutorProvider;
    private readonly Func<string, bool> _openRepository;
    private readonly TextBox _directoryPath = new() { MinWidth = 360 };
    private readonly CheckBox _bareRepository = new();

    /// <summary>
    ///  Initializes a new instance of the <see cref="InitRepositoryWindow"/> class.
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

        Title = GitUI.TranslatedStrings.InitializeRepository;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        Button browse = new() { Content = GitUI.TranslatedStrings.BrowseForDirectory };
        browse.Click += (_, _) => ThreadHelper.FileAndForget(BrowseForDirectoryAsync);

        Grid directoryPicker = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                _directoryPath,
                browse,
            },
        };
        Grid.SetColumn(browse, 1);
        browse.Margin = new Avalonia.Thickness(8, 0, 0, 0);

        _bareRepository.Content = GitUI.TranslatedStrings.BareRepository;

        Button initialize = new() { Content = GitUI.TranslatedStrings.InitializeRepository, IsDefault = true };
        initialize.Click += (_, _) => Initialize();
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { initialize, cancel },
        };

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = GitUI.TranslatedStrings.RepositoryDirectory },
                directoryPicker,
                _bareRepository,
                buttons,
            },
        };
    }

    private async Task BrowseForDirectoryAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is string path)
        {
            _directoryPath.Text = path;
        }
    }

    private void Initialize()
    {
        string path = _directoryPath.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.ChooseDirectory);
            return;
        }

        if (File.Exists(path))
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.CannotInitializeRepositoryOnFile);
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
    }
}
