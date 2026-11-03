using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Selects the supported options for merging a branch.
/// </summary>
public sealed class MergeOptionsWindow : OptionsWindow
{
    private readonly CheckBox _allowFastForward = new() { Content = GitUI.TranslatedStrings.MergeAllowFastForward, IsChecked = MergeOptions.Default.AllowFastForward };
    private readonly CheckBox _squash = new() { Content = GitUI.TranslatedStrings.MergeSquash };
    private readonly CheckBox _noCommit = new() { Content = GitUI.TranslatedStrings.MergeNoCommit };
    private readonly CheckBox _allowUnrelatedHistories = new() { Content = GitUI.TranslatedStrings.MergeAllowUnrelatedHistories };
    private readonly CheckBox _includeLog = new() { Content = GitUI.TranslatedStrings.MergeIncludeLog };
    private readonly Button _merge = new() { Content = GitUI.TranslatedStrings.MergeExecute };
    private readonly ComboBox _strategy = new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
    private readonly TextBox _message = new() { AcceptsReturn = true, Height = 70, PlaceholderText = GitUI.TranslatedStrings.MergeMessageHint };

    /// <summary>
    ///  Initializes a new instance of the <see cref="MergeOptionsWindow"/> class.
    /// </summary>
    /// <param name="branchName">The branch that will be merged into the current branch.</param>
    public MergeOptionsWindow(string branchName)
        : base(GitUI.TranslatedStrings.MergeOptionsTitle, 440, 380)
    {
        _strategy.ItemsSource = MergeWorkflow.Strategies.Select(strategy => strategy.Length == 0 ? GitUI.TranslatedStrings.MergeStrategyDefault : strategy).ToArray();
        _squash.IsCheckedChanged += (_, _) => UpdateActionState();
        _merge.Click += (_, _) => Close(new MergeOptions(
            AllowFastForward: _allowFastForward.IsChecked == true,
            Squash: _squash.IsChecked == true,
            NoCommit: _noCommit.IsChecked == true,
            AllowUnrelatedHistories: _allowUnrelatedHistories.IsChecked == true,
            Strategy: MergeWorkflow.Strategies[Math.Max(_strategy.SelectedIndex, 0)],
            Message: _message.Text,
            IncludeLog: _includeLog.IsChecked == true));

        SetOptionsContent(
            _merge,
            [
                new TextBlock { Text = string.Format(GitUI.TranslatedStrings.MergeOptionsBranchPrompt, branchName), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                _allowFastForward,
                _squash,
                _noCommit,
                _allowUnrelatedHistories,
                _includeLog,
                new TextBlock { Text = GitUI.TranslatedStrings.MergeStrategyLabel },
                _strategy,
                new TextBlock { Text = GitUI.TranslatedStrings.MergeMessageLabel },
                _message,
            ]);

        UpdateActionState();
    }

    private void UpdateActionState()
    {
        _allowFastForward.IsEnabled = _squash.IsChecked != true;
        if (_squash.IsChecked == true)
        {
            _allowFastForward.IsChecked = false;
        }
    }
}
