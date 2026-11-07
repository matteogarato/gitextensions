using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///  A rebase todo action that does not need an additional message editor.
/// </summary>
public enum RebaseTodoAction
{
    Pick,
    Squash,
    Fixup,
    Edit,
    Drop,
}

/// <summary>
///  One commit of the rebase todo list.
/// </summary>
public sealed class RebaseTodoEntry(string commitId, string subject, RebaseTodoAction action = RebaseTodoAction.Pick)
{
    public string CommitId { get; } = commitId;

    public string Subject { get; } = subject;

    public RebaseTodoAction Action { get; set; } = action;

    public override string ToString() => $"{CommitId[..Math.Min(CommitId.Length, 8)]} {Subject}";
}

/// <summary>
///  Builds the todo list of an interactive rebase and the command that applies it without opening an external editor.
/// </summary>
public static class InteractiveRebaseWorkflow
{
    private const char FieldSeparator = '\t';

    /// <summary>
    ///  Lists the commits that a rebase of the current branch would replay, oldest first.
    /// </summary>
    public static IReadOnlyList<RebaseTodoEntry> ListCommits(IGitModule module, string range)
    {
        GitArgumentBuilder args = new("log")
        {
            "--reverse",
            "--no-merges",
            "--pretty=format:%H%x09%s",
            range.Quote(),
        };
        string output = module.GitExecutable.Execute(args).StandardOutput;
        return ParseCommits(output);
    }

    /// <summary>
    ///  Parses <c>hash TAB subject</c> lines into todo entries.
    /// </summary>
    internal static IReadOnlyList<RebaseTodoEntry> ParseCommits(string output)
        => [.. output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(FieldSeparator, 2))
            .Where(parts => parts[0].Length > 0)
            .Select(parts => new RebaseTodoEntry(parts[0], parts.Length > 1 ? parts[1] : string.Empty))];

    /// <summary>
    ///  Returns the text of the todo file for <paramref name="entries"/>.
    /// </summary>
    public static string BuildTodo(IEnumerable<RebaseTodoEntry> entries)
        => string.Concat(entries.Select(entry => $"{entry.Action.ToString().ToLowerInvariant()} {entry.CommitId} {entry.Subject}\n"));

    /// <summary>
    ///  Returns an error when the list cannot be applied, or <see langword="null"/>.
    /// </summary>
    public static string? Validate(IReadOnlyList<RebaseTodoEntry> entries)
    {
        RebaseTodoEntry? first = entries.FirstOrDefault(entry => entry.Action != RebaseTodoAction.Drop);
        return first?.Action is RebaseTodoAction.Squash or RebaseTodoAction.Fixup
            ? TranslatedStrings.RebaseTodoFirstCannotSquash
            : null;
    }

    /// <summary>
    ///  Builds the rebase command that replaces git's todo list with the file at <paramref name="todoPath"/>.
    /// </summary>
    public static ArgumentString BuildCommand(RebaseConfiguration configuration, string todoPath)
    {
        string editor = $"cp {todoPath.Replace('\\', '/').Quote("'")}";
        ArgumentString rebase = RebaseWorkflow.BuildRebase(configuration with { Interactive = true });
        return $"-c sequence.editor={editor.Quote()} {rebase}";
    }

    /// <summary>
    ///  Gets the revision range whose commits the configured rebase replays.
    /// </summary>
    public static string GetRange(RebaseConfiguration configuration)
        => $"{(configuration.Upstream ?? configuration.Onto).Trim()}..HEAD";
}
