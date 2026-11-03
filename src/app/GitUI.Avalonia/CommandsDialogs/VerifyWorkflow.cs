using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

internal static class VerifyWorkflow
{
    private static readonly Regex FsckOutputRegex = new(
        $@"^(?<diagnostic>(?:dangling|missing|unreachable) (?<type>commit|blob|tree|tag)|warning in tree) (?<hash>[a-f\d]{{{ObjectId.Sha1CharCount}}})(?: .*)?$",
        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

    internal static ArgumentString BuildFsck(bool unreachable, bool full, bool noReflogs)
        => new GitArgumentBuilder("fsck-objects")
        {
            { unreachable, "--unreachable" },
            { full, "--full" },
            { noReflogs, "--no-reflogs" },
        };

    internal static ArgumentString BuildSaveLostFound()
        => new GitArgumentBuilder("fsck-objects") { "--lost-found" };

    internal static ArgumentString BuildPrune()
        => new GitArgumentBuilder("prune");

    internal static IReadOnlyList<VerifyObject> ParseFsckOutput(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        List<VerifyObject> objects = [];
        foreach (string line in output.Split(Delimiters.LineFeedAndCarriageReturn, StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = FsckOutputRegex.Match(line);
            if (match.Success)
            {
                objects.Add(new VerifyObject(
                    match.Groups["diagnostic"].Value,
                    match.Groups["type"].Success ? match.Groups["type"].Value : "other",
                    ObjectId.Parse(match.Groups["hash"].Value)));
            }
        }

        return objects;
    }

    internal static IGitCommand BuildRecoveryTag(IGitModule module, VerifyObject verifyObject)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(verifyObject);

        return Commands.CreateTag(
            new GitCreateTagArgs($"LOST_FOUND_{verifyObject.ObjectId}", verifyObject.ObjectId),
            tagMessageFileName: null,
            module.GetPathForGitExecution);
    }
}

internal sealed record VerifyObject(string Diagnostic, string Type, ObjectId ObjectId)
{
    public override string ToString() => $"{Diagnostic}  {ObjectId}";
}
