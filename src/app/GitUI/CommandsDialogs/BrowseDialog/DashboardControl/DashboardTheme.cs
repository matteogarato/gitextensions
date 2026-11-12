using GitExtUtils.GitUI.Theming;
using GitUI.Properties;

namespace GitUI.CommandsDialogs.BrowseDialog.DashboardControl;

internal sealed class DashboardTheme
{
    public static readonly DashboardTheme Light;
    public static readonly DashboardTheme Dark;

    static DashboardTheme()
    {
        // Palette URL: http://paletton.com/#uid=13I0u0k7UUa3cZA5wXlaiQ5cFL3
        Light = new DashboardTheme(Color.FromArgb(248, 248, 255),
            Color.FromArgb(219, 235, 248),
            Color.FromArgb(230, 241, 250),
            Color.FromArgb(172, 208, 239),
            Color.FromArgb(19, 122, 212),
            Color.FromArgb(30, 30, 30),
            Color.FromArgb(100, 127, 210),
            Color.DarkGoldenrod,
            Color.FromArgb(24, 29, 35),
            Color.DimGray,
            Images.DashboardBackgroundBlue);

        Dark = new DashboardTheme(SystemColors.Control,
            SystemColors.Control,
            SystemColors.ControlLight,
            SystemColors.ControlDark,
            SystemColors.ControlDarkDark,
            SystemColors.WindowText,
            Color.LightSkyBlue,
            Color.Goldenrod.AdaptBackColor(),
            SystemColors.ControlText,
            SystemColors.GrayText,
            Images.DashboardBackgroundGrey);
    }

    private DashboardTheme(Color searchBackColor, Color startBackColor, Color contributeBackColor,
        Color headerBackColor, Color logoBackColor,
        Color primaryText, Color secondaryText, Color accentedText,
        Color primaryHeadingText, Color secondaryHeadingText,
        Image backgroundImage)
    {
        SearchBackColor = searchBackColor;
        StartBackColor = startBackColor;
        ContributeBackColor = contributeBackColor;
        HeaderBackColor = headerBackColor;
        LogoBackColor = logoBackColor;
        PrimaryText = primaryText;
        SecondaryText = secondaryText;
        AccentedText = accentedText;
        PrimaryHeadingText = primaryHeadingText;
        SecondaryHeadingText = secondaryHeadingText;
        BackgroundImage = backgroundImage;
    }

    public Color AccentedText { get; }
    public Image BackgroundImage { get; }
    public Color SearchBackColor { get; }
    public Color HeaderBackColor { get; }
    public Color PrimaryHeadingText { get; }
    public Color StartBackColor { get; }
    public Color PrimaryText { get; }
    public Color LogoBackColor { get; }
    public Color ContributeBackColor { get; }
    public Color SecondaryHeadingText { get; }
    public Color SecondaryText { get; }
}
