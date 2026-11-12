using System.ComponentModel.Composition;

namespace GitUIPluginInterfaces.BuildServerIntegration;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
public class BuildServerSettingsUserControlMetadata(string buildServerType)
    : BuildServerAdapterMetadataAttribute(buildServerType);
