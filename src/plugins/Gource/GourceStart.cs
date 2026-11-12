using System.Diagnostics;
using System.Drawing.Imaging;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI;
using GitUI.Avatars;
using ResourceManager;

namespace GitExtensions.Plugins.Gource;

public partial class GourceStart : GitExtensionsFormBase
{
    public GourceStart(string pathToGource, GitUiEventArgs gitUiArgs, string gourceArguments)
    {
        InitializeComponent();
        InitializeComplete();

        // To accommodate the translation app
        if (gitUiArgs is null)
        {
            return;
        }

        PathToGource = pathToGource;
        GitUiArgs = gitUiArgs;
        GitWorkingDir = gitUiArgs.GitModule.WorkingDir;
        GourceArguments = gourceArguments;

        WorkingDir.Text = GitWorkingDir;
        GourcePath.Text = pathToGource;
        Arguments.Text = GourceArguments;
    }

    private GitUiEventArgs GitUiArgs { get; } = null!;

    public string PathToGource { get; set; } = "";

    public string? GitWorkingDir { get; set; }

    public string GourceArguments { get; set; } = "";

    private void RunRealCmdDetached(string cmd, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "\"" + cmd + "\"", Arguments = arguments, WorkingDirectory = WorkingDir.Text
            });
        }
        catch (Exception e)
        {
            MessageBoxes.ShowError(this, e.Message);
        }
    }

    private void Button1Click(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (!File.Exists(GourcePath.Text))
        {
            MessageBoxes.ShowError(this, "Cannot find Gource.\nPlease download Gource and set the correct path.");
            return;
        }

        GourceArguments = Arguments.Text;
        var gourceAvatarsDir = GourceArguments.Contains("$(AVATARS)")
            ? ThreadHelper.JoinableTaskFactory.Run(LoadAvatarsAsync)
            : "";
        var arguments = GourceArguments.Replace("$(AVATARS)", gourceAvatarsDir);
        PathToGource = GourcePath.Text;
        GitWorkingDir = WorkingDir.Text;

        RunRealCmdDetached(GourcePath.Text, arguments);
        Close();
    }

    private async Task<string> LoadAvatarsAsync()
    {
        var gourceAvatarsDir = Path.Join(Path.GetTempPath(), "GitAvatars");

        Directory.CreateDirectory(gourceAvatarsDir);

        foreach (var file in Directory.GetFiles(gourceAvatarsDir))
        {
            File.Delete(file);
        }

        GitArgumentBuilder args = new("log") { "--pretty=format:\"%aE|%aN\"" };
        var lines = GitUiArgs.GitModule.GitExecutable.GetOutput(args).Split('\n');

        var authors = lines.Select(line =>
            {
                var bits = line.Split('|');
                return (email: bits[0], name: bits[1]);
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.email) && !string.IsNullOrWhiteSpace(t.name))
            .GroupBy(t => t.name)
            .Select(g => (g.First().email, name: g.Key));

        await Task.WhenAll(authors.Select(DownloadImageAsync));

        return gourceAvatarsDir;

        async Task DownloadImageAsync((string email, string name) author)
        {
            try
            {
                var image = await AvatarService.DefaultProvider.GetAvatarAsync(author.email, author.name, 90);
                var filename = author.name + ".png";

                if (image is null || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return;
                }

                var filePath = Path.Join(gourceAvatarsDir, filename);
                image.Save(filePath, ImageFormat.Png);
            }
            catch
            {
                // Do nothing
            }
        }
    }

    private void GourceBrowseClick(object sender, EventArgs e)
    {
        using OpenFileDialog fileDialog =
            new() { Filter = "Gource (gource.exe)|gource.exe", FileName = GourcePath.Text };
        fileDialog.ShowDialog(this);

        GourcePath.Text = fileDialog.FileName;
    }

    private void WorkingDirBrowseClick(object sender, EventArgs e)
    {
        using FolderBrowserDialog folderDialog = new() { SelectedPath = WorkingDir.Text };
        folderDialog.ShowDialog(this);
        WorkingDir.Text = folderDialog.SelectedPath;
    }

    private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/acaudwell/Gource/");
    }

    private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/acaudwell/Gource#readme");
    }
}
