using GitCommands.Settings;
using GitExtensions.Extensibility.Git;

namespace GitUI.Editor.Diff;

/// <summary>
///     Highlight for "patches", normal diff files.
/// </summary>
public class PatchHighlightService : DiffHighlightService
{
    // Patterns to check for patches in diff files
    private static readonly string[] _diffFullPrefixes = [" ", "+", "-"];

    public PatchHighlightService(ref string text, bool useGitColoring, DiffViewerLineNumberControl lineNumbersControl)
        : base(ref text, useGitColoring)
    {
        var isGitWordDiff =
            UseGitColoring && AppSettings.DiffDisplayAppearance.Value == DiffDisplayAppearance.GitWordDiff;
        DiffLinesInfo = DiffLineNumAnalyzer.Analyze(text, TextMarkers, false, isGitWordDiff);
        lineNumbersControl.DisplayLineNum(DiffLinesInfo, true);
        SetHighlighting(text);
    }

    public static IGitCommandConfiguration GetGitCommandConfiguration(IGitModule module, bool useGitColoring)
    {
        return GetGitCommandConfiguration(module, useGitColoring, "diff");
    }

    public override string[] GetFullDiffPrefixes()
    {
        return _diffFullPrefixes;
    }
}
