using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO.Abstractions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class ConflictWorkflowTests
{
    [Theory]
    [InlineData(ConflictSide.Ours, "checkout --ours -- \"a.bin\"")]
    [InlineData(ConflictSide.Theirs, "checkout --theirs -- \"a.bin\"")]
    public void BuildCheckoutSide_should_select_the_requested_side(ConflictSide side, string expected)
    {
        ConflictWorkflow.BuildCheckoutSide(side, "a.bin").ToString().Should().Be(expected);
    }

    [Fact]
    public void BuildMerge_should_pass_strategy_and_message_file()
    {
        var module = Substitute.For<IGitModule>();
        module.GetPathForGitExecution(Arg.Any<string?>()).Returns(call => call.Arg<string?>());
        var source = Substitute.For<IGitRef>();
        source.Name.Returns("feature");

        var arguments = MergeWorkflow
            .BuildMerge(module, source, MergeOptions.Default with { Strategy = "ours" }, "/tmp/msg.txt").ToString();

        arguments.Should().Contain("--strategy=ours").And.Contain("-F \"/tmp/msg.txt\"");
    }

    [Fact]
    public void BuildMerge_should_add_log_only_when_requested()
    {
        var module = Substitute.For<IGitModule>();
        module.GetPathForGitExecution(Arg.Any<string?>()).Returns(call => call.Arg<string?>());
        var source = Substitute.For<IGitRef>();
        source.Name.Returns("feature");

        MergeWorkflow.BuildMerge(module, source, MergeOptions.Default).ToString().Should().NotContain("--log");
        MergeWorkflow.BuildMerge(module, source, MergeOptions.Default with { IncludeLog = true }).ToString()
            .Should().Contain($"--log={MergeWorkflow.MergeLogEntryCount}");
    }

    [Fact]
    public void WriteMessageFile_should_return_null_for_blank_message_and_a_file_otherwise()
    {
        MergeWorkflow.WriteMessageFile("  ").Should().BeNull();

        var path = MergeWorkflow.WriteMessageFile("custom message");
        try
        {
            File.ReadAllText(path!).Should().Be("custom message");
        }
        finally
        {
            File.Delete(path!);
        }
    }

    [Fact]
    public void ResolveWithSide_should_take_side_stage_it_and_offer_continue_in_real_repository()
    {
        using var repository = TestRepository.Create();
        repository.Git("merge feature", 1);

        ConflictWorkflow.BuildContinue(repository.Module).Should().NotBeNull();
        ConflictWorkflow.ResolveWithSide(repository.Module, "text.txt", ConflictSide.Theirs).Should().BeNull();
        ConflictWorkflow.ResolveWithSide(repository.Module, "binary.bin", ConflictSide.Ours).Should().BeNull();
        ConflictWorkflow.ResolveWithSide(repository.Module, "removed.txt", ConflictSide.Theirs).Should().BeNull();

        File.ReadAllText(Path.Combine(repository.Directory, "text.txt")).Should().Be("feature\n");
        File.ReadAllText(Path.Combine(repository.Directory, "binary.bin")).Should().Be("main-binary\n");
        File.Exists(Path.Combine(repository.Directory, "removed.txt")).Should().BeFalse();
        repository.Git("diff --name-only --diff-filter=U").Should().BeEmpty();
    }

    [Fact]
    public void BuildContinue_should_be_null_without_an_operation_in_progress()
    {
        using var repository = TestRepository.Create();

        ConflictWorkflow.BuildContinue(repository.Module).Should().BeNull();
    }

    private sealed class TestRepository : IDisposable
    {
        private readonly ServiceContainer _services = new();

        private TestRepository(string directory)
        {
            Directory = directory;
            GitExtUtils.ServiceContainerRegistry.RegisterServices(_services);
            FileSystem fileSystem = new();
            _services.AddService<IFileSystem>(fileSystem);
            _services.AddService<IGitDirectoryResolver>(new GitDirectoryResolver(fileSystem));
            GitCommands.ServiceContainerRegistry.RegisterServices(_services);
            ServiceContainerRegistry.RegisterServices(_services);
            Module = new GitCommands.GitModule(_services.GetRequiredService<IGitExecutorProvider>(), directory);
        }

        public string Directory { get; }

        public GitCommands.GitModule Module { get; }

        void IDisposable.Dispose()
        {
            _services.Dispose();
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            System.IO.Directory.Delete(Directory, true);
        }

        public static TestRepository Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"ge-side-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);
            TestRepository repository = new(directory);
            repository.Git("init -q -b main");
            repository.Git("config user.name t");
            repository.Git("config user.email t@example.com");
            repository.Write("text.txt", "base\n");
            repository.Write("binary.bin", "base-binary\n");
            repository.Write("removed.txt", "base\n");
            repository.Git("add .");
            repository.Git("commit -q -m base");
            repository.Git("branch feature");
            repository.Git("checkout -q feature");
            repository.Write("text.txt", "feature\n");
            repository.Write("binary.bin", "feature-binary\n");
            repository.Git("rm -q removed.txt");
            repository.Git("commit -q -am feature");
            repository.Git("checkout -q main");
            repository.Write("text.txt", "main\n");
            repository.Write("binary.bin", "main-binary\n");
            repository.Write("removed.txt", "main-edit\n");
            repository.Git("commit -q -am main-change");
            return repository;
        }

        public string Git(string arguments, int expectedExitCode = 0)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Directory, RedirectStandardOutput = true, RedirectStandardError = true
            })!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            process.ExitCode.Should().Be(expectedExitCode, process.StandardError.ReadToEnd());
            return output;
        }

        private void Write(string name, string content)
        {
            File.WriteAllText(Path.Combine(Directory, name), content);
        }
    }
}
