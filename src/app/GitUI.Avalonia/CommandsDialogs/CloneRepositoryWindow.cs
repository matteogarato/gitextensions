using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Config;
using GitCommands.Settings;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.Compat;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Clones a Git repository and optionally opens it in the browse window.
/// </summary>
public sealed class CloneRepositoryWindow : GitExtensionsFormBase
{
    private readonly CheckBox _bareRepository = new() { Name = "chkBareRepository" };
    private readonly TextBox _destination = new() { Name = "txtRepositoryDirectory", MinWidth = 360 };

    private readonly CheckBox _initializeSubmodules = new()
    {
        Name = "chkInitializeSubmodules", IsChecked = AppSettings.CloneInitializeAllSubmodules
    };

    private readonly Action<string>? _repositoryCloned;
    private readonly TextBox _source = new() { Name = "txtRepositoryToClone", MinWidth = 360 };
    private readonly IGitUiCommands _uiCommands;
    private string _suggestedDestination = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CloneRepositoryWindow" /> class.
    /// </summary>
    /// <param name="uiCommands">Commands used to run Git with process feedback.</param>
    /// <param name="source">The initial repository URL or local path.</param>
    /// <param name="repositoryCloned">Called after a successful clone.</param>
    public CloneRepositoryWindow(IGitUiCommands uiCommands, string? source, Action<string>? repositoryCloned = null)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);

        _uiCommands = uiCommands;
        _repositoryCloned = repositoryCloned;
        _source.Text = source ?? GetCurrentRemote(uiCommands.Module);
        _destination.Text = GetInitialDestination(_source.Text, uiCommands.Module);
        _suggestedDestination = _destination.Text ?? string.Empty;
        _source.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty
                && string.Equals(_destination.Text, _suggestedDestination, StringComparison.Ordinal))
            {
                _suggestedDestination = GetInitialDestination(_source.Text, _uiCommands.Module);
                _destination.Text = _suggestedDestination;
            }
        };

        Title = TranslatedStrings.ForkCloneRepo;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        Button browse = new() { Content = TranslatedStrings.BrowseForDirectory };
        browse.Click += (_, _) => ThreadHelper.FileAndForget(BrowseForDestinationAsync);

        Grid destinationPicker = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _destination, browse }
        };
        Grid.SetColumn(browse, 1);
        browse.Margin = new Thickness(8, 0, 0, 0);

        _bareRepository.Content = TranslatedStrings.BareRepository;
        _initializeSubmodules.Content = TranslatedStrings.SubmoduleUpdate;

        Button clone = new() { Content = TranslatedStrings.Ok, IsDefault = true };
        clone.Click += (_, _) => ThreadHelper.FileAndForget(CloneAsync);
        Button cancel = new() { Content = TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { clone, cancel }
        };
        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TranslatedStrings.ForkCloneRepo },
                _source,
                new TextBlock { Text = TranslatedStrings.RepositoryDirectory },
                destinationPicker,
                _bareRepository,
                _initializeSubmodules,
                buttons
            }
        };
    }

    private static string GetCurrentRemote(IGitModule module)
    {
        var remotes = module.GetRemoteNames();
        var remote = remotes.FirstOrDefault(name => name.Equals("origin", StringComparison.OrdinalIgnoreCase))
                     ?? remotes.FirstOrDefault();
        return remote is null
            ? string.Empty
            : module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote));
    }

    private static string GetInitialDestination(string? source, IGitModule module)
    {
        var parentPath = AppSettings.DefaultCloneDestinationPath;
        if (string.IsNullOrWhiteSpace(parentPath) && !string.IsNullOrWhiteSpace(module.WorkingDir))
        {
            parentPath = Directory.Exists(module.WorkingDir)
                ? Directory.GetParent(module.WorkingDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName ??
                  module.WorkingDir
                : module.WorkingDir;
        }

        var repositoryName = PathUtil.GetRepositoryName(source);
        return string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(repositoryName)
            ? parentPath
            : Path.Combine(parentPath, repositoryName);
    }

    private async Task BrowseForDestinationAsync()
    {
        var folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            _destination.Text = path;
        }
    }

    private async Task CloneAsync()
    {
        var source = _source.Text?.Trim() ?? string.Empty;

        var destination = _destination.Text?.Trim() ?? string.Empty;
        if (!Path.IsPathRooted(destination))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.ChooseDirectory);
            return;
        }

        try
        {
            Directory.CreateDirectory(destination);
            AppSettings.CloneInitializeAllSubmodules = _initializeSubmodules.IsChecked == true;
            var destinationCommands = _uiCommands.WithWorkingDirectory(destination);
            var arguments = CloneRepositoryWorkflow.BuildCloneCommand(
                destinationCommands.Module,
                source,
                destination,
                _bareRepository.IsChecked == true,
                _initializeSubmodules.IsChecked == true);

            if (!destinationCommands.StartGitCommandProcessDialog(this, arguments))
            {
                return;
            }

            try
            {
                await RepositoryHistoryManager.Remotes.AddAsMostRecentAsync(source);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MessageBoxes.ShowError(this, exception.Message);
            }

            _repositoryCloned?.Invoke(destination);
            Close(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or InvalidOperationException)
        {
            MessageBoxes.ShowError(this, exception.Message);
        }
    }
}
