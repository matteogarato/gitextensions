using ICSharpCode.TextEditor.Document;

namespace GitUI.Editor.Diff;

public struct Segment : ISegment
{
    int ISegment.Offset { get; set; }
    int ISegment.Length { get; set; }
}
