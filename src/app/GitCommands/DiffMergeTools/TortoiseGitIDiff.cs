namespace GitCommands.DiffMergeTools;

internal sealed class TortoiseGitIDiff : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => "TortoiseGitIDiff.exe";
    }

    /// <inheritdoc />
    public override bool IsMergeTool
    {
        get => false;
    }

    /// <inheritdoc />
    public override string DiffCommand
    {
        get => "/left:\"$LOCAL\" /right:\"$REMOTE\" /fit /overlay";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "TortoiseGitIDiff";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"TortoiseGit\bin\" };
    }
}
