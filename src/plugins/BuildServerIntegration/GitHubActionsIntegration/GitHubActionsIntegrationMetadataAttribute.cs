using System.ComponentModel.Composition;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Plugins.GitHubActionsIntegration;

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
public sealed class GitHubActionsIntegrationMetadataAttribute(string buildServerType)
    : BuildServerAdapterMetadataAttribute(buildServerType);
