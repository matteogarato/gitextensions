namespace GitUI.UserControls;

public sealed class TextEventArgs(string text) : EventArgs
{
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));
}
