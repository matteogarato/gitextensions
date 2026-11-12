using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitCommands.Git;

public static partial class Commands
{
    /// <summary>
    ///     Base class for structured git command.
    /// </summary>
    /// Here we can introduce methods which can operate on command structure instead of command string.
    private sealed class GitCommand(bool accessesRemote, bool changesRepoState, GitArgumentBuilder arguments)
        : IGitCommand
    {
        /// <value>Gets whether this command accesses a remote repository.</value>
        bool IGitCommand.AccessesRemote { get; } = accessesRemote;

        /// <value>The arguments for the git command.</value>
        string IGitCommand.Arguments { get; } = arguments.ToString();

        /// <value>Gets whether executing this command will change the repo state.</value>
        bool IGitCommand.ChangesRepoState { get; } = changesRepoState;

        public override string ToString()
        {
            return Arguments;
        }
    }
}
