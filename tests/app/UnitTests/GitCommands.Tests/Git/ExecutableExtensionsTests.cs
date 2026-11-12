using System.ComponentModel;
using System.Reflection;
using System.Text;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Settings;

namespace GitCommandsTests.Git;

public sealed class ExecutableExtensionsTests : IDisposable
{
    private readonly string _appPath = null!;
    private readonly MockExecutable _executable = null!;
    private readonly Executable _gitExecutable = null!;

    public ExecutableExtensionsTests()
    {
        _executable = new MockExecutable();

        // Work around: When running unittest, Application.UserAppDataPath always points to
        // %APPDATA%Roaming\Microsoft Corporation\Microsoft.TestHost.x86
        // We need to correct it to %APPDATA%\GitExtensions\GitExtensions for v3 at least
        var userAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingPath = Path.Combine(userAppDataPath, "GitExtensions\\GitExtensions\\GitExtensions.settings");
        DistributedSettings settingContainer =
            new(null, GitExtSettingsCache.FromCache(settingPath), SettingLevel.Unknown);
        _appPath = settingContainer.GetString("gitcommand", "git.exe");

        // Execute process in GitExtension working directory, so that git will return success exit-code
        // git always return non-zero exit code when run git reset outside of git repository
        // The test host runs from its output directory rather than a Git Extensions working directory.
        var workingDir = Path.GetDirectoryName(Assembly.GetAssembly(typeof(ExecutableExtensionsTests))!.Location);
        _gitExecutable = new Executable(_appPath, workingDir!);
    }

    void IDisposable.Dispose()
    {
        _executable.Verify();
    }

    [Fact]
    public void GetOutput_with_cache_hit()
    {
        const string arguments = "abc";

        CommandCache cache = new();

        cache.Add(
            arguments,
            "Hello",
            "World!");

        // GetOutput always ignore error output (will not add to cache).
        var output = _executable.GetOutput(arguments, cache: cache);

        output.Should().Be("Hello");

        // Cache should still have a single item
        cache.GetCachedCommands().Count.Should().Be(1);
    }

    [Fact]
    public void GetOutput_with_cache_miss()
    {
        const string arguments = "abc";
        const string commandOutput = "Hello World!";

        // Empty cache
        CommandCache cache = new();

        using (_executable.StageOutput(arguments, commandOutput))
        {
            var output = _executable.GetOutput(arguments, cache: cache);

            output.Should().Be(commandOutput);
        }

        // Validate data stored in cache afterwards
        cache.GetCachedCommands().Count.Should().Be(1);
        cache.TryGet(arguments, out var outputBytes, out var errorBytes).Should().BeTrue();
        outputBytes.Should().Be(commandOutput);
        errorBytes.Should().BeEmpty();
    }

    // Process argument upper bound is actually (short.MaxValue - 1)
    [Theory]
    [InlineData(32766, 32766, 32767, 2, new[] { 1, 1 })]
    [InlineData(32764, 1, 32767, 1, new[] { 2 })]
    public void RunBatchCommand_can_handle_max_length_arguments(int arg1Len, int arg2Len,
        int maxLength, int argCount, int[] expectedProcessedCounts)
    {
        // 3: double quotes + ' '
        // 9: 'reset -- '
        // 1: ' ' added after second Add in ArgumentBuilder
        var appLength = _appPath.Length + 3;
        ArgumentBuilder builder = ["reset --"];
        var len = builder.ToString().Length;
        var args = builder.BuildBatchArguments(
            new[]
            {
                GenerateStringByLength(Math.Max(1, arg1Len - appLength - len - 1)),
                GenerateStringByLength(Math.Max(1, arg2Len - appLength - len - 1))
            }, appLength, maxLength);

        args.Count.Should().Be(argCount);

        // The reset command runs in the GE repo dir, so the result depends on workTree contents
        var index = 0;
        var result = _gitExecutable.RunBatchCommand(args, eventArgs =>
        {
            eventArgs.ExecutionResult.Should().BeTrue();
            eventArgs.ProcessedCount.Should().Be(expectedProcessedCounts[index]);
            index++;
        });
    }

    [Theory]
    [InlineData(32766 - 8, 32766 - 8, int.MaxValue)]
    [InlineData(32766 - 9, 1, int.MaxValue)]
    public void RunBatchCommand_throw_when_cmd_exceed_max_length(int arg1Len, int arg2Len,
        int maxLength)
    {
        var args = new ArgumentBuilder { "reset --" }
            .BuildBatchArguments(
                new[]
                {
                    GenerateStringByLength(Math.Max(1, arg1Len - _appPath.Length - 4)),
                    GenerateStringByLength(Math.Max(1, arg2Len - _appPath.Length - 4))
                }, _appPath.Length + 3, maxLength);

        Action act = () => _gitExecutable.RunBatchCommand(args);
        var ex = act.Should().Throw<ExternalOperationException>().Which;
        ex.InnerException.Should().BeOfType<Win32Exception>();
    }

    private static string GenerateStringByLength(int length)
    {
        StringBuilder sb = new(length);

        for (var i = 0; i < length; i++)
        {
            sb.Append((char)((i % 26) + 97));
        }

        return sb.ToString();
    }
}
