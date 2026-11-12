using CommonTestUtils;

[assembly: ConfigureJoinableTaskFactory]
[assembly: AssemblyFixture(typeof(TestAppSettingsFixture))]

[assembly: Trait("Category", "IntegrationTests")]
