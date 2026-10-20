using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using GitExtensions.Extensibility.Git;

using ResourceManager;

namespace GitUI;

public partial class WorkingTreePanel : GitExtensionsControl
{
    private bool _operationInProgress;

    public WorkingTreePanel()
    {
        InitializeComponent();

        unstagedTab.Header = GitUI.TranslatedStrings.UnstagedChanges;
        stagedTab.Header = GitUI.TranslatedStrings.StagedChanges;
        btnStageSelected.Content = GitUI.TranslatedStrings.StageSelectedFiles;
        btnUnstageSelected.Content = GitUI.TranslatedStrings.UnstageSelectedFiles;

        lstUnstaged.ItemTemplate = new FuncDataTemplate<GitItemStatus>(CreateFileRow, supportsRecycling: false);
        lstStaged.ItemTemplate = new FuncDataTemplate<GitItemStatus>(CreateFileRow, supportsRecycling: false);
        lstUnstaged.SelectionChanged += SelectionChangedHandler;
        lstStaged.SelectionChanged += SelectionChangedHandler;
        statusTabs.SelectionChanged += (_, _) => UpdateActionState();
        btnStageSelected.Click += (_, _) => StageSelectedFiles();
        btnUnstageSelected.Click += (_, _) => UnstageSelectedFiles();
        UpdateActionState();

        InitializeComplete();
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? StageRequested;

    public event EventHandler? UnstageRequested;

    public IReadOnlyList<GitItemStatus> SelectedItems => GetSelectedItems(SelectedList);

    public GitItemStatus? SelectedItem => SelectedItems.FirstOrDefault();

    public void SetStatus(IReadOnlyList<GitItemStatus> status)
    {
        GitItemStatus[] unstaged = [.. status
            .Where(item => !item.IsStatusOnly && item.Staged == StagedStatus.WorkTree)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)];
        GitItemStatus[] staged = [.. status
            .Where(item => !item.IsStatusOnly && item.Staged == StagedStatus.Index)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)];

        string[] selectedNames = [.. GetSelectedItems(lstUnstaged)
            .Concat(GetSelectedItems(lstStaged))
            .Select(item => item.Name)
            .Distinct(StringComparer.Ordinal)];
        lstUnstaged.SelectedItems?.Clear();
        lstStaged.SelectedItems?.Clear();
        lstUnstaged.ItemsSource = unstaged;
        lstStaged.ItemsSource = staged;
        RestoreSelection(lstUnstaged, unstaged, selectedNames);
        RestoreSelection(lstStaged, staged, selectedNames);
        UpdateActionState();
    }

    public void Clear()
    {
        lstUnstaged.ItemsSource = null;
        lstStaged.ItemsSource = null;
    }

    public void SetOperationInProgress(bool isInProgress)
    {
        _operationInProgress = isInProgress;
        UpdateActionState();
    }

    /// <summary>
    ///  Requests staging the selected unstaged files.
    /// </summary>
    public void StageSelectedFiles()
    {
        if (btnStageSelected.IsEnabled)
        {
            StageRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///  Requests unstaging the selected staged files.
    /// </summary>
    public void UnstageSelectedFiles()
    {
        if (btnUnstageSelected.IsEnabled)
        {
            UnstageRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static Control CreateFileRow(GitItemStatus? item, INameScope nameScope)
    {
        if (item is null)
        {
            return new TextBlock();
        }

        (string marker, IBrush color) = item switch
        {
            { IsUnmerged: true } => ("U", new SolidColorBrush(Colors.IndianRed)),
            { IsNew: true } => ("A", new SolidColorBrush(Colors.SeaGreen)),
            { IsDeleted: true } => ("D", new SolidColorBrush(Colors.IndianRed)),
            { IsRenamed: true } => ("R", new SolidColorBrush(Colors.SteelBlue)),
            _ => ("M", new SolidColorBrush(Colors.Goldenrod)),
        };

        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = marker,
                    Foreground = color,
                    FontWeight = FontWeight.Bold,
                    Width = 18,
                    Margin = new Avalonia.Thickness(4, 0, 0, 0),
                },
                new TextBlock
                {
                    Text = item.OldName is null ? item.Name : $"{item.OldName} -> {item.Name}",
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
    }

    private void SelectionChangedHandler(object? sender, SelectionChangedEventArgs e)
    {
        UpdateActionState();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private ListBox SelectedList => statusTabs.SelectedItem == stagedTab ? lstStaged : lstUnstaged;

    private void UpdateActionState()
    {
        btnStageSelected.IsEnabled = !_operationInProgress
            && statusTabs.SelectedItem != stagedTab
            && GetSelectedItems(lstUnstaged).Count > 0;
        btnUnstageSelected.IsEnabled = !_operationInProgress
            && statusTabs.SelectedItem == stagedTab
            && GetSelectedItems(lstStaged).Count > 0;
    }

    private static IReadOnlyList<GitItemStatus> GetSelectedItems(ListBox list)
    {
        return list.SelectedItems is { } selectedItems
            ? [.. selectedItems.OfType<GitItemStatus>()]
            : [];
    }

    private static void RestoreSelection(ListBox list, IReadOnlyList<GitItemStatus> items, IReadOnlyList<string> selectedNames)
    {
        foreach (GitItemStatus item in items.Where(item => selectedNames.Contains(item.Name, StringComparer.Ordinal)))
        {
            list.SelectedItems?.Add(item);
        }
    }
}
