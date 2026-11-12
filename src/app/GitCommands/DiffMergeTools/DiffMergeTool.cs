namespace GitCommands.DiffMergeTools;

/// <summary>
///     A base class for diff/merge tool configurations.
/// </summary>
internal abstract class DiffMergeTool
{
    private const string _defaultDiffCommand = "\"$LOCAL\" \"$REMOTE\"";
    private const string _defaultMergeCommand = "\"$LOCAL\" \"$REMOTE\" \"$BASE\" \"$MERGED\"";

    /// <summary>
    ///     Gets the diff command will be invoked by git.
    ///     <see langword="null" /> or <see cref="string.Empty" /> if <see cref="IsDiffTool" /> is <see langword="false" />.
    /// </summary>
    public virtual string DiffCommand
    {
        get => _defaultDiffCommand;
    }

    /// <summary>
    ///     Gets the diff/merge exe file name.
    /// </summary>
    public abstract string ExeFileName { get; }

    /// <summary>
    ///     Indicates whether the tool can be used as a diff tool.
    ///     Default: <see langword="true" />.
    /// </summary>
    public virtual bool IsDiffTool
    {
        get => true;
    }

    /// <summary>
    ///     Indicates whether the tool can be used as a merge tool.
    ///     Default: <see langword="true" />.
    /// </summary>
    public virtual bool IsMergeTool
    {
        get => true;
    }

    /// <summary>
    ///     Gets the merge command will be invoked by git.
    ///     <see langword="null" /> or <see cref="string.Empty" /> if <see cref="IsMergeTool" /> is <see langword="false" />.
    /// </summary>
    public virtual string MergeCommand
    {
        get => _defaultMergeCommand;
    }

    /// <summary>
    ///     Gets the name of the diff/merge tool that will be shown to the user.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    ///     Gets the list of possible locations of the diff/merge tool.
    ///     These location will be used to help the user to automatically locate the tool.
    /// </summary>
    public abstract IEnumerable<string> SearchPaths { get; }
}
