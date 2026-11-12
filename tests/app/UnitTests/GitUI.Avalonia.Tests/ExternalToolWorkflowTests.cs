using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO.Abstractions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class ExternalToolWorkflowTests
{
    private static GitRevision Revision(string id, params string[] parents)
    {
        return new GitRevision(ObjectId.Parse(id))
        {
            ParentIds = [.. parents.Select(parent => ObjectId.Parse(parent))]
        };
    }

    [Fact]
    public void GetConfiguredDiffTool_should_prefer_guitool_then_tool_then_null()
    {
        var module = Substitute.For<IGitModule>();
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
        const string newer = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string older = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string root = "cccccccccccccccccccccccccccccccccccccccc";

        ExternalToolWorkflow.ResolveComparison([Revision(newer, older)]).Should().Be((older, newer));
        ExternalToolWorkflow.ResolveComparison([Revision(newer, older), Revision(older)]).Should().Be((older, newer));
        ExternalToolWorkflow.ResolveComparison([Revision(root)]).Should().BeNull();
        ExternalToolWorkflow.ResolveComparison([]).Should().BeNull();
        ExternalToolWorkflow.ResolveComparison([new GitUIPluginInterfaces.GitRevision(ObjectId.WorkTreeId)]).Should()
            .BeNull();
    }

    [Fact]
    public void OpenWithDifftoolDirDiff_should_launch_the_configured_tool_in_real_repository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-difftool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var marker = Path.Combine(directory, "tool-ran.marker");
        using ServiceContainer services = new();
        try
        {
            GitExtUtils.ServiceContainerRegistry.RegisterServices(services);
            FileSystem fileSystem = new();
            services.AddService<IFileSystem>(fileSystem);
            services.AddService<IGitDirectoryResolver>(new GitDirectoryResolver(fileSystem));
            GitCommands.ServiceContainerRegistry.RegisterServices(services);
            ServiceContainerRegistry.RegisterServices(services);
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
            GitCommands.GitModule module = new(services.GetRequiredService<IGitExecutorProvider>(), directory);

            ExternalToolWorkflow.GetConfiguredDiffTool(module).Should().Be("marker");
            module.OpenWithDifftoolDirDiff("HEAD~1", "HEAD", customTool: null);

            SpinWait.SpinUntil(() => File.Exists(marker), TimeSpan.FromSeconds(20)).Should().BeTrue();
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

    private static void Git(string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory, RedirectStandardError = true
        })!;
        process.WaitForExit();
        process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
    }
}
