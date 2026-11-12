using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitCommands;

public static class FileHelper
{
    private static readonly IEnumerable<string> _binaryExtensions = new[]
    {
        ".avi", // movie
        ".bmp", // image
        ".dat", // data file
        ".bin", // binary file
        ".dll", // dynamic link library
        ".doc", // office word
        ".docx", // office word
        ".ppt", // office powerpoint
        ".pps", // office powerpoint
        ".pptx", // office powerpoint
        ".ppsx", // office powerpoint
        ".dwg", // autocad
        ".exe", // executable
        ".gif", // image
        ".ico", // icon
        ".jpg", // image
        ".jpeg", // image
        ".mpg", // movie
        ".mpeg", // movie
        ".msi", // installer
        ".pdf", // pdf document
        ".png", // image
        ".pdb", // debug file
        ".sc1", // screen file
        ".tif", // image
        ".tiff", // image
        ".vsd", // microsoft visio
        ".vsdx", // microsoft
        ".xls", // microsoft excel
        ".xlsx", // microsoft excel
        ".odt" // Open office
    };

    private static readonly IEnumerable<string> _imageExtensions = new[]
    {
        ".bmp", ".gif", ".ico", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
    };

    public static bool IsBinaryFileName(IGitModule module, string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
               && (IsBinaryAccordingToGitAttributes(module, fileName)
                   ?? HasMatchingExtension(_binaryExtensions, fileName));
    }

    /// <returns>null if no info in .gitattributes (or ambiguous). True if marked as binary, false if marked as text</returns>
    private static bool? IsBinaryAccordingToGitAttributes(IGitModule module, string fileName)
    {
        string[] diffValues =
        [
            "set", "astextplain", "ada", "bibtext", "cpp", "csharp", "css", "dts", "elixir", "fortran", "html", "java",
            "kotlin", "markdown", "matlab", "objc", "pascal", "perl", "php", "python", "ruby", "rust", "scheme", "tex"
        ];
        GitArgumentBuilder cmd = new("check-attr")
        {
            "-z",
            "diff",
            "text",
            "crlf",
            "eol",
            "--",
            fileName.Quote()
        };
        var result = module.GitExecutable.Execute(cmd, throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            return null;
        }

        var lines = result.StandardOutput.Split(Delimiters.NullAndLineFeed);
        Dictionary<string, string> attributes = [];
        for (var i = 0; i < lines.Length - 2; i += 3)
        {
            attributes[lines[i + 1].Trim()] = lines[i + 2].Trim();
        }

        if (attributes.TryGetValue("diff", out var diff))
        {
            if (diff == "unset")
            {
                return true;
            }

            if (diffValues.Contains(diff))
            {
                return false;
            }
        }

        if (attributes.TryGetValue("text", out var text))
        {
            if (text != "unset" && text != "unspecified")
            {
                return false;
            }
        }

        if (attributes.TryGetValue("crlf", out var crlf))
        {
            if (crlf != "unset" && crlf != "unspecified")
            {
                return false;
            }
        }

        if (attributes.TryGetValue("eol", out var eol))
        {
            if (eol != "unset" && eol != "unspecified")
            {
                return false;
            }
        }

        return null;
    }

    public static bool IsImage(string fileName)
    {
        return HasMatchingExtension(_imageExtensions, fileName);
    }

    private static bool HasMatchingExtension(IEnumerable<string> extensions, string fileName)
    {
        return extensions.Any(extension => fileName.EndsWith(extension, StringComparison.CurrentCultureIgnoreCase));
    }

    #region binary file check

    public static bool IsBinaryFileAccordingToContent(byte[]? content)
    {
        // Check for binary file.
        if (content?.Length is > 0)
        {
            var nullCount = 0;
            foreach (var b in content)
            {
                if (b == 0 && ++nullCount > 5)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsBinaryFileAccordingToContent(string? content)
    {
        // Check for binary file.
        if (!string.IsNullOrEmpty(content))
        {
            var nullCount = 0;
            foreach (var c in content)
            {
                if (c == '\0')
                {
                    nullCount++;
                    if (nullCount > 5)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    #endregion
}
