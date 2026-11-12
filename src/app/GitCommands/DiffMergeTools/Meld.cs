namespace GitCommands.DiffMergeTools;

internal sealed class Meld : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "meld.exe" : "meld";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "\"$LOCAL\" \"$BASE\" \"$REMOTE\" --output \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "meld";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"Meld\", @"Meld (x86)\" };
    }
}
