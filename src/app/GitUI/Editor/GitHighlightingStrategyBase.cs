using GitExtensions.Extensibility.Git;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Document.HighlightingStrategy;
using ICSharpCode.TextEditor.Document.LineManager;

namespace GitUI.Editor;

internal abstract class GitHighlightingStrategyBase : IHighlightingStrategy
{
    private readonly char _commentChar;

    private readonly DefaultHighlightingStrategy _defaultHighlightingStrategy =
        HighlightingManager.Manager.DefaultHighlighting;

    protected GitHighlightingStrategyBase(string name, IGitModule module)
    {
        Name = name;

        // By default, comments start with '#'.
        //
        // This can be overridden via the "core.commentchar" configuration setting.
        //
        // However, if "core.commentchar" is "auto", then git attempts to choose a
        // character from "#;@!$%^&|:" which is not present in the message.
        // In such cases it does not appear that the character is provided to the
        // editor. The only way to determine the character is to inspect the message,
        // potentially for a regex resembling "with '(.)' will be ignored", though
        // this likely changes with locale.
        //
        // An alternative approach would be to tally counts for the known set of
        // characters for each line[0] and take the character with most.
        // That would work well in practice.

        const string defaultValue = "#";
        var commentCharSetting = module.GetEffectiveSetting("core.commentchar", defaultValue);
        _commentChar = commentCharSetting.Length == 1 ? commentCharSetting[0] : defaultValue[0];
    }

    protected static HighlightColor ColorNormal { get; } = new(nameof(SystemColors.WindowText), false, false);

    private static HighlightColor ColorComment { get; } = new(Color.DarkGreen, false, false);

    protected abstract void MarkTokens(IDocument document, IList<LineSegment> lines);

    protected bool TryHighlightComment(IDocument document, LineSegment line)
    {
        if (IsComment(document, line))
        {
            line.Words = [new TextWord(document, line, 0, line.Length, ColorComment, false)];
            return true;
        }

        return false;
    }

    #region IHighlightingStrategy

    string IHighlightingStrategy.Name { get; }

    string[] IHighlightingStrategy.Extensions
    {
        get => [];
    }

    Dictionary<string, string> IHighlightingStrategy.Properties { get; } = [];

    HighlightColor IHighlightingStrategy.GetColorFor(string name)
    {
        return _defaultHighlightingStrategy.GetColorFor(name);
    }

    void IHighlightingStrategy.MarkTokens(IDocument document)
    {
        MarkTokens(document, document.LineSegmentCollection);
    }

    void IHighlightingStrategy.MarkTokens(IDocument document, List<LineSegment> lines)
    {
        MarkTokens(document, lines);
    }

    #endregion

    #region Line classifiers

    protected bool IsComment(IDocument document, LineSegment line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            var c = document.GetCharAt(line.Offset + i);

            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return c == _commentChar;
        }

        return false;
    }

    protected static bool IsEmptyOrWhiteSpace(IDocument document, LineSegment line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            var c = document.GetCharAt(line.Offset + i);

            if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}
