namespace GitCommands.Remotes;

/// <summary>
///     Represents a result of <see cref="ConfigFileRemoteSettingsManager.SaveRemote" /> operation.
/// </summary>
public class ConfigFileRemoteSaveResult(string? message, bool shouldUpdateRemote)
{
    /// <summary>
    ///     Indicates whether the "remote update" is desirable after the save operation.
    /// </summary>
    public bool ShouldUpdateRemote { get; } = shouldUpdateRemote;

    /// <summary>
    ///     Gets the output of the save operation (if any).
    /// </summary>
    public string? UserMessage { get; } = message;
}
