using GitExtUtils.GitUI;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.UserControls.RevisionGrid.Columns;

internal readonly struct MultilineIndicator
{
    private static readonly Brush _indicatorForeBrush = new SolidBrush(Color.FromArgb(128, SystemColors.WindowText));
    private static readonly Brush _indicatorBackBrush = new SolidBrush(Color.FromArgb(32, SystemColors.WindowText));

    private const int _dotCount = 3;

    private static readonly int _paddingX = DpiUtil.Scale(4);
    private static readonly int _paddingTop = DpiUtil.Scale(5);
    private static readonly int _paddingBottom = DpiUtil.Scale(4);
    private static readonly int _marginX = DpiUtil.Scale(4);
    private static readonly int _dotSize = DpiUtil.Scale(2);
    private static readonly int _dotSpacing = DpiUtil.Scale(2);

    private readonly DataGridViewCellPaintingEventArgs _e;
    private readonly int _indicatorReservedWidth;
    private readonly int _indicatorRectHeight;
    private readonly int _indicatorRectWidth;
    private readonly bool _isMultiline;

    public Rectangle RemainingCellBounds
    {
        get => _isMultiline ? _e.CellBounds.ReduceRight(_indicatorReservedWidth) : _e.CellBounds;
    }

    public MultilineIndicator(DataGridViewCellPaintingEventArgs e, GitRevision revision)
    {
        _e = e;

        _isMultiline = revision.HasMultiLineMessage;

        if (_isMultiline)
        {
            _indicatorRectWidth = _paddingX + _paddingX + (_dotCount * (_dotSize + _dotSpacing)) - _dotSpacing;
            _indicatorReservedWidth = _indicatorRectWidth + _marginX + _marginX;
            _indicatorRectHeight = _dotSize + _paddingTop + _paddingBottom;

            if (e.CellBounds.Width < 2 * _indicatorReservedWidth)
            {
                _isMultiline = false;
            }
        }
        else
        {
            _indicatorRectWidth = 0;
            _indicatorRectHeight = 0;
            _indicatorReservedWidth = 0;
        }
    }

    public void Render()
    {
        if (!_isMultiline)
        {
            return;
        }

        Rectangle indicatorRect = new(
            _e.CellBounds.Right - _marginX - _indicatorRectWidth,
            _e.CellBounds.Y + ((_e.CellBounds.Height - _indicatorRectHeight) / 2),
            _indicatorRectWidth,
            _indicatorRectHeight);

        _e.Graphics!.FillRectangle(_indicatorBackBrush, indicatorRect);

        var x = indicatorRect.X + _paddingX;
        var y = indicatorRect.Y + _paddingTop;

        for (var i = 0; i < _dotCount; i++)
        {
            _e.Graphics.FillRectangle(_indicatorForeBrush, x, y, _dotSize, _dotSize);
            x += _dotSize + _dotSpacing;
        }
    }
}
