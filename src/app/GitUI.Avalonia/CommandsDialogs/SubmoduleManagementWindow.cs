using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Shims.WinForms;
using GitUI.Compat;
using ListBox = Avalonia.Controls.ListBox;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Displays submodule status and provides add, open, pull, update, URL synchronization, and removal actions.
/// </summary>
public sealed class SubmoduleManagementWindow : GitExtensionsFormBase
{
    private readonly Button _add = new() { Name = "btnAddSubmodule" };

    private readonly TextBlock _emptyMessage = new()
    {
        Name = "lblNoSubmodules", Text = TranslatedStrings.NoSubmodulesConfigured
    };

    private readonly IGitModule _module;
    private readonly Button _open = new() { Name = "btnOpenSubmodule" };
    private readonly Func<string, bool> _openRepository;
    private readonly Button _pullMerge = new() { Name = "btnPullSubmoduleMerge" };
    private readonly Button _pullRebase = new() { Name = "btnPullSubmoduleRebase" };
    private readonly Button _remove = new() { Name = "btnRemoveSubmodule" };
    private readonly ListBox _submoduleList = new() { Name = "lstSubmodules" };
    private readonly List<SubmoduleEntry> _submodules = [];
    private readonly Button _sync = new() { Name = "btnSyncSubmodule" };
    private readonly IGitUiCommands _uiCommands;
    private readonly Button _update = new() { Name = "btnUpdateSubmodule" };

    /// <summary>
    ///     Initializes a new instance of the <see cref="SubmoduleManagementWindow" /> class.
    /// </summary>
    /// <param name="module">The parent repository whose submodules are managed.</param>
    /// <param name="uiCommands">The commands used to run Git with process feedback.</param>
    /// <param name="openRepository">Opens a repository path in the current browse window.</param>
    public SubmoduleManagementWindow(IGitModule module, IGitUiCommands uiCommands, Func<string, bool> openRepository)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(uiCommands);
        ArgumentNullException.ThrowIfNull(openRepository);

        _module = module;
        _uiCommands = uiCommands;
        _openRepository = openRepository;
        NameScope nameScope = new();
        NameScope.SetNameScope(this, nameScope);
        nameScope.Register("lstSubmodules", _submoduleList);
        nameScope.Register("lblNoSubmodules", _emptyMessage);
        nameScope.Register("btnOpenSubmodule", _open);
        nameScope.Register("btnAddSubmodule", _add);
        nameScope.Register("btnPullSubmoduleMerge", _pullMerge);
        nameScope.Register("btnPullSubmoduleRebase", _pullRebase);
        nameScope.Register("btnUpdateSubmodule", _update);
        nameScope.Register("btnRemoveSubmodule", _remove);
        nameScope.Register("btnSyncSubmodule", _sync);
        Title = TranslatedStrings.Submodules;
        Width = 860;
        Height = 400;
        MinWidth = 640;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _submoduleList.SelectionChanged += (_, _) => UpdateActionState();
        _open.Content = TranslatedStrings.Open;
        _open.Click += (_, _) => OpenSelectedSubmodule();
        _add.Content = TranslatedStrings.AddSubmodule;
        _add.Click += (_, _) => ThreadHelper.FileAndForget(AddSubmoduleAsync);
        _pullMerge.Content = RemoveMenuMnemonic(TranslatedStrings.PullMergeMenu);
        _pullMerge.Click += (_, _) => PullSelectedSubmodule(false);
        _pullRebase.Content = RemoveMenuMnemonic(TranslatedStrings.PullRebaseMenu);
        _pullRebase.Click += (_, _) => PullSelectedSubmodule(true);
        _update.Content = TranslatedStrings.SubmoduleUpdate;
        _update.Click += (_, _) => UpdateSelectedSubmodule();
        _remove.Content = TranslatedStrings.RemoveSubmodule;
        _remove.Click += (_, _) => RemoveSelectedSubmodule();
        _sync.Content = TranslatedStrings.SubmoduleSync;
        _sync.Click += (_, _) => SynchronizeSelectedSubmodule();
        Button close = new() { Content = TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close();

        Grid list = new() { Children = { _submoduleList, _emptyMessage } };
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
            {
                _open,
                _add,
                _pullMerge,
                _pullRebase,
                _update,
                _remove,
                _sync,
                close
            }
        };
        Grid.SetRow(actions, 1);
        Content = new Grid
        {
            Margin = new Thickness(12), RowDefinitions = new RowDefinitions("*,Auto"), Children = { list, actions }
        };

        ReloadSubmodules();
    }

    /// <summary>
    ///     Gets a value indicating whether a submodule operation was run while this window was open.
    /// </summary>
    public bool HasChanges { get; private set; }

    private SubmoduleEntry? SelectedSubmodule
    {
        get => _submoduleList.SelectedIndex >= 0 && _submoduleList.SelectedIndex < _submodules.Count
            ? _submodules[_submoduleList.SelectedIndex]
            : null;
    }

    private void ReloadSubmodules(string? selectedPath = null)
    {
        _submodules.Clear();
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        HashSet<string> ancestorPaths = new(pathComparer) { Path.GetFullPath(_module.WorkingDir) };
        LoadSubmodules(_module, string.Empty, ancestorPaths);

        _submoduleList.ItemsSource = _submodules.Select(GetDisplayText).ToArray();
        _emptyMessage.IsVisible = _submodules.Count == 0;
        _submoduleList.SelectedIndex = selectedPath is null
            ? -1
            : _submodules.FindIndex(submodule => submodule.DisplayPath == selectedPath);
        UpdateActionState();
    }

    private void LoadSubmodules(IGitModule module, string parentPath, HashSet<string> ancestorPaths)
    {
        foreach (var submodule in module.GetSubmodulesInfo().OfType<IGitSubmoduleInfo>())
        {
            var displayPath = string.IsNullOrEmpty(parentPath)
                ? submodule.LocalPath
                : $"{parentPath}/{submodule.LocalPath}";
            _submodules.Add(new SubmoduleEntry(module, submodule, displayPath));

            if (!submodule.IsInitialized)
            {
                continue;
            }

            var fullPath = Path.GetFullPath(module.GetSubmoduleFullPath(submodule.LocalPath));
            if (!ancestorPaths.Add(fullPath))
            {
                continue;
            }

            LoadSubmodules(module.GetSubmodule(submodule.LocalPath), displayPath, ancestorPaths);
            ancestorPaths.Remove(fullPath);
        }
    }

    private static string GetDisplayText(SubmoduleEntry entry)
    {
        var submodule = entry.Submodule;
        var status = !submodule.IsInitialized
            ? TranslatedStrings.SubmoduleNotInitialized
            : submodule.IsUpToDate
                ? TranslatedStrings.SubmoduleUpToDate
                : TranslatedStrings.SubmoduleModified;

        return $"{entry.DisplayPath} ({status})";
    }

    private static string RemoveMenuMnemonic(string menuText)
    {
        return menuText.Replace("&", string.Empty, StringComparison.Ordinal);
    }

    private void UpdateActionState()
    {
        var selectedSubmodule = SelectedSubmodule;
        var hasSelection = selectedSubmodule is not null;
        _open.IsEnabled = selectedSubmodule?.Submodule.IsInitialized is true;
        _pullMerge.IsEnabled = selectedSubmodule?.Submodule.IsInitialized is true;
        _pullRebase.IsEnabled = selectedSubmodule?.Submodule.IsInitialized is true;
        _update.IsEnabled = hasSelection;
        _remove.IsEnabled = hasSelection;
        _sync.IsEnabled = hasSelection;
    }

    private void OpenSelectedSubmodule()
    {
        if (SelectedSubmodule is not { Submodule.IsInitialized: true } entry)
        {
            return;
        }

        if (_openRepository(entry.ParentModule.GetSubmoduleFullPath(entry.Submodule.LocalPath)))
        {
            Close();
        }
    }

    private async Task AddSubmoduleAsync()
    {
        AddSubmoduleWindow dialog = new(_uiCommands);
        var added = await dialog.ShowDialog<bool>(this);
        if (!added)
        {
            return;
        }

        HasChanges = true;
        ReloadSubmodules(dialog.LocalPath);
    }

    private void UpdateSelectedSubmodule()
    {
        if (SelectedSubmodule is not { } entry)
        {
            return;
        }

        RunCommand(entry.ParentModule, Commands.SubmoduleUpdate(entry.Submodule.LocalPath), entry.DisplayPath);
    }

    private void PullSelectedSubmodule(bool rebase)
    {
        if (SelectedSubmodule is not { Submodule.IsInitialized: true } entry)
        {
            return;
        }

        var submoduleModule = entry.ParentModule.GetSubmodule(entry.Submodule.LocalPath);
        var remote = RemoteWorkflow.ResolveRemote(submoduleModule);
        if (remote is null)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.NoRemoteConfiguredError);
            return;
        }

        _uiCommands.WithGitModule(submoduleModule).StartGitCommandProcessDialog(
            this,
            RemoteWorkflow.BuildPull(submoduleModule, remote, rebase));
        HasChanges = true;
        ReloadSubmodules(entry.DisplayPath);
    }

    private void SynchronizeSelectedSubmodule()
    {
        if (SelectedSubmodule is not { } entry)
        {
            return;
        }

        RunCommand(entry.ParentModule, Commands.SubmoduleSync(entry.Submodule.LocalPath), entry.DisplayPath);
    }

    private void RemoveSelectedSubmodule()
    {
        if (SelectedSubmodule is not { } entry)
        {
            return;
        }

        var confirmation = string.Format(TranslatedStrings.ConfirmRemoveSubmodule, entry.DisplayPath);
        if (MessageBoxes.Show(
                this,
                confirmation,
                TranslatedStrings.RemoveSubmodule,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SubmoduleWorkflow.Remove(entry.ParentModule, entry.Submodule);
        }
        catch (GitConfigurationException exception)
        {
            MessageBoxes.ShowError(this, exception.ToString());
            return;
        }

        HasChanges = true;
        ReloadSubmodules();
    }

    private void RunCommand(IGitModule module, ArgumentString arguments, string selectedPath)
    {
        _uiCommands.WithGitModule(module).StartGitCommandProcessDialog(this, arguments);
        HasChanges = true;
        ReloadSubmodules(selectedPath);
    }

    private sealed record SubmoduleEntry(IGitModule ParentModule, IGitSubmoduleInfo Submodule, string DisplayPath);
}
