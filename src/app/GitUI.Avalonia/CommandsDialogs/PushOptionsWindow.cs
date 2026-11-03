using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Selects the remote, branch and options for a push.
/// </summary>
public sealed class PushOptionsWindow : OptionsWindow
{
    private readonly ComboBox _remote = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _remoteBranch = new() { PlaceholderText = GitUI.TranslatedStrings.RemoteBranchHint };
    private readonly RadioButton _currentBranch = new() { GroupName = nameof(PushScope), IsChecked = true };
    private readonly RadioButton _allBranches = new() { Content = GitUI.TranslatedStrings.PushScopeAllBranches, GroupName = nameof(PushScope) };
    private readonly RadioButton _tags = new() { Content = GitUI.TranslatedStrings.PushScopeTags, GroupName = nameof(PushScope) };
    private readonly ComboBox _force = new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
    private readonly ComboBox _submodules = new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
    private readonly CheckBox _track = new() { Content = GitUI.TranslatedStrings.PushTrackUpstream };

    /// <summary>
    ///  Initializes a new instance of the <see cref="PushOptionsWindow"/> class.
    /// </summary>
    /// <param name="remotes">The configured remote names.</param>
    /// <param name="defaultRemote">The remote selected initially.</param>
    /// <param name="currentBranch">The branch that is pushed in the current-branch scope.</param>
    /// <param name="trackByDefault">Whether the branch has no upstream yet.</param>
    public PushOptionsWindow(IReadOnlyList<string> remotes, string? defaultRemote, string currentBranch, bool trackByDefault)
        : base(GitUI.TranslatedStrings.PushOptionsTitle, 460, 380)
    {
        _remote.ItemsSource = remotes;
        _remote.SelectedItem = defaultRemote is not null && remotes.Contains(defaultRemote) ? defaultRemote : remotes.FirstOrDefault();
        _currentBranch.Content = string.Format(GitUI.TranslatedStrings.PushScopeCurrentBranch, currentBranch);
        _force.ItemsSource = new[]
        {
            GitUI.TranslatedStrings.ForceNone,
            GitUI.TranslatedStrings.ForceWithLease,
            GitUI.TranslatedStrings.ForceFull,
        };
        _submodules.ItemsSource = new[]
        {
            GitUI.TranslatedStrings.SubmodulesNone,
            GitUI.TranslatedStrings.SubmodulesCheck,
            GitUI.TranslatedStrings.SubmodulesOnDemand,
        };
        _track.IsChecked = trackByDefault;
        _currentBranch.IsCheckedChanged += (_, _) => UpdateActionState();

        Button execute = new() { Content = GitUI.TranslatedStrings.PushExecute, IsDefault = true };
        execute.Click += (_, _) => Close(CreateOptions());

        SetOptionsContent(
            execute,
            [
                new TextBlock { Text = GitUI.TranslatedStrings.RemoteLabel },
                _remote,
                _currentBranch,
                _allBranches,
                _tags,
                new TextBlock { Text = GitUI.TranslatedStrings.RemoteBranchLabel },
                _remoteBranch,
                new TextBlock { Text = GitUI.TranslatedStrings.ForceLabel },
                _force,
                new TextBlock { Text = GitUI.TranslatedStrings.SubmodulesLabel },
                _submodules,
                _track,
            ]);

        UpdateActionState();
    }

    private PushOptions? CreateOptions()
    {
        if (_remote.SelectedItem is not string remote)
        {
            return null;
        }

        PushScope scope = _tags.IsChecked == true ? PushScope.Tags : _allBranches.IsChecked == true ? PushScope.AllBranches : PushScope.CurrentBranch;
        ForcePushOptions force = _force.SelectedIndex switch
        {
            1 => ForcePushOptions.ForceWithLease,
            2 => ForcePushOptions.Force,
            _ => ForcePushOptions.DoNotForce,
        };

        return new PushOptions(
            remote,
            scope == PushScope.CurrentBranch ? _remoteBranch.Text : null,
            force,
            scope != PushScope.Tags && _track.IsChecked == true,
            scope == PushScope.Tags ? 0 : Math.Max(_submodules.SelectedIndex, 0),
            scope);
    }

    private void UpdateActionState()
    {
        _remoteBranch.IsEnabled = _currentBranch.IsChecked == true;
    }
}
