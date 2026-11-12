using GitUIPluginInterfaces;

namespace GitUIPluginInterfacesTests;

public class ManagedExtensibilityTests
{
    [Fact]
    public void ThrowWhenUserPluginsPathAlreadyInitialized()
    {
        ManagedExtensibility.SetUserPluginsPath("A");
        ((Action)(() => ManagedExtensibility.SetUserPluginsPath("B"))).Should().Throw<InvalidOperationException>();
    }
}
