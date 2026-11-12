using System.Collections.Frozen;

namespace GitUI.UserControls.RevisionGrid;

internal readonly struct CellStyle(
    Brush backBrush,
    Color foreColor,
    Color commitBodyForeColor,
    Font normalFont,
    Font boldFont,
    Font monospaceFont,
    FrozenDictionary<string, Color> remoteColors)
{
    public readonly Brush BackBrush = backBrush;
    public readonly Color ForeColor = foreColor;
    public readonly Color CommitBodyForeColor = commitBodyForeColor;
    public readonly Font NormalFont = normalFont;
    public readonly Font BoldFont = boldFont;
    public readonly Font MonospaceFont = monospaceFont;
    public readonly FrozenDictionary<string, Color> RemoteColors = remoteColors;
}
