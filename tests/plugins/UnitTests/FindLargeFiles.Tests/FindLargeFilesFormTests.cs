using GitCommands.Settings;
using GitExtensions.Plugins.FindLargeFiles;

namespace DeleteUnusedBranchesTests;

public class FindLargeFilesFormTests
{
    [Fact]
    public async Task GenerateCommand_without_deletions_should_return_expected()
    {
        var original = AppSettings.GitCommandValue;
        try
        {
            AppSettings.GitCommandValue = @"C:\Program Files\Git\bin\git.exe";
            await Verify(FindLargeFilesForm.GetTestAccessor().GenerateCommand([]));
        }
        finally
        {
            AppSettings.GitCommandValue = original;
        }
    }

    [Fact]
    public async Task GenerateCommand_with_deletions_should_return_expected()
    {
        var original = AppSettings.GitCommandValue;
        try
        {
            AppSettings.GitCommandValue = @"C:\Program Files\Git\bin\git.exe";
            SortableObjectsList gitObjects =
            [
                new GitObject("sha1", "intune/packages/file.intunewin", 1, "commit") { Delete = true },
                new GitObject("sha1", "intune/packages/file with spaces.intunewin", 1, "commit") { Delete = true },
                new GitObject("sha1", "intune/packages/XL Upload/xl-upload.intunewin", 1, "commit") { Delete = true },
                new GitObject("sha1", "readme.md", 1, "commit")
            ];

            await Verify(FindLargeFilesForm.GetTestAccessor().GenerateCommand(gitObjects));
        }
        finally
        {
            AppSettings.GitCommandValue = original;
        }
    }
}
