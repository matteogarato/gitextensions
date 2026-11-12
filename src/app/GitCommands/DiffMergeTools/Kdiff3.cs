namespace GitCommands.DiffMergeTools;

internal sealed class Kdiff3 : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "kdiff3.exe" : "kdiff3";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "\"$BASE\" \"$LOCAL\" \"$REMOTE\" -o \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "kdiff3";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[]
        {
            // regkdiff3path
            @"KDiff3"
        };
    }
}
