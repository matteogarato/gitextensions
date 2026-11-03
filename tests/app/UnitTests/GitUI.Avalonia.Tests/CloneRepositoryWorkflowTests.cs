using System.ComponentModel.Design;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;

namespace GitExtensionsTests;

public sealed class CloneRepositoryWorkflowTests : IDisposable
{
    private readonly ServiceContainer _serviceContainer = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-clone-{Guid.NewGuid():N}");
    private readonly IGitExecutorProvider _executorProvider;

    public CloneRepositoryWorkflowTests()
    {
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        System.IO.Abstractions.IFileSystem fileSystem = new System.IO.Abstractions.FileSystem();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        _serviceContainer.AddService<System.IO.Abstractions.IFileSystem>(fileSystem);
        _serviceContainer.AddService<IGitDirectoryResolver>(gitDirectoryResolver);
        GitCommands.ServiceContainerRegistry.RegisterServices(_serviceContainer);
        _executorProvider = _serviceContainer.GetRequiredService<IGitExecutorProvider>();
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _serviceContainer.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void BuildCloneCommand_should_clone_a_local_repository()
    {
        GitModule source = CreateRepository(Path.Combine(_root, "source"));
        Commit(source, "source commit");
        string destination = Path.Combine(_root, "cloned");
        Directory.CreateDirectory(destination);
        GitModule destinationModule = new(_executorProvider, destination);

        ExecutionResult result = destinationModule.GitExecutable.Execute(
            CloneRepositoryWorkflow.BuildCloneCommand(
                destinationModule,
                source.WorkingDir,
                destination,
                bare: false,
                initializeSubmodules: false),
            cancellationToken: TestContext.Current.CancellationToken);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        destinationModule.GitExecutable.GetOutput("rev-parse HEAD").Trim()
            .Should().Be(source.GitExecutable.GetOutput("rev-parse HEAD").Trim());
        destinationModule.GitExecutable.GetOutput("config --get remote.origin.url").Trim()
            .Should().Be(source.WorkingDir);
    }

    [Fact]
    public void BuildCloneCommand_should_reject_a_relative_destination()
    {
        GitModule module = new(_executorProvider, _root);

        Action buildCommand = () => CloneRepositoryWorkflow.BuildCloneCommand(
            module,
            "https://example.test/repo.git",
            "relative-path",
            bare: false,
            initializeSubmodules: false);

        buildCommand.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildCloneCommand_should_include_bare_and_submodule_options()
    {
        GitModule module = new(_executorProvider, _root);

        ArgumentString command = CloneRepositoryWorkflow.BuildCloneCommand(
            module,
            "https://example.test/repo.git",
            Path.Combine(_root, "cloned"),
            bare: true,
            initializeSubmodules: true);

        command.Arguments.Should().Contain("--bare");
        command.Arguments.Should().Contain("--recurse-submodules");
    }

    private GitModule CreateRepository(string path)
    {
        Directory.CreateDirectory(path);
        GitModule module = new(_executorProvider, path);
        module.Init(bare: false, shared: false);
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        module.SetSetting("core.safecrlf", "false");
        return module;
    }

    private static void Commit(GitModule module, string message)
    {
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit")
        {
            "--quiet",
            "--allow-empty",
            "-m",
            message.Quote(),
        });
    }
}
