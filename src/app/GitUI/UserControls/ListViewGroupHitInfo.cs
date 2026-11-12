namespace GitUI.UserControls;

public class ListViewGroupHitInfo(ListViewGroup group, bool isCollapseButton, Point location)
{
    public ListViewGroup Group { get; } = group;
    public bool IsCollapseButton { get; } = isCollapseButton;
    public Point Location { get; } = location;
}
