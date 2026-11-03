using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using GitExtensions.Extensibility.Git;

using ResourceManager;

namespace GitUI;

// TODO(avalonia-port): grouping, filtering, staging interactions, and the full context menus
// of the WinForms FileStatusList (2,198 LOC) arrive in later milestones.
public partial class FileStatusList : GitExtensionsControl
{
    public FileStatusList()
    {
        InitializeComponent();

        btnFileHistory.Content = GitUI.TranslatedStrings.FileHistory;
        btnBlameFile.Content = GitUI.TranslatedStrings.BlameFile;
        btnFileHistory.Click += (_, _) => FileHistoryRequested?.Invoke(this, EventArgs.Empty);
        btnBlameFile.Click += (_, _) => BlameRequested?.Invoke(this, EventArgs.Empty);
        lstFiles.ItemTemplate = new FuncDataTemplate<GitItemStatus?>(CreateFileRow, supportsRecycling: false);
        lstFiles.SelectionChanged += (_, _) =>
        {
            UpdateActionState();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        };

        InitializeComplete();
        UpdateActionState();
    }

    /// <summary>
    ///  Occurs when the selected file changes (named like the WinForms event).
    /// </summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>
    ///  Occurs when the file history action is requested for the selected file.
    /// </summary>
    public event EventHandler? FileHistoryRequested;

    /// <summary>
    ///  Occurs when the blame action is requested for the selected file.
    /// </summary>
    public event EventHandler? BlameRequested;

    /// <summary>
    ///  Gets the selected file status item, or <see langword="null"/>.
    /// </summary>
    public GitItemStatus? SelectedItem => lstFiles.SelectedItem as GitItemStatus;

    /// <summary>
    ///  Shows the given diff items.
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
        bool hasSelectedFile = SelectedItem is not null;
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

        (string marker, Avalonia.Media.Color color) = item switch
        {
            { IsNew: true } => ("A", Colors.SeaGreen),
            { IsDeleted: true } => ("D", Colors.IndianRed),
            { IsRenamed: true } => ("R", Colors.SteelBlue),
            _ => ("M", Colors.Goldenrod),
        };

        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = marker,
                    Foreground = new SolidColorBrush(color),
                    FontWeight = FontWeight.Bold,
                    Width = 18,
                    Margin = new Avalonia.Thickness(4, 0, 0, 0),
                },
                new TextBlock
                {
                    Text = item.Name,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
    }
}
