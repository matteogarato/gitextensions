using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI.CommandsDialogs;

internal sealed record ManualStashOptions(bool IncludeUntrackedFiles, bool KeepIndex);

/// <summary>
///  Configures the options used when creating a manual stash.
/// </summary>
public sealed class StashOptionsWindow : OptionsWindow
{
    private readonly CheckBox _includeUntrackedFiles = new()
    {
        Content = GitUI.TranslatedStrings.IncludeUntrackedFilesInStash,
    };
    private readonly CheckBox _keepIndex = new()
    {
        Content = GitUI.TranslatedStrings.KeepIndexWhenStashing,
    };

    /// <summary>
    ///  Initializes a new instance of the <see cref="StashOptionsWindow"/> class.
    /// </summary>
    /// <param name="includeUntrackedFiles">Whether untracked files are included in a manual stash.</param>
    /// <param name="keepIndex">Whether the index is kept when creating a manual stash.</param>
    public StashOptionsWindow(bool includeUntrackedFiles, bool keepIndex)
        : base(GitUI.TranslatedStrings.StashOptionsTitle, 420, 360)
    {
        _includeUntrackedFiles.IsChecked = includeUntrackedFiles;
        _keepIndex.IsChecked = keepIndex;

        Button save = new() { Content = GitUI.TranslatedStrings.SaveStashOptions, IsDefault = true };
        save.Click += (_, _) => Close(new ManualStashOptions(
            _includeUntrackedFiles.IsChecked == true,
            _keepIndex.IsChecked == true));

        SetOptionsContent(
            save,
            [
                _includeUntrackedFiles,
                _keepIndex,
            ]);
    }
}
