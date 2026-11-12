using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Selects the remote, branch and options for a push.
/// </summary>
public sealed class PushOptionsWindow : OptionsWindow
{
    private readonly RadioButton _allBranches =
        new() { Content = TranslatedStrings.PushScopeAllBranches, GroupName = nameof(PushScope) };

    private readonly RadioButton _currentBranch = new() { GroupName = nameof(PushScope), IsChecked = true };
    private readonly ComboBox _force = new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
    private readonly ComboBox _remote = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _remoteBranch = new() { PlaceholderText = TranslatedStrings.RemoteBranchHint };

    private readonly ComboBox _submodules =
        new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };

    private readonly RadioButton _tags = new()
    {
        Content = TranslatedStrings.PushScopeTags, GroupName = nameof(PushScope)
    };

    private readonly CheckBox _track = new() { Content = TranslatedStrings.PushTrackUpstream };

    /// <summary>
    ///     Initializes a new instance of the <see cref="PushOptionsWindow" /> class.
    /// </summary>
    /// <param name="remotes">The configured remote names.</param>
    /// <param name="defaultRemote">The remote selected initially.</param>
    /// <param name="currentBranch">The branch that is pushed in the current-branch scope.</param>
    /// <param name="trackByDefault">Whether the branch has no upstream yet.</param>
    public PushOptionsWindow(IReadOnlyList<string> remotes, string? defaultRemote, string currentBranch,
        bool trackByDefault)
        : base(TranslatedStrings.PushOptionsTitle, 460, 380)
    {
        _remote.ItemsSource = remotes;
        _remote.SelectedItem = defaultRemote is not null && remotes.Contains(defaultRemote)
            ? defaultRemote
            : remotes.FirstOrDefault();
        _currentBranch.Content = string.Format(TranslatedStrings.PushScopeCurrentBranch, currentBranch);
        _force.ItemsSource = new[]
        {
            TranslatedStrings.ForceNone, TranslatedStrings.ForceWithLease, TranslatedStrings.ForceFull
        };
        _submodules.ItemsSource = new[]
        {
            TranslatedStrings.SubmodulesNone, TranslatedStrings.SubmodulesCheck,
            TranslatedStrings.SubmodulesOnDemand
        };
        _track.IsChecked = trackByDefault;
        _currentBranch.IsCheckedChanged += (_, _) => UpdateActionState();

        Button execute = new() { Content = TranslatedStrings.PushExecute, IsDefault = true };
        execute.Click += (_, _) => Close(CreateOptions());

        SetOptionsContent(
            execute,
            [
                new TextBlock { Text = TranslatedStrings.RemoteLabel },
                _remote,
                _currentBranch,
                _allBranches,
                _tags,
                new TextBlock { Text = TranslatedStrings.RemoteBranchLabel },
                _remoteBranch,
                new TextBlock { Text = TranslatedStrings.ForceLabel },
                _force,
                new TextBlock { Text = TranslatedStrings.SubmodulesLabel },
                _submodules,
                _track
            ]);

        UpdateActionState();
    }

    private PushOptions? CreateOptions()
    {
        if (_remote.SelectedItem is not string remote)
        {
            return null;
        }

        var scope = _tags.IsChecked == true ? PushScope.Tags :
            _allBranches.IsChecked == true ? PushScope.AllBranches : PushScope.CurrentBranch;
        var force = _force.SelectedIndex switch
        {
            1 => ForcePushOptions.ForceWithLease,
            2 => ForcePushOptions.Force,
            _ => ForcePushOptions.DoNotForce
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
