namespace GitExtensions.Extensibility.Git;

public readonly struct ConflictedFileData(ObjectId objectId, string filename)
{
    public ObjectId ObjectId { get; } = objectId;
    public string Filename { get; } = filename;
}
