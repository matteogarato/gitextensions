using GitExtUtils.GitUI;
using ICSharpCode.TextEditor.Gui;

namespace GitUI.Editor;

/// <summary>
///     This class display avatars in the gutter in a blame control.
/// </summary>
public class BlameAuthorMargin(TextArea textArea) : AbstractMargin(textArea)
{
    private static readonly int _ageBucketMarkerWidth = Convert.ToInt32(4 * DpiUtil.ScaleX);
    private readonly Color _backgroundColor = SystemColors.Window;
    private readonly Dictionary<int, SolidBrush> _brushs = [];
    private Image?[]? _avatars;
    private GitBlameEntry[]? _blameLines;
    private bool _isVisible = true;

    public int LineHeight
    {
        get => textArea.TextView.FontHeight;
    }

    public override int Width
    {
        get => LineHeight + _ageBucketMarkerWidth + DpiUtil.Scale(2);
    }

    public override bool IsVisible
    {
        get => _isVisible;
    }

    public void Initialize(IEnumerable<GitBlameEntry> blameLines)
    {
        _blameLines = [.. blameLines];
        _avatars = Array.ConvertAll(_blameLines, a => a.Avatar);

        // Update the resolution otherwise the image is not drawn at the good size :(
        foreach (var avatar in _avatars)
        {
            if (avatar is Bitmap bitmapAvatar)
            {
                bitmapAvatar.SetResolution(DpiUtil.DpiX, DpiUtil.DpiY);
            }
        }

        // Build brushes
        foreach (var blameLine in _blameLines)
        {
            if (!_brushs.ContainsKey(blameLine.AgeBucketIndex))
            {
                _brushs.Add(blameLine.AgeBucketIndex, new SolidBrush(blameLine.AgeBucketColor));
            }
        }
    }

    public void SetVisiblity(bool isVisible)
    {
        _isVisible = isVisible;
    }

    public override void Paint(Graphics g, Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || _blameLines?.Length is not > 0)
        {
            return;
        }

        g.Clear(_backgroundColor);

        if (_avatars?.Length is not > 0)
        {
            return;
        }

        var verticalOffset = textArea.VirtualTop.Y;
        var lineStart = verticalOffset / LineHeight;
        var negativeOffset = (lineStart * LineHeight) - verticalOffset;
        var lineCount = (int)Math.Ceiling((double)(rect.Height - negativeOffset) / LineHeight);

        for (var i = 0; i < lineCount; i++)
        {
            if (lineStart + i >= _avatars.Length)
            {
                break;
            }

            var y = negativeOffset + (i * LineHeight);
            g.FillRectangle(_brushs[_blameLines[lineStart + i].AgeBucketIndex], 0, y, _ageBucketMarkerWidth,
                LineHeight);

            if (_avatars[lineStart + i] is not null)
            {
                g.DrawImage(_avatars[lineStart + i]!, new Point(_ageBucketMarkerWidth, y));
            }
        }

        base.Paint(g, rect);
    }
}
