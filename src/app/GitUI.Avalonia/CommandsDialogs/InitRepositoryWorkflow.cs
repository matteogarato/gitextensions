using GitCommands;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

internal static class InitRepositoryWorkflow
{
    internal static GitModule Initialize(IGitExecutorProvider gitExecutorProvider, string path, bool bare)
    {
        ArgumentNullException.ThrowIfNull(gitExecutorProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Path.IsPathRooted(path))
        {
            throw new ArgumentException("The repository directory must be an absolute path.", nameof(path));
        }

        if (File.Exists(path))
        {
            throw new ArgumentException("The repository directory cannot be a file.", nameof(path));
        }

        GitModule module = new(gitExecutorProvider, path);
        Directory.CreateDirectory(module.WorkingDir);
        module.Init(bare, bare);
        return module;
    }
}
