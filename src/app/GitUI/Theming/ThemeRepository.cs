using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming;

public interface IThemeRepository
{
    Theme GetTheme(ThemeId themeId, IReadOnlyList<string> variations);

    void Save(Theme theme);

    Theme GetInvariantTheme();

    IEnumerable<ThemeId> GetThemeIds();

    void Delete(ThemeId themeId);
}

public class ThemeRepository(IThemePersistence persistence, IThemePathProvider themePathProvider)
    : IThemeRepository
{
    private readonly IThemePersistence _persistence =
        persistence ?? throw new ArgumentNullException(nameof(persistence));

    private readonly IThemePathProvider _themePathProvider =
        themePathProvider ?? throw new ArgumentNullException(nameof(themePathProvider));

    public ThemeRepository()
        : this(
            new ThemePersistence(new ThemeLoader(new ThemeCssUrlResolver(new ThemePathProvider()),
                new ThemeFileReader())), new ThemePathProvider())
    {
    }

    Theme IThemeRepository.GetTheme(ThemeId themeId, IReadOnlyList<string> variations)
    {
        var themePath = _themePathProvider.GetThemePath(themeId);
        return _persistence.Load(themePath, themeId, variations);
    }

    void IThemeRepository.Save(Theme theme)
    {
        Directory.CreateDirectory(_themePathProvider.UserThemesDirectory!);
        _persistence.Save(theme, _themePathProvider.GetThemePath(theme.Id));
    }

    Theme IThemeRepository.GetInvariantTheme()
    {
        return GetTheme(ThemeId.DefaultLight, variations: []);
    }

    IEnumerable<ThemeId> IThemeRepository.GetThemeIds()
    {
        return GetBuiltinThemeIds().Concat(GetUserCustomizedThemeIds());
    }

    void IThemeRepository.Delete(ThemeId themeId)
    {
        if (themeId.IsBuiltin)
        {
            throw new InvalidOperationException("Only user-defined theme can be deleted");
        }

        var themePath = _themePathProvider.GetThemePath(themeId);
        File.Delete(themePath);
    }

    private IEnumerable<ThemeId> GetBuiltinThemeIds()
    {
        return new DirectoryInfo(_themePathProvider.AppThemesDirectory)
            .EnumerateFiles("*" + _themePathProvider.ThemeExtension, SearchOption.TopDirectoryOnly)
            .Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
            .Select(fileName => new ThemeId(fileName, true))
            .OrderBy(id => id.Name);
    }

    private IEnumerable<ThemeId> GetUserCustomizedThemeIds()
    {
        if (_themePathProvider.UserThemesDirectory is null
            || new DirectoryInfo(_themePathProvider.UserThemesDirectory) is not { } directory
            || !directory.Exists)
        {
            return [];
        }

        return directory
            .EnumerateFiles("*" + _themePathProvider.ThemeExtension, SearchOption.TopDirectoryOnly)
            .Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
            .Select(fileName => new ThemeId(fileName));
    }
}
