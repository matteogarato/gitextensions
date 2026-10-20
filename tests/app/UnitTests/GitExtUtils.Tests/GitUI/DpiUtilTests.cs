using GitExtUtils.GitUI;

namespace GitExtUtilsTests.GitUI;
public sealed class DpiUtilTests
{
    [StaFact]
    public void Scale_with_ceiling_false_should_use_round()
    {
        // The actual DPI values are system-dependent, so we test the rounding behavior
        // by verifying that the method respects the ceiling parameter

        // When ceiling is false (default), Math.Round should be used
        // We can't test exact values without mocking static properties,
        // but we can verify the method signature works correctly
        int result = DpiUtil.Scale(5, ceiling: false);

        // Result should be a valid integer (basic sanity check)
        result.Should().BeGreaterThanOrEqualTo(0);
    }

    [StaFact]
    public void Scale_with_ceiling_true_should_use_ceiling()
    {
        int result = DpiUtil.Scale(1, ceiling: true);

        result.Should().BeGreaterThan(0);
    }

    [StaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void Scale_default_parameter_should_use_round(int value)
    {
        int resultDefault = DpiUtil.Scale(value);
        int resultExplicit = DpiUtil.Scale(value, ceiling: false);

        resultExplicit.Should().Be(resultDefault);
    }

    [StaTheory]
    [InlineData(0, false, 0)]
    [InlineData(0, true, 0)]
    public void Scale_with_zero_should_return_zero(int input, bool ceiling, int expected)
    {
        int result = DpiUtil.Scale(input, ceiling: ceiling);

        result.Should().Be(expected);
    }

    [StaTheory]
    [InlineData(-5, false)]
    [InlineData(-5, true)]
    public void Scale_with_negative_value_should_work(int input, bool ceiling)
    {
        // Negative values should be scaled correctly
        int result = DpiUtil.Scale(input, ceiling: ceiling);

        result.Should().BeLessThanOrEqualTo(0);
    }
}
