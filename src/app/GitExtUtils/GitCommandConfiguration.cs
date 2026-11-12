using System.Collections.Concurrent;
using GitExtensions.Extensibility.Git;

namespace GitExtUtils;

public sealed class GitCommandConfiguration : IGitCommandConfiguration
{
    private readonly ConcurrentDictionary<string, GitConfigItem[]> _configByCommand
        = new(StringComparer.Ordinal);

    static GitCommandConfiguration()
    {
        // The set of default configuration items for Git Extensions
        Default = new GitCommandConfiguration();

        Default.Add(new GitConfigItem("rebase.autosquash", "false"), "rebase");

        Default.Add(new GitConfigItem("log.showsignature", "false"), "log", "show", "whatchanged");

        Default.Add(new GitConfigItem("color.ui", "never"), "diff", "range-diff", "grep");
        Default.Add(new GitConfigItem("diff.submodule", "short"), "diff");
        Default.Add(new GitConfigItem("diff.noprefix", "false"), "diff");
        Default.Add(new GitConfigItem("diff.mnemonicprefix", "false"), "diff");
        Default.Add(new GitConfigItem("diff.ignoresubmodules", "none"), "diff", "status");
        Default.Add(new GitConfigItem("core.safecrlf", "false"), "diff");
    }

    /// <summary>
    ///     Gets the default configuration for git commands used by Git Extensions.
    /// </summary>
    public static GitCommandConfiguration Default { get; }

    void IGitCommandConfiguration.Add(GitConfigItem configItem, params string[] commands)
    {
        foreach (var command in commands)
        {
            _configByCommand.AddOrUpdate(
                command,
                _ => [configItem],
                (_, items) => items.AppendTo(configItem));
        }
    }

    IReadOnlyList<GitConfigItem> IGitCommandConfiguration.Get(string command)
    {
        return _configByCommand.TryGetValue(command, out var items)
            ? items
            : [];
    }
}
