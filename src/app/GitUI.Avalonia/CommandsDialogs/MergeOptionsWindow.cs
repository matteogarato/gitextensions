using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Selects the supported options for merging a branch.
/// </summary>
public sealed class MergeOptionsWindow : OptionsWindow
{
    private readonly CheckBox _allowFastForward = new()
    {
        Content = TranslatedStrings.MergeAllowFastForward, IsChecked = MergeOptions.Default.AllowFastForward
    };

    private readonly CheckBox _allowUnrelatedHistories =
        new() { Content = TranslatedStrings.MergeAllowUnrelatedHistories };

    private readonly CheckBox _includeLog = new() { Content = TranslatedStrings.MergeIncludeLog };
    private readonly Button _merge = new() { Content = TranslatedStrings.MergeExecute };

    private readonly TextBox _message = new()
    {
        AcceptsReturn = true, Height = 70, PlaceholderText = TranslatedStrings.MergeMessageHint
    };

    private readonly CheckBox _noCommit = new() { Content = TranslatedStrings.MergeNoCommit };
    private readonly CheckBox _squash = new() { Content = TranslatedStrings.MergeSquash };

    private readonly ComboBox
        _strategy = new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };

    /// <summary>
    ///     Initializes a new instance of the <see cref="MergeOptionsWindow" /> class.
    /// </summary>
    /// <param name="branchName">The branch that will be merged into the current branch.</param>
    public MergeOptionsWindow(string branchName)
        : base(TranslatedStrings.MergeOptionsTitle, 440, 380)
    {
        _strategy.ItemsSource = MergeWorkflow.Strategies
            .Select(strategy => strategy.Length == 0 ? TranslatedStrings.MergeStrategyDefault : strategy).ToArray();
        _squash.IsCheckedChanged += (_, _) => UpdateActionState();
        _merge.Click += (_, _) => Close(new MergeOptions(
            _allowFastForward.IsChecked == true,
            _squash.IsChecked == true,
            _noCommit.IsChecked == true,
            _allowUnrelatedHistories.IsChecked == true,
            MergeWorkflow.Strategies[Math.Max(_strategy.SelectedIndex, 0)],
            _message.Text,
            _includeLog.IsChecked == true));

        SetOptionsContent(
            _merge,
            [
                new TextBlock
                {
                    Text = string.Format(TranslatedStrings.MergeOptionsBranchPrompt, branchName),
                    TextWrapping = TextWrapping.Wrap
                },
                _allowFastForward,
                _squash,
                _noCommit,
                _allowUnrelatedHistories,
                _includeLog,
                new TextBlock { Text = TranslatedStrings.MergeStrategyLabel },
                _strategy,
                new TextBlock { Text = TranslatedStrings.MergeMessageLabel },
                _message
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
