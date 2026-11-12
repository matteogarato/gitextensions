using GitCommands.Git.Extensions;
using GitCommands.Git.Tag;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitCommands.Git;

public static partial class Commands
{
    public static IGitCommand CheckoutBranch(
        string branchName,
        bool remote,
        LocalChangesAction localChanges = LocalChangesAction.DontChange,
        CheckoutNewBranchMode newBranchMode = CheckoutNewBranchMode.DontCreate,
        string? newBranchName = null)
    {
        return new GitCommand(false, true,
            new GitArgumentBuilder("checkout")
            {
                { localChanges is LocalChangesAction.Merge, "--merge" },
                { localChanges is LocalChangesAction.Reset, "--force" },
                { remote && newBranchMode is CheckoutNewBranchMode.Create, $"-b {newBranchName.Quote()}" },
                { remote && newBranchMode is CheckoutNewBranchMode.Reset, $"-B {newBranchName.Quote()}" },
                { remote && newBranchMode is CheckoutNewBranchMode.Create, "--track" },
                branchName.QuoteNe()
            });
    }

    public static IGitCommand CreateTag(GitCreateTagArgs gitCreateTagArgs, string? tagMessageFileName,
        Func<string?, string?> getPathForGitExecution)
    {
        Validate(gitCreateTagArgs, tagMessageFileName);

        return new GitCommand(false, true,
            new GitArgumentBuilder("tag")
            {
                { gitCreateTagArgs.Force, "-f" },
                GetArgumentForOperation(gitCreateTagArgs),
                {
                    gitCreateTagArgs.Operation.CanProvideMessage(),
                    $"-F {getPathForGitExecution(tagMessageFileName).Quote()}"
                },
                gitCreateTagArgs.TagName.Trim().Quote(),
                "--",
                gitCreateTagArgs.ObjectId
            });

        static string? GetArgumentForOperation(GitCreateTagArgs gitCreateTagArgs)
        {
            return gitCreateTagArgs.Operation switch
            {
                /* Lightweight */
                TagOperation.Lightweight => null,
                /* Annotate */
                TagOperation.Annotate => "-a",
                /* Sign with default GPG */
                TagOperation.SignWithDefaultKey => "-s",
                /* Sign with specific GPG */
                TagOperation.SignWithSpecificKey => $"-u {gitCreateTagArgs.SignKeyId}",
                _ => throw new NotSupportedException($"Invalid tag operation: {gitCreateTagArgs.Operation}")
            };
        }

        static void Validate(GitCreateTagArgs gitCreateTagArgs, string? tagMessageFileName)
        {
            if (gitCreateTagArgs.ObjectId.IsArtificial)
            {
                throw new ArgumentException("A valid, non-artificial revision is required for tagging.",
                    nameof(gitCreateTagArgs.ObjectId));
            }

            if (string.IsNullOrWhiteSpace(gitCreateTagArgs.TagName))
            {
                throw new ArgumentException("TagName is required.");
            }

            if (gitCreateTagArgs.Operation.CanProvideMessage() && string.IsNullOrWhiteSpace(tagMessageFileName))
            {
                throw new ArgumentException("TagMessageFileName is required.");
            }

            if (gitCreateTagArgs.Operation == TagOperation.SignWithSpecificKey &&
                string.IsNullOrWhiteSpace(gitCreateTagArgs.SignKeyId))
            {
                throw new ArgumentException("SignKeyId is required.");
            }
        }
    }

    /// <summary>
    ///     Builds a command to delete a local tag.
    /// </summary>
    /// <param name="tagName">The tag name to delete.</param>
    /// <returns>A state-changing local Git command.</returns>
    public static IGitCommand DeleteTag(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        return new GitCommand(false, true,
            new GitArgumentBuilder("tag") { "-d", "--", tagName.QuoteNe() });
    }

    /// <summary>
    ///     Builds a command to run Git garbage collection for the current repository.
    /// </summary>
    /// <returns>A state-changing local Git command.</returns>
    public static IGitCommand GarbageCollect()
    {
        return new GitCommand(false, true,
            new GitArgumentBuilder("gc"));
    }

    public static IGitCommand DeleteBranch(IReadOnlyCollection<IGitRef> branches, bool force)
    {
        ArgumentNullException.ThrowIfNull(branches);
        if (branches.Count == 0)
        {
            throw new ArgumentException("At least one branch is required.", nameof(branches));
        }

        var hasRemoteBranch = branches.Any(branch => branch.IsRemote);
        var hasNonRemoteBranch = branches.Any(branch => !branch.IsRemote);
        return new GitCommand(false, true,
            new GitArgumentBuilder("branch")
            {
                "--delete",
                { force, "--force" },
                { hasRemoteBranch && hasNonRemoteBranch, "--all" },
                { hasRemoteBranch && !hasNonRemoteBranch, "--remotes" },
                branches.Select(branch => branch.Name.Quote())
            });
    }

    public static IGitCommand DeleteRemoteBranches(string remote, IEnumerable<string> branchLocalNames)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(branchLocalNames);

        return new GitCommand(true, true,
            new GitArgumentBuilder("push")
            {
                remote, branchLocalNames.Select(branch => $":refs/heads/{branch.Quote()}")
            });
    }
}
