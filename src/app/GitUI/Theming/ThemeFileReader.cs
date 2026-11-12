namespace GitUI.Theming;

public interface IThemeFileReader
{
    string ReadThemeFile(string themeFileName);
}

public class ThemeFileReader : IThemeFileReader
{
    private const int _maxFileSize = 1024 * 1024;

    string IThemeFileReader.ReadThemeFile(string themeFileName)
    {
        FileInfo fileInfo = new(themeFileName);
        if (fileInfo.Exists && fileInfo.Length > _maxFileSize)
        {
            throw new ThemeException($"Theme file size exceeds {_maxFileSize:#,##0} bytes", themeFileName);
        }

        try
        {
            return File.ReadAllText(themeFileName);
        }
        catch (SystemException ex)
        {
            throw new ThemeException(ex.Message, themeFileName, ex);
        }
    }
}
