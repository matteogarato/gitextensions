using System.Text;

namespace GitCommands.Git;

public interface IGitRevisionSummaryBuilder
{
    string? BuildSummary(string? body);
}

public class GitRevisionSummaryBuilder : IGitRevisionSummaryBuilder
{
    private const int _commitSummaryMaxLineLength = 150;
    private const int _commitSummaryMaxNumberOfLines = 30;

    private const int _lineEllipsisLength = 7;
    private const int _commitSummaryEllipsisLength = 5;

    // Maximum size of the commit summary with ellipsis strings on each lines and at the end
    private const int _commitSummaryWorstCaseLength =
        (_commitSummaryMaxNumberOfLines * (_commitSummaryMaxLineLength + _lineEllipsisLength)) +
        _commitSummaryEllipsisLength;

    string? IGitRevisionSummaryBuilder.BuildSummary(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        StringBuilder s = new(Math.Min(body.Length, _commitSummaryWorstCaseLength));

        var lineCount = 0;
        var lineStartPos = 0;
        int pos;
        for (pos = 0; pos < body.Length; ++pos)
        {
            if (body[pos] == '\n')
            {
                if (pos - lineStartPos > _commitSummaryMaxLineLength)
                {
                    AppendLine(_commitSummaryMaxLineLength, true);
                }
                else
                {
                    AppendLine(pos - lineStartPos);
                }

                lineStartPos = pos + 1;

                if (++lineCount == _commitSummaryMaxNumberOfLines)
                {
                    return s.Append("[...]").ToString();
                }
            }

            if (pos == body.Length - 1)
            {
                if (pos - lineStartPos > _commitSummaryMaxLineLength)
                {
                    AppendLine(_commitSummaryMaxLineLength, true);
                }
                else
                {
                    AppendLine(pos - lineStartPos + 1);
                }
            }

            void AppendLine(int length, bool withEllipsis = false)
            {
                s.Append(body.AsSpan(lineStartPos, length));
                if (withEllipsis)
                {
                    s.Append(" [...]");
                }

                s.Append('\n');
            }
        }

        return s.ToString().TrimEnd('\n');
    }
}
