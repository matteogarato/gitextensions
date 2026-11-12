namespace GitCommands.Submodules;

public class SubmoduleStatusEventArgs(SubmoduleInfoResult info, bool structureUpdated, CancellationToken token)
    : EventArgs
{
    public SubmoduleInfoResult Info { get; } = info;

    /// <summary>
    ///     First update of the submodule structure. Status of the submodule will be updated asynchronously.
    /// </summary>
    public bool StructureUpdated { get; } = structureUpdated;

    public CancellationToken Token { get; } = token;
}
