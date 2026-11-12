using GitCommands.Settings;

namespace GitCommands.Utils;

public static class AutoCrlfExtensions
{
    public static string AdjustLineEndings(this string text, AutoCrlfType? autocrlf)
    {
        if (string.IsNullOrWhiteSpace(text) || autocrlf != AutoCrlfType.True)
        {
            return text;
        }

        if (text.Contains("\r\n"))
        {
            // AutoCRLF is set to true but the text contains windows endings.
            // Maybe the user that committed the file had another AutoCRLF setting.
            return text.Replace("\r\n", Environment.NewLine);
        }

        if (text.Contains('\r'))
        {
            // Old MAC lines (pre OS X). See "if (text.Contains("\r\n"))" above.
            return text.Replace("\r", Environment.NewLine);
        }

        return text.Replace("\n", Environment.NewLine);
    }
}
