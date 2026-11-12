using GitCommands;

namespace GitCommandsTests;

public class PathEqualityComparerTests
{
    private readonly PathEqualityComparer _comparer = null!;

    public PathEqualityComparerTests()
    {
        _comparer = new PathEqualityComparer();
    }

    [Theory]
    [InlineData("C:\\WORK\\GitExtensions\\", "C:/Work/GitExtensions/")]
    [InlineData("\\\\my-pc\\Work\\GitExtensions\\", "//my-pc/WORK/GitExtensions/")]
    public void Equals_should_return_true_for_equivalent_paths(string input, string expected)
    {
        true.Should().Be(Equals(input, expected));
    }
}
