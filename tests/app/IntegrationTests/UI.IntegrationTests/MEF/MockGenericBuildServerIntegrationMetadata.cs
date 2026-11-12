using System.ComponentModel.Composition;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace UITests.MEF;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
public class MockGenericBuildServerIntegrationMetadata(string buildServerType)
    : BuildServerAdapterMetadataAttribute(buildServerType);
