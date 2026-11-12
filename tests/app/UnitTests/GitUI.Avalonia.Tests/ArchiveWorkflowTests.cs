using System.ComponentModel.Design;
using System.IO.Abstractions;
using System.IO.Compression;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AvaloniaTests;

public sealed class ArchiveWorkflowTests : IDisposable
{
    private readonly IGitExecutorProvider _executorProvider;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-archive-{Guid.NewGuid():N}");
    private readonly ServiceContainer _serviceContainer = new();

    public ArchiveWorkflowTests()
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
    public void BuildArchive_should_build_zip_arguments_with_pathspecs()
    {
        const string revisionId = "0123456789012345678901234567890123456789";
        var outputPath = Path.Combine(_root, "archive output.zip");
        var path = "folder/path with spaces.txt";
        GitCommands.GitModule module = new(_executorProvider, _root);
        GitUIPluginInterfaces.GitRevision revision = new(ObjectId.Parse(revisionId));

        var arguments =
            ArchiveWorkflow.BuildArchive(module, revision, outputPath, ArchiveFormat.Zip, [path]);

        arguments.ToString().Should()
            .Be($"archive --format=zip --output \"{outputPath}\" \"{revisionId}\" -- \"{path}\"");
    }

    [Fact]
    public void GetChangedPaths_and_BuildArchive_should_archive_only_non_deleted_changed_files()
    {
        var module = CreateRepository();
        WriteFile(module, "keep.txt", "before");
        WriteFile(module, "remove.txt", "removed");
        Commit(module, "baseline");
        var baseline = ReadHead(module);

        WriteFile(module, "keep.txt", "after");
        WriteFile(module, "added.txt", "added");
        File.Delete(Path.Combine(module.WorkingDir, "remove.txt"));
        Commit(module, "target");
        var target = ReadHead(module);

        var changedPaths = ArchiveWorkflow.GetChangedPaths(module, baseline, target);
        changedPaths.Should().BeEquivalentTo("keep.txt", "added.txt");
        changedPaths.Should().NotContain("remove.txt");

        var outputPath = Path.Combine(_root, "filtered.zip");
        var result = module.GitExecutable.Execute(
            ArchiveWorkflow.BuildArchive(module, target, outputPath, ArchiveFormat.Zip, changedPaths),
            cancellationToken: TestContext.Current.CancellationToken);

        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
        using var archive = ZipFile.OpenRead(outputPath);
        archive.Entries.Select(entry => entry.FullName).Should().BeEquivalentTo("keep.txt", "added.txt");
    }

    [Fact]
    public void BuildArchive_should_reject_an_empty_path_filter()
    {
        GitCommands.GitModule module = new(_executorProvider, _root);
        GitUIPluginInterfaces.GitRevision revision = new(ObjectId.Parse("0123456789012345678901234567890123456789"));

        Action buildCommand = () => ArchiveWorkflow.BuildArchive(
            module,
            revision,
            Path.Combine(_root, "empty.zip"),
            ArchiveFormat.Zip,
            []);

        buildCommand.Should().Throw<ArgumentException>();
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

    private static void WriteFile(GitCommands.GitModule module, string path, string content)
    {
        File.WriteAllText(Path.Combine(module.WorkingDir, path), content);
        module.GitExecutable.RunCommand(new GitArgumentBuilder("add") { "--all" });
    }

    private static void Commit(GitCommands.GitModule module, string message)
    {
        module.GitExecutable.RunCommand(new GitArgumentBuilder("commit") { "--quiet", "-m", message.Quote() });
    }

    private static GitUIPluginInterfaces.GitRevision ReadHead(GitCommands.GitModule module)
    {
        return new(ObjectId.Parse(module.GitExecutable.GetOutput("rev-parse HEAD").Trim()));
    }
}
