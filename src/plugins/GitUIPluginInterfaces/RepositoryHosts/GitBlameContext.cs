using GitExtensions.Extensibility.Git;

namespace GitUIPluginInterfaces.RepositoryHosts;

// This is stored in ContextMenuStrip.Tag, so converting to a struct won't be beneficial
public class GitBlameContext(string fileName, int lineIndex, int blameLine, ObjectId blameId)
{
    public int LineIndex { get; } = lineIndex;
    public int BlameLine { get; } = blameLine;
    public ObjectId BlameId { get; } = blameId;
    public string FileName { get; } = fileName;
}
