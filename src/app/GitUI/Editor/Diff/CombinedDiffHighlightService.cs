using GitExtensions.Extensibility.Git;

namespace GitUI.Editor.Diff;

public class CombinedDiffHighlightService : DiffHighlightService
{
    private static readonly string[] _diffFullPrefixes = ["  ", "++", "+ ", " +", "--", "- ", " -"];

    public CombinedDiffHighlightService(ref string text, bool useGitColoring,
        DiffViewerLineNumberControl lineNumbersControl)
        : base(ref text, useGitColoring)
    {
        DiffLinesInfo = DiffLineNumAnalyzer.Analyze(text, TextMarkers, true);
        lineNumbersControl.DisplayLineNum(DiffLinesInfo, true);
        SetHighlighting(text);
    }

    public static IGitCommandConfiguration GetGitCommandConfiguration(IGitModule module, bool useGitColoring)
    {
        return GetGitCommandConfiguration(module, useGitColoring, "diff-tree");
    }

    public override string[] GetFullDiffPrefixes()
    {
        return _diffFullPrefixes;
    }
}
