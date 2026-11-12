using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Lists the files that differ between two revisions and shows the patch of the selected file.
/// </summary>
public sealed class CompareRevisionsWindow : Window
{
    private readonly TextBox _diff = new()
    {
        Name = "txtCompareDiff",
        AcceptsReturn = true,
        IsReadOnly = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = "monospace"
    };

    private readonly CancellationTokenSequence _diffSequence = new();
    private readonly ListBox _files = new() { Name = "lstCompareFiles" };
    private readonly CancellationTokenSequence _filesSequence = new();
    private readonly ObjectId _firstId;
    private readonly IGitModule _module;
    private readonly ObjectId _secondId;
    private readonly ObjectId _secondParentId;
    private readonly TextBlock _status = new() { Name = "lblCompareStatus", TextWrapping = TextWrapping.Wrap };

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompareRevisionsWindow" /> class.
    /// </summary>
    /// <param name="module">The repository containing both revisions.</param>
    /// <param name="first">The older revision.</param>
    /// <param name="second">The newer revision.</param>
    public CompareRevisionsWindow(IGitModule module, GitRevision first, GitRevision second)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        _module = module;
        _firstId = first.ObjectId;
        _secondId = second.ObjectId;
        _secondParentId = second.FirstParentId;
        Title = string.Format(TranslatedStrings.CompareRevisionsWindowTitle, _firstId.ToShortString(),
            _secondId.ToShortString());
        Width = 1000;
        Height = 700;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _files.ItemTemplate = new FuncDataTemplate<GitItemStatus>(
            (item, _) => new TextBlock
            {
                Text = item is null ? string.Empty : FormatFile(item),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 2)
            },
            false);
        _files.SelectionChanged += (_, _) => ThreadHelper.FileAndForget(LoadSelectedDiffAsync);

        Button close = new()
        {
            Content = TranslatedStrings.Close, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right
        };
        close.Click += (_, _) => Close();

        Grid content = new()
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("2*,Auto,3*,Auto"),
            Children = { _files, _status, _diff, close }
        };
        Grid.SetRow(_status, 1);
        Grid.SetRow(_diff, 2);
        Grid.SetRow(close, 3);
        Content = content;

        Opened += (_, _) => ThreadHelper.FileAndForget(LoadFilesAsync);
        Closed += (_, _) =>
        {
            _filesSequence.Dispose();
            _diffSequence.Dispose();
        };
    }

    /// <summary>
    ///     Formats a changed file as a status letter followed by its path.
    /// </summary>
    internal static string FormatFile(GitItemStatus item)
    {
        var status = item.IsNew ? 'A'
            : item.IsDeleted ? 'D'
            : item.IsRenamed ? 'R'
            : item.IsCopied ? 'C'
            : 'M';

        return item.OldName is null
            ? $"{status}  {item.Name}"
            : $"{status}  {item.OldName} -> {item.Name}";
    }

    private async Task LoadFilesAsync()
    {
        var cancellationToken = _filesSequence.Next();
        _status.Text = TranslatedStrings.LoadingData;
        try
        {
            var files = await Task.Run(
                () => _module.GetDiffFilesWithSubmodulesStatus(_firstId, _secondId, _secondParentId,
                    cancellationToken: cancellationToken),
                cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var failed = files.FirstOrDefault(file => file.ErrorMessage is not null);
            if (failed is not null)
            {
                _status.Text = failed.ErrorMessage;
                return;
            }

            _files.ItemsSource = files;
            _status.Text = files.Count == 0
                ? TranslatedStrings.NoResultsFound
                : string.Format(TranslatedStrings.CompareRevisionsFileCount, files.Count);
            if (files.Count > 0)
            {
                _files.SelectedIndex = 0;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _status.Text = exception.ToStringWithData();
        }
    }

    private async Task LoadSelectedDiffAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var cancellationToken = _diffSequence.Next();
        if (_files.SelectedItem is not GitItemStatus file)
        {
            _diff.Text = string.Empty;
            return;
        }

        _diff.Text = string.Empty;
        try
        {
            (var patch, var error) = await Task.Run(
                () => _module.GetSingleDiffAsync(
                    _firstId,
                    _secondId,
                    file.Name,
                    file.OldName,
                    string.Empty,
                    _module.FilesEncoding,
                    false,
                    true,
                    false,
                    GitCommandConfiguration.Default,
                    cancellationToken),
                cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _diff.Text = error ?? patch?.Text ?? patch?.Header ?? string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!cancellationToken.IsCancellationRequested)
            {
                _diff.Text = exception.ToStringWithData();
            }
        }
    }
}
