using System.ComponentModel.Design;
using System.IO.Abstractions;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AvaloniaTests;

public sealed class CloneRepositoryWorkflowTests : IDisposable
{
    private readonly IGitExecutorProvider _executorProvider;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-clone-{Guid.NewGuid():N}");
    private readonly ServiceContainer _serviceContainer = new();

    public CloneRepositoryWorkflowTests()
    {
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        IFileSystem fileSystem = new FileSystem();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        _serviceContainer.AddService(fileSystem);
        _serviceContainer.AddService<IGitDirectoryResolver>(gitDirectoryResolver);
        GitCommands.ServiceContainerRegistry.RegisterServices(_serviceContainer);
        _executorProvider = _serviceContainer.GetRequiredService<IGitExecutorProvider>();
        Directory.CreateDirectory(_root);
    }

    void IDisposable.Dispose()
    {
        _serviceContainer.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public void BuildCloneCommand_should_clone_a_local_repository()
    {
        var source = CreateRepository(Path.Combine(_root, "source"));
        Commit(source, "source commit");
        var destination = Path.Combine(_root, "cloned");
        Directory.CreateDirectory(destination);
        GitCommands.GitModule destinationModule = new(_executorProvider, destination);

        var result = destinationModule.GitExecutable.Execute(
            CloneRepositoryWorkflow.BuildCloneCommand(
                destinationModule,
                source.WorkingDir,
                destination,
                bare: false,
                initializeSubmodules: false),
            cancellationToken: TestContext.Current.CancellationToken);

        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
        ((string)destinationModule.GitExecutable.GetOutput("rev-parse HEAD").Trim()).Should()
            .Be(source.GitExecutable.GetOutput("rev-parse HEAD").Trim());
        ((string)destinationModule.GitExecutable.GetOutput("config --get remote.origin.url").Trim()).Should()
            .Be(source.WorkingDir);
    }

    [Fact]
    public void BuildCloneCommand_should_reject_a_relative_destination()
    {
        GitCommands.GitModule module = new(_executorProvider, _root);

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
        GitCommands.GitModule module = new(_executorProvider, _root);

        var command = CloneRepositoryWorkflow.BuildCloneCommand(
            module,
            "https://example.test/repo.git",
            Path.Combine(_root, "cloned"),
            bare: true,
            initializeSubmodules: true);

        command.Arguments.Should().Contain("--bare");
        command.Arguments.Should().Contain("--recurse-submodules");
    }

    private GitCommands.GitModule CreateRepository(string path)
    {
        Directory.CreateDirectory(path);
        GitCommands.GitModule module = new(_executorProvider, path);
        module.Init(bare: false, shared: false);
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        module.SetSetting("core.safecrlf", "false");
        return module;
    }

    private static void Commit(GitCommands.GitModule module, string message)
    {
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit")
        {
            "--quiet", "--allow-empty", "-m", message.Quote()
        });
    }
}
