using GitUI.Editor.Diff;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Document.LineManager;

namespace GitUITests.Editor.Diff;

public class LinesMatcherTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(2, 4)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(3, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(4, 3)]
    [InlineData(4, 4)]
    public void GetAllCombinations_should_visit_each_index_pair(int firstEnd, int secondEnd)
    {
        var visited = new bool[firstEnd, secondEnd];
        foreach ((var firstIndex, var secondIndex) in LinesMatcher.GetAllCombinations(firstEnd, secondEnd))
        {
            firstIndex.Should().BeLessThan(firstEnd);
            secondIndex.Should().BeLessThan(secondEnd);
            visited[firstIndex, secondIndex].Should().BeFalse();
            visited[firstIndex, secondIndex] = true;
        }

        for (var i = 0; i < firstEnd; ++i)
        {
            for (var j = 0; j < secondEnd; ++j)
            {
                visited[i, j].Should().BeTrue($"everywhere including at {i}, {j}");
            }
        }
    }

    [Theory]
    [InlineData("", new string[] { }, new int[] { })]
    [InlineData("a", new[] { "a" }, new[] { 0 })]
    [InlineData("a-", new[] { "a" }, new[] { 0 })]
    [InlineData("-a", new[] { "a" }, new[] { 1 })]
    [InlineData("a bc", new[] { "a", "bc" }, new[] { 0, 2 })]
    [InlineData("-a bc", new[] { "a", "bc" }, new[] { 1, 3 })]
    [InlineData("a bc-", new[] { "a", "bc" }, new[] { 0, 2 })]
    [InlineData("---abc---123---def_7---", new[] { "abc", "123", "def_7" }, new[] { 3, 9, 15 })]
    public void GetWords(string text, string[] words, int[] offsets)
    {
        (string Word, int Offset)[] result = [.. LinesMatcher.GetWords(text, TextUtilities.IsLetterDigitOrUnderscore)];
        result.Select(LinesMatcher.SelectWord).Should().BeEquivalentTo(words);
        result.Select(LinesMatcher.SelectStartIndex).Should().BeEquivalentTo(offsets);
    }

    [Theory]
    [InlineData("", new string[] { }, new int[] { })]
    [InlineData("_", new string[] { }, new int[] { })]
    [InlineData("a", new[] { "a" }, new[] { 0 })]
    [InlineData("A", new[] { "A" }, new[] { 0 })]
    [InlineData("camelCase", new[] { "camel", "Case" }, new[] { 0, 5 })]
    [InlineData("DelphiCase", new[] { "Delphi", "Case" }, new[] { 0, 6 })]
    [InlineData("_precedingUnderScore", new[] { "_preceding", "Under", "Score" }, new[] { 0, 10, 15 })]
    [InlineData("python_style", new[] { "python", "style" }, new[] { 0, 7 })]
    [InlineData("multiple__under___scores_", new[] { "multiple", "under", "scores" }, new[] { 0, 10, 18 })]
    [InlineData("CAPITALSwithsuffixNext_Upper_lower", new[] { "CAPITALSwithsuffix", "Next", "Upper", "lower" },
        new[] { 0, 18, 23, 29 })]
    [InlineData("    sum += x;", new[] { "sum", "x" }, new[] { 4, 11 })]
    public void GetSubwords(string text, string[] words, int[] offsets)
    {
        (string Word, int Offset)[] result = [.. LinesMatcher.GetSubwords(text)];
        result.Select(LinesMatcher.SelectWord).Should().BeEquivalentTo(words);
        result.Select(LinesMatcher.SelectStartIndex).Should().BeEquivalentTo(offsets);
    }

    [Fact]
    public void FindLinePairs_shall_not_search_in_too_large_blocks()
    {
        const int removedCount = 10;
        const int maxCombinations = 100 * 100;
        var removedLines = CreateLines(removedCount);
        var addedLines = CreateLines((maxCombinations / removedCount) + 1);
        Dictionary<ISegment, string> lineTexts = [];
        for (var index = 0; index < removedCount; ++index)
        {
            var lineText = $"line{index}";
            lineTexts[removedLines[index]] = lineText;
            lineTexts[addedLines[index + removedCount]] = lineText;
        }

        var pairs =
            LinesMatcher.FindLinePairs(GetText, removedLines, addedLines);

        var pairIndex = 0;
        foreach ((var removedLine, var addedLine) in pairs)
        {
            removedLine.Should().Be(removedLines[pairIndex]);
            addedLine.Should().Be(addedLines[pairIndex]);
            ++pairIndex;
        }

        return;

        string GetText(ISegment line)
        {
            return lineTexts.GetValueOrDefault(line, "other line");
        }
    }

    [Fact]
    public void FindLinePairs_shall_match_trimmed_lines()
    {
        var removedLines = CreateLines(3);
        var addedLines = CreateLines(5);
        Dictionary<ISegment, string> lineTexts = new()
        {
            { removedLines[0], "r0" },
            { removedLines[1], " trimmed line\t" },
            { removedLines[2], "r2" },
            { addedLines[0], "a0" },
            { addedLines[1], "a1" },
            { addedLines[2], "a2" },
            { addedLines[3], "trimmed line" },
            { addedLines[4], "a4" }
        };

        var pairs =
            LinesMatcher.FindLinePairs(GetText, removedLines, addedLines);

        (ISegment RemovedLine, ISegment AddedLine)[] expectedPairs =
        [
            (removedLines[0], addedLines[0]),
            (removedLines[1], addedLines[3]),
            (removedLines[2], addedLines[4])
        ];
        pairs.Should().BeEquivalentTo(expectedPairs);

        return;

        string GetText(ISegment line)
        {
            return lineTexts.GetValueOrDefault(line, "other line");
        }
    }

    [Fact]
    public void FindLinePairs_shall_match_lines_whose_common_words_have_maximum_summedup_length()
    {
        var removedLines = CreateLines(4);
        var addedLines = CreateLines(5);
        Dictionary<ISegment, string> lineTexts = new()
        {
            { removedLines[0], "line 0 had some words" },
            { removedLines[1], "line 1 had some more common words" },
            { removedLines[2], " trimmed line\t" },
            { removedLines[3], "r2" },
            { addedLines[0], "some words" },
            { addedLines[1], "line 1 has some words" },
            { addedLines[2], "line 2 has some common words" },
            { addedLines[3], "trimmed line" },
            { addedLines[4], "a4" }
        };

        var pairs =
            LinesMatcher.FindLinePairs(GetText, removedLines, addedLines);

        (ISegment RemovedLine, ISegment AddedLine)[] expectedPairs =
        [
            (removedLines[0], addedLines[1]), // longer common words
            (removedLines[1], addedLines[2]), // most long common words
            (removedLines[2], addedLines[3]), // trimmed
            (removedLines[3], addedLines[4]) // single remaining lines after best match
        ];
        pairs.Should().BeEquivalentTo(expectedPairs);

        return;

        string GetText(ISegment line)
        {
            return lineTexts.GetValueOrDefault(line, "other line");
        }
    }

    private static LineSegment[] CreateLines(int count)
    {
        var lines = new LineSegment[count];
        for (var index = 0; index < count; ++index)
        {
            lines[index] = new LineSegment();
        }

        return lines;
    }
}
