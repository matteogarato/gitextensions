using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands.Utils;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using JetBrains.Annotations;

namespace GitCommands.Git;

/// <summary>
///     Provides extension methods for <see cref="IExecutable" /> that provider operations on executables
///     at a higher level than <see cref="IExecutable.Start" />.
/// </summary>
public static partial class ExecutableExtensions
{
    [GeneratedRegex(@"\u001B[\u0040-\u005F].*?[\u0040-\u007E]", RegexOptions.ExplicitCapture)]
    private static partial Regex AnsiCodeRegex { get; }

    /// <summary>
    ///     Launches a process for the executable and returns its output.
    /// </summary>
    /// <remarks>
    ///     This method uses <see cref="ThreadHelper.JoinableTaskFactory" /> to allow the calling thread to
    ///     do useful work while waiting for the process to exit. Internally, this method delegates to
    ///     <see cref="GetOutputAsync" />.
    /// </remarks>
    /// <param name="executable">The executable from which to launch a process.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
    /// <param name="outputEncoding">
    ///     The text encoding to use when decoding bytes read from the process's standard output and
    ///     standard error streams, or <c>null</c> if the default encoding is to be used.
    /// </param>
    /// <param name="cache">A <see cref="CommandCache" /> to use if command results may be cached, otherwise <c>null</c>.</param>
    /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
    /// <returns>
    ///     The concatenation of standard output (standard error is ignored). To receive these outputs separately, use
    ///     <see cref="Execute" /> instead.
    /// </returns>
    [MustUseReturnValue("If output text is not required, use " + nameof(RunCommand) + " instead")]
    public static string GetOutput(
        this IExecutable executable,
        ArgumentString arguments = default,
        byte[]? input = null,
        Encoding? outputEncoding = null,
        CommandCache? cache = null,
        bool stripAnsiEscapeCodes = true)
    {
        return ThreadHelper.JoinableTaskFactory.Run(() =>
            executable.GetOutputAsync(arguments, input, outputEncoding, cache, stripAnsiEscapeCodes));
    }

    /// <summary>
    ///     Launches a process for the executable per batch item and returns its output.
    /// </summary>
    /// <remarks>
    ///     This method uses <see cref="GetOutput" /> to get concatenated outputs of multiple commands in batch.
    /// </remarks>
    /// <param name="executable">The executable from which to launch processes.</param>
    /// <param name="batchArguments">The array of batch arguments to pass to the executable.</param>
    /// <param name="input">
    ///     Bytes to be written to each process's standard input stream, or <c>null</c> if no input is
    ///     required.
    /// </param>
    /// <param name="outputEncoding">
    ///     The text encoding to use when decoding bytes read from each process's standard output and
    ///     standard error streams, or <c>null</c> if the default encoding is to be used.
    /// </param>
    /// <param name="cache">A <see cref="CommandCache" /> to use if command results may be cached, otherwise <c>null</c>.</param>
    /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
    /// <returns>
    ///     The concatenation of standard output (standard error is ignored). To receive these outputs separately, use
    ///     <see cref="Execute" /> instead.
    /// </returns>
    [MustUseReturnValue("If output text is not required, use " + nameof(RunCommand) + " instead")]
    public static string GetBatchOutput(
        this IExecutable executable,
        ICollection<BatchArgumentItem> batchArguments,
        byte[]? input = null,
        Encoding? outputEncoding = null,
        CommandCache? cache = null,
        bool stripAnsiEscapeCodes = true)
    {
        StringBuilder sb = new();
        foreach (var batch in batchArguments)
        {
            sb.Append(executable.GetOutput(batch.Argument, input, outputEncoding, cache, stripAnsiEscapeCodes));
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Launches a process for the executable and returns its output.
    /// </summary>
    /// <param name="executable">The executable from which to launch a process.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
    /// <param name="outputEncoding">
    ///     The text encoding to use when decoding bytes read from the process's standard output and
    ///     standard error streams, or <c>null</c> if the default encoding is to be used.
    /// </param>
    /// <param name="cache">A <see cref="CommandCache" /> to use if command results may be cached, otherwise <c>null</c>.</param>
    /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
    /// <returns>
    ///     A task that yields the concatenation of standard output (standard error is ignored). To receive these outputs
    ///     separately, use <see cref="ExecuteAsync" /> instead.
    /// </returns>
    public static async Task<string> GetOutputAsync(
        this IExecutable executable,
        ArgumentString arguments = default,
        byte[]? input = null,
        Encoding? outputEncoding = null,
        CommandCache? cache = null,
        bool stripAnsiEscapeCodes = true)
    {
        outputEncoding ??= GitModule.SystemEncoding;

        if (cache?.TryGet(arguments, out var output, out _) is true)
        {
            return output;
        }

        using var process = executable.Start(
            arguments,
            false,
            input is not null,
            true,
            outputEncoding,
            throwOnErrorExit: true);
        if (input is not null)
        {
#if DEBUG
            Debug.WriteLine($"git {arguments} {Encoding.UTF8.GetString(input)}");
#endif
            await process.StandardInput.BaseStream.WriteAsync(input.AsMemory(0, input.Length));
            process.StandardInput.Close();
        }
#if DEBUG
        else
        {
            Debug.WriteLine($"git {arguments}");
        }
#endif

        using MemoryStream outputBuffer = new();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputBuffer);
        var exitTask = process.WaitForExitAsync();

        await Task.WhenAll(outputTask, exitTask);

        var outputStr = CleanString(stripAnsiEscapeCodes,
            EncodingHelper.DecodeString(outputBuffer.ToArray(), [], ref outputEncoding));
        if (cache is not null && await exitTask == 0)
        {
            cache.Add(arguments, outputStr, "");
        }

        return outputStr;
    }

    /// <summary>
    ///     Launches a process for the executable and returns <c>true</c> if its exit code is zero.
    /// </summary>
    /// <remarks>
    ///     This method uses <see cref="ThreadHelper.JoinableTaskFactory" /> to allow the calling thread to
    ///     do useful work while waiting for the process to exit. Internally, this method delegates to
    ///     <see cref="RunCommandAsync" />.
    /// </remarks>
    /// <param name="executable">The executable from which to launch a process.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
    /// <param name="createWindow">A flag indicating whether a console window should be created and bound to the process.</param>
    /// <param name="throwOnErrorExit">A flag configuring whether to throw an exception if the exit code is not 0.</param>
    /// <returns><c>true</c> if the process's exit code was zero, otherwise <c>false</c>.</returns>
    [MustUseReturnValue("Callers should verify that " + nameof(RunCommand) + " returned true")]
    public static bool RunCommand(
        this IExecutable executable,
        ArgumentString arguments = default,
        byte[]? input = null,
        bool createWindow = false,
        bool throwOnErrorExit = true)
    {
        return ThreadHelper.JoinableTaskFactory.Run(() =>
            executable.RunCommandAsync(arguments, input, createWindow, throwOnErrorExit));
    }

    /// <summary>
    ///     Launches a process for the executable per batch item, and returns <see cref="ExecutionResult" />.
    /// </summary>
    /// <remarks>
    ///     This method uses <see cref="RunCommand" /> to execute multiple commands in batch, used in accordance with
    ///     <see cref="ArgumentBuilderExtensions.BuildBatchArguments(ArgumentBuilder, IEnumerable{string}, int?, int)" />
    ///     to work around Windows command line length 32767 character limitation
    ///     <see
    ///         href="https://docs.microsoft.com/en-us/windows/desktop/api/processthreadsapi/nf-processthreadsapi-createprocessa" />
    ///     .
    /// </remarks>
    /// <param name="executable">The executable from which to launch processes.</param>
    /// <param name="batchArguments">The array of batch arguments to pass to the executable.</param>
    /// <param name="writeInput">
    ///     A callback that writes bytes to the process's standard input stream, or <c>null</c> if no
    ///     input is required.
    /// </param>
    /// <param name="throwOnErrorExit">A flag configuring whether to throw an exception if the exit code is not 0.</param>
    /// <returns>
    ///     An <see cref="ExecutionResult" /> object that gives access to exit code, standard output and standard error
    ///     values.
    /// </returns>
    [MustUseReturnValue("Callers should verify that " + nameof(RunBatchCommand) + " returned true")]
    public static ExecutionResult? RunBatchCommand(
        this IExecutable executable,
        ICollection<BatchArgumentItem> batchArguments,
        Action<BatchProgressEventArgs>? action = null,
        Action<StreamWriter>? writeInput = null,
        bool throwOnErrorExit = true)
    {
        var total = batchArguments.Sum(item => item.BatchItemsCount);
        ExecutionResult? result = null;

        foreach (var item in batchArguments)
        {
            var itemResult =
                executable.Execute(item.Argument, writeInput, throwOnErrorExit: throwOnErrorExit);
            result = result is null
                ? itemResult
                : new ExecutionResult(
                    executable,
                    item.Argument,
                    result?.StandardOutput + itemResult.StandardOutput,
                    result?.StandardError + itemResult.StandardError,
                    result?.ExitCode is > 0 or < 0 ? result?.ExitCode : itemResult.ExitCode);

            // Invoke batch progress callback
            action?.Invoke(new BatchProgressEventArgs(item.BatchItemsCount, result?.ExitedSuccessfully ?? false));
        }

        return result;
    }

    /// <summary>
    ///     Launches a process for the executable and returns <c>true</c> if its exit code is zero.
    /// </summary>
    /// <param name="executable">The executable from which to launch a process.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="input">Bytes to be written to the process's standard input stream, or <c>null</c> if no input is required.</param>
    /// <param name="createWindow">A flag indicating whether a console window should be created and bound to the process.</param>
    /// <param name="throwOnErrorExit">A flag configuring whether to throw an exception if the exit code is not 0.</param>
    /// <returns>A task that yields <c>true</c> if the process's exit code was zero, otherwise <c>false</c>.</returns>
    public static async Task<bool> RunCommandAsync(
        this IExecutable executable,
        ArgumentString arguments = default,
        byte[]? input = null,
        bool createWindow = false,
        bool throwOnErrorExit = true)
    {
        using var process =
            executable.Start(arguments, createWindow, input is not null, throwOnErrorExit: throwOnErrorExit);
        if (input is not null)
        {
            // Note that output is not redirected, any output is written to the console
            await process.StandardInput.BaseStream.WriteAsync(input.AsMemory(0, input.Length));
            process.StandardInput.Close();
        }

        return await process.WaitForExitAsync() == 0;
    }

    /// <summary>
    ///     Launches a process for the executable and returns an object detailing exit code, standard output and standard error
    ///     values.
    /// </summary>
    /// <remarks>
    ///     This method uses <see cref="ThreadHelper.JoinableTaskFactory" /> to allow the calling thread to
    ///     do useful work while waiting for the process to exit. Internally, this method delegates to
    ///     <see cref="ExecuteAsync" />.
    /// </remarks>
    /// <param name="executable">The executable from which to launch a process.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="writeInput">
    ///     A callback that writes bytes to the process's standard input stream, or <c>null</c> if no
    ///     input is required.
    /// </param>
    /// <param name="outputEncoding">
    ///     The text encoding to use when decoding bytes read from the process's standard output and
    ///     standard error streams, or <c>null</c> if the default encoding is to be used.
    /// </param>
    /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
    /// <param name="throwOnErrorExit">A flag configuring whether to throw an exception if the exit code is not 0.</param>
    /// <param name="cancellationToken">An optional token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     An <see cref="ExecutionResult" /> object that gives access to exit code, standard output and standard error
    ///     values.
    /// </returns>
    [MustUseReturnValue("If execution result is not required, use " + nameof(RunCommand) + " instead")]
    public static ExecutionResult Execute(
        this IExecutable executable,
        ArgumentString arguments,
        Action<StreamWriter>? writeInput = null,
        Encoding? outputEncoding = null,
        CommandCache? cache = null,
        bool stripAnsiEscapeCodes = true,
        bool throwOnErrorExit = true,
        CancellationToken cancellationToken = default)
    {
        return ThreadHelper.JoinableTaskFactory.Run(() => executable.ExecuteAsync(arguments, writeInput, outputEncoding,
            cache, "", stripAnsiEscapeCodes, throwOnErrorExit, cancellationToken));
    }

    /// <summary>
    ///     Launches a process for the executable and returns an object detailing exit code, standard output and standard error
    ///     values.
    /// </summary>
    /// <param name="executable">The executable from which to launch a process.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="writeInput">
    ///     A callback that writes bytes to the process's standard input stream, or <c>null</c> if no
    ///     input is required.
    /// </param>
    /// <param name="outputEncoding">
    ///     The text encoding to use when decoding bytes read from the process's standard output and
    ///     standard error streams, or <c>null</c> if the default encoding is to be used.
    /// </param>
    /// <param name="stripAnsiEscapeCodes">A flag indicating whether ANSI escape codes should be removed from output strings.</param>
    /// <param name="throwOnErrorExit">A flag configuring whether to throw an exception if the exit code is not 0.</param>
    /// <param name="cancellationToken">An optional token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     A task that yields an <see cref="ExecutionResult" /> object that gives access to exit code, standard output
    ///     and standard error values.
    /// </returns>
    public static async Task<ExecutionResult> ExecuteAsync(
        this IExecutable executable,
        ArgumentString arguments,
        Action<StreamWriter>? writeInput = null,
        Encoding? outputEncoding = null,
        CommandCache? cache = null,
        string extraCacheKey = "",
        bool stripAnsiEscapeCodes = true,
        bool throwOnErrorExit = true,
        CancellationToken cancellationToken = default)
    {
        outputEncoding ??= GitModule.SystemEncoding;

        var cacheKey =
            $"{arguments} #{executable.WorkingDir}::{(stripAnsiEscapeCodes ? "stripped" : "plain")}::{outputEncoding.WebName}::{extraCacheKey}";
        if (cache?.TryGet(cacheKey, out var cachedOutput, out var cachedError) is true)
        {
            return new ExecutionResult(
                executable,
                arguments,
                cachedOutput,
                cachedError,
                0);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var process = executable.Start(arguments, false, writeInput is not null, true, outputEncoding,
            throwOnErrorExit: throwOnErrorExit, cancellationToken: cancellationToken);
        using MemoryStream outputBuffer = new();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputBuffer, cancellationToken);

        if (writeInput is not null)
        {
#if DEBUG
            using MemoryStream mem = new();
            using StreamWriter sw = new(mem);
            writeInput(sw);

            Debug.WriteLine($"git {arguments} {Encoding.UTF8.GetString(mem.ToArray(), 0, (int)mem.Length)}");
#endif

            // TODO do we want to make this async?
            writeInput(process.StandardInput);
            process.StandardInput.Close();
        }
#if DEBUG
        else
        {
            Debug.WriteLine($"git {arguments}");
        }
#endif

        // Wait for the process to exit (or be cancelled) and for the output
        var exitCode = await process.WaitForExitAsync(cancellationToken);
        await outputTask;
        cancellationToken.ThrowIfCancellationRequested();

        var output = CleanString(stripAnsiEscapeCodes,
            outputEncoding.GetString(outputBuffer.GetBuffer(), 0, (int)outputBuffer.Length));
        var error = CleanString(stripAnsiEscapeCodes, process.StandardError);
        if (cache is not null && exitCode == 0)
        {
            cache.Add(cacheKey, output, error);
        }

        return new ExecutionResult(
            executable,
            arguments,
            output,
            error,
            exitCode);
    }

    [Pure]
    private static string CleanString(bool stripAnsiEscapeCodes, string s)
    {
        // NOTE Regex returns the original string if no ANSI codes are found (no allocation)
        return stripAnsiEscapeCodes
            ? AnsiCodeRegex.Replace(s, "")
            : s;
    }
}
