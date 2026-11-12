using Microsoft.WindowsAPICodePack.Taskbar;

namespace GitUI;

public sealed class WindowsThumbnailToolbarButton(
    string text,
    Image image,
    EventHandler<ThumbnailButtonClickedEventArgs> click)
{
    public EventHandler<ThumbnailButtonClickedEventArgs> Click { get; } = click;
    public Image Image { get; } = image;
    public string Text { get; } = text;
}
