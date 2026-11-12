namespace GitCommands.DiffMergeTools;

internal class TortoiseGitMerge : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => "TortoiseGitMerge.exe";
    }

    /// <inheritdoc />
    public override bool IsDiffTool
    {
        get => false;
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "-base:\"$BASE\" -mine:\"$LOCAL\" -theirs:\"$REMOTE\" -merged:\"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "tortoisemerge";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"TortoiseGit\bin\" };
    }
}
