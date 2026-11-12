using GitExtensions.Extensibility.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Plugins.AzureDevOpsIntegration.Settings;

/// <summary>
///     Describes available settings of the Azure DevOps (or TFS>=2015) Build integration plugin
/// </summary>
public class IntegrationSettings
{
    private const string _projectUrlKey = "ProjectUrl";
    private const string _buildDefinitionFilterKey = "BuildDefinitionNameFilter";
    private const string _apiTokenKey = "RestApiToken";
    private const string _repositoryNameKey = "RepositoryName";

    /// <summary>
    ///     Contains the url to the home page of a Azure DevOps / TFS project the build server to integrate belongs to
    /// </summary>
    public string ProjectUrl { get; set; } = "";

    /// <summary>
    ///     Contains a regular expression that is used to filter the displayed build results to the given build definitions
    /// </summary>
    public string BuildDefinitionFilter { get; set; } = "";

    /// <summary>
    ///     Contains a authentication token which is required and used to request build result information from the Azure
    ///     DevOps / TFS instance.
    /// </summary>
    public string ApiToken { get; set; } = "";

    /// <summary>
    ///     The repository name, used to scope build definition queries to the correct repository.
    /// </summary>
    public string RepositoryName { get; set; } = "";

    /// <summary>
    ///     Reads these settings from the given <see cref="SettingsSource" />
    /// </summary>
    public static IntegrationSettings ReadFrom(SettingsSource config)
    {
        var projectUrl = config?.GetString(_projectUrlKey, "") ?? "";
        var buildDefinitionFilter = config?.GetString(_buildDefinitionFilterKey, "") ?? "";
        var apiToken = config?.GetString(_apiTokenKey, "") ?? "";
        var repositoryName = config?.GetString(_repositoryNameKey, "") ?? "";

        return new IntegrationSettings
        {
            ProjectUrl = projectUrl,
            BuildDefinitionFilter = buildDefinitionFilter,
            ApiToken = apiToken,
            RepositoryName = repositoryName
        };
    }

    /// <summary>
    ///     Writes these settings to the given <see cref="SettingsSource" />
    /// </summary>
    public void WriteTo(SettingsSource config)
    {
        config.SetString(_projectUrlKey, ProjectUrl);
        config.SetString(_buildDefinitionFilterKey, BuildDefinitionFilter);
        config.SetString(_apiTokenKey, ApiToken);
        config.SetString(_repositoryNameKey, RepositoryName);
    }

    /// <summary>
    ///     Validates if these settings are valid and good to use.
    ///     A PAT is optional — when absent, default Windows credentials are used.
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ProjectUrl) && BuildServerSettingsHelper.IsRegexValid(BuildDefinitionFilter);
    }
}
