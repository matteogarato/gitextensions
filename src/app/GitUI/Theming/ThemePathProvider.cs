using GitCommands.Settings;
using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming;

public interface IThemePathProvider
{
    string AppThemesDirectory { get; }

    string? UserThemesDirectory { get; }

    string ThemeExtension { get; }
    string GetThemePath(ThemeId id);
}

public class ThemePathProvider : IThemePathProvider
{
    private const string _subdirectory = "Themes";

    public ThemePathProvider()
    {
        var appDirectory = AppSettings.GetGitExtensionsDirectory() ??
                           throw new DirectoryNotFoundException("Application directory not found");
        AppThemesDirectory = Path.Join(appDirectory, _subdirectory);

        var userDirectory = AppSettings.ApplicationDataPath.Value;

        // in portable version appDirectory and userDirectory are same,
        // hence we don't have a separate directory for user themes
        UserThemesDirectory = string.Equals(appDirectory, userDirectory, StringComparison.OrdinalIgnoreCase)
            ? null
            : Path.Join(userDirectory!, _subdirectory);

        ThemeExtension = ".css";
    }

    string IThemePathProvider.AppThemesDirectory { get; }

    string? IThemePathProvider.UserThemesDirectory { get; }

    string IThemePathProvider.ThemeExtension { get; }

    /// <exception cref="InvalidOperationException">
    ///     Attempt to resolve a custom theme from a %UserAppData% folder in a portable version.
    /// </exception>
    /// <exception cref="FileNotFoundException">Theme does not exist.</exception>
    string IThemePathProvider.GetThemePath(ThemeId id)
    {
        string path;
        if (id.IsBuiltin)
        {
            var name = id == ThemeId.DefaultLight ? ThemeId.InvariantThemeFileName : id.Name;
            path = Path.Join(AppThemesDirectory, name + ThemeExtension);
        }
        else
        {
            if (UserThemesDirectory is null)
            {
                throw new InvalidOperationException("Portable mode only supports local themes");
            }

            path = Path.Join(UserThemesDirectory, id.Name + ThemeExtension);
        }

        return path;
    }
}
