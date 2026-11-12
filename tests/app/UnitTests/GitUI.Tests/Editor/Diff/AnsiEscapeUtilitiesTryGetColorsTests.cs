using GitUI.Editor.Diff;

namespace GitUITests.Editor.Diff;

public class AnsiEscapeUtilitiesTryGetColorsTests : AnsiEscapeUtilitiesTestBase
{
    private readonly Color _textColor = Color.FromArgb(0, 0, 0);

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldReset_WhenEscapeCodesIsEmpty()
    {
        // currentColorId should be preserved
        List<int> escapeCodes = [];
        var currentColorId = YellowId;

        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeFalse();
        backColor.Should().BeNull();
        foreColor.Should().BeNull();
        currentColorId.Should().Be(BlackId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBackColorAndForeColorToNull_WhenEscapeCodeIs0()
    {
        // currentColorId should be reset
        List<int> escapeCodes = [0];
        var currentColorId = YellowId;

        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeFalse();
        backColor.Should().BeNull();
        foreColor.Should().BeNull();
        currentColorId.Should().Be(BlackId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBoldAndForeColor_WhenEscapeCodeIs1()
    {
        // currentColorId should be unchanged, just bold
        List<int> escapeCodes = [1];
        var currentColorId = RedId;

        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().BeNull();
        foreColor.Should().Be(GetAnsiColor(bold: true));
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBoldAndForeColor_WhenEscapeCodeIsx1()
    {
        // currentColorId should be unchanged, just bold
        List<int> escapeCodes = [1];
        var currentColorId = RedId;

        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().BeNull();
        foreColor.Should().Be(GetAnsiColor(bold: true));
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBold()
    {
        // Some special cases, not fully specified
        var currentColorId = YellowId;

        List<int> escapeCodes = [0, 91];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().BeNull();
        foreColor.Should().Be(GetAnsiColor(bold: true));
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBoldBold()
    {
        // Some special cases, not fully specified
        var currentColorId = YellowId;

        List<int> escapeCodes = [1, 91];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().BeNull();
        foreColor.Should().Be(GetAnsiColor(bold: true, bright: true));
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBoldDim()
    {
        // Some special cases, not fully specified
        var currentColorId = YellowId;

        List<int> escapeCodes = [1, 2, 31];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().BeNull();
        foreColor.Should().Be(GetAnsiColor(bold: true, dim: true, bright: true));
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBoldDim2()
    {
        // Some special cases, not fully specified
        var currentColorId = YellowId;

        List<int> escapeCodes = [2, 91];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().BeNull();
        foreColor.Should().Be(GetAnsiColor(bold: true, dim: true));
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBackBoldBold()
    {
        // Some special cases, not fully specified
        var currentColorId = YellowId;

        List<int> escapeCodes = [1, 101];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().Be(GetAnsiColor(fore: false, bold: true, bright: true));
        currentColorId.Should().Be(YellowId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetBackDimBold()
    {
        // Some special cases, not fully specified
        var currentColorId = YellowId;

        List<int> escapeCodes = [1, 2, 101];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId);

        result.Should().BeTrue();
        backColor.Should().Be(GetAnsiColor(fore: false, bold: true, dim: true, bright: true));
        currentColorId.Should().Be(YellowId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldKeepThemeDim()
    {
        // Some special cases, not fully specified
        var currentColorId = YellowId;

        List<int> escapeCodes = [1, 2, 31];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId, true);

        result.Should().BeTrue();
        backColor.Should().BeNull();
        foreColor.Should().Be(GetAnsiColor(bold: true, dim: true));
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetThemeBold()
    {
        // Adjusting difftastic colors
        var currentColorId = YellowId;

        List<int> escapeCodes = [1, 31];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId, true);

        result.Should().BeTrue();
        backColor.Should().Be(GetAnsiColor(fore: false));
        foreColor.Should().Be(_textColor);
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetThemeBold2()
    {
        // Adjusting difftastic colors
        var currentColorId = YellowId;

        List<int> escapeCodes = [1, 91];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId, true);

        result.Should().BeTrue();
        backColor.Should().Be(GetAnsiColor(fore: false, bold: true));
        foreColor.Should().Be(_textColor);
        currentColorId.Should().Be(RedId);
    }

    [Fact]
    public void TryGetColorsFromEscapeSequence_ShouldSetThemeNormal()
    {
        // Adjusting difftastic colors
        var currentColorId = YellowId;

        List<int> escapeCodes = [0, 31];
        var result = AnsiEscapeUtilities.TestAccessor.TryGetColorsFromEscapeSequence(escapeCodes, out var backColor,
            out var foreColor, ref currentColorId, true);

        result.Should().BeTrue();
        backColor.Should().Be(GetAnsiColor(fore: false, dim: true));
        foreColor.Should().Be(_textColor);
        currentColorId.Should().Be(RedId);
    }
}
