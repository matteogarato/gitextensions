using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.Compat;
using MessageBoxDefaultButton = GitExtensions.Shims.WinForms.MessageBoxDefaultButton;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Displays read-only reflog entries for HEAD and local or remote branches.
/// </summary>
public sealed class ReflogWindow : GitExtensionsFormBase
{
    private readonly ListBox _entries = new();
    private readonly IGitModule _module;
    private readonly ComboBox _references = new() { MinWidth = 240 };
    private CancellationTokenSource? _loadCancellation;
    private long _loadVersion;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReflogWindow" /> class.
    /// </summary>
    /// <param name="module">The repository whose reflog is displayed.</param>
    public ReflogWindow(IGitModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _module = module;

        Title = TranslatedStrings.ReflogWindowTitle;
        Width = 900;
        Height = 560;
        MinWidth = 600;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeEntryContextMenu();
        _references.SelectionChanged += (_, _) => ThreadHelper.FileAndForget(LoadSelectedReferenceAsync);
        _entries.ItemTemplate = new FuncDataTemplate<ReflogEntry>(
            (entry, _) =>
            {
                Grid row = new()
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,220,*"),
                    Children =
                    {
                        new TextBlock { Text = entry.Sha.ToString(), Margin = new Thickness(4, 2) },
                        new TextBlock { Text = entry.Reference, Margin = new Thickness(8, 2) },
                        new TextBlock { Text = entry.Action, Margin = new Thickness(8, 2) }
                    }
                };
                Grid.SetColumn(row.Children[1], 1);
                Grid.SetColumn(row.Children[2], 2);
                return row;
            },
            false);

        Grid header = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,220,*"),
            Children =
            {
                new TextBlock
                {
                    Text = TranslatedStrings.ReflogSha,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(4, 2)
                },
                new TextBlock
                {
                    Text = TranslatedStrings.ReflogReference,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(8, 2)
                },
                new TextBlock
                {
                    Text = TranslatedStrings.ReflogAction,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(8, 2)
                }
            }
        };
        Grid.SetColumn(header.Children[1], 1);
        Grid.SetColumn(header.Children[2], 2);

        Button close = new() { Content = TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close();

        Grid.SetColumn(_references, 1);
        Grid.SetRow(header, 1);
        Grid.SetColumnSpan(header, 3);
        Grid.SetRow(_entries, 2);
        Grid.SetColumnSpan(_entries, 3);
        Grid.SetRow(close, 3);
        Grid.SetColumn(close, 2);
        Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowSpacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = TranslatedStrings.ReflogReference, VerticalAlignment = VerticalAlignment.Center
                },
                _references,
                header,
                _entries,
                close
            }
        };

        Opened += (_, _) => ThreadHelper.FileAndForget(LoadReferencesAsync);
        Closed += (_, _) => CancelCurrentLoad();
    }

    /// <summary>
    ///     Gets the command chosen from the entry context menu, to be run by the caller after the window closes.
    /// </summary>
    public ArgumentString? RequestedCommand { get; private set; }

    private void InitializeEntryContextMenu()
    {
        MenuItem createBranch = new() { Header = TranslatedStrings.CreateBranchMenu };
        createBranch.Click += (_, _) => ThreadHelper.FileAndForget(CreateBranchAsync);
        MenuItem resetSoft = new() { Header = TranslatedStrings.ResetSoftToRevisionMenu };
        resetSoft.Click += (_, _) => RequestReset(ResetMode.Soft);
        MenuItem resetMixed = new() { Header = TranslatedStrings.ResetMixedToRevisionMenu };
        resetMixed.Click += (_, _) => RequestReset(ResetMode.Mixed);
        MenuItem resetHard = new() { Header = TranslatedStrings.ResetHardToRevisionMenu };
        resetHard.Click += (_, _) => RequestReset(ResetMode.Hard);
        ContextMenu menu = new() { ItemsSource = new[] { createBranch, resetSoft, resetMixed, resetHard } };
        AvaloniaTranslationUtils.NormalizeMenuMnemonics(menu);
        menu.Opening += (_, e) => e.Cancel = _entries.SelectedItem is not ReflogEntry;
        _entries.ContextMenu = menu;
    }

    private async Task CreateBranchAsync()
    {
        if (_entries.SelectedItem is not ReflogEntry entry)
        {
            return;
        }

        TextPromptWindow prompt = new(TranslatedStrings.CreateBranchMenu, TranslatedStrings.BranchNamePrompt);
        var branchName = await prompt.ShowDialog<string?>(this);
        if (branchName is null)
        {
            return;
        }

        if (BranchWorkflow.ValidateNewBranchName(_module, branchName) is { } error)
        {
            MessageBoxes.ShowError(this, error);
            return;
        }

        RequestedCommand = ReflogWorkflow.BuildCreateBranch(branchName.Trim(), entry);
        Close();
    }

    private void RequestReset(ResetMode mode)
    {
        if (_entries.SelectedItem is not ReflogEntry entry)
        {
            return;
        }

        if (mode == ResetMode.Hard
            && MessageBoxes.Show(this, TranslatedStrings.ConfirmHardResetToRevision,
                TranslatedStrings.ResetHardToRevisionMenu, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        RequestedCommand = ReflogWorkflow.BuildResetTo(entry, mode);
        Close();
    }

    private async Task LoadReferencesAsync()
    {
        try
        {
            var references = await Task.Run(() =>
            {
                var localBranches =
                    _module.GetRefs(RefsFilter.Heads).Select(reference => reference.Name);
                var remoteBranches = _module.GetRemoteBranches().Select(reference => reference.Name);
                string[] names =
                [
                    "HEAD", .. localBranches.Order(StringComparer.Ordinal),
                    .. remoteBranches.Order(StringComparer.Ordinal)
                ];
                return names
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            });

            if (IsVisible)
            {
                _references.ItemsSource = references;
                _references.SelectedIndex = 0;
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private async Task LoadSelectedReferenceAsync()
    {
        if (_references.SelectedItem is not string reference)
        {
            return;
        }

        CancelCurrentLoad();
        CancellationTokenSource cancellation = new();
        _loadCancellation = cancellation;
        var version = Interlocked.Increment(ref _loadVersion);

        try
        {
            var entries = await Task.Run(
                () => ReflogWorkflow.Read(_module, reference),
                cancellation.Token);

            if (!cancellation.IsCancellationRequested && IsVisible && version == Volatile.Read(ref _loadVersion))
            {
                _entries.ItemsSource = entries;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (!cancellation.IsCancellationRequested)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelCurrentLoad()
    {
        _loadCancellation?.Cancel();
        Interlocked.Increment(ref _loadVersion);
    }
}
