using System.IO.Abstractions;

namespace GitCommands;

public interface ISshPathLocator
{
    string? GetSshFromGitDir(string gitBinDirectory);
}

public sealed class SshPathLocator(IFileSystem fileSystem) : ISshPathLocator
{
    public SshPathLocator()
        : this(new FileSystem())
    {
    }

    /// <summary>
    ///     Get ssh path from Git installation.
    ///     (Also used by plugins to get the OpenSSH path).
    /// </summary>
    /// <param name="gitBinDirectory">Git installation directory.</param>
    /// <returns>Path to ssh.exe or null.</returns>
    string? ISshPathLocator.GetSshFromGitDir(string gitBinDirectory)
    {
        if (string.IsNullOrEmpty(gitBinDirectory))
        {
            return null;
        }

        try
        {
            // gitBinDirectory will normally end with a directory separator
            // (at least this is what AppSettings.GitBinDir ensures),
            // but then GetParent() returns the same directory, only without the trailing separator
            var gitDirInfo = fileSystem.Directory.GetParent(gitBinDirectory.RemoveTrailingPathSeparator());
            if (gitDirInfo is null)
            {
                return null;
            }

            return fileSystem.Directory.EnumerateFiles(gitDirInfo.FullName, "ssh.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
