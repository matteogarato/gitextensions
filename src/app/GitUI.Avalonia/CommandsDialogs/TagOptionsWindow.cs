using Avalonia.Controls;
using Avalonia.Media;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Collects the name and annotation for a lightweight or annotated tag.
/// </summary>
public sealed class TagOptionsWindow : OptionsWindow
{
    private readonly CheckBox _annotated = new() { Content = TranslatedStrings.AnnotatedTagOption };

    private readonly TextBox _message = new()
    {
        MinHeight = 90, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, IsEnabled = false
    };

    private readonly TextBox _name = new() { MinWidth = 320 };

    /// <summary>
    ///     Initializes a new instance of the <see cref="TagOptionsWindow" /> class.
    /// </summary>
    /// <param name="objectId">The commit to tag.</param>
    public TagOptionsWindow(ObjectId objectId)
        : base(TranslatedStrings.TagOptionsTitle, 460, 380)
    {
        _annotated.IsCheckedChanged += (_, _) => _message.IsEnabled = _annotated.IsChecked == true;

        Button create = new() { Content = TranslatedStrings.CreateTagMenu, IsDefault = true };
        create.Click += (_, _) => Close(new TagConfiguration(
            _name.Text?.Trim() ?? string.Empty,
            objectId,
            _annotated.IsChecked == true ? TagOperation.Annotate : TagOperation.Lightweight,
            _message.Text ?? string.Empty));

        SetOptionsContent(
            create,
            [
                new TextBlock { Text = TranslatedStrings.TagNamePrompt },
                _name,
                _annotated,
                new TextBlock { Text = TranslatedStrings.TagMessagePrompt },
                _message
            ]);
    }
}
