using GitUI;

namespace GitExtUtilsTests;
public class Win32ApiTests
{
    [Theory]
    [MemberData(nameof(PointCoordinates))]
    public void LParam_to_Point_restores_original_point(
        short x,
        short y)
    {
        Point point = ToLParam(x, y).ToPoint();

        point.X.Should().Be(x);
        point.Y.Should().Be(y);
    }

    [Theory]
    [MemberData(nameof(PointCoordinates))]
    public void LParam_to_point_in_ICSharpCode_restores_original_point(
        short x,
        short y)
    {
        Point point = ICSharpCode.TextEditor.Util.Win32Util.ToPoint(ToLParam(x, y));

        point.X.Should().Be(x);
        point.Y.Should().Be(y);
    }

    public static IEnumerable<object[]> PointCoordinates
    {
        get
        {
            short[] coordinateValues = [short.MinValue, -1, 0, 1, short.MaxValue];
            foreach (short x in coordinateValues)
            {
                foreach (short y in coordinateValues)
                {
                    yield return new object[] { x, y };
                }
            }
        }
    }

    private static IntPtr ToLParam(short x, short y) =>
        new(
            x & 0x0000_FFFF |
            (y & 0x0000_FFFF) << 16);
}
