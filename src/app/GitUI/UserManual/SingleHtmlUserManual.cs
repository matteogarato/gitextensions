using GitCommands.Settings;

namespace GitUI.UserManual;

public class SingleHtmlUserManual(string anchorName) : IProvideUserManual
{
    private static string? _location;

    public static string Location
    {
        get
        {
            if (_location is null)
            {
                var path = Path.Join(AppSettings.GetInstallDir()!, "help");
                Uri uri = new(path);
                _location = uri.AbsolutePath;
            }

            return _location;
        }
    }

    string IProvideUserManual.GetUrl()
    {
        return string.Format("{0}/index.html{1}{2}",
            Location, string.IsNullOrEmpty(anchorName) ? "" : "#", anchorName);
    }
}
