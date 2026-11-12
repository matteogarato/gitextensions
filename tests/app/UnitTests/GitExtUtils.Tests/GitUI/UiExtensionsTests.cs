using GitExtensions.Extensibility.Extensions;

namespace GitExtUtilsTests.GitUI;

public sealed class UiExtensionsTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("b", "n", "b\r\n\r\nNotes:\r\n    n")]
    [InlineData("b\r\nb2", "n\r\nn2", "b\r\nb2\r\n\r\nNotes:\r\n    n\r\n    n2")]
    [InlineData("b\r\nb2\r\n", "n\r\nn2", "b\r\nb2\r\n\r\n\r\nNotes:\r\n    n\r\n    n2")]
    [InlineData("b", "", "b")]
    [InlineData("b", "\t", "b\r\n\r\nNotes:\r\n    \t")]
    [InlineData("b", "  ", "b\r\n\r\nNotes:\r\n      ")]
    [InlineData("b", null, "b")]
    [InlineData("", "n", "\r\nNotes:\r\n    n")]
    public void FormatBodyAndNotes(string? body, string? notes, string? expected)
    {
        UiExtensions.FormatBodyAndNotes(body!, notes)?.ReplaceLineEndings("\r\n").Should().Be(expected);
        // under linux we`d get only \n newline sequences, leading to failing tests
    }
}
