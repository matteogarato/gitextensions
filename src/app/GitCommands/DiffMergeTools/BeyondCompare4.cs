namespace GitCommands.DiffMergeTools;

internal sealed class BeyondCompare4 : DiffMergeTool
{
    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "bcomp.exe" : "bcompare";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "bc";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { @"Beyond Compare 4 (x86)\", @"Beyond Compare 4\" };
    }
}
