namespace GitCommandsTests;

public static class PlatformTestConditions
{
    public static bool IsWindows
    {
        get => OperatingSystem.IsWindows();
    }
}
