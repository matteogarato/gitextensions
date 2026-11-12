namespace GitExtUtils.GitUI.Theming;

public class BrushScope : IDisposable
{
    private readonly bool _isSystemBrush;

    private BrushScope(Brush brush, bool isSystemBrush)
    {
        Brush = brush;
        _isSystemBrush = isSystemBrush;
    }

    public Brush Brush { get; }

    void IDisposable.Dispose()
    {
        if (!_isSystemBrush)
        {
            Brush.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public static BrushScope ForSystemBrush(Brush brush)
    {
        return new BrushScope(brush, true);
    }

    public static BrushScope ForRegularBrush(Brush brush)
    {
        return new BrushScope(brush, false);
    }
}
