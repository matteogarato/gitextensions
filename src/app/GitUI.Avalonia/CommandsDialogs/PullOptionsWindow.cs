using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Selects the remote, branch and options for a pull or fetch.
/// </summary>
public sealed class PullOptionsWindow : OptionsWindow
{
    private readonly ComboBox _remote = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _remoteBranch = new() { PlaceholderText = GitUI.TranslatedStrings.RemoteBranchHint };
    private readonly RadioButton _merge = new() { Content = GitUI.TranslatedStrings.PullModeMerge, GroupName = nameof(PullMode), IsChecked = true };
    private readonly RadioButton _rebase = new() { Content = GitUI.TranslatedStrings.PullModeRebase, GroupName = nameof(PullMode) };
    private readonly RadioButton _fetchOnly = new() { Content = GitUI.TranslatedStrings.PullModeFetchOnly, GroupName = nameof(PullMode) };
    private readonly ComboBox _tags = new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
    private readonly CheckBox _unshallow = new() { Content = GitUI.TranslatedStrings.PullUnshallow };
    private readonly CheckBox _prune = new() { Content = GitUI.TranslatedStrings.PullPrune };
    private readonly CheckBox _autoStash = new() { Content = GitUI.TranslatedStrings.PullAutoStash };

    /// <summary>
    ///  Initializes a new instance of the <see cref="PullOptionsWindow"/> class.
    /// </summary>
    /// <param name="remotes">The configured remote names.</param>
    /// <param name="defaultRemote">The remote selected initially.</param>
    /// <param name="autoStash">The initial value of the auto-stash option.</param>
    public PullOptionsWindow(IReadOnlyList<string> remotes, string? defaultRemote, bool autoStash)
        : base(GitUI.TranslatedStrings.PullOptionsTitle, 460, 380)
    {
        _remote.ItemsSource = remotes;
        _remote.SelectedItem = defaultRemote is not null && remotes.Contains(defaultRemote) ? defaultRemote : remotes.FirstOrDefault();
        _tags.ItemsSource = new[]
        {
            GitUI.TranslatedStrings.TagsDefault,
            GitUI.TranslatedStrings.TagsAll,
            GitUI.TranslatedStrings.TagsNone,
        };
        _autoStash.IsChecked = autoStash;
        _merge.IsCheckedChanged += (_, _) => UpdateActionState();
        _rebase.IsCheckedChanged += (_, _) => UpdateActionState();
        _fetchOnly.IsCheckedChanged += (_, _) => UpdateActionState();

        Button execute = new() { Content = GitUI.TranslatedStrings.PullExecute, IsDefault = true };
        execute.Click += (_, _) => Close(CreateOptions());

        SetOptionsContent(
            execute,
            [
                new TextBlock { Text = GitUI.TranslatedStrings.RemoteLabel },
                _remote,
                new TextBlock { Text = GitUI.TranslatedStrings.RemoteBranchLabel },
                _remoteBranch,
                _merge,
                _rebase,
                _fetchOnly,
                new TextBlock { Text = GitUI.TranslatedStrings.TagsLabel },
                _tags,
                _unshallow,
                _prune,
                _autoStash,
            ]);

        UpdateActionState();
    }

    private PullOptions? CreateOptions()
    {
        if (_remote.SelectedItem is not string remote)
        {
            return null;
        }

        PullMode mode = _fetchOnly.IsChecked == true ? PullMode.FetchOnly : _rebase.IsChecked == true ? PullMode.Rebase : PullMode.Merge;
        return new PullOptions(
            remote,
            _remoteBranch.Text,
            mode,
            (PullTagsMode)Math.Max(_tags.SelectedIndex, 0),
            _unshallow.IsChecked == true,
            mode == PullMode.FetchOnly && _prune.IsChecked == true,
            mode != PullMode.FetchOnly && _autoStash.IsChecked == true);
    }

    private void UpdateActionState()
    {
        bool fetchOnly = _fetchOnly.IsChecked == true;
        _prune.IsEnabled = fetchOnly;
        _autoStash.IsEnabled = !fetchOnly;
    }
}
