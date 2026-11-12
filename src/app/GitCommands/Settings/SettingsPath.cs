using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Settings;

namespace GitCommands.Settings;

public class SettingsPath(IConfigValueStore? parent, string pathName) : SettingsSource
{
    private readonly string _pathNameWithSeparator = string.IsNullOrWhiteSpace(pathName) ? "" : $"{pathName}.";

    public string PathFor(string subPath)
    {
        return $"{_pathNameWithSeparator}{subPath}";
    }

    public override string? GetValue(string name)
    {
        Validates.NotNull(parent);
        return parent.GetValue(PathFor(name));
    }

    public override void SetValue(string name, string? value)
    {
        Validates.NotNull(parent);
        parent.SetValue(PathFor(name), value);
    }
}
