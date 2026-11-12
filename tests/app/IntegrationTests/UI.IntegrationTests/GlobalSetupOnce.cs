using UITests;

[assembly: AssemblyFixture(typeof(GlobalSetupOnce))]

// No namespace, so it runs for all tests.
#pragma warning disable CA1050 // Declare types in namespaces
namespace UITests;

public sealed class GlobalSetupOnce : IAsyncLifetime
#pragma warning restore CA1050 // Declare types in namespaces
{
    ValueTask IAsyncLifetime.InitializeAsync()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        return ValueTask.CompletedTask;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
