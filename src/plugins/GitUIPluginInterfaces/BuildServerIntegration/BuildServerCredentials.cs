namespace GitUIPluginInterfaces.BuildServerIntegration;

public class BuildServerCredentials : IBuildServerCredentials
{
    BuildServerCredentialsType IBuildServerCredentials.BuildServerCredentialsType { get; set; }

    string? IBuildServerCredentials.Username { get; set; }

    string? IBuildServerCredentials.Password { get; set; }

    string? IBuildServerCredentials.BearerToken { get; set; }
}
