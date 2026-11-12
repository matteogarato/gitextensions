using System.ComponentModel.Composition;
using System.Text;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.AutoCompileSubmodules.Properties;
using ResourceManager;

namespace GitExtensions.Plugins.AutoCompileSubmodules;

[Export(typeof(IGitPlugin))]
public class AutoCompileSubModulesPlugin : GitPluginBase, IGitPluginForRepository
{
    private const string _defaultMsBuildPath = @"C:\Windows\Microsoft.NET\Framework\v3.5\msbuild.exe";

    private readonly TranslationString _doYouWantBuild =
        new("Do you want to build {0}?\n\n{1}");

    private readonly TranslationString _enterCorrectMsBuildPath =
        new("Please enter correct MSBuild path in the plugin settings dialog and try again.");

    private readonly StringSetting _msBuildArguments = new("msbuild.exe arguments", "/p:Configuration=Debug");

    private readonly BoolSetting _msBuildEnabled = new("Enabled", false);
    private readonly StringSetting _msBuildPath = new("Path to msbuild.exe", FindMsBuild());

    public AutoCompileSubModulesPlugin() : base(true)
    {
        Id = new Guid("D4D1ACB7-0B6B-4A3C-B0DB-A25056A277D9");
        Name = "Auto compile submodules";
        Translate(AppSettings.CurrentTranslation);
        Icon = Resources.IconAutoCompileSubmodules;
    }

    private static string FindMsBuild()
    {
        return File.Exists(_defaultMsBuildPath) ? _defaultMsBuildPath : "";
    }

    /// <summary>
    ///     Automatically compile all solution files found in any submodule
    /// </summary>
    private void GitUiCommandsPostUpdateSubmodules(object? sender, GitUiPostActionEventArgs e)
    {
        if (e.ActionDone && _msBuildEnabled.ValueOrDefault(Settings))
        {
            Execute(e);
        }
    }

    private static string SolutionFilesToString(IReadOnlyList<FileInfo> solutionFiles)
    {
        StringBuilder solutionString = new();

        for (var n = solutionFiles.Count - 1; n > 0; n--)
        {
            var solutionFile = solutionFiles[n];
            solutionString.Append(solutionFile.Name);
            solutionString.Append('\n');
        }

        return solutionString.ToString();
    }

    #region IGitPlugin Members

    public override IEnumerable<ISetting> GetSettings()
    {
        yield return _msBuildEnabled;
        yield return _msBuildPath;
        yield return _msBuildArguments;
    }

    public override void Register(IGitUiCommands gitUiCommands)
    {
        // Connect to events
        gitUiCommands.PostUpdateSubmodules += GitUiCommandsPostUpdateSubmodules;
    }

    public override void Unregister(IGitUiCommands gitUiCommands)
    {
        // Connect to events
        gitUiCommands.PostUpdateSubmodules -= GitUiCommandsPostUpdateSubmodules;
    }

    public override bool Execute(GitUiEventArgs args)
    {
        // Only build when plugin is enabled
        if (string.IsNullOrEmpty(args.GitModule.WorkingDir))
        {
            return false;
        }

        var msbuildPath = _msBuildPath.ValueOrDefault(Settings);

        DirectoryInfo workingDir = new(args.GitModule.WorkingDir);
        var solutionFiles = workingDir.GetFiles("*.sln", SearchOption.AllDirectories);

        for (var n = solutionFiles.Length - 1; n > 0; n--)
        {
            var solutionFile = solutionFiles[n];

            var result =
                MessageBoxes.Show(args.OwnerForm,
                    string.Format(_doYouWantBuild.Text,
                        solutionFile.Name,
                        SolutionFilesToString(solutionFiles)),
                    "Build",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
            {
                return false;
            }

            if (result != DialogResult.Yes)
            {
                continue;
            }

            if (string.IsNullOrEmpty(msbuildPath) || !File.Exists(msbuildPath))
            {
                MessageBoxes.ShowError(args.OwnerForm, _enterCorrectMsBuildPath.Text);
            }
            else
            {
                args.GitUiCommands.StartCommandLineProcessDialog(args.OwnerForm, msbuildPath,
                    solutionFile.FullName + " " + _msBuildArguments.ValueOrDefault(Settings));
            }
        }

        return false;
    }

    #endregion
}
