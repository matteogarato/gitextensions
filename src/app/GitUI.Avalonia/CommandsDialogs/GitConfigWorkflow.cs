using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///  The level of the Git configuration that a value is read from and written to.
/// </summary>
public enum GitConfigScope
{
    Global,
    Local,
}

/// <summary>
///  Reads and writes the Git configuration keys exposed by the settings dialog.
/// </summary>
public static class GitConfigWorkflow
{
    /// <summary>
    ///  The configuration keys that the settings dialog edits as free text.
    /// </summary>
    public static IReadOnlyList<string> TextKeys { get; } =
    [
        "user.name",
        "user.email",
        "core.editor",
        "diff.tool",
        "merge.tool",
        "init.defaultBranch",
    ];

    /// <summary>
    ///  The only configuration key edited as a tri-state boolean (unset, true, false).
    /// </summary>
    public const string PullRebaseKey = "pull.rebase";

    /// <summary>
    ///  Builds the command that reads a single key from one configuration level.
    /// </summary>
    public static ArgumentString BuildGet(GitConfigScope scope, string key)
        => new GitArgumentBuilder("config")
        {
            Scope(scope),
            "--get",
            key
        };

    /// <summary>
    ///  Builds the command that writes, or removes when <paramref name="value"/> is empty, a key.
    /// </summary>
    public static ArgumentString BuildSet(GitConfigScope scope, string key, string? value)
        => string.IsNullOrEmpty(value)
            ? new GitArgumentBuilder("config") { Scope(scope), "--unset", key }
            : new GitArgumentBuilder("config") { Scope(scope), key, value.Quote() };

    /// <summary>
    ///  Reads a value from one configuration level.
    /// </summary>
    /// <returns>The value, or an empty string when the key is not set at that level.</returns>
    public static string Read(IGitModule module, GitConfigScope scope, string key)
        => module.GitExecutable.Execute(BuildGet(scope, key), throwOnErrorExit: false).StandardOutput.Trim();

    /// <summary>
    ///  Writes a value to one configuration level and removes the key when the value is empty.
    /// </summary>
    /// <returns>An error message, or <see langword="null"/> when the value was stored.</returns>
    public static string? Write(IGitModule module, GitConfigScope scope, string key, string? value)
    {
        ExecutionResult result = module.GitExecutable.Execute(BuildSet(scope, key, value), throwOnErrorExit: false);

        // Removing a key that is not set exits with 5 and is not an error for the user.
        const int KeyNotSetExitCode = 5;
        return result.ExitedSuccessfully || (string.IsNullOrEmpty(value) && result.ExitCode == KeyNotSetExitCode)
            ? null
            : result.AllOutput.Trim();
    }

    private static string Scope(GitConfigScope scope) => scope == GitConfigScope.Global ? "--global" : "--local";
}
