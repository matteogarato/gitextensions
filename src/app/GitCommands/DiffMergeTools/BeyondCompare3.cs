namespace GitCommands.DiffMergeTools;

internal sealed class BeyondCompare3 : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => "bcomp.exe";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "bc3";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"Beyond Compare 3 (x86)\", @"Beyond Compare 3\" };
    }
}
