using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace GitUI.CommandsDialogs;

/// <summary>
///  A minimal modal dialog asking for a single line of text.
/// </summary>
public sealed class TextPromptWindow : Window
{
    private readonly TextBox _input = new() { MinWidth = 280 };
    private readonly Func<string, string>? _normaliseOnLostFocus;

    public TextPromptWindow(string title, string prompt, string initialText = "")
        : this(title, prompt, initialText, normaliseOnLostFocus: null)
    {
    }

    internal TextPromptWindow(string title, string prompt, string initialText, Func<string, string>? normaliseOnLostFocus)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _normaliseOnLostFocus = normaliseOnLostFocus;
        _input.Text = initialText;
        _input.LostFocus += (_, _) => NormaliseInput();
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                NormaliseInput();
                Close(_input.Text);
            }
        };

        Button accept = new() { Content = "OK", IsDefault = true };
        accept.Click += (_, _) =>
        {
            NormaliseInput();
            Close(_input.Text);
        };
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = prompt },
                _input,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { accept, cancel },
                },
            },
        };
    }

    private void NormaliseInput()
    {
        if (_normaliseOnLostFocus is null)
        {
            return;
        }

        int caretIndex = _input.CaretIndex;
        _input.Text = _normaliseOnLostFocus(_input.Text ?? string.Empty);
        _input.CaretIndex = Math.Min(caretIndex, _input.Text.Length);
    }
}
