using GitExtensions.Extensibility;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using ICSharpCode.TextEditor.Document.HighlightingStrategy;
using ICSharpCode.TextEditor.Gui;

namespace GitUI.Editor.Diff;

public class DiffViewerLineNumberControl(TextArea textArea) : AbstractMargin(textArea)
{
    private const int _textHorizontalMargin = 4;
    private static readonly IReadOnlyDictionary<int, DiffLineInfo> _empty = new Dictionary<int, DiffLineInfo>();
    private IReadOnlyDictionary<int, DiffLineInfo> _diffLines = _empty;
    private bool _showLeftColumn = true;
    private bool _visible = true;

    /// <summary>
    ///     Gets the maximum line number from either left or right version.
    /// </summary>
    public int MaxLineNumber { get; private set; }

    public override int Width
    {
        get
        {
            if (_visible && _diffLines.Count != 0)
            {
                // add a space behind each number
                var maxDigits = MaxLineNumber > 0 ? (int)Math.Log10(MaxLineNumber) + 1 : 0;
                var length = (_showLeftColumn ? 2 : 1) * (1 + maxDigits);
                return _textHorizontalMargin + (textArea.TextView.WideSpaceWidth * length);
            }

            return 0;
        }
    }

    public override bool IsVisible
    {
        get => _visible;
    }

    /// <summary>
    ///     returns the according line numbers or null if the caretLine is not mapped.
    /// </summary>
    /// <param name="caretLine">0-based (in contrast to the displayed line numbers which are 1-based).</param>
    public DiffLineInfo? GetLineInfo(int caretLine)
    {
        _diffLines.TryGetValue(caretLine + 1, out var diffLine);
        return diffLine;
    }

    public override void Paint(Graphics g, Rectangle rect)
    {
        var numbersWidth = Width - _textHorizontalMargin;
        var leftWidth = _showLeftColumn ? _textHorizontalMargin + (numbersWidth / 2) : 0;
        var rightWidth = rect.Width - leftWidth;

        var fontHeight = textArea.TextView.FontHeight;
        var lineNumberPainterColor = textArea.Document.HighlightingStrategy.GetColorFor("LineNumbers");
        var lineNumberCurrentPainterColor =
            textArea.Document.HighlightingStrategy.GetColorFor("LineNumberSelected");
        var fillBrush = textArea.Enabled
            ? BrushRegistry.GetBrush(lineNumberPainterColor.BackgroundColor)
            : SystemBrushes.InactiveBorder;
        var drawBrush = BrushRegistry.GetBrush(lineNumberPainterColor.Color);
        var currentLineBrush = BrushRegistry.GetBrush(lineNumberCurrentPainterColor.Color);

        for (var y = 0;
             y < ((DrawingPosition.Height + textArea.TextView.VisibleLineDrawingRemainder) / fontHeight) + 1;
             ++y)
        {
            var ypos = drawingPosition.Y + (fontHeight * y) - textArea.TextView.VisibleLineDrawingRemainder;
            Rectangle backgroundRectangle = new(drawingPosition.X, ypos, drawingPosition.Width, fontHeight);
            if (!rect.IntersectsWith(backgroundRectangle))
            {
                continue;
            }

            g.FillRectangle(fillBrush, backgroundRectangle);
            var curLine =
                textArea.Document.GetFirstLogicalLine(
                    textArea.Document.GetVisibleLine(textArea.TextView.FirstVisibleLine) + y);

            if (curLine >= textArea.Document.TotalNumberOfLines
                || !_diffLines.TryGetValue(curLine + 1, out var diffLine))
            {
                continue;
            }

            if (diffLine.LineType is DiffLineType.MinusPlus or DiffLineType.MinusLeft or DiffLineType.PlusRight)
            {
                if (diffLine.LineType is not DiffLineType.PlusRight)
                {
                    var leftBrush = BrushRegistry.GetBrush(AppColor.AnsiTerminalRedBackNormal.GetThemeColor());
                    g.FillRectangle(leftBrush,
                        new Rectangle(0, backgroundRectangle.Top, backgroundRectangle.Width / 2,
                            backgroundRectangle.Height));
                }

                if (diffLine.LineType is not DiffLineType.MinusLeft)
                {
                    var rightBrush = BrushRegistry.GetBrush(AppColor.AnsiTerminalGreenBackNormal.GetThemeColor());
                    g.FillRectangle(rightBrush,
                        new Rectangle(backgroundRectangle.Width / 2, backgroundRectangle.Top, rightWidth,
                            backgroundRectangle.Height));
                }
            }
            else if (diffLine.LineType != DiffLineType.Context)
            {
                var brush = diffLine.LineType switch
                {
                    DiffLineType.Plus => BrushRegistry.GetBrush(AppColor.AnsiTerminalGreenBackNormal.GetThemeColor()),
                    DiffLineType.Minus => BrushRegistry.GetBrush(AppColor.AnsiTerminalRedBackNormal.GetThemeColor()),
                    DiffLineType.Header => BrushRegistry.GetBrush(AppColor.DiffSection.GetThemeColor()),
                    DiffLineType.Grep => BrushRegistry.GetBrush(AppColor.AnsiTerminalRedBackNormal.GetThemeColor()),
                    _ => default
                };

                DebugHelpers.Assert(brush is not null,
                    string.Format("brush is not null, unknow diff line style {0}", diffLine.LineType));
                g.FillRectangle(brush,
                    new Rectangle(0, backgroundRectangle.Top, leftWidth, backgroundRectangle.Height));
                g.FillRectangle(brush,
                    new Rectangle(leftWidth, backgroundRectangle.Top, rightWidth, backgroundRectangle.Height));
            }

            var isCurrentLine = curLine == textArea.Caret.Line && MarkSelectedLine;
            var lineBrush = isCurrentLine ? currentLineBrush : drawBrush;
            var font = isCurrentLine
                ? lineNumberCurrentPainterColor.GetFont(TextEditorProperties.FontContainer)
                : lineNumberPainterColor.GetFont(TextEditorProperties.FontContainer);
            if (diffLine.LeftLineNumber != DiffLineInfo.NotApplicableLineNum)
            {
                g.DrawString(diffLine.LeftLineNumber.ToString(),
                    font,
                    lineBrush,
                    new Point(_textHorizontalMargin, backgroundRectangle.Top));
            }

            if (diffLine.RightLineNumber != DiffLineInfo.NotApplicableLineNum)
            {
                g.DrawString(diffLine.RightLineNumber.ToString(),
                    font,
                    lineBrush,
                    new Point(leftWidth, backgroundRectangle.Top));
            }
        }
    }

    public void DisplayLineNum(DiffLinesInfo result, bool showLeftColumn)
    {
        _diffLines = result.DiffLines;
        MaxLineNumber = result.MaxLineNumber;
        _showLeftColumn = showLeftColumn;
    }

    public void Clear()
    {
        _diffLines = _empty;
        MaxLineNumber = 0;
    }

    public void SetVisibility(bool visible)
    {
        _visible = visible;
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(DiffViewerLineNumberControl diffViewerLineNumberControl)
    {
        public DiffLinesInfo Result
        {
            get
            {
                DiffLinesInfo diffLinesInfo = new();
                foreach (var kvp in diffViewerLineNumberControl._diffLines)
                {
                    diffLinesInfo.Add(kvp.Value);
                }

                return diffLinesInfo;
            }
        }
    }
}
