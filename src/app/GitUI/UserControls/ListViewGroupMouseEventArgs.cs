namespace GitUI.UserControls;

/// <inheritdoc />
internal sealed class ListViewGroupMouseEventArgs(
    MouseButtons button,
    ListViewGroupHitInfo groupHitInfo,
    int clicks,
    int delta)
    : MouseEventArgs(button, clicks, groupHitInfo.Location.X, groupHitInfo.Location.Y, delta)
{
    public ListViewGroupHitInfo GroupInfo { get; } = groupHitInfo;
    public bool Handled { get; set; }
}
