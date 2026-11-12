namespace GitUI;

public sealed class WindowsThumbnailToolbarButtons(
    WindowsThumbnailToolbarButton commit,
    WindowsThumbnailToolbarButton pull,
    WindowsThumbnailToolbarButton push,
    WindowsThumbnailToolbarButton closeAll)
{
    public WindowsThumbnailToolbarButton CloseAll { get; } = closeAll;
    public WindowsThumbnailToolbarButton Commit { get; } = commit;
    public WindowsThumbnailToolbarButton Pull { get; } = pull;
    public WindowsThumbnailToolbarButton Push { get; } = push;
}
