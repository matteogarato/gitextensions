using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

using ResourceManager;

namespace GitUI.Editor;

// TODO(avalonia-port): milestone M1.3 — a read-only patch viewer on AvaloniaEdit.
// The WinForms FileViewer's view modes (blob, blame, ranges), encodings, syntax highlighting,
// and margins arrive in later milestones.
public partial class FileViewer : GitExtensionsControl
{
    public FileViewer()
    {
        InitializeComponent();

        TextEditor.TextArea.TextView.LineTransformers.Add(new DiffLineColorizer());
        TextEditor.TextArea.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);

        InitializeComplete();
    }

    /// <summary>
    ///  Occurs when the selected text changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    ///  Gets the displayed text.
    /// </summary>
    public string Text => TextEditor.Document?.Text ?? string.Empty;

    /// <summary>
    ///  Gets the offset of the current selection.
    /// </summary>
    public int SelectionPosition => TextEditor.TextArea.Selection.SurroundingSegment.Offset;

    /// <summary>
    ///  Gets the length of the current selection.
    /// </summary>
    public int SelectionLength => TextEditor.TextArea.Selection.Length;

    /// <summary>
    ///  Shows a unified diff (patch) text.
    /// </summary>
    public void ViewPatch(string? text)
    {
        TextEditor.Document ??= new TextDocument();
        TextEditor.Document.Text = text ?? string.Empty;
        TextEditor.Select(0, 0);
        TextEditor.ScrollToHome();
    }

    /// <summary>
    ///  Colors added/removed/section lines of a unified diff, approximating the WinForms
    ///  diff highlight service until the full highlighting port lands.
    /// </summary>
    private sealed class DiffLineColorizer : DocumentColorizingTransformer
    {
        private static readonly IBrush _addedBrush = new SolidColorBrush(Colors.SeaGreen).ToImmutable();
        private static readonly IBrush _removedBrush = new SolidColorBrush(Colors.IndianRed).ToImmutable();
        private static readonly IBrush _sectionBrush = new SolidColorBrush(Colors.SteelBlue).ToImmutable();

        protected override void ColorizeLine(DocumentLine line)
        {
            string text = CurrentContext.Document.GetText(line.Offset, Math.Min(line.Length, 4));
            IBrush? brush = text switch
            {
                _ when text.StartsWith("+++") || text.StartsWith("---") => _sectionBrush,
                _ when text.StartsWith("@@") => _sectionBrush,
                _ when text.StartsWith('+') => _addedBrush,
                _ when text.StartsWith('-') => _removedBrush,
                _ => null,
            };

            if (brush is not null)
            {
                ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetForegroundBrush(brush));
            }
        }
    }
}
