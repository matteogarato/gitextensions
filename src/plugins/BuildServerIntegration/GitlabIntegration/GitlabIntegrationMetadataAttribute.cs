using System.ComponentModel.Composition;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Plugins.GitlabIntegration;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
public class GitlabIntegrationMetadataAttribute(string buildServerType)
    : BuildServerAdapterMetadataAttribute(buildServerType);
