using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming;

public interface IThemeCssUrlResolver
{
    string ResolveCssUrl(string url);
}

public class ThemeCssUrlResolver(IThemePathProvider themePathProvider) : IThemeCssUrlResolver
{
    private const string _cssVariableUserThemesDirectory = "{UserAppData}/";

    string IThemeCssUrlResolver.ResolveCssUrl(string url)
    {
        if (url.EndsWith(themePathProvider.ThemeExtension, StringComparison.OrdinalIgnoreCase))
        {
            url = url[..^themePathProvider.ThemeExtension.Length];
        }

        var id = url.StartsWith(_cssVariableUserThemesDirectory)
            ? new ThemeId(url[_cssVariableUserThemesDirectory.Length..])
            : new ThemeId(url, true);

        try
        {
            return themePathProvider.GetThemePath(id);
        }
        catch (Exception ex)
        {
            throw new ThemeCssUrlResolverException(ex.Message, ex);
        }
    }
}
