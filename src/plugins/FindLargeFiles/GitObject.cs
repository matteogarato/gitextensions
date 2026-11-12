namespace GitExtensions.Plugins.FindLargeFiles;

public class GitObject(string sha, string path, int size, string commit)
{
    public string Sha { get; set; } = sha;
    public string Path { get; set; } = path;
    internal int SizeInBytes { get; } = size;

    public string Size
    {
        get => string.Format("{0:F2} Mb", SizeInBytes / 1024.0f / 1024);
    }

    internal int CompressedSizeInBytes { get; set; } = -1;

    public string CompressedSize
    {
        get => CompressedSizeInBytes >= 0
            ? string.Format("{0:F2} Mb", CompressedSizeInBytes / 1024.0f / 1024)
            : "<Unknown>";
    }

    public int CommitCount
    {
        get => Commit.Count;
    }

    public DateTime LastCommitDate { get; set; }
    public bool Delete { get; set; }
    internal HashSet<string> Commit { get; } = [commit];
}
