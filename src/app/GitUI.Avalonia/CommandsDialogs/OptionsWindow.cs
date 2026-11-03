using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Base class of the small modal option dialogs: fixed width, content-sized height and an accept/cancel button row.
///  Derived windows close with their options object as result, or with <see langword="null"/> when cancelled.
/// </summary>
public abstract class OptionsWindow : Window
{
    private const double ContentMargin = 12;
    private const double ContentSpacing = 8;

    protected OptionsWindow(string title, double width, double minWidth = 0)
    {
        Title = title;
        Width = width;
        MinWidth = minWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    /// <summary>
    ///  Lays out <paramref name="controls"/> vertically followed by the <paramref name="accept"/> and a cancel button.
    /// </summary>
    protected void SetOptionsContent(Button accept, IEnumerable<Control> controls)
    {
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        StackPanel panel = new() { Margin = new Avalonia.Thickness(ContentMargin), Spacing = ContentSpacing };
        panel.Children.AddRange(controls);
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = ContentSpacing,
            Children = { accept, cancel },
        });

        Content = panel;
    }
}
