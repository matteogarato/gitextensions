using System.ComponentModel.Design;
using System.IO.Abstractions;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AvaloniaTests;

public sealed class VerifyWorkflowTests : IDisposable
{
    private readonly IGitExecutorProvider _executorProvider;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-verify-{Guid.NewGuid():N}");
    private readonly ServiceContainer _serviceContainer = new();

    public VerifyWorkflowTests()
    {
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        IFileSystem fileSystem = new FileSystem();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        _serviceContainer.AddService(typeof(IFileSystem), fileSystem);
        _serviceContainer.AddService(typeof(IGitDirectoryResolver), gitDirectoryResolver);
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
    public void BuildFsck_should_add_requested_options()
    {
        var arguments = VerifyWorkflow.BuildFsck(true, true, true);

        arguments.ToString().Should().Be("fsck-objects --unreachable --full --no-reflogs");
    }

    [Fact]
    public void ParseFsckOutput_should_extract_supported_object_diagnostics()
    {
        const string objectId = "0123456789012345678901234567890123456789";
        var output = $"dangling blob {objectId}\nwarning in tree {objectId}\nunrecognized diagnostic {objectId}\n";

        var objects = VerifyWorkflow.ParseFsckOutput(output);

        objects.Should().HaveCount(2);
        objects[0].Diagnostic.Should().Be("dangling blob");
        objects[0].Type.Should().Be("blob");
        objects[0].ObjectId.ToString().Should().Be(objectId);
        objects[1].Diagnostic.Should().Be("warning in tree");
        objects[1].Type.Should().Be("other");
    }

    [Fact]
    public void BuildFsck_should_find_a_real_unreferenced_git_object()
    {
        var module = CreateRepository();
        var filePath = Path.Combine(_root, "loose-object.txt");
        File.WriteAllText(filePath, "unreferenced object");
        var objectId = module.GitExecutable.GetOutput(new GitArgumentBuilder("hash-object") { "-w", filePath.Quote() })
            .Trim();

        var result = module.GitExecutable.Execute(
            VerifyWorkflow.BuildFsck(false, false, false),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);

        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
        VerifyWorkflow.ParseFsckOutput(result.AllOutput)
            .Should()
            .Contain(item => item.Type == "blob" && item.ObjectId.ToString() == objectId);
    }

    private GitCommands.GitModule CreateRepository()
    {
        var path = Path.Combine(_root, "repository");
        Directory.CreateDirectory(path);
        GitCommands.GitModule module = new(_executorProvider, path);
        module.Init(bare: false, shared: false);
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        module.SetSetting("core.safecrlf", "false");
        return module;
    }
}
