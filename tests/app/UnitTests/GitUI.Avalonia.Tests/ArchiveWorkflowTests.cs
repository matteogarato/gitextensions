using System.ComponentModel.Design;
using System.IO.Compression;
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

public sealed class ArchiveWorkflowTests : IDisposable
{
    private readonly ServiceContainer _serviceContainer = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-archive-{Guid.NewGuid():N}");
    private readonly IGitExecutorProvider _executorProvider;

    public ArchiveWorkflowTests()
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
    public void BuildArchive_should_build_zip_arguments_with_pathspecs()
    {
        const string revisionId = "0123456789012345678901234567890123456789";
        string outputPath = Path.Combine(_root, "archive output.zip");
        string path = "folder/path with spaces.txt";
        GitModule module = new(_executorProvider, _root);
        GitRevision revision = new(ObjectId.Parse(revisionId));

        ArgumentString arguments = ArchiveWorkflow.BuildArchive(module, revision, outputPath, ArchiveFormat.Zip, [path]);

        arguments.ToString().Should().Be($"archive --format=zip --output \"{outputPath}\" \"{revisionId}\" -- \"{path}\"");
    }

    [Fact]
    public void GetChangedPaths_and_BuildArchive_should_archive_only_non_deleted_changed_files()
    {
        GitModule module = CreateRepository();
        WriteFile(module, "keep.txt", "before");
        WriteFile(module, "remove.txt", "removed");
        Commit(module, "baseline");
        GitRevision baseline = ReadHead(module);

        WriteFile(module, "keep.txt", "after");
        WriteFile(module, "added.txt", "added");
        File.Delete(Path.Combine(module.WorkingDir, "remove.txt"));
        Commit(module, "target");
        GitRevision target = ReadHead(module);

        IReadOnlyList<string> changedPaths = ArchiveWorkflow.GetChangedPaths(module, baseline, target);
        changedPaths.Should().BeEquivalentTo("keep.txt", "added.txt");
        changedPaths.Should().NotContain("remove.txt");

        string outputPath = Path.Combine(_root, "filtered.zip");
        ExecutionResult result = module.GitExecutable.Execute(
            ArchiveWorkflow.BuildArchive(module, target, outputPath, ArchiveFormat.Zip, changedPaths),
            cancellationToken: TestContext.Current.CancellationToken);

        result.ExitedSuccessfully.Should().BeTrue(result.AllOutput);
        using ZipArchive archive = ZipFile.OpenRead(outputPath);
        archive.Entries.Select(entry => entry.FullName).Should().BeEquivalentTo("keep.txt", "added.txt");
    }

    [Fact]
    public void BuildArchive_should_reject_an_empty_path_filter()
    {
        GitModule module = new(_executorProvider, _root);
        GitRevision revision = new(ObjectId.Parse("0123456789012345678901234567890123456789"));

        Action buildCommand = () => ArchiveWorkflow.BuildArchive(
            module,
            revision,
            Path.Combine(_root, "empty.zip"),
            ArchiveFormat.Zip,
            []);

        buildCommand.Should().Throw<ArgumentException>();
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

    private static void WriteFile(GitModule module, string path, string content)
    {
        File.WriteAllText(Path.Combine(module.WorkingDir, path), content);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--all" });
    }

    private static void Commit(GitModule module, string message)
    {
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit")
        {
            "--quiet",
            "-m",
            message.Quote(),
        });
    }

    private static GitRevision ReadHead(GitModule module)
        => new(ObjectId.Parse(module.GitExecutable.GetOutput("rev-parse HEAD").Trim()));
}
