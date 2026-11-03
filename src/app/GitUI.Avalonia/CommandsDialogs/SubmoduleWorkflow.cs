using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

internal static class SubmoduleWorkflow
{
    internal static ArgumentString BuildAdd(
        string remotePath,
        string localPath,
        string? branch,
        bool force,
        IEnumerable<GitConfigItem>? configs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        return Commands.AddSubmodule(remotePath, localPath, branch ?? string.Empty, force, configs);
    }

    internal static void Remove(IGitModule module, IGitSubmoduleInfo submodule)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(submodule);

        ISubmodulesConfigFile submoduleConfigFile = module.GetSubmodulesConfigFile();
        module.UnstageFile(submodule.LocalPath);
        submoduleConfigFile.RemoveConfigSection($"submodule \"{submodule.Name}\"");
        submoduleConfigFile.Save();
        module.StageFile(".gitmodules");
        module.RemoveConfigSection("submodule", submodule.Name);
    }
}
