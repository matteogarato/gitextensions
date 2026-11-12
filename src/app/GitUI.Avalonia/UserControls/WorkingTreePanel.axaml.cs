using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using GitUI.Compat;

namespace GitUI.UserControls;

public partial class WorkingTreePanel : GitExtensionsControl
{
    private bool _canDiscardSelectedLines;
    private bool _canStageSelectedLines;
    private bool _canUnstageSelectedLines;

    private bool _operationInProgress;
    private CommitTemplateItem? _selectedCommitTemplate;

    public WorkingTreePanel()
    {
        InitializeComponent();

        unstagedTab.Header = TranslatedStrings.UnstagedChanges;
        stagedTab.Header = TranslatedStrings.StagedChanges;
        btnStageSelected.Content = TranslatedStrings.StageSelectedFiles;
        btnStageSelectedLines.Content = TranslatedStrings.StageSelectedLines;
        btnUnstageSelected.Content = TranslatedStrings.UnstageSelectedFiles;
        btnUnstageSelectedLines.Content = TranslatedStrings.UnstageSelectedLines;
        btnDiscardSelectedLines.Content = TranslatedStrings.DiscardSelectedLines;
        btnDiscardStagedSelectedLines.Content = TranslatedStrings.DiscardSelectedLines;
        btnDiscardSelected.Content = TranslatedStrings.DiscardSelectedFiles;
        btnStashSelected.Content = TranslatedStrings.StashSelectedFiles;
        btnIgnoreSelected.Content = TranslatedStrings.AddToGitIgnore;
        btnCommit.Content = TranslatedStrings.CommitButton;
        chkAmend.Content = TranslatedStrings.AmendCommitOption;
        chkSignOff.Content = TranslatedStrings.SignOffCommitOption;
        chkNoVerify.Content = TranslatedStrings.NoVerifyCommitOption;
        chkResetAuthor.Content = TranslatedStrings.CommitResetAuthor;
        txtCommitAuthor.PlaceholderText = TranslatedStrings.CommitAuthor;
        txtCommitGpgKeyId.PlaceholderText = TranslatedStrings.CommitGpgKeyId;
        cbxPreviousCommitMessages.PlaceholderText = TranslatedStrings.CommitMessageHistory;
        cbxCommitTemplates.PlaceholderText = TranslatedStrings.CommitTemplates;
        cbxCommitGpgSigning.PlaceholderText = TranslatedStrings.CommitGpgSigning;
        cbxCommitGpgSigning.ItemsSource = new[]
        {
            new CommitSigningOption(TranslatedStrings.CommitGpgDefault, null),
            new CommitSigningOption(TranslatedStrings.CommitGpgDisabled, false),
            new CommitSigningOption(TranslatedStrings.CommitGpgDefaultKey, true),
            new CommitSigningOption(TranslatedStrings.CommitGpgSpecificKey, true, true)
        };
        cbxCommitGpgSigning.ItemTemplate = new FuncDataTemplate<CommitSigningOption>(
            (option, _) => new TextBlock { Text = option?.Text },
            false);
        cbxCommitTemplates.ItemTemplate = new FuncDataTemplate<CommitTemplateItem>(
            (template, _) => new TextBlock { Text = template?.Name },
            false);
        cbxPreviousCommitMessages.ItemTemplate = new FuncDataTemplate<CommitMessageHistoryItem>(
            (item, _) => new TextBlock { Text = item?.Preview, TextTrimming = TextTrimming.CharacterEllipsis },
            false);
        cbxCommitGpgSigning.SelectedIndex = 0;
        cbxCommitGpgSigning.SelectionChanged += (_, _) => UpdateCommitSigningState();
        cbxCommitTemplates.SelectionChanged += (_, _) => SelectCommitTemplate();
        cbxPreviousCommitMessages.SelectionChanged += (_, _) => SelectCommitMessageHistory();
        chkAmend.IsCheckedChanged += (_, _) =>
        {
            UpdateCommitSigningState();
            AmendChanged?.Invoke(this, chkAmend.IsChecked == true);
        };
        btnCommitAndPush.Content = TranslatedStrings.CommitAndPush;
        btnCommitAndPush.IsVisible = AppSettings.ShowCommitAndPush;
        txtCommitMessage.IsEnabled = AppSettings.UseFormCommitMessage;
        cbxPreviousCommitMessages.IsEnabled = AppSettings.UseFormCommitMessage;
        cbxCommitTemplates.IsEnabled = AppSettings.UseFormCommitMessage;
        txtCommitMessage.Text = AppSettings.LastCommitMessage;
        txtCommitMessage.PlaceholderText = TranslatedStrings.CommitMessageWatermark;

        lstUnstaged.ItemTemplate = new FuncDataTemplate<GitItemStatus>(CreateFileRow, false);
        lstStaged.ItemTemplate = new FuncDataTemplate<GitItemStatus>(CreateFileRow, false);
        lstUnstaged.SelectionChanged += SelectionChangedHandler;
        lstStaged.SelectionChanged += SelectionChangedHandler;
        statusTabs.SelectionChanged += (_, _) => UpdateActionState();
        btnStageSelected.Click += (_, _) => StageSelectedFiles();
        btnStageSelectedLines.Click += (_, _) => StageSelectedLines();
        btnUnstageSelected.Click += (_, _) => UnstageSelectedFiles();
        btnUnstageSelectedLines.Click += (_, _) => UnstageSelectedLines();
        btnDiscardSelectedLines.Click += (_, _) => DiscardSelectedLines();
        btnDiscardStagedSelectedLines.Click += (_, _) => DiscardSelectedLines();
        btnDiscardSelected.Click += (_, _) => DiscardSelectedFiles();
        btnStashSelected.Click += (_, _) => StashSelectedFiles();
        btnIgnoreSelected.Click += (_, _) => IgnoreSelectedFiles();
        btnCommit.Click += (_, _) => CommitRequested?.Invoke(this, GetCommitRequest());
        btnCommitAndPush.Click += (_, _) => CommitRequested?.Invoke(this, GetCommitRequest(true));
        UpdateCommitSigningState();
        UpdateActionState();

        InitializeComplete();
    }

    /// <summary>
    ///     Gets the number of staged files currently listed.
    /// </summary>
    /// <summary>
    ///     Gets the number of staged and unstaged files currently listed.
    /// </summary>
    public int ChangedCount
    {
        get => StagedCount +
               (lstUnstaged.ItemsSource is IReadOnlyCollection<GitItemStatus> unstaged ? unstaged.Count : 0);
    }

    public int StagedCount
    {
        get => lstStaged.ItemsSource is IReadOnlyCollection<GitItemStatus> staged ? staged.Count : 0;
    }

    public IReadOnlyList<GitItemStatus> SelectedItems
    {
        get => GetSelectedItems(SelectedList);
    }

    public GitItemStatus? SelectedItem
    {
        get => SelectedItems.FirstOrDefault();
    }

    /// <summary>
    ///     Gets the commit message currently entered.
    /// </summary>
    public string CommitMessage
    {
        get => txtCommitMessage.Text ?? string.Empty;
    }

    private ListBox SelectedList
    {
        get => statusTabs.SelectedItem == stagedTab ? lstStaged : lstUnstaged;
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? StageRequested;

    /// <summary>
    ///     Occurs when the user asks to stage selected changed lines.
    /// </summary>
    public event EventHandler? StageSelectedLinesRequested;

    /// <summary>
    ///     Occurs when the user asks to unstage selected changed lines.
    /// </summary>
    public event EventHandler? UnstageSelectedLinesRequested;

    /// <summary>
    ///     Occurs when the user asks to discard selected changed lines.
    /// </summary>
    public event EventHandler? DiscardSelectedLinesRequested;

    public event EventHandler? UnstageRequested;

    public event EventHandler? DiscardRequested;

    /// <summary>
    ///     Occurs when the user asks to stash the selected unstaged files.
    /// </summary>
    public event EventHandler? StashSelectedFilesRequested;

    /// <summary>
    ///     Raised when the selected untracked files should be added to <c>.gitignore</c>.
    /// </summary>
    public event EventHandler? IgnoreSelectedFilesRequested;

    /// <summary>
    ///     Occurs when the user asks to commit the staged changes.
    /// </summary>
    public event EventHandler<CommitRequest>? CommitRequested;

    /// <summary>
    ///     Raised when the amend option is toggled, with the new state.
    /// </summary>
    public event EventHandler<bool>? AmendChanged;

    public void SetStatus(IReadOnlyList<GitItemStatus> status)
    {
        GitItemStatus[] unstaged =
        [
            .. status
                .Where(item => !item.IsStatusOnly && item.Staged == StagedStatus.WorkTree)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        ];
        GitItemStatus[] staged =
        [
            .. status
                .Where(item => !item.IsStatusOnly && item.Staged == StagedStatus.Index)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        ];

        string[] selectedNames =
        [
            .. GetSelectedItems(lstUnstaged)
                .Concat(GetSelectedItems(lstStaged))
                .Select(item => item.Name)
                .Distinct(StringComparer.Ordinal)
        ];
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
        _canStageSelectedLines = false;
        _canUnstageSelectedLines = false;
        _canDiscardSelectedLines = false;
        lstUnstaged.ItemsSource = null;
        lstStaged.ItemsSource = null;
        UpdateActionState();
    }

    public CommitRequest GetCommitRequest()
    {
        return GetCommitRequest(false);
    }

    /// <summary>
    ///     Sets the available previous commit messages.
    /// </summary>
    /// <param name="messages">The messages to display.</param>
    public void SetCommitHistory(IReadOnlyList<CommitMessageHistoryItem> messages)
    {
        cbxPreviousCommitMessages.SelectedItem = null;
        cbxPreviousCommitMessages.ItemsSource = messages;
    }

    /// <summary>
    ///     Sets the available commit templates.
    /// </summary>
    /// <param name="templates">The templates to display.</param>
    public void SetCommitTemplates(IReadOnlyList<CommitTemplateItem> templates)
    {
        cbxCommitTemplates.SelectedItem = null;
        cbxCommitTemplates.ItemsSource = templates;
    }

    /// <summary>
    ///     Sets the commit message shown in the editor.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public void SetCommitMessage(string message)
    {
        _selectedCommitTemplate = null;
        txtCommitMessage.Text = message;
    }

    /// <summary>
    ///     Moves keyboard focus to the commit message editor.
    /// </summary>
    public void FocusCommitMessage()
    {
        txtCommitMessage.Focus();
    }

    public void ResetCommitOptions()
    {
        txtCommitMessage.Text = string.Empty;
        chkAmend.IsChecked = false;
        chkNoVerify.IsChecked = false;
        cbxCommitTemplates.SelectedItem = null;
        cbxPreviousCommitMessages.SelectedItem = null;
        _selectedCommitTemplate = null;
    }

    public void SetOperationInProgress(bool isInProgress)
    {
        _operationInProgress = isInProgress;
        btnCommit.IsEnabled = !isInProgress;
        btnCommitAndPush.IsEnabled = !isInProgress;
        UpdateActionState();
    }

    /// <summary>
    ///     Sets whether the selected diff currently has stageable lines.
    /// </summary>
    public void SetCanStageSelectedLines(bool canStage)
    {
        _canStageSelectedLines = canStage;
        UpdateActionState();
    }

    /// <summary>
    ///     Sets whether the selected staged diff currently has unstageable lines.
    /// </summary>
    public void SetCanUnstageSelectedLines(bool canUnstage)
    {
        _canUnstageSelectedLines = canUnstage;
        UpdateActionState();
    }

    /// <summary>
    ///     Sets whether the selected diff currently has lines that can be discarded.
    /// </summary>
    public void SetCanDiscardSelectedLines(bool canDiscard)
    {
        _canDiscardSelectedLines = canDiscard;
        UpdateActionState();
    }

    /// <summary>
    ///     Requests discarding the selected changed lines of the current file diff.
    /// </summary>
    public void DiscardSelectedLines()
    {
        if (btnDiscardSelectedLines.IsEnabled || btnDiscardStagedSelectedLines.IsEnabled)
        {
            DiscardSelectedLinesRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Requests removing the selected changed lines of the current staged file diff from the index.
    /// </summary>
    public void UnstageSelectedLines()
    {
        if (btnUnstageSelectedLines.IsEnabled)
        {
            UnstageSelectedLinesRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Requests staging the selected unstaged files.
    /// </summary>
    public void StageSelectedFiles()
    {
        if (btnStageSelected.IsEnabled)
        {
            StageRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Requests staging the selected changed lines in the current file diff.
    /// </summary>
    public void StageSelectedLines()
    {
        if (btnStageSelectedLines.IsEnabled)
        {
            StageSelectedLinesRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Requests discarding the selected unstaged files.
    /// </summary>
    public void DiscardSelectedFiles()
    {
        if (btnDiscardSelected.IsEnabled)
        {
            DiscardRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Requests adding the selected untracked files to <c>.gitignore</c>.
    /// </summary>
    public void IgnoreSelectedFiles()
    {
        if (btnIgnoreSelected.IsEnabled)
        {
            IgnoreSelectedFilesRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Requests stashing the selected unstaged files.
    /// </summary>
    public void StashSelectedFiles()
    {
        if (btnStashSelected.IsEnabled)
        {
            StashSelectedFilesRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Requests unstaging the selected staged files.
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

        (var marker, IBrush color) = item switch
        {
            { IsUnmerged: true } => ("U", new SolidColorBrush(Colors.IndianRed)),
            { IsNew: true } => ("A", new SolidColorBrush(Colors.SeaGreen)),
            { IsDeleted: true } => ("D", new SolidColorBrush(Colors.IndianRed)),
            { IsRenamed: true } => ("R", new SolidColorBrush(Colors.SteelBlue)),
            _ => ("M", new SolidColorBrush(Colors.Goldenrod))
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = marker,
                    Foreground = color,
                    FontWeight = FontWeight.Bold,
                    Width = 18,
                    Margin = new Thickness(4, 0, 0, 0)
                },
                new TextBlock
                {
                    Text = item.OldName is null ? item.Name : $"{item.OldName} -> {item.Name}",
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };
    }

    private CommitRequest GetCommitRequest(bool pushAfterCommit)
    {
        var signingOption = cbxCommitGpgSigning.SelectedItem as CommitSigningOption;
        var gpgKeyId = signingOption?.RequiresKey == true ? txtCommitGpgKeyId.Text ?? string.Empty : string.Empty;
        return new CommitRequest(
            txtCommitMessage.Text ?? string.Empty,
            chkAmend.IsChecked == true,
            chkSignOff.IsChecked == true,
            chkNoVerify.IsChecked == true,
            txtCommitAuthor.Text ?? string.Empty,
            signingOption?.Sign,
            gpgKeyId,
            chkAmend.IsChecked == true && chkResetAuthor.IsChecked == true,
            _selectedCommitTemplate is not null,
            _selectedCommitTemplate?.Text ?? string.Empty,
            pushAfterCommit,
            AppSettings.UseFormCommitMessage);
    }

    private void SelectCommitTemplate()
    {
        if (cbxCommitTemplates.SelectedItem is not CommitTemplateItem template)
        {
            return;
        }

        _selectedCommitTemplate = template;
        txtCommitMessage.Text = template.Text;
        cbxPreviousCommitMessages.SelectedItem = null;
    }

    private void SelectCommitMessageHistory()
    {
        if (cbxPreviousCommitMessages.SelectedItem is not CommitMessageHistoryItem item)
        {
            return;
        }

        _selectedCommitTemplate = null;
        txtCommitMessage.Text = item.Message;
        cbxCommitTemplates.SelectedItem = null;
    }

    private void UpdateCommitSigningState()
    {
        txtCommitGpgKeyId.IsVisible = cbxCommitGpgSigning.SelectedItem is CommitSigningOption { RequiresKey: true };
        chkResetAuthor.IsEnabled = chkAmend.IsChecked == true;
    }

    private void SelectionChangedHandler(object? sender, SelectionChangedEventArgs e)
    {
        UpdateActionState();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateActionState()
    {
        btnStageSelected.IsEnabled = !_operationInProgress
                                     && statusTabs.SelectedItem != stagedTab
                                     && GetSelectedItems(lstUnstaged).Count > 0;
        btnStageSelectedLines.IsEnabled = btnStageSelected.IsEnabled && _canStageSelectedLines;
        btnDiscardSelected.IsEnabled = btnStageSelected.IsEnabled;
        btnIgnoreSelected.IsEnabled = !_operationInProgress
                                      && statusTabs.SelectedItem != stagedTab
                                      && GetSelectedItems(lstUnstaged).Any(item => !item.IsTracked && item.IsNew);
        btnStashSelected.IsEnabled = !_operationInProgress
                                     && statusTabs.SelectedItem != stagedTab
                                     && GetSelectedItems(lstUnstaged).Count > 0;
        btnUnstageSelected.IsEnabled = !_operationInProgress
                                       && statusTabs.SelectedItem == stagedTab
                                       && GetSelectedItems(lstStaged).Count > 0;
        btnUnstageSelectedLines.IsEnabled = btnUnstageSelected.IsEnabled && _canUnstageSelectedLines;
        btnDiscardSelectedLines.IsEnabled = btnStageSelected.IsEnabled && _canDiscardSelectedLines;
        btnDiscardStagedSelectedLines.IsEnabled = btnUnstageSelected.IsEnabled && _canDiscardSelectedLines;
    }

    private static IReadOnlyList<GitItemStatus> GetSelectedItems(ListBox list)
    {
        return list.SelectedItems is { } selectedItems
            ? [.. selectedItems.OfType<GitItemStatus>()]
            : [];
    }

    private static void RestoreSelection(ListBox list, IReadOnlyList<GitItemStatus> items,
        IReadOnlyList<string> selectedNames)
    {
        foreach (var item in items.Where(item => selectedNames.Contains(item.Name, StringComparer.Ordinal)))
        {
            list.SelectedItems?.Add(item);
        }
    }

    private sealed record CommitSigningOption(string Text, bool? Sign, bool RequiresKey = false);
}
