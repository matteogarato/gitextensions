using Avalonia.Media;

namespace GitUI.UserControls.RevisionGrid.Graph.Rendering;

// Twin of GitUI/UserControls/RevisionGrid/Graph/Rendering/Context.cs: the GDI Graphics is
// replaced by Avalonia's DrawingContext; the geometry stays System.Drawing like upstream.
internal readonly ref struct Context(
    RevisionGraphConfig config,
    DrawingContext drawingContext,
    Pen pen,
    int laneWidth,
    int rowHeight)
{
    public readonly RevisionGraphConfig Config = config;
    public readonly DrawingContext DrawingContext = drawingContext;
    public readonly Pen Pen = pen;
    public readonly Size CellSize = new(laneWidth, rowHeight);
}
