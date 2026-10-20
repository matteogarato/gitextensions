using GitExtUtils.GitUI.Theming;

namespace GitExtUtilsTests;
public class ColorTransformationTests
{
    [Theory]
    [MemberData(nameof(WithinOriginalSegmentTestCases))]
    public void Within_original_segment_makes_linear_interpolation(
        double exampleOrig,
        double oppositeOrig,
        double example,
        double opposite,
        double alpha)
    {
        double transformed = ColorHelper.TestAccessor.Transform(
            (exampleOrig * (1 - alpha)) + (oppositeOrig * alpha),
            exampleOrig,
            oppositeOrig,
            example,
            opposite);

        double expected = (example * (1 - alpha)) + (opposite * alpha);
        transformed.Should().BeApproximately(expected, 0.001);
    }

    public static IEnumerable<object[]> WithinOriginalSegmentTestCases
    {
        get
        {
            double[] originalValues = [0d, 0.5d, 1d];
            double[] oppositeValues = [0.25d, 0.75d];

            foreach (double exampleOrig in originalValues)
            {
                foreach (double oppositeOrig in oppositeValues)
                {
                    foreach (double example in originalValues)
                    {
                        foreach (double opposite in oppositeValues)
                        {
                            foreach (double alpha in originalValues)
                            {
                                yield return new object[] { exampleOrig, oppositeOrig, example, opposite, alpha };
                            }
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0.4d, 0.7d, 0.6d, 0.8d, 0.2d, 0.3d)]
    [InlineData(0.4d, 0.7d, 0.6d, 1.0d, 0.2d, 0.3d)]
    [InlineData(0.4d, 0.7d, 0.6d, 0.0d, 0.2d, 0.8d)]
    [InlineData(0.4d, 0.7d, 0.6d, 0.8d, 0.1d, 0.15d)]
    [InlineData(0.4d, 0.7d, 0.6d, 1.0d, 0.1d, 0.15d)]
    [InlineData(0.4d, 0.7d, 0.6d, 0.0d, 0.1d, 0.9d)]
    [InlineData(0.6d, 0.3d, 0.4d, 0.2d, 0.8d, 0.7d)]
    [InlineData(0.6d, 0.3d, 0.4d, 0.0d, 0.8d, 0.7d)]
    [InlineData(0.6d, 0.3d, 0.4d, 1.0d, 0.8d, 0.2d)]
    [InlineData(0.6d, 0.3d, 0.4d, 0.2d, 0.9d, 0.85d)]
    [InlineData(0.6d, 0.3d, 0.4d, 0.0d, 0.9d, 0.85d)]
    [InlineData(0.6d, 0.3d, 0.4d, 1.0d, 0.9d, 0.1d)]
    public void Outside_original_segment_interpolates_between_boundary_and_segment(
        double exampleOrig, double oppositeOrig, double example, double opposite, double orig, double expected)
    {
        double transformed = ColorHelper.TestAccessor.Transform(
            orig,
            exampleOrig,
            oppositeOrig,
            example,
            opposite);

        transformed.Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData("#000000", 0.5f)]
    [InlineData("#757575", 0.5f)]
    [InlineData("#999999", 1.0f)] // write white on a lighter background with increased threshold
    public void Should_return_white_when_color_is_dark(string backgroundColor, float luminanceThreshold)
    {
        Color correspondingForeColor = ColorHelper.TestAccessor.GetContrastColor(ColorTranslator.FromHtml(backgroundColor), luminanceThreshold);

        correspondingForeColor.Should().Be(Color.White);
    }

    [Theory]
    [InlineData("#FFFFFF", 0.5f)]
    [InlineData("#999999", 0.5f)]
    [InlineData("#777777", 0.4f)] // write black on a lighter background with decreased threshold
    public void Should_return_black_when_color_is_light(string backgroundColor, float luminanceThreshold)
    {
        Color correspondingForeColor = ColorHelper.TestAccessor.GetContrastColor(ColorTranslator.FromHtml(backgroundColor), luminanceThreshold);

        correspondingForeColor.Should().Be(Color.Black);
    }
}
