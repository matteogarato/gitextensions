using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming;

public interface IThemePersistence
{
    Theme Load(string themeFileName, ThemeId id, IReadOnlyList<string> variations);
    void Save(Theme theme, string themeFileName);
}

public class ThemePersistence(IThemeLoader themeLoader) : IThemePersistence
{
    private const string _format = ".{0} {{ color: {1}}}";

    Theme IThemePersistence.Load(string themeFileName, ThemeId themeId, IReadOnlyList<string> variations)
    {
        return themeLoader.LoadTheme(themeFileName, themeId, variations);
    }

    void IThemePersistence.Save(Theme theme, string themeFileName)
    {
        IThemeSerializationData serializationData = theme;
        var serialized = string.Join(
            Environment.NewLine,
            serializationData.SysColorValues.Select(_ => string.Format(_format, _.Key, FormatColor(_.Value))).Concat(
                serializationData.AppColorValues.Select(_ => string.Format(_format, _.Key, FormatColor(_.Value)))));

        File.WriteAllText(themeFileName, serialized);
    }

    private static string FormatColor(Color color)
    {
        var rgb = color.ToArgb() & 0x00_ff_ff_ff;
        return $"#{rgb:x6}";
    }

    internal static class TestAccessor
    {
        public static string FormatColor(Color color)
        {
            return ThemePersistence.FormatColor(color);
        }
    }
}
