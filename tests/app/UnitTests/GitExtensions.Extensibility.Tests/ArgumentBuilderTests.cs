using GitExtensions.Extensibility;

namespace GitExtensions.ExtensibilityTests;

public sealed class ArgumentBuilderTests
{
    [Fact]
    public void Adds_simple_parameters()
    {
        Test(
            "",
            []);

        Test(
            "foo",
            ["foo"]);

        Test(
            "foo bar",
            ["foo", "bar"]);

        Test(
            "foo bar",
            ["foo", null, "bar"]);

        Test(
            "foo bar",
            ["foo", "", "bar"]);

        Test(
            "",
            [null]);

        static void Test(string expected, ArgumentBuilder command)
        {
            command.ToString().Should().Be(expected);
        }
    }

    [Fact]
    public void IsEmpty()
    {
        ArgumentBuilder builder = [];
        builder.IsEmpty.Should().BeTrue();

        builder.Add("test");
        builder.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Length()
    {
        ArgumentBuilder builder = [];
        builder.GetTestAccessor().Arguments.Length.Should().Be(0);

        builder.Add("test");
        builder.GetTestAccessor().Arguments.Length.Should().Be(4);

        var args = "Lorem ipsum dolor sit amet, solet soleat option mel no.";
        var expectedLength = args.Length;
        builder.AddRange(args.LazySplit(' '));
        builder.GetTestAccessor().Arguments.Length.Should().Be(expectedLength + /* 'test ' */5);
    }

    [Theory]
    [InlineData(new[] { (string?)null }, 0, "")]
    [InlineData(new[] { "" }, 0, "")]
    [InlineData(new[] { "", null }, 0, "")]
    [InlineData(new[] { "test" }, 4, "test")]
    [InlineData(new[] { "test", "test" }, 9, "test test")]
    [InlineData(new[] { "", "test" }, 4, "test")]
    [InlineData(new[] { "test", null, "test" }, 9, "test test")]
    public void Add(string[] args, int expectedLength, string expected)
    {
        ArgumentBuilder builder = [.. args];

        builder.GetTestAccessor().Arguments.Length.Should().Be(expectedLength);
        builder.ToString().Should().Be(expected);
    }
}
