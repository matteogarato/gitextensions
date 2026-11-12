using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Collects the interactive, range and auto-stash options for rebasing the current branch.
/// </summary>
public sealed class RebaseOptionsWindow : OptionsWindow
{
    private readonly CheckBox _autoSquash = new() { Content = TranslatedStrings.RebaseAutosquash, IsEnabled = false };
    private readonly CheckBox _autoStash = new() { Content = TranslatedStrings.RebaseAutoStash };
    private readonly CheckBox _interactive = new() { Content = TranslatedStrings.RebaseInteractive };
    private readonly CheckBox _specificRange = new() { Content = TranslatedStrings.RebaseSpecificRange };

    private readonly TextBox _upstream =
        new() { PlaceholderText = TranslatedStrings.RebaseRangeUpstreamPrompt, IsEnabled = false };

    private readonly TextBlock _validationError = new()
    {
        Text = TranslatedStrings.RebaseRangeUpstreamRequired, IsVisible = false, TextWrapping = TextWrapping.Wrap
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="RebaseOptionsWindow" /> class.
    /// </summary>
    /// <param name="branchName">The selected branch to rebase onto.</param>
    /// <param name="autoStash">Whether auto-stash is enabled by default.</param>
    /// <param name="autoSquash">Whether autosquash is enabled by default.</param>
    public RebaseOptionsWindow(string branchName, bool autoStash, bool autoSquash)
        : base(TranslatedStrings.RebaseOptionsTitle, 440, 380)
    {
        _autoStash.IsChecked = autoStash;
        _autoSquash.IsChecked = autoSquash;
        _interactive.Click += (_, _) => _autoSquash.IsEnabled = _interactive.IsChecked == true;
        _specificRange.Click += (_, _) => _upstream.IsEnabled = _specificRange.IsChecked == true;
        _upstream.TextChanged += (_, _) => _validationError.IsVisible = false;

        Button rebase = new() { Content = TranslatedStrings.RebaseExecute, IsDefault = true };
        rebase.Click += RebaseClick;

        SetOptionsContent(
            rebase,
            [
                new TextBlock
                {
                    Text = string.Format(TranslatedStrings.RebaseOptionsBranchPrompt, branchName),
                    TextWrapping = TextWrapping.Wrap
                },
                _interactive,
                _autoSquash,
                _specificRange,
                _upstream,
                _validationError,
                _autoStash
            ]);

        return;

        void RebaseClick(object? sender, RoutedEventArgs e)
        {
            if (_specificRange.IsChecked == true && string.IsNullOrWhiteSpace(_upstream.Text))
            {
                _validationError.IsVisible = true;
                return;
            }

            var upstream = _specificRange.IsChecked == true ? _upstream.Text?.Trim() : null;
            Close(new RebaseConfiguration(
                branchName,
                _autoStash.IsChecked == true,
                _interactive.IsChecked == true,
                _autoSquash.IsChecked == true,
                upstream));
        }
    }
}
