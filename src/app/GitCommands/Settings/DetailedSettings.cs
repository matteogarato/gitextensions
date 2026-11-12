using GitExtensions.Extensibility.Settings;

namespace GitCommands.Settings;

public static class DetailedSettings
{
    private static readonly SettingsPath _settingsPath = new(null, "Detailed");

    public static BoolSetting GetRemoteBranchesDirectlyFromRemote { get; } =
        new(_settingsPath.PathFor(nameof(GetRemoteBranchesDirectlyFromRemote)), false);

    public static BoolSetting AddMergeLogMessages { get; } =
        new(_settingsPath.PathFor(nameof(AddMergeLogMessages)), false);

    public static NumberSetting<int> MergeLogMessagesCount { get; } =
        new(_settingsPath.PathFor(nameof(MergeLogMessagesCount)), 20);
}
