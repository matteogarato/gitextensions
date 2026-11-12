using GitExtensions.Extensibility;

namespace GitUI.UserControls;

/// <summary>
///     Suppresses invalid cursor movement keypresses in order to avoid the "ding" sound.
/// </summary>
/// The end positions cannot be retrieved in the KeyDown handler
/// because they are implemented using SendMessage calls.
internal sealed class TextBoxSilencer
{
    private readonly RichTextBox _textBox;

    public TextBoxSilencer(RichTextBox textBox)
    {
        _textBox = textBox;
        _textBox.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _textBox.SelectionLength > 0)
        {
            return;
        }

        var text = _textBox.Text;
        var position = _textBox.SelectionStart;

        var isAtFirstColumn = position == _textBox.GetFirstCharIndexOfCurrentLine();
        var isAtEndColumn = position == text.GetLineEnd(position);
        var isAtFirstLine = position <= text.GetLineEnd();
        var isAtLastLine = text.IndexOfAny(Delimiters.LineFeedAndCarriageReturnSearchValues, position) < 0;
        var ctrl = e.Control;

        switch (e.KeyCode)
        {
            case Keys.Up when isAtFirstLine:
            case Keys.Down when isAtLastLine:
            case Keys.Home when isAtFirstColumn && (!ctrl || isAtFirstLine):
            case Keys.End when isAtEndColumn && (!ctrl || isAtLastLine):
            case Keys.Left or Keys.PageUp when isAtFirstLine && isAtFirstColumn:
            case Keys.Right or Keys.PageDown when isAtLastLine && isAtEndColumn:
                e.Handled = true;
                break;
        }
    }
}
