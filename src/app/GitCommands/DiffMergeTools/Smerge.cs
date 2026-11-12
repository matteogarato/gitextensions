namespace GitCommands.DiffMergeTools;

internal sealed class Smerge : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "smerge.exe" : "smerge";
    }

    /// <inheritdoc />
    public override string DiffCommand
    {
        get => "mergetool \"$LOCAL\" \"$REMOTE\" -o=\"$MERGED\"";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "mergetool \"$BASE\" \"$LOCAL\" \"$REMOTE\" -o=\"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "smerge";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"Sublime Merge\" };
    }
}
