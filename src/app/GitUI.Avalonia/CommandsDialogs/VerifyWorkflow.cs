using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

internal static class VerifyWorkflow
{
    private static readonly Regex _fsckOutputRegex = new(
        $@"^(?<diagnostic>(?:dangling|missing|unreachable) (?<type>commit|blob|tree|tag)|warning in tree) (?<hash>[a-f\d]{{{ObjectId.Sha1CharCount}}})(?: .*)?$",
        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

    internal static ArgumentString BuildFsck(bool unreachable, bool full, bool noReflogs)
    {
        return new GitArgumentBuilder("fsck-objects")
        {
            { unreachable, "--unreachable" }, { full, "--full" }, { noReflogs, "--no-reflogs" }
        };
    }

    internal static ArgumentString BuildSaveLostFound()
    {
        return new GitArgumentBuilder("fsck-objects") { "--lost-found" };
    }

    internal static ArgumentString BuildPrune()
    {
        return new GitArgumentBuilder("prune");
    }

    internal static IReadOnlyList<VerifyObject> ParseFsckOutput(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        List<VerifyObject> objects = [];
        foreach (var line in output.Split(Delimiters.LineFeedAndCarriageReturn,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var match = _fsckOutputRegex.Match(line);
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
            null,
            module.GetPathForGitExecution);
    }
}

internal sealed record VerifyObject(string Diagnostic, string Type, ObjectId ObjectId)
{
    public override string ToString()
    {
        return $"{Diagnostic}  {ObjectId}";
    }
}
