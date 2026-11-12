namespace GitCommands.DiffMergeTools;

internal sealed class DiffMerge : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "sgdm.exe" : "diffmerge";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "-merge -result=\"$MERGED\" \"$LOCAL\" \"$BASE\" \"$REMOTE\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "diffmerge";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"SourceGear\Common\DiffMerge\", @"SourceGear\DiffMerge\" };
    }
}
