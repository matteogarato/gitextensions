namespace GitUI.Theming;

public class ThemeCssUrlResolverException(string message, Exception innerException)
    : ThemeException(message, innerException);
