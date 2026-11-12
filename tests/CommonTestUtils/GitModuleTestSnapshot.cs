namespace CommonTestUtils;

public class GitModuleTestSnapshot
{
    private readonly List<(string FilePath, byte[] Content)> _files = [];
    private readonly List<string> _folders = [];
    private readonly string _repositoryName;

    public GitModuleTestSnapshot(string repositoryName, string path)
    {
        _repositoryName = repositoryName;

        DirectoryInfo sourcePath = new(path);

        PopulateFrom(sourcePath, "");
    }

    public GitModuleTestHelper Clone()
    {
        return CloneTo(FileSystemUtility.GetTemporaryPath());
    }

    public GitModuleTestHelper CloneTo(string path)
    {
        foreach (var folderPath in _folders)
        {
            Directory.CreateDirectory(Path.Combine(path, folderPath));
        }

        foreach ((var filePath, var content) in _files)
        {
            File.WriteAllBytes(Path.Combine(path, filePath), content);
        }

        return new GitModuleTestHelper(_repositoryName, path, true);
    }

    private void PopulateFrom(DirectoryInfo sourcePath, string relativePath)
    {
        foreach (var entry in sourcePath.EnumerateFileSystemInfos())
        {
            var entryPath = Path.Combine(relativePath, entry.Name);

            if (entry is DirectoryInfo subPath)
            {
                _folders.Add(entryPath);

                PopulateFrom(subPath, entryPath);
            }
            else
            {
                _files.Add((entryPath, File.ReadAllBytes(entry.FullName)));
            }
        }
    }
}
