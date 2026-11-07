using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUIPluginInterfaces;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Lists the files that differ between two revisions and shows the patch of the selected file.
/// </summary>
public sealed class CompareRevisionsWindow : Window
{
    private readonly IGitModule _module;
    private readonly ObjectId _firstId;
    private readonly ObjectId _secondId;
    private readonly ObjectId _secondParentId;
    private readonly CancellationTokenSequence _filesSequence = new();
    private readonly CancellationTokenSequence _diffSequence = new();
    private readonly ListBox _files = new() { Name = "lstCompareFiles" };
    private readonly TextBox _diff = new()
    {
        Name = "txtCompareDiff",
        AcceptsReturn = true,
        IsReadOnly = true,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        FontFamily = "monospace",
    };
    private readonly TextBlock _status = new() { Name = "lblCompareStatus", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    /// <summary>
    ///  Initializes a new instance of the <see cref="CompareRevisionsWindow"/> class.
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
        Title = string.Format(GitUI.TranslatedStrings.CompareRevisionsWindowTitle, _firstId.ToShortString(), _secondId.ToShortString());
        Width = 1000;
        Height = 700;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _files.ItemTemplate = new FuncDataTemplate<GitItemStatus>(
            (item, _) => new TextBlock
            {
                Text = item is null ? string.Empty : FormatFile(item),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                Margin = new Avalonia.Thickness(4, 2),
            },
            supportsRecycling: false);
        _files.SelectionChanged += (_, _) => ThreadHelper.FileAndForget(LoadSelectedDiffAsync);

        Button close = new() { Content = GitUI.TranslatedStrings.Close, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();

        Grid content = new()
        {
            Margin = new Avalonia.Thickness(12),
            RowDefinitions = new RowDefinitions("2*,Auto,3*,Auto"),
            Children = { _files, _status, _diff, close },
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
    ///  Formats a changed file as a status letter followed by its path.
    /// </summary>
    internal static string FormatFile(GitItemStatus item)
    {
        char status = item.IsNew ? 'A'
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
        CancellationToken cancellationToken = _filesSequence.Next();
        _status.Text = GitUI.TranslatedStrings.LoadingData;
        try
        {
            IReadOnlyList<GitItemStatus> files = await Task.Run(
                () => _module.GetDiffFilesWithSubmodulesStatus(_firstId, _secondId, _secondParentId, cancellationToken: cancellationToken),
                cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            GitItemStatus? failed = files.FirstOrDefault(file => file.ErrorMessage is not null);
            if (failed is not null)
            {
                _status.Text = failed.ErrorMessage;
                return;
            }

            _files.ItemsSource = files;
            _status.Text = files.Count == 0 ? GitUI.TranslatedStrings.NoResultsFound : string.Format(GitUI.TranslatedStrings.CompareRevisionsFileCount, files.Count);
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
        CancellationToken cancellationToken = _diffSequence.Next();
        if (_files.SelectedItem is not GitItemStatus file)
        {
            _diff.Text = string.Empty;
            return;
        }

        _diff.Text = string.Empty;
        try
        {
            (Patch? patch, string? error) = await Task.Run(
                () => _module.GetSingleDiffAsync(
                    _firstId,
                    _secondId,
                    file.Name,
                    file.OldName,
                    extraDiffArguments: string.Empty,
                    _module.FilesEncoding,
                    cacheResult: false,
                    isTracked: true,
                    useGitColoring: false,
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
