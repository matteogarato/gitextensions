using System.Globalization;
using System.Text;

namespace GitExtensions.Extensibility.Git;

public sealed class GitBlame(IReadOnlyList<GitBlameLine> lines)
{
    public IReadOnlyList<GitBlameLine> Lines { get; } = lines;
}

public sealed class GitBlameLine(GitBlameCommit commit, int finalLineNumber, int originLineNumber, string text)
{
    public GitBlameCommit Commit { get; } = commit;
    public int FinalLineNumber { get; } = finalLineNumber;
    public int OriginLineNumber { get; } = originLineNumber;
    public string Text { get; } = text;
}

public sealed class GitBlameCommit(
    ObjectId objectId,
    string author,
    string authorMail,
    DateTime authorTime,
    string authorTimeZone,
    string committer,
    string committerMail,
    DateTime committerTime,
    string committerTimeZone,
    string summary,
    string fileName)
{
    public ObjectId ObjectId { get; } = objectId;
    public string Author { get; } = author;
    public string AuthorMail { get; } = authorMail;
    public DateTime AuthorTime { get; } = authorTime;
    public string AuthorTimeZone { get; } = authorTimeZone;
    public string Committer { get; } = committer;
    public string CommitterMail { get; } = committerMail;
    public DateTime CommitterTime { get; } = committerTime;
    public string CommitterTimeZone { get; } = committerTimeZone;
    public string Summary { get; } = summary;
    public string FileName { get; } = fileName;

    public string ToString(Func<string?, string?> summaryBuilderFunc)
    {
        return ToString(summaryBuilderFunc(Summary) ?? Summary);
    }

    public override string ToString()
    {
        return ToString(Summary);
    }

    private string ToString(string summary)
    {
        StringBuilder s = new();

        s.Append("Author: ").AppendLine(Author);
        s.Append("Author date: ").AppendLine(AuthorTime.ToString(CultureInfo.CurrentCulture));
        if (Author != Committer || AuthorTime != CommitterTime)
        {
            s.Append("Committer: ").AppendLine(Committer);
            s.Append("Commit date: ").AppendLine(CommitterTime.ToString(CultureInfo.CurrentCulture));
        }

        s.Append("Commit hash: ").AppendLine(ObjectId.ToShortString());
        s.Append("Summary: ").AppendLine(summary);
        s.AppendLine();
        s.Append("FileName: ").Append(FileName);

        return s.ToString();
    }
}
