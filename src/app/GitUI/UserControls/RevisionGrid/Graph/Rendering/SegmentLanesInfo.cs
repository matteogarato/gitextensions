namespace GitUI.UserControls.RevisionGrid.Graph.Rendering;

internal readonly ref struct SegmentLanesInfo(
    int startLane,
    int centerLane,
    int endLane,
    int primaryEndLane,
    bool isTheRevisionLane,
    bool drawFromStart,
    bool drawToEnd)
{
    public readonly int StartLane = startLane;
    public readonly int CenterLane = centerLane;
    public readonly int EndLane = endLane;
    public readonly int PrimaryEndLane = primaryEndLane;
    public readonly bool IsTheRevisionLane = isTheRevisionLane;
    public readonly bool DrawFromStart = drawFromStart;
    public readonly bool DrawToEnd = drawToEnd;
}
