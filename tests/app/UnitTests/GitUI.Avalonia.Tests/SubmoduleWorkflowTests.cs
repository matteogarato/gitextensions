using System.ComponentModel.Design;
using System.IO.Abstractions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;

namespace GitUI.AvaloniaTests;

public sealed class SubmoduleWorkflowTests : IDisposable
{
    private readonly IGitExecutorProvider _executorProvider;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ge-submodule-{Guid.NewGuid():N}");
    private readonly ServiceContainer _serviceContainer = new();

    public SubmoduleWorkflowTests()
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
    public void BuildAdd_should_clone_and_stage_a_submodule()
    {
        var parent = CreateRepository(Path.Combine(_root, "parent"));
        var child = CreateRepository(Path.Combine(_root, "child"));
        Commit(parent, "parent");
        Commit(child, "child");

        const string submodulePath = "libraries/child";
        var result = parent.GitExecutable.Execute(
            SubmoduleWorkflow.BuildAdd(
                child.WorkingDir,
                submodulePath,
                null,
                false,
                Commands.GetAllowFileConfig()),
            cancellationToken: TestContext.Current.CancellationToken);

        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
        ((string)parent.GitExecutable.GetOutput($"ls-files --stage -- {submodulePath}")).Should().Contain("160000");
        ((string)parent.GitExecutable.GetOutput("diff --cached --name-only -- .gitmodules").Trim()).Should()
            .Be(".gitmodules");
        Enumerable.OfType<IGitSubmoduleInfo>(parent.GetSubmodulesInfo())
            .Should().ContainSingle(info => info.LocalPath == submodulePath && info.IsInitialized);
    }

    [Fact]
    public void Update_should_initialize_a_deinitialized_submodule()
    {
        var parent = CreateRepository(Path.Combine(_root, "parent"));
        var child = CreateRepository(Path.Combine(_root, "child"));
        Commit(parent, "parent");
        Commit(child, "child");

        const string submodulePath = "libraries/child";
        AddSubmodule(parent, child, submodulePath);
        Commit(parent, "add submodule");
        parent.GitExecutable.RunCommand(new GitArgumentBuilder("submodule")
        {
            "deinit", "-f", "--", submodulePath.QuoteNe()
        });

        var submodulePathOnDisk = parent.GetSubmoduleFullPath(submodulePath);
        File.Exists(Path.Combine(submodulePathOnDisk, ".git")).Should().BeFalse();

        var result = parent.GitExecutable.Execute(
            Commands.SubmoduleUpdate(submodulePath, Commands.GetAllowFileConfig()),
            cancellationToken: TestContext.Current.CancellationToken);

        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
        File.Exists(Path.Combine(submodulePathOnDisk, ".git")).Should().BeTrue();
        parent.GetSubmodule(submodulePath).GetCurrentCheckout().Should().Be(child.GetCurrentCheckout());
    }

    [Fact]
    public void Update_should_initialize_nested_submodules_recursively()
    {
        var parent = CreateRepository(Path.Combine(_root, "parent"));
        var child = CreateRepository(Path.Combine(_root, "child"));
        var nested = CreateRepository(Path.Combine(_root, "nested"));
        Commit(parent, "parent");
        Commit(child, "child");
        Commit(nested, "nested");

        const string submodulePath = "libraries/child";
        const string nestedPath = "dependencies/nested";
        AddSubmodule(child, nested, nestedPath);
        Commit(child, "add nested submodule");
        AddSubmodule(parent, child, submodulePath);
        Commit(parent, "add submodule");

        var nestedPathOnDisk = Path.Combine(parent.GetSubmoduleFullPath(submodulePath), nestedPath);
        File.Exists(Path.Combine(nestedPathOnDisk, ".git")).Should().BeFalse();

        var result = parent.GitExecutable.Execute(
            Commands.SubmoduleUpdate(submodulePath, Commands.GetAllowFileConfig()),
            cancellationToken: TestContext.Current.CancellationToken);

        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
        File.Exists(Path.Combine(nestedPathOnDisk, ".git")).Should().BeTrue();
        parent.GetSubmodule(submodulePath).GetSubmodule(nestedPath).GetCurrentCheckout()
            .Should().Be(nested.GetCurrentCheckout());
    }

    [Fact]
    public void Sync_should_update_the_submodule_remote_from_gitmodules()
    {
        var parent = CreateRepository(Path.Combine(_root, "parent"));
        var child = CreateRepository(Path.Combine(_root, "child"));
        Commit(parent, "parent");
        Commit(child, "child");

        const string submodulePath = "libraries/child";
        const string synchronizedUrl = "https://example.invalid/updated-child.git";
        AddSubmodule(parent, child, submodulePath);
        Commit(parent, "add submodule");
        parent.GitExecutable.RunCommand(new GitArgumentBuilder("config")
        {
            "-f", ".gitmodules", $"submodule.{submodulePath}.url", synchronizedUrl.Quote()
        });

        var result = parent.GitExecutable.Execute(
            Commands.SubmoduleSync(submodulePath),
            cancellationToken: TestContext.Current.CancellationToken);

        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
        ((string)parent.GetSetting($"submodule.{submodulePath}.url")).Should().Be(synchronizedUrl);
    }

    [Fact]
    public void Remove_should_unstage_the_gitlink_and_remove_the_last_submodule_configuration()
    {
        var parent = CreateRepository(Path.Combine(_root, "parent"));
        var child = CreateRepository(Path.Combine(_root, "child"));
        Commit(parent, "parent");
        Commit(child, "child");

        const string submodulePath = "libraries/child";
        AddSubmodule(parent, child, submodulePath);
        Commit(parent, "add submodule");

        var submodule = Enumerable
            .OfType<IGitSubmoduleInfo>(parent.GetSubmodulesInfo())
            .Single(info => info.LocalPath == submodulePath);

        SubmoduleWorkflow.Remove(parent, submodule);

        ((string)parent.GitExecutable.GetOutput($"ls-files --stage -- {submodulePath}")).Should().BeEmpty();
        ((string)parent.GitExecutable.GetOutput("ls-files --stage -- .gitmodules")).Should().NotBeEmpty();
        ((string)parent.GetSetting("submodule.libraries/child.url")).Should().BeEmpty();
        File.ReadAllText(Path.Combine(parent.WorkingDir, ".gitmodules")).Should().BeEmpty();
        Directory.Exists(parent.GetSubmoduleFullPath(submodulePath)).Should().BeTrue();
        ((string)GitCommands.GitModule.TryFindGitWorkingDir(parent.GetSubmoduleFullPath(submodulePath))).Should()
            .NotBeNull();
    }

    [Fact]
    public void Remove_should_keep_other_submodule_configurations_staged()
    {
        var parent = CreateRepository(Path.Combine(_root, "parent"));
        var firstChild = CreateRepository(Path.Combine(_root, "first-child"));
        var secondChild = CreateRepository(Path.Combine(_root, "second-child"));
        Commit(parent, "parent");
        Commit(firstChild, "first child");
        Commit(secondChild, "second child");

        const string firstPath = "libraries/first";
        const string secondPath = "libraries/second";
        AddSubmodule(parent, firstChild, firstPath);
        AddSubmodule(parent, secondChild, secondPath);
        Commit(parent, "add submodules");

        var firstSubmodule = Enumerable
            .OfType<IGitSubmoduleInfo>(parent.GetSubmodulesInfo())
            .Single(info => info.LocalPath == firstPath);

        SubmoduleWorkflow.Remove(parent, firstSubmodule);

        var gitmodules = File.ReadAllText(Path.Combine(parent.WorkingDir, ".gitmodules"));
        gitmodules.Should().Contain($"[submodule \"{secondPath}\"]");
        gitmodules.Should().NotContain($"[submodule \"{firstPath}\"]");
        ((string)parent.GitExecutable.GetOutput("diff --cached --name-only -- .gitmodules").Trim()).Should()
            .Be(".gitmodules");
        ((string)parent.GitExecutable.GetOutput($"ls-files --stage -- {firstPath}")).Should().BeEmpty();
        ((string)parent.GitExecutable.GetOutput($"ls-files --stage -- {secondPath}")).Should().NotBeEmpty();
        Directory.Exists(parent.GetSubmoduleFullPath(firstPath)).Should().BeTrue();
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

    private static void AddSubmodule(GitCommands.GitModule parent, GitCommands.GitModule child, string path)
    {
        var result = parent.GitExecutable.Execute(SubmoduleWorkflow.BuildAdd(
            child.WorkingDir,
            path,
            null,
            false,
            Commands.GetAllowFileConfig()));
        ((bool)result.ExitedSuccessfully).Should().BeTrue(result.AllOutput);
    }
}
