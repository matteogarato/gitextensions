using ResourceManager.CommitDataRenders;

namespace ResourceManagerTests.CommitDataRenders;
public class MonospacedHeaderLabelFormatterTests
{
    private MonospacedHeaderLabelFormatter _formatter = null!;

    public MonospacedHeaderLabelFormatterTests()
    {
        _formatter = new MonospacedHeaderLabelFormatter();
    }

    [Theory]
    [InlineData(null, 10, ":         ")]
    [InlineData("", 10, ":         ")]
    [InlineData(" ", 10, " :        ")]
    [InlineData("a", 10, "a:        ")]
    [InlineData("abc", 1, "abc:")]
    [InlineData("John Doe <John.Doe@test.com>", 40, "John Doe &lt;John.Doe@test.com&gt;:     ")]
    public void FormatLabel_should_render_correctly(string? given, int desiredLength, string expected)
    {
        _formatter.FormatLabel(given!, desiredLength).Should().Be(expected);
    }
}
