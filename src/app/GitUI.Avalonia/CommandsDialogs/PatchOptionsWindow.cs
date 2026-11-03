using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Selects the whitespace and sign-off options for applying a patch.
/// </summary>
public sealed class PatchOptionsWindow : OptionsWindow
{
    private readonly CheckBox _ignoreWhitespace = new() { Content = GitUI.TranslatedStrings.PatchIgnoreWhitespace };
    private readonly CheckBox _signOff = new() { Content = GitUI.TranslatedStrings.SignOffCommitOption };

    /// <summary>
    ///  Initializes a new instance of the <see cref="PatchOptionsWindow"/> class.
    /// </summary>
    /// <param name="allowSignOff">Whether sign-off is available, which is only the case for mailbox patches.</param>
    public PatchOptionsWindow(bool allowSignOff)
        : base(GitUI.TranslatedStrings.PatchOptionsTitle, 380)
    {
        _signOff.IsEnabled = allowSignOff;

        Button apply = new() { Content = GitUI.TranslatedStrings.PatchApplyButton, IsDefault = true };
        apply.Click += (_, _) => Close(new PatchApplyOptions(_ignoreWhitespace.IsChecked == true, _signOff.IsEnabled && _signOff.IsChecked == true));

        SetOptionsContent(
            apply,
            [
                _ignoreWhitespace,
                _signOff,
            ]);
    }
}
