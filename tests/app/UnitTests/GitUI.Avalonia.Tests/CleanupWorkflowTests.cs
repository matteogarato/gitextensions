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

public sealed class CleanupWorkflowTests : IDisposable
{
    private readonly ServiceContainer _serviceContainer = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-cleanup-{Guid.NewGuid():N}");
    private readonly IGitExecutorProvider _executorProvider;

    public CleanupWorkflowTests()
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
    public void BuildClean_should_quote_filters_and_place_pathspecs_after_separator()
    {
        ArgumentString arguments = CleanupWorkflow.BuildClean(
            CleanMode.All,
            dryRun: true,
            directories: true,
            paths: ["folder name", "--force"],
            excludes: ["keep file.txt"]);

        arguments.ToString().Should().Be(
            "clean -x -d --dry-run \"--exclude=keep file.txt\" -- \"folder name\" \"--force\"");
    }

    [Fact]
    public void BuildSubmoduleClean_should_build_recursive_clean_command()
    {
        ArgumentString arguments = CleanupWorkflow.BuildSubmoduleClean(
            CleanMode.OnlyIgnored,
            dryRun: false,
            directories: true,
            paths: ["module path"]);

        arguments.ToString().Should().Be("submodule foreach --recursive git clean -X -d -f -- \"module path\"");
    }

    [Fact]
    public void ToRepositoryPath_should_reject_paths_outside_the_repository()
    {
        string repository = Path.Combine(_root, "repository");
        string outside = Path.Combine(_root, "outside.txt");

        Action convertPath = () => CleanupWorkflow.ToRepositoryPath(repository, outside);

        convertPath.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildClean_should_preview_and_remove_only_the_selected_untracked_path()
    {
        GitModule module = CreateRepository();
        WriteFile(module, "tracked.txt", "tracked");
        Commit(module, "baseline");
        WriteUntrackedFile(module, "remove file.txt", "remove");
        WriteUntrackedFile(module, "keep.txt", "keep");

        ArgumentString preview = CleanupWorkflow.BuildClean(
            CleanMode.OnlyNonIgnored,
            dryRun: true,
            directories: false,
            paths: ["remove file.txt"]);
        ExecutionResult previewResult = module.GitExecutable.Execute(
            preview,
            cancellationToken: TestContext.Current.CancellationToken);

        previewResult.ExitedSuccessfully.Should().BeTrue(previewResult.AllOutput);
        previewResult.AllOutput.Should().Contain("Would remove remove file.txt");
        File.Exists(Path.Combine(module.WorkingDir, "remove file.txt")).Should().BeTrue();

        ExecutionResult cleanupResult = module.GitExecutable.Execute(
            CleanupWorkflow.BuildClean(
                CleanMode.OnlyNonIgnored,
                dryRun: false,
                directories: false,
                paths: ["remove file.txt"]),
            cancellationToken: TestContext.Current.CancellationToken);

        cleanupResult.ExitedSuccessfully.Should().BeTrue(cleanupResult.AllOutput);
        File.Exists(Path.Combine(module.WorkingDir, "remove file.txt")).Should().BeFalse();
        File.Exists(Path.Combine(module.WorkingDir, "keep.txt")).Should().BeTrue();
        File.Exists(Path.Combine(module.WorkingDir, "tracked.txt")).Should().BeTrue();
    }

    [Fact]
    public void BuildClean_should_preserve_excluded_paths_and_remove_ignored_files_in_all_mode()
    {
        GitModule module = CreateRepository();
        WriteFile(module, ".gitignore", "ignored.txt");
        Commit(module, "baseline");
        WriteUntrackedFile(module, "ignored.txt", "ignored");
        WriteUntrackedFile(module, "keep file.txt", "keep");
        WriteUntrackedFile(module, "remove.txt", "remove");

        ExecutionResult cleanupResult = module.GitExecutable.Execute(
            CleanupWorkflow.BuildClean(
                CleanMode.All,
                dryRun: false,
                directories: false,
                excludes: ["keep file.txt"]),
            cancellationToken: TestContext.Current.CancellationToken);

        cleanupResult.ExitedSuccessfully.Should().BeTrue(cleanupResult.AllOutput);
        File.Exists(Path.Combine(module.WorkingDir, "ignored.txt")).Should().BeFalse();
        File.Exists(Path.Combine(module.WorkingDir, "remove.txt")).Should().BeFalse();
        File.Exists(Path.Combine(module.WorkingDir, "keep file.txt")).Should().BeTrue();
    }

    private GitModule CreateRepository()
    {
        string path = Path.Combine(_root, $"repository-{Guid.NewGuid():N}");
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

    private static void WriteUntrackedFile(GitModule module, string path, string content)
    {
        File.WriteAllText(Path.Combine(module.WorkingDir, path), content);
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
}
