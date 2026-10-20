using CommonTestUtils;
using Xunit;

[assembly: ConfigureJoinableTaskFactory]
[assembly: AssemblyFixture(typeof(TestAppSettingsFixture))]

[assembly: Trait("Category", "IntegrationTests")]
