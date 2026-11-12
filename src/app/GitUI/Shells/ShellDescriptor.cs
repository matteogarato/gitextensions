namespace GitUI.Shells;

public abstract class ShellDescriptor : IShellDescriptor
{
    /// <inheritdoc />
    protected string? ExecutableCommandLine { get; set; }

    /// <inheritdoc />
    protected string ExecutableName { get; set; } = null!;

    /// <inheritdoc />
    protected string? ExecutablePath { get; set; }

    /// <inheritdoc />
    protected Image Icon { get; set; } = null!;

    /// <inheritdoc />
    protected string Name { get; set; } = null!;

    /// <inheritdoc />
    string? IShellDescriptor.ExecutableCommandLine
    {
        get => ExecutableCommandLine;
    }

    /// <inheritdoc />
    string IShellDescriptor.ExecutableName
    {
        get => ExecutableName;
    }

    /// <inheritdoc />
    string? IShellDescriptor.ExecutablePath
    {
        get => ExecutablePath;
    }

    /// <inheritdoc />
    bool IShellDescriptor.HasExecutable
    {
        get => ExecutablePath is not null;
    }

    /// <inheritdoc />
    Image IShellDescriptor.Icon
    {
        get => Icon;
    }

    /// <inheritdoc />
    string IShellDescriptor.Name
    {
        get => Name;
    }

    /// <inheritdoc />
    public abstract string GetChangeDirCommand(string path);
}
