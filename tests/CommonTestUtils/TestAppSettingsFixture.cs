using AppSettings = GitCommands.Settings.AppSettings;

namespace CommonTestUtils;

/// <summary>
///     Assembly fixture that sets up <see cref="AppSettings" /> once before the first test of an assembly
///     and disposes them after the last one. Register it with
///     <c>[assembly: AssemblyFixture(typeof(TestAppSettingsFixture))]</c> in each consuming test assembly.
/// </summary>
public sealed class TestAppSettingsFixture : IDisposable
{
    private readonly Semaphore _semaphore = new(1, 1, "GitExtensionsTestAssemblySerializer");

    public TestAppSettingsFixture()
    {
        _semaphore.WaitOne();

        // A test host may run under the shared dotnet runtime rather than a native testhost.exe apphost (e.g. on the
        // arm64 CI runner, which has no native testhost.exe). In that case Application.ExecutablePath — and therefore
        // AppSettings.GetGitExtensionsDirectory() — points at the dotnet install directory instead of the test output,
        // so app-relative content such as the Themes folder cannot be found and theme loading throws (and, worse, pops
        // a modal error dialog that hangs the headless run). Pin the path to the test's own directory so it resolves.
        AppSettings.GetTestAccessor().ApplicationExecutablePath =
            Path.Combine(AppContext.BaseDirectory, "GitExtensions.exe");

        File.Delete(AppSettings.SettingsContainer.SettingsCache.SettingsFilePath);
        AppSettings.SettingsContainer.SettingsCache.Load();

        AppSettings.CheckForUpdates = false;
        AppSettings.ShowAvailableDiffTools = false;

        // Create the settings file so that the SettingsCache does not think it should reload the file again and again
        AppSettings.SettingsContainer.SettingsCache.Save();
    }

    void IDisposable.Dispose()
    {
        try
        {
            Epilogue.ExecuteAfterSuiteActions();
        }
        finally
        {
            try
            {
                AppSettings.SettingsContainer.SettingsCache.Dispose();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
