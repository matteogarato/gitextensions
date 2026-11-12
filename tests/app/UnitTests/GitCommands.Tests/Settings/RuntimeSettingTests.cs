using GitCommands.Settings;

namespace GitCommandsTests.Settings;

public sealed class RuntimeSettingTests
{
    [Fact]
    public void EnumRuntimeSetting()
    {
        SettingsPath rootSettingsPath = new AppSettingsPath("");
        const string name = "SettingX";

        var persistentSetting = Setting.Create(rootSettingsPath, name, Enum.HardCodedDefault);
        RuntimeSetting<Enum> runtimeSetting = new(persistentSetting);

        runtimeSetting.Value.Should().Be(Enum.HardCodedDefault);

        runtimeSetting.Value = Enum.PersistentValue;
        runtimeSetting.Value.Should().Be(Enum.PersistentValue);
        persistentSetting.Value.Should().Be(Enum.HardCodedDefault);

        runtimeSetting.Save();
        runtimeSetting.Value.Should().Be(Enum.PersistentValue);
        persistentSetting.Value.Should().Be(Enum.PersistentValue);

        runtimeSetting.Value = Enum.RuntimeValue;
        runtimeSetting.Value.Should().Be(Enum.RuntimeValue);
        persistentSetting.Value.Should().Be(Enum.PersistentValue);

        runtimeSetting.Reload();
        runtimeSetting.Value.Should().Be(Enum.PersistentValue);
        persistentSetting.Value.Should().Be(Enum.PersistentValue);

        runtimeSetting.ResetToDefault();
        runtimeSetting.Value.Should().Be(Enum.HardCodedDefault);
        persistentSetting.Value.Should().Be(Enum.PersistentValue);
    }

    private enum Enum
    {
        Foo,
        Bar,
        HardCodedDefault,
        PersistentValue,
        RuntimeValue
    }
}
