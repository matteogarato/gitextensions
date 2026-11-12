namespace GitUI.UserControls.RevisionGrid.Graph.Rendering;

internal readonly struct DiagonalSegmentInfo(
    bool drawFromStart,
    bool drawToEnd,
    bool drawCenterToStartPerpendicularly,
    bool drawCenter,
    bool drawCenterPerpendicularly,
    bool drawCenterToEndPerpendicularly,
    bool isTheRevisionLane,
    int horizontalOffset)
{
    public readonly bool DrawFromStart = drawFromStart;
    public readonly bool DrawToEnd = drawToEnd;
    public readonly bool DrawCenterToStartPerpendicularly = drawCenterToStartPerpendicularly;
    public readonly bool DrawCenter = drawCenter;
    public readonly bool DrawCenterPerpendicularly = drawCenterPerpendicularly;
    public readonly bool DrawCenterToEndPerpendicularly = drawCenterToEndPerpendicularly;
    public readonly bool IsTheRevisionLane = isTheRevisionLane;
    public readonly int HorizontalOffset = horizontalOffset;
}
