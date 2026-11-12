using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using GitExtensions.Extensibility.Git;
using GitUI.Compat;
using Color = Avalonia.Media.Color;

namespace GitUI.UserControls;

/// <summary>
///     Path-related actions offered by the changed-file list context menu.
/// </summary>
public enum FileListAction
{
    CopyPath,
    CopyFullPath,
    Open,
    ShowInFolder
}

// TODO(avalonia-port): grouping, filtering, staging interactions, and the full context menus
// of the WinForms FileStatusList (2,198 LOC) arrive in later milestones.
public partial class FileStatusList : GitExtensionsControl
{
    public FileStatusList()
    {
        InitializeComponent();

        btnFileHistory.Content = TranslatedStrings.FileHistory;
        btnBlameFile.Content = TranslatedStrings.BlameFile;
        btnFileHistory.Click += (_, _) => FileHistoryRequested?.Invoke(this, EventArgs.Empty);
        btnBlameFile.Click += (_, _) => BlameRequested?.Invoke(this, EventArgs.Empty);
        MenuItem copyPath = new() { Header = TranslatedStrings.CopyPathMenu };
        MenuItem copyFullPath = new() { Header = TranslatedStrings.CopyFullPathMenu };
        MenuItem openFile = new() { Header = TranslatedStrings.OpenFileMenu };
        MenuItem showInFolder = new() { Header = TranslatedStrings.ShowInFolderMenu };
        copyPath.Click += (_, _) => PathActionRequested?.Invoke(this, FileListAction.CopyPath);
        copyFullPath.Click += (_, _) => PathActionRequested?.Invoke(this, FileListAction.CopyFullPath);
        openFile.Click += (_, _) => PathActionRequested?.Invoke(this, FileListAction.Open);
        showInFolder.Click += (_, _) => PathActionRequested?.Invoke(this, FileListAction.ShowInFolder);
        ContextMenu menu = new() { ItemsSource = new[] { copyPath, copyFullPath, openFile, showInFolder } };
        menu.Opening += (_, _) =>
        {
            foreach (var item in new[] { copyPath, copyFullPath, openFile, showInFolder })
            {
                item.IsEnabled = SelectedItem is not null;
            }
        };
        lstFiles.ContextMenu = menu;
        lstFiles.ItemTemplate = new FuncDataTemplate<GitItemStatus?>(CreateFileRow);
        lstFiles.SelectionChanged += (_, _) =>
        {
            UpdateActionState();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        };

        InitializeComplete();
        UpdateActionState();
    }

    /// <summary>
    ///     Gets the selected file status item, or <see langword="null" />.
    /// </summary>
    public GitItemStatus? SelectedItem
    {
        get => lstFiles.SelectedItem as GitItemStatus;
    }

    /// <summary>
    ///     Occurs when the selected file changes (named like the WinForms event).
    /// </summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>
    ///     Occurs when the file history action is requested for the selected file.
    /// </summary>
    public event EventHandler? FileHistoryRequested;

    /// <summary>
    ///     Occurs when the blame action is requested for the selected file.
    /// </summary>
    public event EventHandler? BlameRequested;

    /// <summary>
    ///     Occurs when a path-related context menu action is requested for the selected file.
    /// </summary>
    public event EventHandler<FileListAction>? PathActionRequested;

    /// <summary>
    ///     Shows the given diff items.
    /// </summary>
    public void SetDiffs(IReadOnlyList<GitItemStatus> items)
    {
        lstFiles.ItemsSource = items;
        lblCount.Text = $"{items.Count} {(items.Count == 1 ? "file" : "files")}";

        // Like the WinForms FileStatusList, select the first file automatically.
        if (items.Count > 0)
        {
            lstFiles.SelectedIndex = 0;
        }

        UpdateActionState();
    }

    public void Clear()
    {
        lstFiles.ItemsSource = null;
        lblCount.Text = string.Empty;
        UpdateActionState();
    }

    private void UpdateActionState()
    {
        var hasSelectedFile = SelectedItem is not null;
        btnFileHistory.IsEnabled = hasSelectedFile;
        btnBlameFile.IsEnabled = hasSelectedFile;
    }

    private static Control CreateFileRow(GitItemStatus? item, INameScope nameScope)
    {
        // Avalonia rebuilds the template with null content while clearing recycled containers.
        if (item is null)
        {
            return new Panel();
        }

        (var marker, var color) = item switch
        {
            { IsNew: true } => ("A", Colors.SeaGreen),
            { IsDeleted: true } => ("D", Colors.IndianRed),
            { IsRenamed: true } => ("R", Colors.SteelBlue),
            _ => ("M", Colors.Goldenrod)
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = marker,
                    Foreground = new SolidColorBrush(color),
                    FontWeight = FontWeight.Bold,
                    Width = 18,
                    Margin = new Thickness(4, 0, 0, 0)
                },
                new TextBlock { Text = item.Name, TextTrimming = TextTrimming.CharacterEllipsis }
            }
        };
    }
}
