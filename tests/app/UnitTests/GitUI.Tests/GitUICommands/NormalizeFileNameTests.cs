using GitCommands.Git;
using GitUI;

namespace GitUITests.GitUICommands;

public sealed class NormalizeFileNameTests
{
    [Theory]
    [InlineData(@"file", "file")]
    [InlineData(@"file.ext", "file.ext")]
    [InlineData(@"path\file.ext", "path/file.ext")]
    [InlineData(@"path/file.ext", "path/file.ext")]
    [InlineData(@"c:\path\file.ext", "c:/path/file.ext")]
    [InlineData(@"c:\path/file.ext", "c:/path/file.ext")]
    [InlineData(@"c:/path/file.ext", "c:/path/file.ext")]
    [InlineData(@"c:\working\dir\path\file.ext", "path/file.ext")]
    [InlineData(@"c:/working/dir/path/file.ext", "path/file.ext")]
    [InlineData(@"C:\working\dir\path\file.ext", "C:/working/dir/path/file.ext")]
    [InlineData(@"C:/working/dir/path/file.ext", "C:/working/dir/path/file.ext")]
    public void NormalizeFileNameTest(string fileName, string expected)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), @"c:\working\dir");
        GitUiCommands commands = new(GitUiCommands.EmptyServiceProvider, module);

        commands.GetTestAccessor().NormalizeFileName(fileName).Should().Be(expected);
    }
}
