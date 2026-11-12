using GitUI.Theming;

namespace GitUITests.Theming;

public class ThemePersistenceTests
{
    public static IEnumerable<object[]> ColorFormatCases()
    {
        yield return [Color.FromArgb(255, 0, 0), "#ff0000"];

        yield return [Color.FromArgb(0, 255, 0), "#00ff00"];

        yield return [Color.FromArgb(0, 0, 255), "#0000ff"];
    }

    [Theory]
    [MemberData(nameof(ColorFormatCases))]
    public void Should_format_color_in_css_rgb_hex_format(Color color, string expected)
    {
        ThemePersistence.TestAccessor.FormatColor(color).Should().Be(expected);
    }
}
