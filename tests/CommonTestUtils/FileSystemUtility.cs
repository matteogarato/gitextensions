namespace CommonTestUtils;

internal class FileSystemUtility
{
    public static string GetTemporaryPath()
    {
        var tempPath = Path.GetTempPath();

        // workaround macOS symlinking its temp folder
        if (tempPath.StartsWith("/var"))
        {
            tempPath = "/private" + tempPath;
        }

        return Path.Combine(tempPath, Path.GetRandomFileName());
    }
}
