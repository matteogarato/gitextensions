using GitCommands;

namespace GitCommandsTests.Helpers;
public class PathUtilTest
{
    [Theory]
    [InlineData('a', true)]
    [InlineData('z', true)]
    [InlineData('A', true)]
    [InlineData('Z', true)]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('-', true)]
    [InlineData('_', true)]
    [InlineData('.', true)]
    [InlineData('/', true)]
    [InlineData('!', true)]
    [InlineData('}', true)]
    [InlineData('@', true)]
    [InlineData(' ', false)]
    [InlineData('~', false)]
    [InlineData('^', false)]
    [InlineData(':', false)]
    [InlineData('"', false)]
    [InlineData('<', false)]
    [InlineData('>', false)]
    [InlineData('|', false)]
    [InlineData('\0', false)]
    [InlineData('\t', false)]
    [InlineData('\n', false)]
    [InlineData('\r', false)]
    [InlineData('\x7F', false)]
    public void IsValidPathChar_should_return_expected(char c, bool expected)
    {
        PathUtil.IsValidPathChar(c).Should().Be(expected);
    }

    [Fact]
    public void IsValidPathChar_should_return_false_for_all_invalid_path_chars()
    {
        foreach (char c in Path.GetInvalidPathChars())
        {
            PathUtil.IsValidPathChar(c).Should().BeFalse($"character U+{(int)c:X4} should be invalid");
        }
    }

    [Fact]
    public void IsValidPathChar_should_accept_all_ascii_letters_and_digits()
    {
        for (char c = 'a'; c <= 'z'; c++)
        {
            PathUtil.IsValidPathChar(c).Should().BeTrue();
        }

        for (char c = 'A'; c <= 'Z'; c++)
        {
            PathUtil.IsValidPathChar(c).Should().BeTrue();
        }

        for (char c = '0'; c <= '9'; c++)
        {
            PathUtil.IsValidPathChar(c).Should().BeTrue();
        }
    }

    [Fact]
    public void ToPosixPathTest()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            "C:/Work/GitExtensions/".Should().Be("C:/Work/GitExtensions/".ToPosixPath());
            "C:/Work/GitExtensions/".Should().Be("C:\\Work\\GitExtensions\\".ToPosixPath());
        }
        else
        {
            "C:/Work/GitExtensions/".Should().Be("C:/Work/GitExtensions/".ToPosixPath());
            "C:\\Work\\GitExtensions\\".Should().Be("C:\\Work\\GitExtensions\\".ToPosixPath());
            "/var/tmp/".Should().Be("/var/tmp/".ToPosixPath());
        }
    }

    [Fact]
    public void ToNativePathTest()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            "C:\\Work\\GitExtensions\\".Should().Be("C:\\Work\\GitExtensions\\".ToNativePath());
            "C:\\Work\\GitExtensions\\".Should().Be("C:/Work/GitExtensions/".ToNativePath());
            "\\\\my-pc\\Work\\GitExtensions\\".Should().Be("\\\\my-pc\\Work\\GitExtensions\\".ToNativePath());
        }
        else
        {
            "C:\\Work\\GitExtensions\\".Should().Be("C:\\Work\\GitExtensions\\".ToNativePath());
            "/Work/GitExtensions/".Should().Be("/Work/GitExtensions/".ToNativePath());
            "//server/share/".Should().Be("//server/share/".ToNativePath());
        }
    }

    [Fact]
    public void ToWslPathTest()
    {
        PathUtil.ToWslPath(null).Should().BeNull();
        "/mnt/c/Work/GitExtensions/".Should().Be(@"C:/Work/GitExtensions/".ToWslPath());
        "/mnt/c/Work/GitExtensions/".Should().Be(@"C:\Work\GitExtensions\".ToWslPath());
        "/var/tmp/".Should().Be(@"/var/tmp/".ToWslPath());
    }

    [Fact]
    public void ToCygwinPathTest()
    {
        PathUtil.ToCygwinPath(null).Should().BeNull();
        "/cygdrive/c/Work/GitExtensions/".Should().Be(@"C:/Work/GitExtensions/".ToCygwinPath());
        "/cygdrive/c/Work/GitExtensions/".Should().Be(@"C:\Work\GitExtensions\".ToCygwinPath());
        "/var/tmp/".Should().Be(@"/var/tmp/".ToCygwinPath());
    }

    [Fact]
    public void ToMountPathTest()
    {
        const string prefix = "PrEfiX";
        PathUtil.ToMountPath(null, prefix).Should().BeNull();
        "".Should().Be(@"".ToMountPath(prefix));
        "C".Should().Be(@"C".ToMountPath(prefix));
        $".:".Should().Be(@".:".ToMountPath(prefix));
        $"{prefix}c".Should().Be(@"C:".ToMountPath(prefix));
        $"{prefix}c_".Should().Be(@"C:_".ToMountPath(prefix));
        $"{prefix}c/".Should().Be(@"C:\".ToMountPath(prefix));
        $"{prefix}c/".Should().Be(@"C:/".ToMountPath(prefix));
        $"{prefix}c/folder".Should().Be(@"C:\folder".ToMountPath(prefix));
        $"{prefix}c/Work/GitExtensions/".Should().Be(@"C:/Work/GitExtensions/".ToMountPath(prefix));
        $"{prefix}c/Work/GitExtensions/".Should().Be(@"C:\Work\GitExtensions\".ToMountPath(prefix));
        "/var/tmp/".Should().Be(@"/var/tmp/".ToMountPath(prefix));
    }

    [Fact]
    public void EnsureTrailingPathSeparatorTest()
    {
        ((string?)null).EnsureTrailingPathSeparator().Should().BeNull();
        "".Should().Be("".EnsureTrailingPathSeparator());

        if (Path.DirectorySeparatorChar == '\\')
        {
            "C\\".Should().Be("C".EnsureTrailingPathSeparator());
            "C:\\".Should().Be("C:".EnsureTrailingPathSeparator());
            "C:\\".Should().Be("C:\\".EnsureTrailingPathSeparator());
            "C:\\Work\\GitExtensions\\".Should().Be("C:\\Work\\GitExtensions".EnsureTrailingPathSeparator());
            "C:\\Work\\GitExtensions\\".Should().Be("C:\\Work\\GitExtensions\\".EnsureTrailingPathSeparator());
            "C:/Work/GitExtensions/".Should().Be("C:/Work/GitExtensions/".EnsureTrailingPathSeparator());
            "\\".Should().Be("\\".EnsureTrailingPathSeparator());
            "/".Should().Be("/".EnsureTrailingPathSeparator());
        }
        else
        {
            "/".Should().Be("/".EnsureTrailingPathSeparator());
            "/Work/GitExtensions/".Should().Be("/Work/GitExtensions".EnsureTrailingPathSeparator());
            "/Work/GitExtensions/".Should().Be("/Work/GitExtensions/".EnsureTrailingPathSeparator());
            "/Work/GitExtensions\\/".Should().Be("/Work/GitExtensions\\".EnsureTrailingPathSeparator());
        }
    }

    [Fact]
    public void RemoveTrailingPathSeparatorTest()
    {
        ((string?)null).RemoveTrailingPathSeparator().Should().BeNull();
        "".Should().Be("".RemoveTrailingPathSeparator());

        char s = Path.DirectorySeparatorChar;

        "C:".Should().Be($"C:{s}".RemoveTrailingPathSeparator());
        "foo".Should().Be("foo".RemoveTrailingPathSeparator());
        "foo".Should().Be($"foo{s}".RemoveTrailingPathSeparator());
        $"foo{s}bar".Should().Be($"foo{s}bar".RemoveTrailingPathSeparator());
        $"foo{s}bar".Should().Be($"foo{s}bar{s}".RemoveTrailingPathSeparator());

        "foo".Should().Be("foo/".RemoveTrailingPathSeparator());
        "foo/bar".Should().Be("foo/bar".RemoveTrailingPathSeparator());
        "foo/bar".Should().Be("foo/bar/".RemoveTrailingPathSeparator());
    }

    [Fact]
    public void IsLocalFileTest()
    {
        true.Should().Be(PathUtil.IsLocalFile("\\\\my-pc\\Work\\GitExtensions"));
        true.Should().Be(PathUtil.IsLocalFile("//my-pc/Work/GitExtensions"));
        true.Should().Be(PathUtil.IsLocalFile("C:\\Work\\GitExtensions"));
        true.Should().Be(PathUtil.IsLocalFile("C:\\Work\\GitExtensions\\"));
        true.Should().Be(PathUtil.IsLocalFile("/Work/GitExtensions"));
        true.Should().Be(PathUtil.IsLocalFile("/Work/GitExtensions/"));
        false.Should().Be(PathUtil.IsLocalFile("ssh://domain\\user@serverip/cache/git/something/something.git"));
    }

    [Fact]
    public void GetFileNameTest()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            "GitExtensions".Should().Be(PathUtil.GetFileName("\\\\my-pc\\Work\\GitExtensions"));
            "GitExtensions".Should().Be(PathUtil.GetFileName("C:\\Work\\GitExtensions"));
            "".Should().Be(PathUtil.GetFileName("C:\\Work\\GitExtensions\\"));
        }
        else
        {
            "GitExtensions".Should().Be(PathUtil.GetFileName("//my-pc/Work/GitExtensions"));
            "GitExtensions".Should().Be(PathUtil.GetFileName("/Work/GitExtensions"));
            "".Should().Be(PathUtil.GetFileName("/Work/GitExtensions/"));
        }
    }

    [Fact]
    public void GetRepositoryNameTest()
    {
        "gitextensions".Should().Be(PathUtil.GetRepositoryName("https://github.com/gitextensions/gitextensions.git"));
        "gitextensions".Should().Be(PathUtil.GetRepositoryName("https://github.com/jeffqc/gitextensions"));
        "test".Should().Be(PathUtil.GetRepositoryName("git://mygitserver/git/test.git"));
        "test".Should().Be(PathUtil.GetRepositoryName("ssh://mygitserver/git/test.git"));
        "test".Should().Be(PathUtil.GetRepositoryName("ssh://john.doe@mygitserver/git/test.git"));
        "MyAwesomeRepo".Should().Be(PathUtil.GetRepositoryName("ssh://john-abraham.doe@mygitserver/git/MyAwesomeRepo.git"));
        "somerepo".Should().Be(PathUtil.GetRepositoryName("git@anotherserver.mysubnet.com:project/somerepo.git"));
        "somerepo".Should().Be(PathUtil.GetRepositoryName("http://anotherserver.mysubnet.com/project/somerepo.git"));
        "Hello Günter".Should().Be(PathUtil.GetRepositoryName("http://anotherserver.mysubnet.com/project/Hello+G%C3%BCnter.git"));
        "Hello Günter".Should().Be(PathUtil.GetRepositoryName("ssh://anotherserver.mysubnet.com/project/Hello+G%C3%BCnter.git"));
        "Hello Günter".Should().Be(PathUtil.GetRepositoryName("git://anotherserver.mysubnet.com/project/Hello+G%C3%BCnter.git"));
        "Hello Günter".Should().Be(PathUtil.GetRepositoryName("git@anotherserver.mysubnet.com:project/Hello+G%C3%BCnter.git"));

        "".Should().Be(PathUtil.GetRepositoryName(""));
        "".Should().Be(PathUtil.GetRepositoryName(null));
        if (Path.DirectorySeparatorChar == '\\')
        {
            "my_repo".Should().Be(PathUtil.GetRepositoryName(@"C:\dev\my_repo"));
            "Hello+G%C3%BCnter".Should().Be(PathUtil.GetRepositoryName(@"C:\dev\Hello+G%C3%BCnter"));
            "gitextensions".Should().Be(PathUtil.GetRepositoryName(@"\\networkshare\folder1\folder2\gitextensions"));
        }
        else
        {
            "my_repo".Should().Be(PathUtil.GetRepositoryName(@"/dev/my_repo"));
            "Hello+G%C3%BCnter".Should().Be(PathUtil.GetRepositoryName(@"/dev/Hello+G%C3%BCnter"));
            "gitextensions".Should().Be(PathUtil.GetRepositoryName(@"//networkshare/folder1/folder2/gitextensions"));
        }
    }

    [Theory(SkipUnless = nameof(GitCommandsTests.PlatformTestConditions.IsWindows), SkipType = typeof(GitCommandsTests.PlatformTestConditions))]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" ", "")]
    [InlineData("c:", "")]
    [InlineData("C:\\", "C:\\")]
    [InlineData("a:\\folder\\filename.txt", "a:\\folder\\filename.txt")]
    [InlineData("a:\\folder\\..\\filename.txt", "a:\\filename.txt")]
    [InlineData("file:///C:/Test%20Project.exe", "C:\\Test Project.exe")]
    [InlineData("C:\\Progra~1\\", "C:\\Program Files\\")]
    [InlineData("C:\\Progra~1", "C:\\Program Files")]
    [InlineData("\\\\folder\\filename.txt", "\\\\folder\\filename.txt")]
    [InlineData("a:\\\\folder/filename.txt", "a:\\folder\\filename.txt")]
    [InlineData(@"c:\folder#\filename.txt", @"c:\folder#\filename.txt")]
    [InlineData(@"C:\WORK\..\WORK\.\GitExtensions\", @"C:\WORK\GitExtensions\")]
    [InlineData(@"\\my-pc\Work\.\GitExtensions\", @"\\my-pc\Work\GitExtensions\")]
    [InlineData(@"\\wsl$\Ubuntu\home\jack\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    [InlineData(@"\\Wsl$\Ubuntu\home\jack\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    [InlineData(@"\\w$\work\", "")]
    public void NormalizePath(string? path, string expected)
    {
        PathUtil.NormalizePath(path!).Should().Be(expected);
    }

    [Theory(SkipUnless = nameof(GitCommandsTests.PlatformTestConditions.IsWindows), SkipType = typeof(GitCommandsTests.PlatformTestConditions))]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    [InlineData("c:", "c:")]
    [InlineData("C:\\", "C:\\")]
    [InlineData(@"\\wsl$\Ubuntu\home\jack\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    [InlineData(@"\\Wsl.LoCALhosT\Ubuntu\home\jack\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\jack\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    public void NormalizeWslPath(string? path, string expected)
    {
        PathUtil.NormalizeWslPath(path!).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\WORK\GitExtensions\", @"C:\WORK\GitExtensions\")]
    [InlineData(@"\\my-pc\Work\GitExtensions\", @"\\my-pc\Work\GitExtensions\")]
    [InlineData(@"\\wsl$\Ubuntu\home\jack\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    [InlineData(@"\\wsl$\Ubuntu\home\jack\.\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    [InlineData(@"\\WSL$\Ubuntu\home\jack\.\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    public void Resolve_should_return_expected_path(string? path, string expected)
    {
        PathUtil.Resolve(path!).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\WORK\", @"GitExtensions\", @"C:\WORK\GitExtensions\")]
    [InlineData(@"C:\WORK\", @" file .txt ", @"C:\WORK\ file .txt ")]
    [InlineData(@"\\wsl$\", @"Ubuntu\home\jack\work\", @"\\wsl$\Ubuntu\home\jack\work\")]
    public void Resolve_with_relative_path_should_return_expected_path(string? path, string relativePath, string expected)
    {
        PathUtil.Resolve(path!, relativePath).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\w$\work\", typeof(UriFormatException))]
    [InlineData(@":$\work\", typeof(UriFormatException))]
    [InlineData(@"C:", typeof(UriFormatException))]
    [InlineData(@"\\wsl$", typeof(UriFormatException))]
    [InlineData(null, typeof(ArgumentException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData(" ", typeof(ArgumentException))]
    public void Resolve_should_throw_expected_exception(string? input, Type expectedException)
    {
        Assert.Throws(expectedException, () => PathUtil.Resolve(input!));
    }

    [Theory]
    [InlineData(@"\\wsl$\Ubuntu\work\..\GitExtensions\", @"\\wsl$\Ubuntu\GitExtensions\")]
    [InlineData(@"\\Wsl$\Ubuntu\work\..\GitExtensions\", @"\\wsl$\Ubuntu\GitExtensions\")]
    [InlineData(@"\\WSL$\Ubuntu\work\..\GitExtensions\", @"\\wsl$\Ubuntu\GitExtensions\")]
    public void ResolveWsl_should_return_expected_path(string? path, string expected)
    {
        PathUtil.ResolveWsl(path!).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\w$\work\", typeof(ArgumentException))]
    [InlineData(@":$\work\", typeof(ArgumentException))]
    [InlineData(@"C:\work\", typeof(ArgumentException))]
    [InlineData(null, typeof(ArgumentException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData(" ", typeof(ArgumentException))]
    public void ResolveWsl_should_throw_expected_exception(string? input, Type expectedException)
    {
        Assert.Throws(expectedException, () => PathUtil.ResolveWsl(input!));
    }

    [Theory]
    [InlineData(@"C:\work\..\GitExtensions\", false)]
    [InlineData(@"\\Wsl$\Ubuntu\work\..\GitExtensions\", false)]
    [InlineData(@"\\wsl.localhost\Ubuntu\work\..\GitExtensions\", true)]
    [InlineData(@"\\wsl.localhost/Ubuntu\work\..\GitExtensions\", false)]
    public void IsWslLocalhostPath(string? path, bool expected)
    {
        PathUtil.TestAccessor.IsWslLocalhostPrefixPath(path!).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\Wsl$\Ubuntu\work\..\GitExtensions\", true, true)]
    [InlineData(@"\\wsl$\Ubuntu\work\..\GitExtensions\", true, true)]
    [InlineData(@"C:\work\..\GitExtensions\", false, false)]
    [InlineData(@"\\Wsl$/Ubuntu\work\..\GitExtensions\", false, false)]
    [InlineData(@"\\Wsl.localhost\GitExtensions\", true, false)]
    public void IsWslPath(string? path, bool expected, bool expectedPrefix)
    {
        PathUtil.IsWslPath(path).Should().Be(expected);
        PathUtil.TestAccessor.IsWslPrefixPath(path!).Should().Be(expectedPrefix);
    }

    [Theory]
    [InlineData(@"\\Wsl$\Ubuntu\work\..\GitExtensions\", "Ubuntu")]
    [InlineData(@"\\wsl$\Ubuntu/work/../GitExtensions", "Ubuntu")]
    [InlineData(@"\\wsl$\Ubuntu-20.04\work\..\GitExtensions\", "Ubuntu-20.04")]
    [InlineData(@"C:\work\..\GitExtensions\", "")]
    public void GetWslDistro(string? path, string expected)
    {
        PathUtil.GetWslDistro(path).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\wsl$/Ubuntu/work/../GitExtensions", "")]
    public void GetWslDistro_unexpected_usage(string? path, string expected)
    {
        PathUtil.GetWslDistro(path).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\wsl$\Ubuntu\work\..\GitExtensions\", "", @"//wsl$/Ubuntu/work/../GitExtensions/")]
    [InlineData(@"C:\work\..\GitExtensions\", "", @"C:/work/../GitExtensions/")]
    [InlineData(@"work\..\GitExtensions\", "", @"work/../GitExtensions/")]
    public void GetPathForGitExecution_GetWindowsPath_default(string? path, string wslDistro, string expected)
    {
        PathUtil.GetPathForGitExecution(path, wslDistro).Should().Be(expected);
        PathUtil.GetWindowsPath(expected, wslDistro).Should().Be(path);
    }

    [Theory]
    [InlineData(@"\\Wsl$\Ubuntu\work\..\GitExtensions\", "Ubuntu", @"/work/../GitExtensions/")]
    [InlineData(@"\\wsl$\Ubuntu\work/../GitExtensions", "Ubuntu", @"/work/../GitExtensions")]
    [InlineData(@"\\wsl$\Ubuntu-20.04\work\..\GitExtensions\", "Ubuntu-20.04", @"/work/../GitExtensions/")]
    [InlineData(@"C:\work\..\GitExtensions\", "Ubuntu", @"/mnt/c/work/../GitExtensions/")]
    [InlineData(@"work\..\GitExtensions\", "Ubuntu", @"work/../GitExtensions/")]
    public void GetPathForGitExecution_wsl(string? path, string wslDistro, string expected)
    {
        PathUtil.GetPathForGitExecution(path, wslDistro).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\wsl$/Ubuntu/work/../GitExtensions", "Ubuntu", @"//wsl$/Ubuntu/work/../GitExtensions")]
    [InlineData(@"\\wsl$\Ubuntu-20.04\work\..\GitExtensions\", "Ubuntu", @"//wsl$/Ubuntu-20.04/work/../GitExtensions/")]
    public void GetPathForGitExecution_unexpected_usage(string? path, string wslDistro, string expected)
    {
        PathUtil.GetPathForGitExecution(path, wslDistro).Should().Be(expected);
    }

    // Mostly opposite to GetRepoPath_wsl
    [Theory]
    [InlineData(@"\\wsl$\Ubuntu\work\..\GitExtensions\", "Ubuntu", @"/work/../GitExtensions/")]
    [InlineData(@"\\wsl$\Ubuntu\work\..\GitExtensions", "Ubuntu", @"/work/../GitExtensions")]
    [InlineData(@"\\wsl$\Ubuntu-20.04\work\..\GitExtensions\", "Ubuntu-20.04", @"/work/../GitExtensions/")]
    [InlineData(@"C:\work\..\GitExtensions\", "Ubuntu", @"/mnt/c/work/../GitExtensions/")]
    [InlineData(@"\\wsl$\Ubuntu\work\..\GitExtensions\", "Ubuntu", @"work/../GitExtensions/")]
    public void GetWindowsPath_wsl(string expected, string wslDistro, string? path)
    {
        PathUtil.GetWindowsPath(path, wslDistro).Should().Be(expected);
    }

    [Theory(SkipUnless = nameof(GitCommandsTests.PlatformTestConditions.IsWindows), SkipType = typeof(GitCommandsTests.PlatformTestConditions))]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a:\\folder\\filename.txt")]
    [InlineData("a:\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\filename.txt")]
    [InlineData("file:////folder/filename.txt")]
    [MemberData(nameof(GetInvalidPaths))]
    public void TryFindFullPath_not_throw_if_file_not_exist(string? fileName)
    {
        PathUtil.TryFindFullPath(fileName!, out _).Should().BeFalse();
    }

    public static IEnumerable<object?[]> GetInvalidPaths()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            yield return new object?[] { @"c::\word" };
            yield return new object?[] { "\"c:\\word\t\\\"" };
            yield return new object?[] { @".c:\Programs\" };
            yield return new object?[] { "c:\\Programs\\Get\"\\" };
        }
        else
        {
            // I am not able to figure out any invalid (giving exception) path under mono
        }
    }

    [Fact]
    public void GetDisplayPath()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        PathUtil.GetDisplayPath(Path.Combine(home, "SomePath")).Should().Be(@"~\SomePath");
        PathUtil.GetDisplayPath("c:\\SomePath").Should().Be("c:\\SomePath");
    }

    [Theory]
    [InlineData("/foo/bar", new[] { "\\foo\\", "\\" })]
    [InlineData("/foo/bar/", new[] { "\\foo\\", "\\" })]
    [InlineData("/foo", new[] { "\\" })]
    [InlineData("/foo/", new[] { "\\" })]
    [InlineData("/", new string[0])]
    [InlineData("C:\\foo\\bar", new[] { "C:\\foo\\", "C:\\" })]
    [InlineData("C:\\", new string[0])]
    public void FindAncestors(string? path, string[] expected)
    {
        PathUtil.FindAncestors(path!).ToArray().Should().Equal(expected);
    }

    public static IEnumerable<object?[]> InvalidFolders
    {
        get
        {
            yield return new object?[] { null };
            yield return new object?[] { "" };
            yield return new object?[] { @"A:\does\not\exist" };
        }
    }

    [Theory, MemberData(nameof(InvalidFolders))]
    public void DeleteWithExtremePrejudice_should_return_true_for_empty_input_or_absent_folders(string? path)
    {
        path.TryDeleteDirectory(out string? errorMessage).Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void DeleteWithExtremePrejudice_should_return_true_if_delete_successful()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);
        Directory.Exists(tempPath).Should().BeTrue();

        tempPath.TryDeleteDirectory(out string? errorMessage).Should().BeTrue();
        errorMessage.Should().BeNull();
        Directory.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public void TryFindShellPath_should_not_throw_for_nonexistent_shell()
    {
        // This exercises the null-safety guards in TryFindShellPath:
        // - GetEnvironmentVariable("ProgramW6432") may return null
        // - AppSettings.LinuxToolsDir may return ""
        // The method should handle all these gracefully without throwing.
        bool result = PathUtil.TryFindShellPath("nonexistent_shell_1234567890.exe", out string? shellPath);
        result.Should().BeFalse();
        shellPath.Should().BeNull();
    }

    [Theory]
    [InlineData("", "git-bash.exe")]
    [InlineData("bin", "sh.exe")]
    [InlineData("usr/bin", "bash.exe")]
    public void TryFindShellInGitDir_should_find_a_shell_shipped_with_git(string subDirectory, string shell)
    {
        string gitDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string shellDir = Path.Combine(gitDir, subDirectory.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            Directory.CreateDirectory(shellDir);
            string expected = Path.Combine(shellDir, shell);
            File.WriteAllText(expected, "");

            PathUtil.TryFindShellInGitDir(gitDir, shell, out string? shellPath).Should().BeTrue();
            shellPath.Should().Be(expected);
        }
        finally
        {
            if (Directory.Exists(gitDir))
            {
                Directory.Delete(gitDir, recursive: true);
            }
        }
    }

    [Fact]
    public void TryFindShellInGitDir_should_prefer_usr_bin_over_bin_for_the_same_shell_name()
    {
        // "bin\bash.exe" is a thin compatibility wrapper around the real MSYS2 bash shipped in
        // "usr\bin\bash.exe"; running mintty against the wrapper breaks its detection of when an
        // interactive git command has finished. Regression test for #13312.
        string gitDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string binDir = Path.Combine(gitDir, "bin");
        string usrBinDir = Path.Combine(gitDir, "usr", "bin");

        try
        {
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(usrBinDir);
            File.WriteAllText(Path.Combine(binDir, "bash.exe"), "");
            string expected = Path.Combine(usrBinDir, "bash.exe");
            File.WriteAllText(expected, "");

            PathUtil.TryFindShellInGitDir(gitDir, "bash.exe", out string? shellPath).Should().BeTrue();
            shellPath.Should().Be(expected);
        }
        finally
        {
            if (Directory.Exists(gitDir))
            {
                Directory.Delete(gitDir, recursive: true);
            }
        }
    }

    [Fact]
    public void TryFindShellInGitDir_should_not_find_a_shell_which_is_not_there()
    {
        string gitDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        PathUtil.TryFindShellInGitDir(gitDir, "sh.exe", out string? shellPath).Should().BeFalse();
        shellPath.Should().BeNull();
    }

    [Theory]
    [InlineData(@"C:\Users\Acker Liu\Git\bash.exe", @"""C:\Users\Acker Liu\Git\bash.exe""")]
    [InlineData(@"C:\Program Files\Git\bash.exe", @"""C:\Program Files\Git\bash.exe""")]
    [InlineData(@"C:\NoSpaces\bash.exe", @"""C:\NoSpaces\bash.exe""")]
    [InlineData(null, "")]
    public void Quote_should_handle_paths_with_spaces(string? path, string expected)
    {
        path.Quote().Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Users\Acker Liu\Git\bash.exe", @"""C:\Users\Acker Liu\Git\bash.exe""")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void QuoteNE_should_handle_paths_with_spaces(string? path, string? expected)
    {
        path.QuoteNE().Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("blah", false)]
    [InlineData("https://github.com/gitextensions/gitextensions", true)]
    [InlineData("https://github.com/gitextensions/gitextensions.git", true)]
    [InlineData("github.com/gitextensions/gitextensions.git", true)]
    [InlineData("HTTPS://MYPRIVATEGITHUB.COM:8080/LOUDREPO.GIT", true)]
    [InlineData("git://myurl/myrepo.git", true)]
    [InlineData("git@github.com:gitextensions/gitextensions.git", true)]
    [InlineData("git@github.com:gitextensions/gitextensions.git/", true)]
    [InlineData("https://github.com/gitextensions/gitextensions/issues/12210#issuecomment-2672903979", false)]
    [InlineData("https://github.com/gitextensions/gitextensions?query=param", false)]
    [InlineData("github.com/gitextensions", false)]
    [InlineData("github.com/gitextensions/gitextensions/pull/9018", false)]
    [InlineData("http://", false)]
    [InlineData("HTTPS://www", true)]
    [InlineData("git://", true)]
    [InlineData("file:", false)]
    [InlineData("SSH:", true)]
    [InlineData("SSH", false)]
    [InlineData("https://myhost:12368/", true)]
    public void CanBeGitURL(string? url, bool expected)
    {
        PathUtil.CanBeGitURL(url).Should().Be(expected);
    }
}
