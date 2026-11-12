using GitExtUtils.GitUI;
using ICSharpCode.TextEditor.Util;

namespace GitExtUtilsTests;

public class Win32ApiTests
{
    public static IEnumerable<object[]> PointCoordinates
    {
        get
        {
            short[] coordinateValues = [short.MinValue, -1, 0, 1, short.MaxValue];
            foreach (var x in coordinateValues)
            {
                foreach (var y in coordinateValues)
                {
                    yield return new object[] { x, y };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(PointCoordinates))]
    public void LParam_to_Point_restores_original_point(
        short x,
        short y)
    {
        var point = Win32ApiUtil.ToPoint(ToLParam(x, y));

        point.X.Should().Be(x);
        point.Y.Should().Be(y);
    }

    [Theory]
    [MemberData(nameof(PointCoordinates))]
    public void LParam_to_point_in_ICSharpCode_restores_original_point(
        short x,
        short y)
    {
        var point = Win32Util.ToPoint(ToLParam(x, y));

        point.X.Should().Be(x);
        point.Y.Should().Be(y);
    }

    private static IntPtr ToLParam(short x, short y)
    {
        return new IntPtr(
            (x & 0x0000_FFFF) |
            ((y & 0x0000_FFFF) << 16));
    }
}
