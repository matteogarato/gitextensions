using CommonTestUtils;
using GitUI.CommandsDialogs;

namespace GitUITests.CommandsDialogs;

public sealed class FormFileHistoryControllerTests
{
    private readonly FormFileHistoryController _controller = null!;

    public FormFileHistoryControllerTests()
    {
        _controller = new FormFileHistoryController();
    }

    [Theory]
    [InlineData(@"Does not exist")]
    [InlineData("")]
    [InlineData(" ")]
    public void TryGetExactPathName_Should_return_null_on_not_existing_file(string path)
    {
        var lowercasePath = path.ToLower();
        var isExistingOnFileSystem = _controller.TryGetExactPath(lowercasePath, out var exactPath);

        isExistingOnFileSystem.Should().BeFalse();
        exactPath.Should().BeNull();
    }

    [Fact]
    public void TryGetExactPathName_Should_handle_network_path()
    {
        var path = @"\\" + Environment.MachineName.ToLower() + @"\c$\Windows\System32";

        var lowercasePath = path.ToLower();
        var isExistingOnFileSystem = _controller.TryGetExactPath(lowercasePath, out var exactPath);

        isExistingOnFileSystem.Should().BeTrue();

        exactPath.Should().Be(path);
    }

    [Theory]
    [InlineData("Folder1\\file1.txt", true, true)]
    [InlineData("FOLDER1\\file1.txt", true, false)]
    [InlineData("fOLDER1\\file1.txt", true, false)]
    [InlineData("Folder2\\file1.txt", false, false)]
    public void TryGetExactPathName_should_check_if_path_matches_case(string relativePath, bool isResolved,
        bool doesMatch)
    {
        using GitModuleTestHelper repo = new();

        // Create a file
        var notUsed = repo.CreateFile(Path.Combine(repo.TemporaryPath, "Folder1"), "file1.txt", "bla");

        var expected = Path.Combine(repo.TemporaryPath, relativePath);

        _controller.TryGetExactPath(expected, out var exactPath).Should().Be(isResolved);
        if (doesMatch)
        {
            exactPath.Should().Be(expected);
        }
        else
        {
            exactPath.Should().NotBe(expected);
        }
    }
}
