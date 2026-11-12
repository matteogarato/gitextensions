using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO.Abstractions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AvaloniaTests;

public sealed class ConflictResolutionTests
{
    [Fact]
    public async Task GetConflictsAsync_should_list_conflict_and_stage_resolution_in_real_repository()
    {
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        var directory = Path.Combine(Path.GetTempPath(), $"ge-conflict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        using ServiceContainer serviceContainer = new();
        try
        {
            RegisterServices(serviceContainer);
            Git(directory, "init -q -b main");
            Git(directory, "config user.name t");
            Git(directory, "config user.email t@example.com");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "base\n");
            Git(directory, "add conflict.txt");
            Git(directory, "commit -q -m base");
            Git(directory, "branch feature");
            Git(directory, "checkout -q feature");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "feature\n");
            Git(directory, "commit -q -am feature");
            Git(directory, "checkout -q main");
            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "main\n");
            Git(directory, "commit -q -am main-change");
            Git(directory, "merge feature", 1);

            GitModule module = new(serviceContainer.GetRequiredService<IGitExecutorProvider>(), directory);
            IReadOnlyList<ConflictData> conflicts = await module.GetConflictsAsync();

            conflicts.Select(conflict => conflict.Filename).Should().ContainSingle().Which.Should().Be("conflict.txt");

            File.WriteAllText(Path.Combine(directory, "conflict.txt"), "resolved\n");
            module.StageFile("conflict.txt");

            (await module.GetConflictsAsync()).Should().BeEmpty();
            Git(directory, "diff --cached --name-only").Should().Be("conflict.txt");
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, true);
        }
    }

    private static void RegisterServices(ServiceContainer serviceContainer)
    {
        GitExtUtils.ServiceContainerRegistry.RegisterServices(serviceContainer);
        FileSystem fileSystem = new();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        serviceContainer.AddService<IFileSystem>(fileSystem);
        serviceContainer.AddService<IGitDirectoryResolver>(gitDirectoryResolver);
        GitCommands.ServiceContainerRegistry.RegisterServices(serviceContainer);
        ServiceContainerRegistry.RegisterServices(serviceContainer);
    }

    private static string Git(string workingDirectory, string arguments, int expectedExitCode = 0)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        process.ExitCode.Should().Be(expectedExitCode, process.StandardError.ReadToEnd());
        return output;
    }
}
