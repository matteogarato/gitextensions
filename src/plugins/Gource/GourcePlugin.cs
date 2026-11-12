using System.ComponentModel.Composition;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.Gource.Properties;
using GitExtUtils.GitUI;
using GitUI;
using ResourceManager;

namespace GitExtensions.Plugins.Gource;

[Export(typeof(IGitPlugin))]
public class GourcePlugin : GitPluginBase, IGitPluginForRepository
{
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        UseProxy = true, DefaultProxyCredentials = CredentialCache.DefaultCredentials
    });

    private readonly StringSetting _gourceArguments =
        new("Arguments", "--hide filenames --user-image-dir \"$(AVATARS)\"");

    private readonly StringSetting _gourcePath = new("Path to Gource", "");

    public GourcePlugin() : base(true)
    {
        Id = new Guid("F0A6A769-6DCC-4452-9A43-343347015EEC");
        Name = "Gource";
        Translate(AppSettings.CurrentTranslation);
        Icon = Resources.IconGource;
    }

    private static void UnZipFiles(IWin32Window? owner, string zipPathAndFile, string outputFolder, bool deleteZipFile)
    {
        try
        {
            if (outputFolder != "")
            {
                Directory.CreateDirectory(outputFolder);
            }

            var outputFolderFullPath = Path.GetFullPath(string.IsNullOrEmpty(outputFolder) ? "." : outputFolder);

            using (var archive = ZipFile.OpenRead(zipPathAndFile))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) || entry.FullName.Contains(".ini"))
                    {
                        continue;
                    }

                    var destinationPath = Path.Combine(outputFolder, entry.FullName).Replace("\\ ", "\\");
                    var fullDestinationPath = Path.GetFullPath(destinationPath);
                    if (!fullDestinationPath.StartsWith(outputFolderFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var fullDirPath = Path.GetDirectoryName(fullDestinationPath);
                    if (fullDirPath is not null && !Directory.Exists(fullDirPath))
                    {
                        Directory.CreateDirectory(fullDirPath);
                    }

                    entry.ExtractToFile(fullDestinationPath, true);
                }
            }

            if (deleteZipFile)
            {
                File.Delete(zipPathAndFile);
            }
        }
        catch (Exception e)
        {
            MessageBoxes.ShowError(owner, e.Message);
        }
    }

    private static async Task<int> DownloadFileAsync(IWin32Window? owner, string remoteFilename, string localFilename)
    {
        try
        {
            using var remoteStream = await _httpClient.GetStreamAsync(remoteFilename);
            using var localStream = File.Create(localFilename);
            await remoteStream.CopyToAsync(localStream);

            return (int)localStream.Position;
        }
        catch (Exception e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            MessageBoxes.ShowError(owner, e.Message);
            return 0;
        }
    }

    private static async Task<string> SearchForGourceUrlAsync(IWin32Window? owner)
    {
        // All Gource releases do not have binary releases, use a fallback
        const string latestApiUrl = "https://api.github.com/repos/acaudwell/Gource/releases/latest";
        const string latestBinReleaseUrl = "https://api.github.com/repos/acaudwell/Gource/releases/tags/gource-0.53";
        const string win64ZipSuffix = ".win64.zip";

        try
        {
            var url = await FindWin64AssetAsync(latestApiUrl);
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }

            return await FindWin64AssetAsync(latestBinReleaseUrl);
        }
        catch (Exception ex)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            MessageBoxes.ShowError(owner, ex.Message);
            return string.Empty;
        }

        async Task<string> FindWin64AssetAsync(string apiUrl)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, apiUrl);
            request.Headers.UserAgent.ParseAdd("GitExtensions");
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (name.StartsWith("gource-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(win64ZipSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }

    #region Translation

    private readonly TranslationString _currentDirectoryIsNotValidGit = new(
        "The current directory is not a valid git repository.\n\n" +
        "Gource can be only be started from a valid git repository.");

    private readonly TranslationString _resetConfigPath = new("Cannot find Gource in the configured path: {0}.\n\n" +
                                                              "Do you want to reset the configured path?");

    private readonly TranslationString _gource = new("Gource");

    private readonly TranslationString _doYouWantDownloadGource = new("There is no path to Gource configured.\n\n" +
                                                                      "Do you want to automatically download Gource?");

    private readonly TranslationString _download = new("Download");

    private readonly TranslationString _cannotFindGource = new("Cannot find Gource.\n" +
                                                               "Please download Gource and set the path in the plugins settings dialog.");

    private readonly TranslationString _bytesDownloaded = new("{0} bytes downloaded.");
    private readonly TranslationString _gourceDownloadedAndUnzipped = new("Gource has been downloaded and unzipped.");

    private readonly TranslationString _downloadingFailed = new("Downloading failed.\n" +
                                                                "Please download Gource and set the path in the plugins settings dialog.");

    #endregion

    #region IGitPlugin Members

    public override IEnumerable<ISetting> GetSettings()
    {
        // return all settings or introduce implementation based on reflection on GitPluginBase level
        yield return _gourcePath;
        yield return _gourceArguments;
    }

    public override bool Execute(GitUiEventArgs args)
    {
        if (!args.GitModule.IsValidGitWorkingDir())
        {
            MessageBoxes.ShowError(args.OwnerForm, _currentDirectoryIsNotValidGit.Text);
            return false;
        }

        var pathToGource = _gourcePath.ValueOrDefault(Settings);

        if (!string.IsNullOrEmpty(pathToGource) && !File.Exists(pathToGource))
        {
            var result = MessageBoxes.Show(
                args.OwnerForm,
                string.Format(_resetConfigPath.Text, pathToGource), _gource.Text, MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                Settings.SetValue(_gourcePath.Name, _gourcePath.DefaultValue);
                pathToGource = _gourcePath.DefaultValue;
            }
        }

        if (string.IsNullOrEmpty(pathToGource))
        {
            if (MessageBoxes.Show(
                    args.OwnerForm, _doYouWantDownloadGource.Text, _download.Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                var gourceUrl = ThreadHelper.JoinableTaskFactory.Run(() => SearchForGourceUrlAsync(args.OwnerForm));

                if (string.IsNullOrEmpty(gourceUrl))
                {
                    MessageBoxes.ShowError(args.OwnerForm, _cannotFindGource.Text);
                    return false;
                }

                var downloadDir = Path.GetTempPath();
                var fileName = Path.Join(downloadDir, "gource.zip");
                var downloadSize =
                    ThreadHelper.JoinableTaskFactory.Run(() => DownloadFileAsync(args.OwnerForm, gourceUrl, fileName));
                if (downloadSize > 0)
                {
                    MessageBoxes.Show(args.OwnerForm, string.Format(_bytesDownloaded.Text, downloadSize), "Information",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Directory.CreateDirectory(Path.Join(downloadDir, "gource"));
                    UnZipFiles(args.OwnerForm, fileName, Path.Join(downloadDir, "gource"), true);

                    var newGourcePath = Path.Join(downloadDir, "gource\\gource.exe");
                    if (File.Exists(newGourcePath))
                    {
                        MessageBoxes.Show(args.OwnerForm, _gourceDownloadedAndUnzipped.Text, "Information",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        pathToGource = newGourcePath;
                    }
                }
                else
                {
                    MessageBoxes.ShowError(args.OwnerForm, _downloadingFailed.Text);
                }
            }
        }

        using GourceStart gourceStart = new(pathToGource, args, _gourceArguments.ValueOrDefault(Settings));
        gourceStart.ShowDialog(args.OwnerForm);
        Settings.SetValue(_gourceArguments.Name, gourceStart.GourceArguments);
        Settings.SetValue(_gourcePath.Name, gourceStart.PathToGource);

        return false;
    }

    #endregion
}
