using Microsoft.Win32;

namespace GitCommands.DiffMergeTools;

internal sealed class VsDiffMerge : DiffMergeTool
{
    private const string _exeName = "vsdiffmerge.exe";

    /// <inheritdoc />
    public override string MergeCommand
    {
        get => "/m \"$REMOTE\" \"$LOCAL\" \"$BASE\" \"$MERGED\"";
    }

    /// <inheritdoc />
    public override string ExeFileName
    {
        get => _exeName;
    }

    /// <inheritdoc />
    public override string Name
    {
        get => "vsdiffmerge";
    }

    /// <inheritdoc />
    public override IEnumerable<string> SearchPaths
    {
        get => new[] { GetVsDiffMergePath() };
    }

    private static string GetVsDiffMergePath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return _exeName;
        }

        // For 2017 (15.0) and later, VsDiffMerge is not installed by default but often included
        // C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer
        string[] vsVersions = ["14.0", "12.0", "11.0"];

        foreach (var version in vsVersions)
        {
            var registryKeyString =
                $@"SOFTWARE{(Environment.Is64BitProcess ? @"\Wow6432Node\" : "\\")}Microsoft\VisualStudio\{version}";
            using var localMachineKey = Registry.LocalMachine.OpenSubKey(registryKeyString);
            var path = localMachineKey?.GetValue("InstallDir") as string;
            if (!string.IsNullOrEmpty(path))
            {
                return Path.Join(path, _exeName);
            }
        }

        return _exeName;
    }
}
