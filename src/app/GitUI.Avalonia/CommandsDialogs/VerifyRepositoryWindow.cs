using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Shims.WinForms;
using GitUI.Compat;
using CheckBox = Avalonia.Controls.CheckBox;
using ListBox = Avalonia.Controls.ListBox;
using TextBox = Avalonia.Controls.TextBox;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Inspects unreachable Git objects and offers recovery and cleanup operations.
/// </summary>
public sealed class VerifyRepositoryWindow : GitExtensionsFormBase
{
    private const string _lostFoundTagPrefix = "LOST_FOUND_";
    private readonly Button _btnClose = new() { Content = TranslatedStrings.Close, IsCancel = true };
    private readonly Button _btnDeleteTags = new() { Content = TranslatedStrings.VerifyDeleteLostFoundTags };
    private readonly Button _btnPrune = new() { Content = TranslatedStrings.VerifyPrune };
    private readonly Button _btnRecover = new() { Content = TranslatedStrings.VerifyRecoverSelected };
    private readonly Button _btnSaveLostFound = new() { Content = TranslatedStrings.VerifySaveLostFound };
    private readonly Button _btnScan = new() { Content = TranslatedStrings.VerifyScan, IsDefault = true };

    private readonly CheckBox _chkCommitsAndTags =
        new() { Content = TranslatedStrings.VerifyShowCommitsAndTags, IsChecked = true };

    private readonly CheckBox _chkFull = new() { Content = TranslatedStrings.VerifyFullCheck };
    private readonly CheckBox _chkNoReflogs = new() { Content = TranslatedStrings.VerifyNoReflogs };

    private readonly CheckBox _chkOtherObjects =
        new() { Content = TranslatedStrings.VerifyShowOtherObjects, IsChecked = true };

    private readonly CheckBox _chkUnreachable = new() { Content = TranslatedStrings.VerifyShowUnreachable };
    private readonly ListBox _objects = new() { SelectionMode = SelectionMode.Multiple };

    private readonly TextBox _preview = new()
    {
        AcceptsReturn = true, IsReadOnly = true, TextWrapping = TextWrapping.Wrap
    };

    private readonly IGitUiCommands _uiCommands;
    private IReadOnlyList<VerifyObject> _allObjects = [];
    private bool _isScanning;
    private CancellationTokenSource? _previewCancellation;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VerifyRepositoryWindow" /> class.
    /// </summary>
    /// <param name="uiCommands">Commands used to run Git with process feedback.</param>
    public VerifyRepositoryWindow(IGitUiCommands uiCommands)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);
        _uiCommands = uiCommands;

        Title = TranslatedStrings.VerifyRepositoryTitle;
        Width = 1080;
        Height = 680;
        MinWidth = 760;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _objects.ItemTemplate = new FuncDataTemplate<VerifyObject>(
            (verifyObject, _) => new TextBlock
            {
                Text = verifyObject.ToString(),
                Margin = new Thickness(4, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            },
            false);
        _objects.SelectionChanged += (_, _) => ThreadHelper.FileAndForget(PreviewSelectedObjectAsync);

        _chkUnreachable.IsCheckedChanged += (_, _) => ThreadHelper.FileAndForget(ScanAsync);
        _chkFull.IsCheckedChanged += (_, _) => ThreadHelper.FileAndForget(ScanAsync);
        _chkNoReflogs.IsCheckedChanged += (_, _) => ThreadHelper.FileAndForget(ScanAsync);
        _chkCommitsAndTags.IsCheckedChanged += (_, _) => ApplyTypeFilter();
        _chkOtherObjects.IsCheckedChanged += (_, _) => ApplyTypeFilter();
        _btnScan.Click += (_, _) => ThreadHelper.FileAndForget(ScanAsync);
        _btnSaveLostFound.Click +=
            (_, _) => ThreadHelper.FileAndForget(() => RunRepositoryActionAsync(SaveLostFoundAsync));
        _btnRecover.Click += (_, _) => ThreadHelper.FileAndForget(() => RunRepositoryActionAsync(RecoverSelectedAsync));
        _btnPrune.Click += (_, _) => ThreadHelper.FileAndForget(() => RunRepositoryActionAsync(PruneAsync));
        _btnDeleteTags.Click += (_, _) =>
            ThreadHelper.FileAndForget(() => RunRepositoryActionAsync(DeleteLostFoundTagsAsync));
        _btnClose.Click += (_, _) => Close(false);

        StackPanel filters = new()
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = TranslatedStrings.VerifyOptions, FontWeight = FontWeight.Bold },
                _chkUnreachable,
                _chkFull,
                _chkNoReflogs,
                new TextBlock { Text = TranslatedStrings.VerifyObjectTypes, FontWeight = FontWeight.Bold },
                _chkCommitsAndTags,
                _chkOtherObjects
            }
        };

        Grid results = new()
        {
            RowDefinitions = new RowDefinitions("*,4,*"),
            Children =
            {
                _objects,
                new GridSplitter { ResizeDirection = GridResizeDirection.Rows, Height = 4 },
                new ScrollViewer { Content = _preview }
            }
        };
        Grid.SetRow(results.Children[1], 1);
        Grid.SetRow(results.Children[2], 2);

        Grid workspace = new()
        {
            ColumnDefinitions = new ColumnDefinitions("220,8,*"),
            Children =
            {
                filters, new GridSplitter { ResizeDirection = GridResizeDirection.Columns, Width = 8 }, results
            }
        };
        Grid.SetColumn(workspace.Children[1], 1);
        Grid.SetColumn(results, 2);

        StackPanel operationButtons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _btnSaveLostFound, _btnRecover, _btnPrune, _btnDeleteTags }
        };
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { _btnScan, _btnClose }
        };

        DockPanel footer = new() { Children = { buttons, operationButtons } };
        DockPanel.SetDock(buttons, Dock.Right);
        DockPanel.SetDock(operationButtons, Dock.Left);

        Content = new DockPanel { Margin = new Thickness(12), Children = { footer, workspace } };
        DockPanel.SetDock(footer, Dock.Bottom);

        Opened += (_, _) => ThreadHelper.FileAndForget(ScanAsync);
        Closed += (_, _) => CancelPreview();
    }

    private IGitModule Module
    {
        get => _uiCommands.Module;
    }

    private async Task ScanAsync()
    {
        if (_isScanning || !IsVisible)
        {
            return;
        }

        _isScanning = true;
        SetBusy(true);
        try
        {
            var arguments = VerifyWorkflow.BuildFsck(
                _chkUnreachable.IsChecked == true,
                _chkFull.IsChecked == true,
                _chkNoReflogs.IsChecked == true);
            var result =
                await Task.Run(() => Module.GitExecutable.Execute(arguments, throwOnErrorExit: false));
            if (!result.ExitedSuccessfully)
            {
                MessageBoxes.ShowError(this, result.AllOutput);
                return;
            }

            _allObjects = VerifyWorkflow.ParseFsckOutput(result.AllOutput);
            ApplyTypeFilter();
        }
        catch (Exception exception) when (exception is ExternalOperationException or IOException
                                              or UnauthorizedAccessException or ArgumentException
                                              or InvalidOperationException)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
        finally
        {
            _isScanning = false;
            SetBusy(false);
        }
    }

    private async Task PreviewSelectedObjectAsync()
    {
        CancelPreview();
        if ((_objects.SelectedItems?.OfType<VerifyObject>() ?? []).FirstOrDefault() is not { } selected)
        {
            _preview.Text = string.Empty;
            return;
        }

        CancellationTokenSource cancellation = new();
        _previewCancellation = cancellation;
        try
        {
            var content = await Task.Run(
                () => Module.ShowObject(selected.ObjectId, selected.Type == "blob") ?? string.Empty,
                cancellation.Token);
            if (!cancellation.IsCancellationRequested && IsVisible)
            {
                _preview.Text = content;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (ExternalOperationException exception) when (!cancellation.IsCancellationRequested)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
        finally
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                _previewCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task SaveLostFoundAsync()
    {
        if (!_uiCommands.StartGitCommandProcessDialog(this, VerifyWorkflow.BuildSaveLostFound()))
        {
            return;
        }

        _uiCommands.RepoChangedNotifier.Notify();
        await ScanAsync();
    }

    private async Task RecoverSelectedAsync()
    {
        VerifyObject[] selectedObjects = [.. _objects.SelectedItems?.OfType<VerifyObject>() ?? []];
        if (selectedObjects.Length == 0)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.VerifySelectObjects);
            return;
        }

        var existingTags = Module.GetRefs(RefsFilter.Tags)
            .Select(gitRef => gitRef.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var verifyObject in selectedObjects)
        {
            var tagName = $"{_lostFoundTagPrefix}{verifyObject.ObjectId}";
            if (existingTags.Contains(tagName))
            {
                continue;
            }

            if (!_uiCommands.StartCommandLineProcessDialog(this, VerifyWorkflow.BuildRecoveryTag(Module, verifyObject)))
            {
                return;
            }

            existingTags.Add(tagName);
        }

        await ScanAsync();
    }

    private async Task PruneAsync()
    {
        if (!MessageBoxes.Confirm(
                this,
                TranslatedStrings.VerifyPruneConfirm,
                TranslatedStrings.VerifyPruneCaption,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2))
        {
            return;
        }

        if (!_uiCommands.StartGitCommandProcessDialog(this, VerifyWorkflow.BuildPrune()))
        {
            return;
        }

        _uiCommands.RepoChangedNotifier.Notify();
        await ScanAsync();
    }

    private async Task DeleteLostFoundTagsAsync()
    {
        var tagNames = Module.GetRefs(RefsFilter.Tags)
            .Select(gitRef => gitRef.Name)
            .Where(name => name.StartsWith(_lostFoundTagPrefix, StringComparison.Ordinal))
            .ToArray();
        if (tagNames.Length == 0)
        {
            return;
        }

        if (!MessageBoxes.Confirm(
                this,
                TranslatedStrings.VerifyDeleteTagsConfirm,
                TranslatedStrings.VerifyDeleteTagsCaption,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2))
        {
            return;
        }

        foreach (var tagName in tagNames)
        {
            if (!_uiCommands.StartCommandLineProcessDialog(this, Commands.DeleteTag(tagName)))
            {
                return;
            }
        }

        await ScanAsync();
    }

    private void ApplyTypeFilter()
    {
        if (_chkCommitsAndTags.IsChecked != true && _chkOtherObjects.IsChecked != true)
        {
            _chkOtherObjects.IsChecked = true;
        }

        var showCommitsAndTags = _chkCommitsAndTags.IsChecked == true;
        var showOtherObjects = _chkOtherObjects.IsChecked == true;
        _objects.ItemsSource = _allObjects
            .Where(item => (showCommitsAndTags && item.Type is "commit" or "tag")
                           || (showOtherObjects && item.Type is not ("commit" or "tag")))
            .ToArray();
    }

    private void SetBusy(bool isBusy)
    {
        _btnScan.IsEnabled = !isBusy;
        _btnSaveLostFound.IsEnabled = !isBusy;
        _btnRecover.IsEnabled = !isBusy;
        _btnPrune.IsEnabled = !isBusy;
        _btnDeleteTags.IsEnabled = !isBusy;
        _chkUnreachable.IsEnabled = !isBusy;
        _chkFull.IsEnabled = !isBusy;
        _chkNoReflogs.IsEnabled = !isBusy;
        _chkCommitsAndTags.IsEnabled = !isBusy;
        _chkOtherObjects.IsEnabled = !isBusy;
    }

    private void CancelPreview()
    {
        _previewCancellation?.Cancel();
    }

    private async Task RunRepositoryActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is ExternalOperationException or IOException
                                              or UnauthorizedAccessException or ArgumentException
                                              or InvalidOperationException)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }
}
