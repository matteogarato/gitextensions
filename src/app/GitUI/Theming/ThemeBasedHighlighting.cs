using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Document.HighlightingStrategy;
using ICSharpCode.TextEditor.Document.LineManager;

namespace GitUI.Theming;

public class ThemeBasedHighlighting(IHighlightingStrategy original) : IHighlightingStrategyUsingRuleSets
{
    HighlightColor? IHighlightingStrategy.GetColorFor(string name)
    {
        return original.GetColorFor(name)?.Transform();
    }

    string IHighlightingStrategy.Name
    {
        get => original.Name;
    }

    string[] IHighlightingStrategy.Extensions
    {
        get => original.Extensions;
    }

    Dictionary<string, string> IHighlightingStrategy.Properties
    {
        get => original.Properties;
    }

    void IHighlightingStrategy.MarkTokens(IDocument document)
    {
        original.MarkTokens(document);
        foreach (var line in document.LineSegmentCollection)
        {
            foreach (var word in line.Words)
            {
                if (word.SyntaxColor is not null)
                {
                    word.SyntaxColor = word.SyntaxColor.Transform();
                }
            }
        }
    }

    void IHighlightingStrategy.MarkTokens(IDocument document, List<LineSegment> lines)
    {
        original.MarkTokens(document, lines);
        foreach (var line in lines)
        {
            foreach (var word in line.Words)
            {
                if (word.SyntaxColor is not null)
                {
                    word.SyntaxColor = word.SyntaxColor.Transform();
                }
            }
        }
    }

    HighlightRuleSet IHighlightingStrategyUsingRuleSets.GetRuleSet(Span span)
    {
        return (original as IHighlightingStrategyUsingRuleSets ??
                throw new NotSupportedException())
            .GetRuleSet(span);
    }

    HighlightColor IHighlightingStrategyUsingRuleSets.GetColor(IDocument document, LineSegment keyWord, int index,
        int length)
    {
        return (original as IHighlightingStrategyUsingRuleSets ??
                throw new NotSupportedException())
            .GetColor(document, keyWord, index, length);
    }
}
