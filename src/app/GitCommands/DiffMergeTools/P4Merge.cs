namespace GitCommands.DiffMergeTools;

internal sealed class P4Merge : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "p4merge.exe" : "p4merge";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "\"$BASE\" \"$LOCAL\" \"$REMOTE\" \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "p4merge";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"Perforce\" };
    }
}
