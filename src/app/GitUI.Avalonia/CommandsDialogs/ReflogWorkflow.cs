using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

internal sealed record ReflogEntry(ObjectId Sha, string Reference, string Action);

internal static class ReflogWorkflow
{
    public static ArgumentString BuildRead(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return new GitArgumentBuilder("reflog")
        {
            "--no-abbrev",
            reference.Quote(),
        };
    }

    public static ArgumentString BuildCreateBranch(string branchName, ReflogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return Commands.Branch(branchName, entry.Sha, checkout: true);
    }

    public static ArgumentString BuildResetTo(ReflogEntry entry, ResetMode mode)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return Commands.Reset(mode, entry.Sha.ToString(), quiet: false);
    }

    public static IReadOnlyList<ReflogEntry> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        List<ReflogEntry> entries = [];
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            ReadOnlySpan<char> record = line.AsSpan().TrimEnd('\r');
            int shaEnd = record.IndexOf(' ');
            if (shaEnd <= 0)
            {
                continue;
            }

            int referenceEnd = record[(shaEnd + 1)..].IndexOf(": ");
            if (referenceEnd < 0 || !ObjectId.TryParse(record[..shaEnd], out ObjectId sha))
            {
                continue;
            }

            referenceEnd += shaEnd + 1;
            string reference = record[(shaEnd + 1)..referenceEnd].ToString();
            string action = record[(referenceEnd + 2)..].ToString();
            entries.Add(new ReflogEntry(sha, reference, action));
        }

        return entries;
    }

    public static IReadOnlyList<ReflogEntry> Read(IGitModule module, string reference)
    {
        ArgumentNullException.ThrowIfNull(module);
        string output = module.GitExecutable.GetOutput(BuildRead(reference));
        return Parse(output);
    }
}
