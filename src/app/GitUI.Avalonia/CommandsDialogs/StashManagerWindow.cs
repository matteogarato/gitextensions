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
///  The action chosen in the <see cref="StashManagerWindow"/>.
/// </summary>
public enum StashManagerAction
{
    Apply,
    Pop,
    Drop,
    Clear,
}

/// <summary>
///  The outcome of the <see cref="StashManagerWindow"/>: an action and, except for clearing, the target stash.
/// </summary>
public sealed record StashManagerResult(StashManagerAction Action, GitStash? Stash);

/// <summary>
///  Lists stashes, previews the files and diff of the selected one and lets the user apply, pop, drop or clear.
/// </summary>
public sealed class StashManagerWindow : Window
{
    private readonly IGitModule _module;
    private readonly CancellationTokenSequence _filesSequence = new();
    private readonly CancellationTokenSequence _diffSequence = new();
    private readonly ListBox _stashes = new() { Name = "lstStashes" };
    private readonly ListBox _files = new() { Name = "lstStashFiles" };
    private readonly TextBox _diff = new()
    {
        Name = "txtStashDiff",
        AcceptsReturn = true,
        IsReadOnly = true,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        FontFamily = "monospace",
    };
    private readonly Button _apply = new() { Name = "btnStashApply" };
    private readonly Button _pop = new() { Name = "btnStashPop" };
    private readonly Button _drop = new() { Name = "btnStashDrop" };
    private readonly Button _clear = new() { Name = "btnStashClear" };

    /// <summary>
    ///  Initializes a new instance of the <see cref="StashManagerWindow"/> class.
    /// </summary>
    public StashManagerWindow(IGitModule module, IReadOnlyList<GitStash> stashes)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(stashes);

        _module = module;
        Title = GitUI.TranslatedStrings.StashManagerTitle;
        Width = 1000;
        Height = 700;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _stashes.ItemsSource = stashes;
        _stashes.ItemTemplate = new FuncDataTemplate<GitStash>(
            (item, _) => new TextBlock { Text = item?.Summary ?? string.Empty, Margin = new Avalonia.Thickness(4, 2) },
            supportsRecycling: false);
        _files.ItemTemplate = new FuncDataTemplate<GitItemStatus>(
            (item, _) => new TextBlock { Text = item is null ? string.Empty : CompareRevisionsWindow.FormatFile(item), Margin = new Avalonia.Thickness(4, 2) },
            supportsRecycling: false);
        _stashes.SelectionChanged += (_, _) =>
        {
            UpdateButtons();
            ThreadHelper.FileAndForget(LoadFilesAsync);
        };
        _files.SelectionChanged += (_, _) => ThreadHelper.FileAndForget(LoadDiffAsync);

        _apply.Content = GitUI.TranslatedStrings.StashApplyMenu;
        _pop.Content = GitUI.TranslatedStrings.StashPopMenu;
        _drop.Content = GitUI.TranslatedStrings.StashDropMenu;
        _clear.Content = GitUI.TranslatedStrings.StashClearButton;
        _apply.Click += (_, _) => Complete(StashManagerAction.Apply);
        _pop.Click += (_, _) => Complete(StashManagerAction.Pop);
        _drop.Click += (_, _) => Complete(StashManagerAction.Drop);
        _clear.Click += (_, _) => Complete(StashManagerAction.Clear);
        Button close = new() { Content = GitUI.TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close(null);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _apply, _pop, _drop, _clear, close },
        };

        Grid top = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            Children = { _stashes, _files },
        };
        Grid.SetColumn(_files, 1);

        Grid content = new()
        {
            Margin = new Avalonia.Thickness(12),
            RowSpacing = 8,
            RowDefinitions = new RowDefinitions("2*,3*,Auto"),
            Children = { top, _diff, buttons },
        };
        Grid.SetRow(_diff, 1);
        Grid.SetRow(buttons, 2);
        Content = content;

        _clear.IsEnabled = stashes.Count > 0;
        UpdateButtons();
        if (stashes.Count > 0)
        {
            _stashes.SelectedIndex = 0;
        }

        Closed += (_, _) =>
        {
            _filesSequence.Dispose();
            _diffSequence.Dispose();
        };
    }

    private void Complete(StashManagerAction action)
    {
        GitStash? stash = _stashes.SelectedItem as GitStash;
        if (action != StashManagerAction.Clear && stash is null)
        {
            return;
        }

        Close(new StashManagerResult(action, stash));
    }

    private void UpdateButtons()
    {
        bool hasSelection = _stashes.SelectedItem is GitStash;
        _apply.IsEnabled = hasSelection;
        _pop.IsEnabled = hasSelection;
        _drop.IsEnabled = hasSelection;
    }

    private async Task LoadFilesAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        CancellationToken cancellationToken = _filesSequence.Next();
        _files.ItemsSource = null;
        _diff.Text = string.Empty;
        if (_stashes.SelectedItem is not GitStash stash)
        {
            return;
        }

        try
        {
            IReadOnlyList<GitItemStatus> files = await Task.Run(() => _module.GetStashDiffFiles(stash.Name), cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _files.ItemsSource = files;
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
            _diff.Text = exception.ToStringWithData();
        }
    }

    private async Task LoadDiffAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        CancellationToken cancellationToken = _diffSequence.Next();
        _diff.Text = string.Empty;
        if (_stashes.SelectedItem is not GitStash stash || _files.SelectedItem is not GitItemStatus file)
        {
            return;
        }

        try
        {
            (Patch? patch, string? error) = await Task.Run(
                () => _module.GetSingleDiffAsync(
                    _module.RevParse(stash.Name + "^"),
                    _module.RevParse(stash.Name),
                    file.Name,
                    file.OldName,
                    extraDiffArguments: string.Empty,
                    _module.FilesEncoding,
                    cacheResult: false,
                    isTracked: !file.IsNew,
                    useGitColoring: false,
                    GitCommandConfiguration.Default,
                    cancellationToken),
                cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _diff.Text = error ?? patch?.Text ?? string.Empty;
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
