using System.Diagnostics;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

[DebuggerDisplay("GitItem( {" + nameof(FileName) + "} )")]
public class GitItem : IObjectGitItem
{
    public GitItem(int mode, GitObjectType objectType, ObjectId objectId, string name)
    {
        Mode = mode;
        ObjectType = objectType;
        ObjectId = objectId;
        FileName = Name = name;
    }

    public string FileName { get; set; }
    public int Mode { get; }

    ObjectId IGitItem.ObjectId { get; }
    GitObjectType IObjectGitItem.ObjectType { get; }
    string INamedGitItem.Name { get; }

    string IGitItem.Guid
    {
        get => ObjectId.ToString();
    }
}
