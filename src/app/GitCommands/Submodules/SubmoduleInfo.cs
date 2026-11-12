namespace GitCommands.Submodules;

/// <summary>
///     Contains submodule information that is loaded asynchronously.
/// </summary>
public class SubmoduleInfo(string text, string path, bool bold)
{
    /// <summary>
    ///     User-friendly display text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///     Full path to submodule
    /// </summary>
    public string Path { get; } = path;

    public bool Bold { get; } = bold;

    public DetailedSubmoduleInfo? Detailed { get; set; }
}
