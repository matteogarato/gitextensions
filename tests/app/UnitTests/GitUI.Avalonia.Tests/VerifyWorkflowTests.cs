using System.ComponentModel.Design;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using GitUIPluginInterfaces;
using Microsoft.VisualStudio.Threading;

namespace GitExtensionsTests;

public sealed class VerifyWorkflowTests : IDisposable
{
    private readonly ServiceContainer _serviceContainer = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-verify-{Guid.NewGuid():N}");
    private readonly IGitExecutorProvider _executorProvider;

    public VerifyWorkflowTests()
    {
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        System.IO.Abstractions.IFileSystem fileSystem = new System.IO.Abstractions.FileSystem();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        _serviceContainer.AddService(typeof(System.IO.Abstractions.IFileSystem), fileSystem);
        _serviceContainer.AddService(typeof(IGitDirectoryResolver), gitDirectoryResolver);
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
    public void BuildFsck_should_add_requested_options()
    {
        ArgumentString arguments = VerifyWorkflow.BuildFsck(unreachable: true, full: true, noReflogs: true);

        arguments.ToString().Should().Be("fsck-objects --unreachable --full --no-reflogs");
    }

    [Fact]
    public void ParseFsckOutput_should_extract_supported_object_diagnostics()
    {
        const string objectId = "0123456789012345678901234567890123456789";
        string output = $"dangling blob {objectId}\nwarning in tree {objectId}\nunrecognized diagnostic {objectId}\n";

        IReadOnlyList<VerifyObject> objects = VerifyWorkflow.ParseFsckOutput(output);

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
        GitModule module = CreateRepository();
        string filePath = Path.Combine(_root, "loose-object.txt");
        File.WriteAllText(filePath, "unreferenced object");
        string objectId = module.GitExecutable.GetOutput(new GitArgumentBuilder("hash-object")
        {
            "-w",
            filePath.Quote(),
        }).Trim();

        ExecutionResult result = module.GitExecutable.Execute(
            VerifyWorkflow.BuildFsck(unreachable: false, full: false, noReflogs: false),
            throwOnErrorExit: false,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        VerifyWorkflow.ParseFsckOutput(result.AllOutput)
            .Should()
            .Contain(item => item.Type == "blob" && item.ObjectId.ToString() == objectId);
    }

    private GitModule CreateRepository()
    {
        string path = Path.Combine(_root, "repository");
        Directory.CreateDirectory(path);
        GitModule module = new(_executorProvider, path);
        module.Init(bare: false, shared: false);
        module.SetSetting("user.name", "Avalonia Test");
        module.SetSetting("user.email", "avalonia@example.com");
        module.SetSetting("core.safecrlf", "false");
        return module;
    }
}
