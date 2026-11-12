using System.IO.Abstractions;
using GitCommands.Git.Extensions;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git.Tag;

public interface IGitTagController
{
    /// <summary>
    ///     Create the Tag depending on input parameter.
    /// </summary>
    /// <param name="args">tag creation arguments</param>
    /// <param name="parentWindow">the UI window to act as the parent of the create tag dialog</param>
    /// <returns>the true if the tag is created.</returns>
    bool CreateTag(GitCreateTagArgs args, IWin32Window parentWindow);
}

public class GitTagController(IGitUiCommands uiCommands, IFileSystem fileSystem) : IGitTagController
{
    public GitTagController(IGitUiCommands uiCommands)
        : this(uiCommands, new FileSystem())
    {
    }

    /// <summary>
    ///     Create the Tag depending on input parameter.
    /// </summary>
    /// <param name="args">tag creation arguments</param>
    /// <param name="parentWindow">the UI window to act as the parent of the create tag dialog</param>
    /// <returns>the true if the tag is created.</returns>
    bool IGitTagController.CreateTag(GitCreateTagArgs args, IWin32Window parentWindow)
    {
        ArgumentNullException.ThrowIfNull(parentWindow);

        string? tagMessageFileName = null;
        if (args.Operation.CanProvideMessage())
        {
            tagMessageFileName = Path.Join(GetWorkingDirPath(), "TAGMESSAGE");
            fileSystem.File.WriteAllText(tagMessageFileName, args.TagMessage);
        }

        try
        {
            return uiCommands.StartCommandLineProcessDialog(parentWindow,
                Commands.CreateTag(args, tagMessageFileName, uiCommands.Module.GetPathForGitExecution));
        }
        finally
        {
            if (tagMessageFileName is not null && fileSystem.File.Exists(tagMessageFileName))
            {
                fileSystem.File.Delete(tagMessageFileName);
            }
        }
    }

    private string GetWorkingDirPath()
    {
        return uiCommands.Module.WorkingDir;
    }
}
