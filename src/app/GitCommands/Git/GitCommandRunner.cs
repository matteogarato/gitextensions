using System.Text;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;

namespace GitCommands.Git;

public sealed class GitCommandRunner(IExecutable gitExecutable, Func<Encoding> defaultEncoding) : IGitCommandRunner
{
    IProcess IGitCommandRunner.RunDetached(
        CancellationToken cancellationToken,
        ArgumentString arguments = default,
        bool createWindow = false,
        bool redirectInput = false,
        bool redirectOutput = false,
        Encoding? outputEncoding = null,
        bool throwOnErrorExit = true)
    {
        if (outputEncoding is null && redirectOutput)
        {
            outputEncoding = defaultEncoding();
        }

        return gitExecutable.Start(arguments, createWindow, redirectInput, redirectOutput, outputEncoding, false,
            throwOnErrorExit, cancellationToken);
    }

    void IGitCommandRunner.RunDetached(
        ArgumentString arguments = default,
        bool createWindow = false,
        bool redirectInput = false,
        bool redirectOutput = false,
        Encoding? outputEncoding = null)
    {
        ThreadHelper.FileAndForget(async () =>
        {
            using var process = RunDetached(CancellationToken.None, arguments, createWindow, redirectInput,
                redirectOutput, outputEncoding);
            await process.WaitForExitAsync();
        });
    }
}
