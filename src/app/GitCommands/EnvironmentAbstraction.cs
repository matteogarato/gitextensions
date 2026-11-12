namespace GitCommands;

public interface IEnvironmentAbstraction
{
    /// <inheritdoc cref="Environment.Exit" />
    void Exit(int exitCode);

    /// <inheritdoc cref="Environment.GetCommandLineArgs" />
    string[] GetCommandLineArgs();

    /// <inheritdoc cref="Environment.GetEnvironmentVariable(string)" />
    string? GetEnvironmentVariable(string variable);

    /// <inheritdoc cref="Environment.GetEnvironmentVariable(string,EnvironmentVariableTarget)" />
    string? GetEnvironmentVariable(string variable, EnvironmentVariableTarget target);

    /// <inheritdoc cref="Environment.GetFolderPath(Environment.SpecialFolder)" />
    string GetFolderPath(Environment.SpecialFolder folder);

    /// <inheritdoc cref="Environment.SetEnvironmentVariable(string,string)" />
    void SetEnvironmentVariable(string variable, string? value);
}

public sealed class EnvironmentAbstraction : IEnvironmentAbstraction
{
    /// <inheritdoc />
    void IEnvironmentAbstraction.Exit(int exitCode)
    {
        Environment.Exit(exitCode);
    }

    /// <inheritdoc />
    string[] IEnvironmentAbstraction.GetCommandLineArgs()
    {
        return Environment.GetCommandLineArgs();
    }

    /// <inheritdoc />
    string? IEnvironmentAbstraction.GetEnvironmentVariable(string variable)
    {
        return Environment.GetEnvironmentVariable(variable);
    }

    /// <inheritdoc />
    string? IEnvironmentAbstraction.GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
    {
        return Environment.GetEnvironmentVariable(variable, target);
    }

    /// <inheritdoc />
    string IEnvironmentAbstraction.GetFolderPath(Environment.SpecialFolder folder)
    {
        return Environment.GetFolderPath(folder);
    }

    /// <inheritdoc />
    void IEnvironmentAbstraction.SetEnvironmentVariable(string variable, string? value)
    {
        Environment.SetEnvironmentVariable(variable, value);
    }
}
