namespace GitUI.UserControls.RevisionGrid.Graph.Rendering;

internal readonly ref struct Context(
    RevisionGraphConfig config,
    Graphics graphics,
    Pen pen,
    int laneWidth,
    int rowHeight)
{
    public readonly RevisionGraphConfig Config = config;
    public readonly Graphics Graphics = graphics;
    public readonly Pen Pen = pen;
    public readonly Size CellSize = new(laneWidth, rowHeight);
}
