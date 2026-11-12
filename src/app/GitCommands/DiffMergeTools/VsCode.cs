namespace GitCommands.DiffMergeTools;

internal sealed class VsCode : DiffMergeTool
{
    private static readonly string[] _folders = GetFolders();

    /// <inheritdoc />
    public override string DiffCommand
    {
        get => "--new-window --wait --diff \"$LOCAL\" \"$REMOTE\"";
    }

    /// <inheritdoc />
    public override string ExeFileName
    {
        get => OperatingSystem.IsWindows() ? "Code.exe" : "code";
    }

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "--new-window --wait --merge \"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "vscode";
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
            Path.Join(folder, @"Programs\Microsoft VS Code"),
            @"Microsoft VS Code\"
        ];
    }
}
