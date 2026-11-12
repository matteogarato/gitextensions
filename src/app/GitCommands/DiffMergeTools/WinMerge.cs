namespace GitCommands.DiffMergeTools;

internal sealed class WinMerge : DiffMergeTool
{
    /// <inheritdoc />
    public override string DiffCommand
    {
        get => "-e -u \"$LOCAL\" \"$REMOTE\"";
    }

    /// <inheritdoc />
    public override string ExeFileName
    {
        get => "winmergeu.exe";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get =>
            "-e -u  -wl -wr -fm -dl \"Mine: $LOCAL\" -dm \"Merged: $BASE\" -dr \"Theirs: $REMOTE\" \"$LOCAL\" \"$BASE\" \"$REMOTE\" -o \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "winmerge";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"WinMerge\" };
    }
}
