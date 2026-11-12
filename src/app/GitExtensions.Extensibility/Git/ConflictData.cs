using System.Diagnostics;

namespace GitExtensions.Extensibility.Git;

[DebuggerDisplay("{" + nameof(Filename) + "}")]
public readonly struct ConflictData(
    ConflictedFileData @base,
    ConflictedFileData local,
    ConflictedFileData remote)
{
    public ConflictedFileData Base { get; } = @base;
    public ConflictedFileData Local { get; } = local;
    public ConflictedFileData Remote { get; } = remote;

    public string Filename
    {
        get => Local.Filename ?? Base.Filename ?? Remote.Filename;
    }
}
