using System.ComponentModel.Design;
using System.Diagnostics;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using GitUIPluginInterfaces;
using NSubstitute;

namespace GitExtensionsTests;

public sealed class ExternalToolWorkflowTests
{
    private static GitRevision Revision(string id, params string[] parents)
        => new(ObjectId.Parse(id)) { ParentIds = [.. parents.Select(parent => ObjectId.Parse(parent))] };

    [Fact]
    public void GetConfiguredDiffTool_should_prefer_guitool_then_tool_then_null()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GetEffectiveSetting("diff.guitool", Arg.Any<string>()).Returns("");
        module.GetEffectiveSetting("diff.tool", Arg.Any<string>()).Returns("meld");
        ExternalToolWorkflow.GetConfiguredDiffTool(module).Should().Be("meld");

        module.GetEffectiveSetting("diff.guitool", Arg.Any<string>()).Returns("kdiff3");
        ExternalToolWorkflow.GetConfiguredDiffTool(module).Should().Be("kdiff3");

        module.GetEffectiveSetting("diff.guitool", Arg.Any<string>()).Returns("");
        module.GetEffectiveSetting("diff.tool", Arg.Any<string>()).Returns(" ");
        ExternalToolWorkflow.GetConfiguredDiffTool(module).Should().BeNull();
    }

    [Fact]
    public void ResolveComparison_should_use_parent_for_one_commit_and_both_for_two()
    {
        const string Newer = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string Older = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string Root = "cccccccccccccccccccccccccccccccccccccccc";

        ExternalToolWorkflow.ResolveComparison([Revision(Newer, Older)]).Should().Be((Older, Newer));
        ExternalToolWorkflow.ResolveComparison([Revision(Newer, Older), Revision(Older)]).Should().Be((Older, Newer));
        ExternalToolWorkflow.ResolveComparison([Revision(Root)]).Should().BeNull();
        ExternalToolWorkflow.ResolveComparison([]).Should().BeNull();
        ExternalToolWorkflow.ResolveComparison([new GitRevision(ObjectId.WorkTreeId)]).Should().BeNull();
    }

    [Fact]
    public void OpenWithDifftoolDirDiff_should_launch_the_configured_tool_in_real_repository()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ge-difftool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string marker = Path.Combine(directory, "tool-ran.marker");
        using ServiceContainer services = new();
        try
        {
            GitExtUtils.ServiceContainerRegistry.RegisterServices(services);
            System.IO.Abstractions.FileSystem fileSystem = new();
            services.AddService<System.IO.Abstractions.IFileSystem>(fileSystem);
            services.AddService<IGitDirectoryResolver>(new GitDirectoryResolver(fileSystem));
            GitCommands.ServiceContainerRegistry.RegisterServices(services);
            GitUI.ServiceContainerRegistry.RegisterServices(services);
            Git(directory, "init -q -b main");
            Git(directory, "config user.name t");
            Git(directory, "config user.email t@example.com");
            File.WriteAllText(Path.Combine(directory, "a.txt"), "1\n");
            Git(directory, "add a.txt");
            Git(directory, "commit -q -m one");
            File.WriteAllText(Path.Combine(directory, "a.txt"), "2\n");
            Git(directory, "commit -q -am two");
            Git(directory, "config diff.tool marker");
            Git(directory, "config difftool.marker.cmd \"touch '" + marker + "'\"");
            GitModule module = new(services.GetRequiredService<IGitExecutorProvider>(), directory);

            ExternalToolWorkflow.GetConfiguredDiffTool(module).Should().Be("marker");
            module.OpenWithDifftoolDirDiff("HEAD~1", "HEAD", customTool: null);

            SpinWait.SpinUntil(() => File.Exists(marker), TimeSpan.FromSeconds(20)).Should().BeTrue();
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Git(string workingDirectory, string arguments)
    {
        using Process process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = workingDirectory, RedirectStandardError = true })!;
        process.WaitForExit();
        process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
    }
}
