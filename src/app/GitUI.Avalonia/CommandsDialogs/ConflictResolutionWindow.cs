using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Lists unresolved files and opens the repository's configured mergetool.
/// </summary>
public sealed class ConflictResolutionWindow : Window
{
    private readonly ListBox _conflicts = new() { Height = 280 };

    private readonly Button _continue =
        new() { Content = TranslatedStrings.ContinueAfterResolution, IsVisible = false };

    private readonly IGitModule _module;
    private readonly bool _offerContinue;
    private readonly Button _openMergeTool = new() { Content = TranslatedStrings.OpenMergeTool };
    private readonly Button _stageSelected = new() { Content = TranslatedStrings.StageConflictResolution };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _useOurs = new() { Content = TranslatedStrings.UseOursResolution };
    private readonly Button _useTheirs = new() { Content = TranslatedStrings.UseTheirsResolution };

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConflictResolutionWindow" /> class.
    /// </summary>
    /// <param name="module">The repository whose unresolved conflicts are shown.</param>
    /// <param name="offerContinue">Whether to offer finishing the interrupted operation once all conflicts are resolved.</param>
    public ConflictResolutionWindow(IGitModule module, bool offerContinue = false)
    {
        _module = module;
        _offerContinue = offerContinue;
        Title = TranslatedStrings.ResolveMergeConflicts;
        Width = 640;
        Height = 420;
        MinWidth = 480;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _conflicts.SelectionChanged += (_, _) => UpdateActionState();
        _openMergeTool.Click += (_, _) => ThreadHelper.FileAndForget(OpenMergeToolAsync);
        _stageSelected.Click += (_, _) => ThreadHelper.FileAndForget(StageSelectedAsync);
        _useOurs.Click += (_, _) => ThreadHelper.FileAndForget(() => ResolveWithSideAsync(ConflictSide.Ours));
        _useTheirs.Click += (_, _) => ThreadHelper.FileAndForget(() => ResolveWithSideAsync(ConflictSide.Theirs));
        _continue.Click += (_, _) =>
        {
            ContinueRequested = true;
            Close();
        };

        Button refresh = new() { Content = TranslatedStrings.RefreshConflicts };
        refresh.Click += (_, _) => ThreadHelper.FileAndForget(RefreshConflictsAsync);
        Button close = new() { Content = TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                _status,
                _conflicts,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        _openMergeTool,
                        _useOurs,
                        _useTheirs,
                        _stageSelected,
                        refresh,
                        _continue,
                        close
                    }
                }
            }
        };

        UpdateActionState();
        Opened += (_, _) => ThreadHelper.FileAndForget(RefreshConflictsAsync);
    }

    /// <summary>
    ///     Gets a value indicating whether the user chose to finish the interrupted operation.
    /// </summary>
    public bool ContinueRequested { get; private set; }

    private string? SelectedFilename
    {
        get => _conflicts.SelectedItem as string;
    }

    private async Task RefreshConflictsAsync()
    {
        try
        {
            IReadOnlyList<ConflictData> conflicts = await _module.GetConflictsAsync();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _conflicts.ItemsSource = conflicts.Select(conflict => conflict.Filename).ToArray();
            _status.Text = conflicts.Count == 0
                ? TranslatedStrings.NoResultsFound
                : TranslatedStrings.UnresolvedConflicts;
            UpdateActionState();
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _status.Text = exception.ToStringWithData();
        }
    }

    private async Task OpenMergeToolAsync()
    {
        var filename = SelectedFilename;
        if (filename is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => _module.RunMergeTool(filename));
            await RefreshConflictsAsync();
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _status.Text = exception.ToStringWithData();
        }
    }

    private async Task StageSelectedAsync()
    {
        var filename = SelectedFilename;
        if (filename is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => _module.StageFile(filename));
            await RefreshConflictsAsync();
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _status.Text = exception.ToStringWithData();
        }
    }

    private async Task ResolveWithSideAsync(ConflictSide side)
    {
        var filename = SelectedFilename;
        if (filename is null)
        {
            return;
        }

        try
        {
            var error = await Task.Run(() => ConflictWorkflow.ResolveWithSide(_module, filename, side));
            await RefreshConflictsAsync();
            if (error is not null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _status.Text = error;
            }
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _status.Text = exception.ToStringWithData();
        }
    }

    private void UpdateActionState()
    {
        var fileSelected = SelectedFilename is not null;
        _openMergeTool.IsEnabled = fileSelected;
        _stageSelected.IsEnabled = fileSelected;
        _useOurs.IsEnabled = fileSelected;
        _useTheirs.IsEnabled = fileSelected;
        _continue.IsVisible = _offerContinue
                              && _conflicts.ItemCount == 0
                              && ConflictWorkflow.BuildContinue(_module) is not null;
    }
}
