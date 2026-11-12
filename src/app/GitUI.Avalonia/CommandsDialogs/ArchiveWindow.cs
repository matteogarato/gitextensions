using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.Compat;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Creates a ZIP or TAR archive from a selected revision.
/// </summary>
public sealed class ArchiveWindow : GitExtensionsFormBase
{
    private readonly CheckBox _changedPathsOnly = new();
    private readonly GitRevision? _diffRevision;

    private readonly TextBox _paths = new()
    {
        AcceptsReturn = true, MinHeight = 90, TextWrapping = TextWrapping.Wrap, IsEnabled = false
    };

    private readonly GitRevision _revision;
    private readonly CheckBox _specificPathsOnly = new();
    private readonly RadioButton _tarFormat = new() { GroupName = "ArchiveFormat" };
    private readonly IGitUiCommands _uiCommands;
    private readonly RadioButton _zipFormat = new() { GroupName = "ArchiveFormat" };

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArchiveWindow" /> class.
    /// </summary>
    /// <param name="uiCommands">Commands used to run Git with process feedback.</param>
    /// <param name="revision">The revision to archive.</param>
    /// <param name="diffRevision">Optional older revision used to limit the archive to changed paths.</param>
    /// <param name="path">An optional pathspec to archive exclusively.</param>
    public ArchiveWindow(
        IGitUiCommands uiCommands,
        GitRevision revision,
        GitRevision? diffRevision = null,
        string? path = null)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);
        ArgumentNullException.ThrowIfNull(revision);

        _uiCommands = uiCommands;
        _revision = revision;
        _diffRevision = diffRevision;

        Title = TranslatedStrings.ArchiveRevisionTitle;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        MinWidth = 440;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _specificPathsOnly.Content = TranslatedStrings.ArchiveSpecificPathsOnly;
        _specificPathsOnly.IsChecked = !string.IsNullOrWhiteSpace(path);
        _specificPathsOnly.IsCheckedChanged += (_, _) => UpdatePathFilterState();
        _changedPathsOnly.Content = TranslatedStrings.ArchiveChangedPathsOnly;
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
            Text = $"{revision.ObjectId.ToShortString()}  {revision.Subject}", TextWrapping = TextWrapping.Wrap
        };

        Button save = new() { Content = TranslatedStrings.ArchiveSaveAs, IsDefault = true };
        save.Click += (_, _) => ThreadHelper.FileAndForget(SaveArchiveAsync);
        Button cancel = new() { Content = TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TranslatedStrings.ArchiveRevisionLabel },
                revisionDetails,
                new TextBlock { Text = TranslatedStrings.ArchiveFormat },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { _zipFormat, _tarFormat }
                },
                _changedPathsOnly,
                _specificPathsOnly,
                _paths,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0),
                    Children = { save, cancel }
                }
            }
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
            paths =
            [
                .. (_paths.Text ?? string.Empty)
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            if (paths.Count == 0)
            {
                MessageBoxes.ShowError(this, TranslatedStrings.NoResultsFound);
                return;
            }
        }
        else if (_changedPathsOnly.IsChecked == true && _diffRevision is not null)
        {
            paths = await Task.Run(() => ArchiveWorkflow.GetChangedPaths(_uiCommands.Module, _diffRevision, _revision));
            if (paths.Count == 0)
            {
                MessageBoxes.ShowError(this, TranslatedStrings.NoResultsFound);
                return;
            }
        }

        var format = _tarFormat.IsChecked == true ? ArchiveFormat.Tar : ArchiveFormat.Zip;
        var extension = format == ArchiveFormat.Zip ? "zip" : "tar";
        FilePickerFileType fileType = new(
            format == ArchiveFormat.Zip
                ? TranslatedStrings.ZipFileFilter
                : TranslatedStrings.TarFileFilter) { Patterns = [$"*.{extension}"] };
        var repositoryName = string.IsNullOrWhiteSpace(_uiCommands.Module.WorkingDir)
            ? "repository"
            : new DirectoryInfo(_uiCommands.Module.WorkingDir).Name;
        var outputFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = TranslatedStrings.SaveArchiveAs,
            SuggestedFileName = $"{repositoryName}_{_revision.ObjectId.ToShortString()}",
            DefaultExtension = extension,
            FileTypeChoices = [fileType]
        });
        if (outputFile is null)
        {
            return;
        }

        if (outputFile.TryGetLocalPath() is not { } outputPath)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.ArchiveFilePathRequired);
            return;
        }

        try
        {
            var arguments =
                ArchiveWorkflow.BuildArchive(_uiCommands.Module, _revision, outputPath, format, paths);
            if (_uiCommands.StartGitCommandProcessDialog(this, arguments))
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
