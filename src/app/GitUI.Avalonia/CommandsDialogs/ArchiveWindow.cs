using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUIPluginInterfaces;
using ResourceManager;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Creates a ZIP or TAR archive from a selected revision.
/// </summary>
public sealed class ArchiveWindow : GitExtensionsFormBase
{
    private readonly IGitUICommands _uiCommands;
    private readonly GitRevision _revision;
    private readonly GitRevision? _diffRevision;
    private readonly CheckBox _specificPathsOnly = new();
    private readonly CheckBox _changedPathsOnly = new();
    private readonly TextBox _paths = new()
    {
        AcceptsReturn = true,
        MinHeight = 90,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        IsEnabled = false,
    };
    private readonly RadioButton _zipFormat = new() { GroupName = "ArchiveFormat" };
    private readonly RadioButton _tarFormat = new() { GroupName = "ArchiveFormat" };

    /// <summary>
    ///  Initializes a new instance of the <see cref="ArchiveWindow"/> class.
    /// </summary>
    /// <param name="uiCommands">Commands used to run Git with process feedback.</param>
    /// <param name="revision">The revision to archive.</param>
    /// <param name="diffRevision">Optional older revision used to limit the archive to changed paths.</param>
    /// <param name="path">An optional pathspec to archive exclusively.</param>
    public ArchiveWindow(
        IGitUICommands uiCommands,
        GitRevision revision,
        GitRevision? diffRevision = null,
        string? path = null)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);
        ArgumentNullException.ThrowIfNull(revision);

        _uiCommands = uiCommands;
        _revision = revision;
        _diffRevision = diffRevision;

        Title = GitUI.TranslatedStrings.ArchiveRevisionTitle;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        MinWidth = 440;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _specificPathsOnly.Content = GitUI.TranslatedStrings.ArchiveSpecificPathsOnly;
        _specificPathsOnly.IsChecked = !string.IsNullOrWhiteSpace(path);
        _specificPathsOnly.IsCheckedChanged += (_, _) => UpdatePathFilterState();
        _changedPathsOnly.Content = GitUI.TranslatedStrings.ArchiveChangedPathsOnly;
        _changedPathsOnly.IsEnabled = diffRevision is not null;
        _changedPathsOnly.IsChecked = diffRevision is not null && string.IsNullOrWhiteSpace(path);
        _changedPathsOnly.IsCheckedChanged += (_, _) => UpdatePathFilterState();
        _paths.Text = path;
        UpdatePathFilterState();

        _zipFormat.Content = "zip";
        _zipFormat.IsChecked = true;
        _tarFormat.Content = "tar";

        TextBlock revisionDetails = new()
        {
            Text = $"{revision.ObjectId.ToShortString()}  {revision.Subject}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };

        Button save = new() { Content = GitUI.TranslatedStrings.ArchiveSaveAs, IsDefault = true };
        save.Click += (_, _) => ThreadHelper.FileAndForget(SaveArchiveAsync);
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = GitUI.TranslatedStrings.ArchiveRevisionLabel },
                revisionDetails,
                new TextBlock { Text = GitUI.TranslatedStrings.ArchiveFormat },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { _zipFormat, _tarFormat },
                },
                _changedPathsOnly,
                _specificPathsOnly,
                _paths,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Avalonia.Thickness(0, 8, 0, 0),
                    Children = { save, cancel },
                },
            },
        };
    }

    private void UpdatePathFilterState()
    {
        if (_specificPathsOnly.IsChecked == true)
        {
            _changedPathsOnly.IsChecked = false;
        }
        else if (_changedPathsOnly.IsChecked == true)
        {
            _specificPathsOnly.IsChecked = false;
        }

        _paths.IsEnabled = _specificPathsOnly.IsChecked == true;
    }

    private async Task SaveArchiveAsync()
    {
        IReadOnlyList<string>? paths = null;
        if (_specificPathsOnly.IsChecked == true)
        {
            paths = [.. (_paths.Text ?? string.Empty)
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            if (paths.Count == 0)
            {
                MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoResultsFound);
                return;
            }
        }
        else if (_changedPathsOnly.IsChecked == true && _diffRevision is not null)
        {
            paths = await Task.Run(() => ArchiveWorkflow.GetChangedPaths(_uiCommands.Module, _diffRevision, _revision));
            if (paths.Count == 0)
            {
                MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoResultsFound);
                return;
            }
        }

        ArchiveFormat format = _tarFormat.IsChecked == true ? ArchiveFormat.Tar : ArchiveFormat.Zip;
        string extension = format == ArchiveFormat.Zip ? "zip" : "tar";
        FilePickerFileType fileType = new(
            format == ArchiveFormat.Zip
                ? GitUI.TranslatedStrings.ZipFileFilter
                : GitUI.TranslatedStrings.TarFileFilter)
        {
            Patterns = [$"*.{extension}"],
        };
        string repositoryName = string.IsNullOrWhiteSpace(_uiCommands.Module.WorkingDir)
            ? "repository"
            : new DirectoryInfo(_uiCommands.Module.WorkingDir).Name;
        IStorageFile? outputFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = GitUI.TranslatedStrings.SaveArchiveAs,
            SuggestedFileName = $"{repositoryName}_{_revision.ObjectId.ToShortString()}",
            DefaultExtension = extension,
            FileTypeChoices = [fileType],
        });
        if (outputFile is null)
        {
            return;
        }

        if (outputFile.TryGetLocalPath() is not string outputPath)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.ArchiveFilePathRequired);
            return;
        }

        try
        {
            ArgumentString arguments = ArchiveWorkflow.BuildArchive(_uiCommands.Module, _revision, outputPath, format, paths);
            if (_uiCommands.StartGitCommandProcessDialog(this, arguments))
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
