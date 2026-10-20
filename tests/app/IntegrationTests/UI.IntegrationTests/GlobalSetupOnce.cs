using Xunit;

[assembly: AssemblyFixture(typeof(GlobalSetupOnce))]

// No namespace, so it runs for all tests.
#pragma warning disable CA1050 // Declare types in namespaces
public sealed class GlobalSetupOnce : IAsyncLifetime
#pragma warning restore CA1050 // Declare types in namespaces
{
    public ValueTask InitializeAsync()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
