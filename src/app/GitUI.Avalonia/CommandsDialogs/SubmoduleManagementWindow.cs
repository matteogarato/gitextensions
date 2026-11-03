using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Shims.WinForms;
using GitUI;
using Microsoft.VisualStudio.Threading;
using ResourceManager;
using ListBox = Avalonia.Controls.ListBox;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Displays submodule status and provides add, open, pull, update, URL synchronization, and removal actions.
/// </summary>
public sealed class SubmoduleManagementWindow : GitExtensionsFormBase
{
    private readonly IGitModule _module;
    private readonly IGitUICommands _uiCommands;
    private readonly ListBox _submoduleList = new() { Name = "lstSubmodules" };
    private readonly TextBlock _emptyMessage = new()
    {
        Name = "lblNoSubmodules",
        Text = GitUI.TranslatedStrings.NoSubmodulesConfigured,
    };
    private readonly Button _open = new() { Name = "btnOpenSubmodule" };
    private readonly Button _add = new() { Name = "btnAddSubmodule" };
    private readonly Button _pullMerge = new() { Name = "btnPullSubmoduleMerge" };
    private readonly Button _pullRebase = new() { Name = "btnPullSubmoduleRebase" };
    private readonly Button _update = new() { Name = "btnUpdateSubmodule" };
    private readonly Button _remove = new() { Name = "btnRemoveSubmodule" };
    private readonly Button _sync = new() { Name = "btnSyncSubmodule" };
    private readonly Func<string, bool> _openRepository;
    private readonly List<SubmoduleEntry> _submodules = [];
    private bool _hasChanges;

    /// <summary>
    ///  Gets a value indicating whether a submodule operation was run while this window was open.
    /// </summary>
    public bool HasChanges => _hasChanges;

    /// <summary>
    ///  Initializes a new instance of the <see cref="SubmoduleManagementWindow"/> class.
    /// </summary>
    /// <param name="module">The parent repository whose submodules are managed.</param>
    /// <param name="uiCommands">The commands used to run Git with process feedback.</param>
    /// <param name="openRepository">Opens a repository path in the current browse window.</param>
    public SubmoduleManagementWindow(IGitModule module, IGitUICommands uiCommands, Func<string, bool> openRepository)
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
        Title = GitUI.TranslatedStrings.Submodules;
        Width = 860;
        Height = 400;
        MinWidth = 640;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _submoduleList.SelectionChanged += (_, _) => UpdateActionState();
        _open.Content = GitUI.TranslatedStrings.Open;
        _open.Click += (_, _) => OpenSelectedSubmodule();
        _add.Content = GitUI.TranslatedStrings.AddSubmodule;
        _add.Click += (_, _) => ThreadHelper.FileAndForget(AddSubmoduleAsync);
        _pullMerge.Content = RemoveMenuMnemonic(GitUI.TranslatedStrings.PullMergeMenu);
        _pullMerge.Click += (_, _) => PullSelectedSubmodule(rebase: false);
        _pullRebase.Content = RemoveMenuMnemonic(GitUI.TranslatedStrings.PullRebaseMenu);
        _pullRebase.Click += (_, _) => PullSelectedSubmodule(rebase: true);
        _update.Content = GitUI.TranslatedStrings.SubmoduleUpdate;
        _update.Click += (_, _) => UpdateSelectedSubmodule();
        _remove.Content = GitUI.TranslatedStrings.RemoveSubmodule;
        _remove.Click += (_, _) => RemoveSelectedSubmodule();
        _sync.Content = GitUI.TranslatedStrings.SubmoduleSync;
        _sync.Click += (_, _) => SynchronizeSelectedSubmodule();
        Button close = new() { Content = GitUI.TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close();

        Grid list = new()
        {
            Children =
            {
                _submoduleList,
                _emptyMessage,
            },
        };
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
            Children =
            {
                _open,
                _add,
                _pullMerge,
                _pullRebase,
                _update,
                _remove,
                _sync,
                close,
            },
        };
        Grid.SetRow(actions, 1);
        Content = new Grid
        {
            Margin = new Avalonia.Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                list,
                actions,
            },
        };

        ReloadSubmodules();
    }

    private void ReloadSubmodules(string? selectedPath = null)
    {
        _submodules.Clear();
        StringComparer pathComparer = OperatingSystem.IsWindows()
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
        foreach (IGitSubmoduleInfo submodule in module.GetSubmodulesInfo().OfType<IGitSubmoduleInfo>())
        {
            string displayPath = string.IsNullOrEmpty(parentPath)
                ? submodule.LocalPath
                : $"{parentPath}/{submodule.LocalPath}";
            _submodules.Add(new(module, submodule, displayPath));

            if (!submodule.IsInitialized)
            {
                continue;
            }

            string fullPath = Path.GetFullPath(module.GetSubmoduleFullPath(submodule.LocalPath));
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
        IGitSubmoduleInfo submodule = entry.Submodule;
        string status = !submodule.IsInitialized
            ? GitUI.TranslatedStrings.SubmoduleNotInitialized
            : submodule.IsUpToDate
                ? GitUI.TranslatedStrings.SubmoduleUpToDate
                : GitUI.TranslatedStrings.SubmoduleModified;

        return $"{entry.DisplayPath} ({status})";
    }

    private static string RemoveMenuMnemonic(string menuText)
        => menuText.Replace("&", string.Empty, StringComparison.Ordinal);

    private void UpdateActionState()
    {
        SubmoduleEntry? selectedSubmodule = SelectedSubmodule;
        bool hasSelection = selectedSubmodule is not null;
        _open.IsEnabled = selectedSubmodule?.Submodule.IsInitialized is true;
        _pullMerge.IsEnabled = selectedSubmodule?.Submodule.IsInitialized is true;
        _pullRebase.IsEnabled = selectedSubmodule?.Submodule.IsInitialized is true;
        _update.IsEnabled = hasSelection;
        _remove.IsEnabled = hasSelection;
        _sync.IsEnabled = hasSelection;
    }

    private SubmoduleEntry? SelectedSubmodule
        => _submoduleList.SelectedIndex >= 0 && _submoduleList.SelectedIndex < _submodules.Count
            ? _submodules[_submoduleList.SelectedIndex]
            : null;

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
        bool added = await dialog.ShowDialog<bool>(this);
        if (!added)
        {
            return;
        }

        _hasChanges = true;
        ReloadSubmodules(dialog.LocalPath);
    }

    private void UpdateSelectedSubmodule()
    {
        if (SelectedSubmodule is not SubmoduleEntry entry)
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

        IGitModule submoduleModule = entry.ParentModule.GetSubmodule(entry.Submodule.LocalPath);
        string? remote = RemoteWorkflow.ResolveRemote(submoduleModule);
        if (remote is null)
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoRemoteConfiguredError);
            return;
        }

        _uiCommands.WithGitModule(submoduleModule).StartGitCommandProcessDialog(
            this,
            RemoteWorkflow.BuildPull(submoduleModule, remote, rebase));
        _hasChanges = true;
        ReloadSubmodules(entry.DisplayPath);
    }

    private void SynchronizeSelectedSubmodule()
    {
        if (SelectedSubmodule is not SubmoduleEntry entry)
        {
            return;
        }

        RunCommand(entry.ParentModule, Commands.SubmoduleSync(entry.Submodule.LocalPath), entry.DisplayPath);
    }

    private void RemoveSelectedSubmodule()
    {
        if (SelectedSubmodule is not SubmoduleEntry entry)
        {
            return;
        }

        string confirmation = string.Format(GitUI.TranslatedStrings.ConfirmRemoveSubmodule, entry.DisplayPath);
        if (GitExtensions.Extensibility.MessageBoxes.Show(
                this,
                confirmation,
                GitUI.TranslatedStrings.RemoveSubmodule,
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
            GitExtensions.Extensibility.MessageBoxes.ShowError(this, exception.ToString());
            return;
        }

        _hasChanges = true;
        ReloadSubmodules();
    }

    private void RunCommand(IGitModule module, ArgumentString arguments, string selectedPath)
    {
        _uiCommands.WithGitModule(module).StartGitCommandProcessDialog(this, arguments);
        _hasChanges = true;
        ReloadSubmodules(selectedPath);
    }

    private sealed record SubmoduleEntry(IGitModule ParentModule, IGitSubmoduleInfo Submodule, string DisplayPath);
}
