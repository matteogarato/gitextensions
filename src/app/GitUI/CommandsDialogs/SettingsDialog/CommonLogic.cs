using System.Text;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using Microsoft.Win32;
using ResourceManager;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace GitUI.CommandsDialogs.SettingsDialog;

public sealed class CommonLogic : Translate
{
    internal const string PresetGitEditorEnvVariableName = "GIT_EDITOR";
    internal const string AmbientGitEditorEnvVariableName = "EDITOR";

    private static readonly TranslationString _cantReadRegistry =
        new("Git Extensions has insufficient permissions to check the registry.");

    public readonly DistributedSettingsSet DistributedSettingsSet;
    public readonly GitConfigSettingsSet GitConfigSettingsSet;
    public readonly IGitModule Module;

    private readonly TranslationString _selectFile =
        new("Select file");

    private CommonLogic()
    {
        // For translation only
        Module = null!;
    }

    public CommonLogic(IGitModule module)
    {
        Validates.NotNull(module);

        Module = module;

        var distributedGlobalSettings = DistributedSettings.CreateGlobal(false);
        var distributedPulledSettings = DistributedSettings.CreateDistributed(module, false);
        var distributedLocalSettings = DistributedSettings.CreateLocal(module, false);
        DistributedSettings distributedEffectiveSettings = new(
            new DistributedSettings(distributedGlobalSettings, distributedPulledSettings.SettingsCache,
                SettingLevel.Distributed),
            distributedLocalSettings.SettingsCache,
            SettingLevel.Effective);
        DistributedSettingsSet = new DistributedSettingsSet(
            distributedEffectiveSettings,
            distributedLocalSettings,
            distributedPulledSettings,
            distributedGlobalSettings);

        var gitExecutable = module.GitExecutable;
        GitConfigSettings systemGitConfigSettings = new(gitExecutable, GitSettingLevel.SystemWide);
        GitConfigSettings globalGitConfigSettings = new(gitExecutable, GitSettingLevel.Global);
        GitConfigSettings localGitConfigSettings = new(gitExecutable, GitSettingLevel.Local);
        EffectiveGitConfigSettings effectiveGitConfigSettings = new(gitExecutable);
        GitConfigSettingsSet = new GitConfigSettingsSet(
            new SettingsSource<IConfigValueStore>(effectiveGitConfigSettings),
            new SettingsSource<IPersistentConfigValueStore>(localGitConfigSettings),
            new SettingsSource<IPersistentConfigValueStore>(globalGitConfigSettings),
            new SettingsSource<IConfigValueStore>(systemGitConfigSettings));
    }

    /// <summary>
    ///     Reads the registry key.
    /// </summary>
    /// <param name="root">Registry root</param>
    /// <param name="subkey">Registry subkey</param>
    /// <param name="key">Registry key, specify <see langword="null" /> to read default key</param>
    /// <returns>registry value or empty string in case of error</returns>
    public static string GetRegistryValue(RegistryKey root, string subkey, string? key = null)
    {
        string? value = null;
        try
        {
            var registryKey = root.OpenSubKey(subkey, false);

            if (registryKey is not null)
            {
                using (registryKey)
                {
                    value = registryKey.GetValue(key) as string;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBoxes.Show(_cantReadRegistry.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        return value ?? "";
    }

    public string? GetGlobalEditor()
    {
        return GetEditorOptions().FirstOrDefault(o => !string.IsNullOrEmpty(o));

        IEnumerable<string?> GetEditorOptions()
        {
            yield return Environment.GetEnvironmentVariable(PresetGitEditorEnvVariableName);
            yield return GitConfigSettingsSet.GlobalSettings.GetValue("core.editor");
            yield return Environment.GetEnvironmentVariable("VISUAL");
            yield return Environment.GetEnvironmentVariable(AmbientGitEditorEnvVariableName);
        }
    }

    public string SelectFile(string initialDirectory, string filter, string prev)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = filter, InitialDirectory = initialDirectory, Title = _selectFile.Text
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : prev;
    }

    public static void FillEncodings(ComboBox combo)
    {
        combo.Items.AddRange([.. AppSettings.AvailableEncodings.Values]);
        combo.DisplayMember = nameof(Encoding.EncodingName);
    }
}
