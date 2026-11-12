using GitUI.Editor.Diff;

namespace GitUITests.Editor.Diff;

public class AnsiEscapeUtilitiesGet8BitColorTests : AnsiEscapeUtilitiesTestBase
{
    [Fact]
    public void Get8bitColor_ShouldReturnBuiltinThemeColors()
    {
        for (var colorId = 0; colorId < 8; ++colorId)
        {
            foreach (var fore in new List<bool> { true, false })
            {
                foreach (var bold in new List<bool> { true, false })
                {
                    foreach (var dim in new List<bool> { false, true })
                    {
                        // Only check the result for red, the other should just get a value
                        // (No need to maintain all ANSI theme colors in parallel)
                        var result = AnsiEscapeUtilities.TestAccessor.Get8BitColor(colorId, fore, bold, dim);
                        if (colorId != RedId)
                        {
                            continue;
                        }

                        result.Should().Be(GetAnsiColor(RedId, fore, bold, dim),
                            $"Failed for fore:{fore}, bold:{bold}, dim:{dim}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Get8bitColor_ShouldReturnCorrect6bitColor()
    {
        for (var i = 16; i < 232; ++i)
        {
            var result = AnsiEscapeUtilities.TestAccessor.Get8BitColor(i, true, false, false);
            result.Should().Be(GetExpectedColor(i));

            // sample specific colors only
            if (i == 196)
            {
                result.Should().Be(Color.FromArgb(255, 0, 0));
            }
            else if (i == 231)
            {
                result.Should().Be(Color.FromArgb(255, 255, 255));
            }
        }

        return;

        static Color GetExpectedColor(int level)
        {
            var i = level - 16;
            var blue = Get8BitFrom6Over3Bit(i % 6);
            var green = Get8BitFrom6Over3Bit(i % 36 / 6);
            var red = Get8BitFrom6Over3Bit(i / 36);

            return Color.FromArgb(red, green, blue);

            static int Get8BitFrom6Over3Bit(int color)
            {
                return color * 51;
            }
        }
    }

    [Fact]
    public void Get8bitColor_ShouldReturnCorrect4bitGrey()
    {
        for (var i = 232; i <= 255; ++i)
        {
            var result = AnsiEscapeUtilities.TestAccessor.Get8BitColor(i, true, false, false);
            result.Should().Be(Get24StepGray(i));

            // border
            if (i == 232)
            {
                result.Should().Be(Color.FromArgb(0, 0, 0));
            }
            else if (i == 255)
            {
                result.Should().Be(Color.FromArgb(253, 253, 253));
            }
        }

        return;

        static Color Get24StepGray(int level)
        {
            var i = (level - 232) * 11;
            return Color.FromArgb(i, i, i);
        }
    }

    [Fact]
    public void Get8bitColor_ShouldThrowException_WhenColorCodeIsOutOfRange()
    {
        var colorCode = 256;

        ((Action)(() => AnsiEscapeUtilities.TestAccessor.Get8BitColor(colorCode, true, false, false))).Should()
            .Throw<ArgumentOutOfRangeException>();
    }
}
