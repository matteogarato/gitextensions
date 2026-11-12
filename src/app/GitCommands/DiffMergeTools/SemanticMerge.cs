namespace GitCommands.DiffMergeTools;

internal sealed class SemanticMerge : DiffMergeTool
{
    private static readonly string[] _folders = GetFolders();

    /// <inheritdoc />
    public override string DiffCommand
    {
        get => "-s \"$LOCAL\" -d \"$REMOTE\"";
    }

    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "semanticmergetool.exe" : "semanticmergetool";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "-s \"$REMOTE\" -d \"$LOCAL\" -b \"$BASE\" -r \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "semanticmerge";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => _folders;
    }

    private static string[] GetFolders()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Join(folder, @"semanticmerge"),
            Path.Join(folder, @"PlasticSCM4\semanticmerge")
        ];
    }
}
