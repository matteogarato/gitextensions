using GitExtUtils.GitUI.Theming;

namespace GitUITests.Theming;
public class WhatSystemColorsAreThemeableTests
{
    [Theory]
    [InlineData(KnownColor.ActiveBorder)]
    [InlineData(KnownColor.WindowText)]
    [InlineData(KnownColor.GradientActiveCaption)]
    [InlineData(KnownColor.MenuHighlight)]

    public void Themable_system_colors_should_contain_these_colors(KnownColor value)
    {
        Theme.TestAccessor.SysColorNames.Should().Contain(value);
    }

    [Theory]
    [InlineData(KnownColor.Transparent)]
    [InlineData(KnownColor.AliceBlue)]
    [InlineData(KnownColor.YellowGreen)]

    public void Themable_system_colors_should_not_contain_these_colors(KnownColor value)
    {
        Theme.TestAccessor.SysColorNames.Should().NotContain(value);
    }
}
