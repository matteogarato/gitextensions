using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

internal static class CloneRepositoryWorkflow
{
    internal static ArgumentString BuildCloneCommand(
        IGitModule destinationModule,
        string source,
        string destination,
        bool bare,
        bool initializeSubmodules)
    {
        ArgumentNullException.ThrowIfNull(destinationModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (!Path.IsPathRooted(destination))
        {
            throw new ArgumentException("The destination must be an absolute path.", nameof(destination));
        }

        return Commands.Clone(
            source,
            destination,
            destinationModule.GetPathForGitExecution,
            bare,
            initializeSubmodules);
    }
}
