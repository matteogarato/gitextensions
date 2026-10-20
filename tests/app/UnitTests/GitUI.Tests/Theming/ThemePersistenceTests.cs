using GitUI.Theming;

namespace GitUITests.Theming;
public class ThemePersistenceTests
{
    public static IEnumerable<object[]> ColorFormatCases()
    {
        yield return [Color.FromArgb(red: 255, green: 0, blue: 0), "#ff0000"];

        yield return [Color.FromArgb(red: 0, green: 255, blue: 0), "#00ff00"];

        yield return [Color.FromArgb(red: 0, green: 0, blue: 255), "#0000ff"];
    }

    [Theory]
    [MemberData(nameof(ColorFormatCases))]
    public void Should_format_color_in_css_rgb_hex_format(Color color, string expected)
    {
        ThemePersistence.TestAccessor.FormatColor(color).Should().Be(expected);
    }
}
