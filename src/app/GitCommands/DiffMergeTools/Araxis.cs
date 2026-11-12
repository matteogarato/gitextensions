namespace GitCommands.DiffMergeTools;

internal sealed class Araxis : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "Compare.exe" : "compare";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "/merge /wait /a2 /3 \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "araxis";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"Araxis\" };
    }
}
