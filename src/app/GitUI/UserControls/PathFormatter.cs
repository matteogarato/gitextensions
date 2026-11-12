using System.Runtime.InteropServices;
using System.Text;
using GitCommands;
using GitCommands.Settings;
using GitExtUtils;

namespace GitUI.UserControls;

internal sealed class PathFormatter(Graphics graphics, Font font)
{
    private const TextFormatFlags _filePathStringFormat =
        TextFormatFlags.NoClipping |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.VerticalCenter |
        TextFormatFlags.TextBoxControl;

    private readonly Font _font = font ?? throw new ArgumentNullException(nameof(font));

    private readonly Graphics _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));

    public (string? prefix, string? text, string? suffix) FormatTextForDrawing(int maxWidth, string name,
        string? oldName)
    {
        string? prefix = null;
        var text = string.IsNullOrEmpty(name) ? "" : "...";
        string? suffix = null;

        switch (AppSettings.TruncatePathMethod)
        {
            case TruncatePathMethod.FileNameOnly:
                (text, suffix) = FormatTextForFileNameOnly(name, oldName);
                return (prefix, text, suffix);

            case TruncatePathMethod.None:
            case TruncatePathMethod.Compact when !OperatingSystem.IsWindows():
                (prefix, text, suffix) = FormatString(name, oldName, 0);
                return (prefix, text, suffix);

            default:
                var maxStep = name.Length + (oldName?.Length ?? 0);
                BinarySearch.Find(0, maxStep + 1, step =>
                {
                    (var tmpPrefix, var tmpText, var tmpSuffix) = FormatString(name, oldName, step);
                    var measuredWidth = MeasureString(tmpPrefix, tmpText, tmpSuffix).Width;
                    var isShortEnough = measuredWidth <= maxWidth;

                    if (isShortEnough)
                    {
                        prefix = tmpPrefix;
                        text = tmpText;
                        suffix = tmpSuffix;
                    }

                    return isShortEnough;
                });

                return (prefix, text, suffix);
        }
    }

    public static (string text, string? suffix) FormatTextForFileNameOnly(string name, string? oldName)
    {
        name = name.TrimEnd(PathUtil.PosixDirectorySeparatorChar);
        var fileName = Path.GetFileName(name);
        var oldFileName = Path.GetFileName(oldName);
        var suffix = fileName == oldFileName ? null : FormatOldName(oldFileName!);
        return (fileName, suffix);
    }

    public Size MeasureString(string? prefix, string? text, string? suffix)
    {
        var str = prefix.Combine(string.Empty, text).Combine(string.Empty, suffix);
        return MeasureString(str, true);
    }

    public Size MeasureString(string? str, bool withPadding = false)
    {
        var formatFlags = _filePathStringFormat;
        if (!withPadding)
        {
            formatFlags |= TextFormatFlags.NoPadding;
        }

        return TextRenderer.MeasureText(
            _graphics,
            str,
            _font,
            new Size(int.MaxValue, int.MaxValue),
            formatFlags);
    }

    public void DrawString(string str, Rectangle rect, Color color)
    {
        TextRenderer.DrawText(_graphics, str, _font, rect, color, _filePathStringFormat);
    }

    private static (string? prefix, string? text, string? suffix) FormatString(string name, string? oldName, int step)
    {
        if (oldName is not null)
        {
            // Suffix (oldName) is truncated first so that 'name' text always takes precedence.
            var oldNameTruncatedChars = Math.Min(step, oldName.Length);
            var nameTruncatedChars = step - oldNameTruncatedChars;

            (var path, var filename) = SplitPathName(TruncatePath(name, name.Length - nameTruncatedChars));
            var suffix = FormatOldName(TruncatePath(oldName, oldName.Length - oldNameTruncatedChars));
            return (path, filename, suffix);
        }

        (var prefix, var text) = SplitPathName(TruncatePath(name, name.Length - step));
        return (prefix, text, null);

        static string TruncatePath(string path, int length)
        {
            if (path.Length == length)
            {
                return path;
            }

            if (length <= 0)
            {
                // A non-empty truncated path must still be represented by "..." so that
                // the caller can always show at least a minimal indication of a path.
                return "...";
            }

            // The win32 method PathCompactPathEx is only supported on Windows
            var truncatePathMethod = AppSettings.TruncatePathMethod;

            if (truncatePathMethod == TruncatePathMethod.Compact && OperatingSystem.IsWindows())
            {
                StringBuilder result = new(length);
                NativeMethods.PathCompactPathEx(result, path, length, 0);
                return result.ToString();
            }

            if (truncatePathMethod == TruncatePathMethod.TrimStart)
            {
                return "..." + path[^length..];
            }

            return path;
        }
    }

    private static string? FormatOldName(string oldName)
    {
        return string.IsNullOrEmpty(oldName) ? null : " (" + oldName + ")";
    }

    private static (string? path, string? fileName) SplitPathName(string? name)
    {
        if (name is null)
        {
            return (null, null);
        }

        var slashIndex = name.TrimEnd(PathUtil.PosixDirectorySeparatorChar)
            .LastIndexOf(PathUtil.PosixDirectorySeparatorChar);
        if (slashIndex >= 0 && slashIndex < name.Length)
        {
            var path = name[..(slashIndex + 1)];
            var fileName = name[(slashIndex + 1)..];
            return (path, fileName);
        }

        return (null, name);
    }

    private static class NativeMethods
    {
        [DllImport("shlwapi.dll")]
        public static extern bool PathCompactPathEx([Out] StringBuilder pszOut, string szPath, int cchMax,
            int dwFlags);
    }

    internal readonly struct TestAccessor
    {
        internal static string? FormatOldName(string oldName)
        {
            return PathFormatter.FormatOldName(oldName);
        }

        internal static (string? path, string? fileName) SplitPathName(string name)
        {
            return PathFormatter.SplitPathName(name);
        }

        internal static (string? prefix, string? text, string? suffix) FormatString(string name, string? oldName,
            int step)
        {
            return PathFormatter.FormatString(name, oldName, step);
        }
    }
}
