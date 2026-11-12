namespace GitExtensions.Extensibility;

public readonly struct ExecutionResult(
    IExecutable executable,
    ArgumentString arguments,
    string standardOutput,
    string standardError,
    int? exitCode)
{
    public const int Success = 0;

    public string Command { get; } = executable.Command;
    public string Arguments { get; } = arguments;
    public string WorkingDir { get; } = executable.WorkingDir;
    public string StandardOutput { get; } = standardOutput;
    public string StandardError { get; } = standardError;
    public int? ExitCode { get; } = exitCode;

    /// <summary>
    ///     Gets a verbose string with the <see cref="ExitCode" /> using <see cref="FormatExitCode" />.
    /// </summary>
    public string? ExitCodeDisplay
    {
        get => ExitCode is { } exitCode ? FormatExitCode(exitCode) : "null";
    }

    public bool ExitedSuccessfully
    {
        get => ExitCode == Success;
    }

    public string AllOutput
    {
        get => string.Concat(StandardOutput, Environment.NewLine, StandardError);
    }

    /// <summary>
    ///     Returns a verbose string of an <paramref name="exitCode" />.
    ///     Absolute values from 128 are presented as hexadecimal and decimal numbers.
    /// </summary>
    public static string FormatExitCode(int exitCode)
    {
        return Math.Abs(exitCode) <= 128 ? $"{exitCode}" : $"0x{exitCode:X8} ({exitCode} dec)";
    }

    public void ThrowIfErrorExit(string? errorMessage = null)
    {
        if (ExitedSuccessfully)
        {
            return;
        }

        var output = string.Concat(errorMessage, Environment.NewLine, StandardError, Environment.NewLine,
            StandardOutput);
        throw new ExternalOperationException(Command, Arguments, WorkingDir, ExitCode, new Exception(output));
    }
}
