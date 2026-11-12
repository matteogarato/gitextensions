using GitCommands.Settings;

namespace GitUI.UserManual;

public class StandardHtmlUserManual(string folder, string anchorName) : IProvideUserManual
{
    string IProvideUserManual.GetUrl()
    {
        var subFolder = string.IsNullOrEmpty(folder) ? string.Empty : folder + ".html";

        return (AppSettings.DocumentationBaseUrl + subFolder).Combine("#", anchorName)!;
    }
}
