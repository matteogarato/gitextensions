using System.Text;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Document.MarkerStrategy;

namespace GitUI.Editor.Diff;

/// <summary>
///     Common class for highlighting of diff style files.
/// </summary>
public abstract class DiffHighlightService : TextHighlightService
{
    private static readonly Color _addedBackColor = AppColor.AnsiTerminalGreenBackNormal.GetThemeColor();
    private static readonly Color _addedForeColor = AppColor.AnsiTerminalGreenForeBold.GetThemeColor();
    private static readonly Color _removedBackColor = AppColor.AnsiTerminalRedBackNormal.GetThemeColor();
    private static readonly Color _removedForeColor = AppColor.AnsiTerminalRedForeBold.GetThemeColor();
    protected readonly List<TextMarker> TextMarkers = [];

    protected readonly bool UseGitColoring;
    protected DiffLinesInfo DiffLinesInfo = null!;

    public DiffHighlightService(ref string text, bool useGitColoring)
    {
        UseGitColoring = useGitColoring;
        SetText(ref text);
    }

    public static IGitCommandConfiguration GetGitCommandConfiguration(IGitModule module, bool useGitColoring,
        string command)
    {
        if (!useGitColoring)
        {
            // Use default
            return null!;
        }

        GitCommandConfiguration commandConfiguration = new();
        var items = GitCommandConfiguration.Default.Get(command);
        foreach (var cfg in items)
        {
            commandConfiguration.Add(cfg, command);
        }

        // https://git-scm.com/docs/git-diff#Documentation/git-diff.txt---color-moved-wsltmodesgt
        // Disable by default, document that this can be enabled.
        SetIfUnsetInGit("diff.colormovedws", "no");

        // https://git-scm.com/docs/git-diff#Documentation/git-diff.txt-diffwordRegex
        // Set to "minimal" diff unless configured.
        SetIfUnsetInGit("diff.wordregex", "\"[a-z0-9_]+|.\"");

        // dimmed-zebra highlights borders better than the default "zebra"
        SetIfUnsetInGit("diff.colormoved", "dimmed-zebra");

        // Use reverse color to follow GE theme
        var reverse = AppSettings.ReverseGitColoring.Value ? "reverse" : "";

        SetIfUnsetInGit("color.diff.old", $"red {reverse}");
        SetIfUnsetInGit("color.diff.new", $"green {reverse}");

        if (AppSettings.ReverseGitColoring.Value)
        {
            // Fix: Force black foreground to avoid that foreground is calculated to white
            GitVersion supportsBrightColors = new("2.26.0.0");
            if (module.GitVersion >= supportsBrightColors)
            {
                SetIfUnsetInGit("color.diff.oldmoved", "black brightmagenta");
                SetIfUnsetInGit("color.diff.newmoved", "black brightblue");
                SetIfUnsetInGit("color.diff.oldmovedalternative", "black brightcyan");
                SetIfUnsetInGit("color.diff.newmovedalternative", "black brightyellow");
            }
            else
            {
                SetIfUnsetInGit("color.diff.oldmoved", "reverse bold magenta");
                SetIfUnsetInGit("color.diff.newmoved", "reverse bold blue");
                SetIfUnsetInGit("color.diff.oldmovedalternative", "reverse bold cyan");
                SetIfUnsetInGit("color.diff.newmovedalternative", "reverse bold yellow");
            }
        }

        // Set dimmed colors, default is gray dimmed/italic
        SetIfUnsetInGit("color.diff.oldmoveddimmed", $"magenta dim {reverse}");
        SetIfUnsetInGit("color.diff.newmoveddimmed", $"blue dim {reverse}");
        SetIfUnsetInGit("color.diff.oldmovedalternativedimmed", $"cyan dim {reverse}");
        SetIfUnsetInGit("color.diff.newmovedalternativedimmed", $"yellow dim {reverse}");

        // range-diff
        if (command == "range-diff")
        {
            // No override for contextBold, contextDimmed
            SetIfUnsetInGit("color.diff.oldbold", $"brightred {reverse}");
            SetIfUnsetInGit("color.diff.newbold", $"brightgreen {reverse}");
            SetIfUnsetInGit("color.diff.olddimmed", $"red dim {reverse}");
            SetIfUnsetInGit("color.diff.newdimmed", $"green dim {reverse}");
        }

        return commandConfiguration;

        void SetIfUnsetInGit(string key, string value)
        {
            // Note: Only check Windows, not WSL settings
            if (string.IsNullOrEmpty(module.GetEffectiveSetting(key)))
            {
                commandConfiguration.Add(new GitConfigItem(key, value), command);
            }
        }
    }

    public override void AddTextHighlighting(IDocument document)
    {
        document.MarkerStrategy.AddMarkers(TextMarkers);
    }

    public override bool IsSearchMatch(DiffViewerLineNumberControl lineNumbersControl, int indexInText)
    {
        return lineNumbersControl.GetLineInfo(indexInText)?.LineType is (DiffLineType.Minus or DiffLineType.Plus
            or DiffLineType.MinusPlus or DiffLineType.Grep);
    }

    public abstract string[] GetFullDiffPrefixes();

    private void SetText(ref string text)
    {
        if (!UseGitColoring)
        {
            return;
        }

        StringBuilder sb = new(text.Length);
        AnsiEscapeUtilities.ParseEscape(text, sb, TextMarkers);

        text = sb.ToString();
    }

    /// <summary>
    ///     Set highlighting for <paramref name="text" />.
    ///     The parsed added/removed lines in <see cref="DiffLinesInfo" /> is used as well as
    ///     the highlighting in <see cref="TextMarkers" /> (if Git highlighting <see cref="UseGitColoring" />),
    ///     is used to mark inline differences (dim unchanged part of lines).
    /// </summary>
    /// <param name="text">The text to process.</param>
    internal void SetHighlighting(string text)
    {
        // Apply GE word highlighting for Patch display (may apply to Difftastic setting, if not available for a repo)
        if (!UseGitColoring || AppSettings.DiffDisplayAppearance.Value != DiffDisplayAppearance.GitWordDiff)
        {
            var markers = UseGitColoring ? [] : TextMarkers;
            AddInlineDifferenceMarkers(markers, text);
            if (UseGitColoring)
            {
                // The in-line diffs must be inserted before the diff to override the markings (the original markers are not changed).
                TextMarkers.InsertRange(0, markers);
            }
        }

        if (!UseGitColoring)
        {
            HighlightAddedAndDeletedLines(TextMarkers);
        }
    }

    /// <summary>
    ///     Highlight lines that are added, removed and header lines.
    ///     This is an alternative configuration to use the Git diff coloring (that has more features).
    /// </summary>
    /// <param name="textMarkers">The markers to append to.</param>
    private void HighlightAddedAndDeletedLines(List<TextMarker> textMarkers)
    {
        foreach (var segment in GetAllLines(DiffLineType.Minus))
        {
            textMarkers.Add(CreateTextMarker(segment.Offset, segment.Length, _removedBackColor));
        }

        foreach (var segment in GetAllLines(DiffLineType.Plus))
        {
            textMarkers.Add(CreateTextMarker(segment.Offset, segment.Length, _addedBackColor));
        }

        foreach (var segment in GetAllLines(DiffLineType.Header))
        {
            textMarkers.Add(CreateTextMarker(segment.Offset, segment.Length, AppColor.DiffSection.GetThemeColor()));
        }
    }

    /// <summary>
    ///     Matches related removed and added lines in a consecutive block of a patch document and marks identical parts
    ///     dimmed.
    /// </summary>
    private void AddInlineDifferenceMarkers(List<TextMarker> textMarkers, string text)
    {
        var index = 0;
        DiffLineInfo[] diffLines = [.. DiffLinesInfo.DiffLines.Values.OrderBy(l => l.LineNumInDiff)];
        const int
            diffContentOffset = 1; // in order to skip the prefixes '-' / '+' (this is only for normal patch format)
        var dimBackground = !UseGitColoring || AppSettings.ReverseGitColoring.Value;

        // Process the next blocks of removed / added diffLines and mark in-line differences
        while (index < diffLines.Length)
        {
            // git-diff presents the removed lines directly followed by the added in a "block"
            IReadOnlyList<ISegment> linesRemoved = GetBlockOfLines(diffLines, DiffLineType.Minus, ref index, false);
            if (linesRemoved.Count == 0)
            {
                continue;
            }

            IReadOnlyList<ISegment> linesAdded = GetBlockOfLines(diffLines, DiffLineType.Plus, ref index, true);
            if (linesAdded.Count == 0)
            {
                continue;
            }

            foreach ((var lineRemoved, var lineAdded) in LinesMatcher.FindLinePairs(GetText, linesRemoved,
                         linesAdded))
            {
                AddDifferenceMarkers(textMarkers, GetText, lineRemoved, lineAdded, diffContentOffset, dimBackground);
            }
        }

        return;

        string GetText(ISegment line)
        {
            return text[(line.Offset + diffContentOffset)..(line.Offset + line.Length)];
        }
    }

    private IEnumerable<ISegment> GetAllLines(DiffLineType diffLineType)
    {
        return DiffLinesInfo?.DiffLines.Where(i => i.Value.LineType == diffLineType && i.Value.LineSegment is not null)
                   .Select(l => l.Value.LineSegment!)
               ?? [];
    }

    /// <summary>
    ///     Get next block of diffLines following beginline
    /// </summary>
    /// <param name="diffLines">The parsed diffLines for the document.</param>
    /// <param name="diffLineType">The type of diffLines to find (e.g. added/removed).</param>
    /// <param name="index">The index in diffLines to start with.</param>
    /// <param name="found">If a lineInDiff was found. This is also used to get the added diffLines just after the removed.</param>
    /// <returns>The block of segments.</returns>
    private static List<ISegment> GetBlockOfLines(DiffLineInfo[] diffLines, DiffLineType diffLineType, ref int index,
        bool found)
    {
        List<ISegment> result = [];
        var gapLines = 0;

        for (; index < diffLines.Length; ++index)
        {
            var diffLine = diffLines[index];
            if (diffLine.LineType != diffLineType)
            {
                if (!found)
                {
                    // Start of block not found yet
                    continue;
                }

                const int maxGapLines = 5;
                if (diffLine?.LineType == DiffLineType.Context && gapLines < maxGapLines)
                {
                    // A gap context diffLines, the block can be extended
                    ++gapLines;
                    continue;
                }

                // Block ended, no more to add (next start search here)
                break;
            }

            ArgumentNullException.ThrowIfNull(diffLine.LineSegment);
            gapLines = 0;
            if (diffLine.IsMovedLine)
            {
                // Ignore this line, seem to be moved
                continue;
            }

            // In block, continue to add
            found = true;
            result.Add(diffLine.LineSegment);
        }

        return result;
    }

    internal static void AddDifferenceMarkers(List<TextMarker> markers, Func<ISegment, string> getText,
        ISegment lineRemoved, ISegment lineAdded, int beginOffset, bool dimBackground)
    {
        var textRemoved = LimitLength(getText(lineRemoved).AsSpan());
        var textAdded = LimitLength(getText(lineAdded).AsSpan());
        var offsetRemoved = lineRemoved.Offset + beginOffset;
        var offsetAdded = lineAdded.Offset + beginOffset;
        (var lengthIdenticalAtStart, var lengthIdenticalAtEnd) = AddDifferenceMarkers(markers, textRemoved, textAdded,
            offsetRemoved, offsetAdded, dimBackground);

        if (lengthIdenticalAtStart > 0)
        {
            markers.Add(CreateDimmedMarker(offsetRemoved, lengthIdenticalAtStart, true, dimBackground));
            markers.Add(CreateDimmedMarker(offsetAdded, lengthIdenticalAtStart, false, dimBackground));
        }

        if (lengthIdenticalAtEnd > 0)
        {
            markers.Add(CreateDimmedMarker(offsetRemoved + textRemoved.Length - lengthIdenticalAtEnd,
                lengthIdenticalAtEnd, true, dimBackground));
            markers.Add(CreateDimmedMarker(offsetAdded + textAdded.Length - lengthIdenticalAtEnd, lengthIdenticalAtEnd,
                false, dimBackground));
        }

        return;

        static ReadOnlySpan<char> LimitLength(ReadOnlySpan<char> text)
        {
            const int maxLength = 2000;
            return text.Length <= maxLength ? text : text[..maxLength];
        }
    }

    private static (int LengthIdenticalAtStart, int LengthIdenticalAtEnd) AddDifferenceMarkers(
        List<TextMarker> markers, ReadOnlySpan<char> textRemoved, ReadOnlySpan<char> textAdded, int offsetRemoved,
        int offsetAdded, bool dimBackground)
    {
        // removed:             added:              "d" stands for "deleted" / "i" for "inserted" -> anchor marker in added / removed
        // "d b R a "           " b A a i"          split at "b" (stands for "before")
        // 1.                   1.
        // "d ""b"" R a "       " ""b"" A a i"      split at "a" (stands for "after")
        // 5.     2.            5.    2.
        // "d ""b"" R ""a"" "   " ""b"" A ""a"" i"  join identical
        //        4.      3.          4.      3.
        // "d"" b ""R"" a """   """ b ""A"" a ""i"

        var lengthIdenticalAtStart = 0;
        var lengthIdenticalAtEnd = 0;

        var endRemoved = textRemoved.Length;
        var endAdded = textAdded.Length;
        if (endRemoved == endAdded && textRemoved == textAdded)
        {
            lengthIdenticalAtStart = endRemoved;
            return (lengthIdenticalAtStart, lengthIdenticalAtEnd);
        }

        (var commonWord, var startIndexIdenticalRemoved, var startIndexIdenticalAdded) =
            LinesMatcher.FindBestMatch(textRemoved.ToString(), textAdded.ToString());
        if (commonWord is not null)
        {
            var lengthIdentical = commonWord.Length;

            // "LeftPart|CommonWord|RightPart"
            // "LeftPart|CommonWord|identical|Different|identical"
            // "LeftPart|CommonWord+identical" ignored  ^^^^^^^^^ -> lengthIdenticalAtEnd (final value)
            var startIndexRightPartRemoved = startIndexIdenticalRemoved + lengthIdentical;
            var startIndexRightPartAdded = startIndexIdenticalAdded + lengthIdentical;
            (var lengthIdenticalAtStartRightPart, lengthIdenticalAtEnd) = AddDifferenceMarkers(markers,
                textRemoved[startIndexRightPartRemoved..], textAdded[startIndexRightPartAdded..],
                offsetRemoved + startIndexRightPartRemoved, offsetAdded + startIndexRightPartAdded, dimBackground);
            lengthIdentical += lengthIdenticalAtStartRightPart;

            ////                                                             "LeftPart|CommonWord+identical"
            ////                                        "identical|Different|identical|CommonWord+identical"
            //// lengthIdenticalAtStart (final value) <- ^^^^^^^^^  ignored "identical+CommonWord+identical"
            (lengthIdenticalAtStart, var lengthIdenticalAtLeftPartEnd) = AddDifferenceMarkers(markers,
                textRemoved[..startIndexIdenticalRemoved], textAdded[..startIndexIdenticalAdded],
                offsetRemoved, offsetAdded, dimBackground);
            lengthIdentical += lengthIdenticalAtLeftPartEnd;
            startIndexIdenticalRemoved -= lengthIdenticalAtLeftPartEnd;
            startIndexIdenticalAdded -= lengthIdenticalAtLeftPartEnd;

            // join with identical part at start or end or dim the identical part
            if (startIndexIdenticalRemoved == lengthIdenticalAtStart &&
                startIndexIdenticalAdded == lengthIdenticalAtStart)
            {
                lengthIdenticalAtStart += lengthIdentical;
            }
            else if (startIndexIdenticalRemoved + lengthIdentical + lengthIdenticalAtEnd == endRemoved
                     && startIndexIdenticalAdded + lengthIdentical + lengthIdenticalAtEnd == endAdded)
            {
                lengthIdenticalAtEnd += lengthIdentical;
            }
            else
            {
                markers.Add(CreateDimmedMarker(offsetRemoved + startIndexIdenticalRemoved, lengthIdentical, true,
                    dimBackground));
                markers.Add(CreateDimmedMarker(offsetAdded + startIndexIdenticalAdded, lengthIdentical, false,
                    dimBackground));
            }
        }
        else
        {
            // find end of identical part at start
            var minEnd = Math.Min(endRemoved, endAdded);
            while (lengthIdenticalAtStart < minEnd
                   && textRemoved[lengthIdenticalAtStart] == textAdded[lengthIdenticalAtStart])
            {
                ++lengthIdenticalAtStart;
            }

            // find start of identical part at end
            var startIndexIdenticalAtEndRemoved = endRemoved;
            var startIndexIdenticalAtEndAdded = endAdded;
            while (startIndexIdenticalAtEndRemoved > lengthIdenticalAtStart && startIndexIdenticalAtEndAdded >
                                                                            lengthIdenticalAtStart
                                                                            && textRemoved[
                                                                                startIndexIdenticalAtEndRemoved - 1] ==
                                                                            textAdded[
                                                                                startIndexIdenticalAtEndAdded - 1])
            {
                --startIndexIdenticalAtEndRemoved;
                --startIndexIdenticalAtEndAdded;
                ++lengthIdenticalAtEnd;
            }

            var lengthDifferentRemoved = startIndexIdenticalAtEndRemoved - lengthIdenticalAtStart;
            var lengthDifferentAdded = startIndexIdenticalAtEndAdded - lengthIdenticalAtStart;
            if (lengthDifferentRemoved == 0 && lengthDifferentAdded > 0)
            {
                markers.Add(CreateAnchorMarker(offsetRemoved + lengthIdenticalAtStart, _addedForeColor));
            }
            else if (lengthDifferentRemoved > 0 && lengthDifferentAdded == 0)
            {
                markers.Add(CreateAnchorMarker(offsetAdded + lengthIdenticalAtStart, _removedForeColor));
            }
        }

        return (lengthIdenticalAtStart, lengthIdenticalAtEnd);
    }

    private static TextMarker CreateAnchorMarker(int offset, Color color)
    {
        return new TextMarker(offset, 0, TextMarkerType.InterChar, color);
    }

    private static TextMarker CreateDimmedMarker(int offset, int length, bool isRemoved, bool dimBackground)
    {
        return dimBackground
            ? CreateTextMarker(offset, length, (isRemoved ? _removedBackColor : _addedBackColor).DimColor().DimColor())
            : new TextMarker(offset, length, TextMarkerType.SolidBlock, AppColor.EditorBackground.GetThemeColor(),
                (isRemoved ? _removedForeColor : _addedForeColor).DimColor());
    }

    private static TextMarker CreateTextMarker(int offset, int length, Color backColor)
    {
        return new TextMarker(offset, length, TextMarkerType.SolidBlock, backColor, backColor.GetTextColor());
    }

    internal static class TestAccessor
    {
        internal static List<ISegment> GetBlockOfLines(DiffLineInfo[] diffLines, DiffLineType diffLineType,
            ref int index, bool found)
        {
            return DiffHighlightService.GetBlockOfLines(diffLines, diffLineType, ref index, found);
        }
    }
}
