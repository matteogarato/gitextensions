namespace GitCommands.Git;

public class FileDeleteException(string fileName, Exception inner) : Exception(inner.Message, inner)
{
    /// <summary>
    ///     Gets the name of the file which could not be deleted.
    /// </summary>
    public string FileName { get; } = fileName;
}
