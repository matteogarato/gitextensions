using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitUI;
using NSubstitute;

namespace GitExtensionsTests;

public sealed class RepositoryPluginCommandRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_should_run_enabled_command_and_notify_repository_change()
    {
        IGitModule module = Substitute.For<IGitModule>();
        ILockableNotifier notifier = Substitute.For<ILockableNotifier>();
        IGitUICommands uiCommands = Substitute.For<IGitUICommands>();
        uiCommands.Module.Returns(module);
        uiCommands.RepoChangedNotifier.Returns(notifier);
        RepositoryPluginCommand command = new() { ChangesRepository = true };

        await RepositoryPluginCommandRunner.ExecuteAsync(uiCommands, command);

        command.ExecutionCount.Should().Be(1);
        command.ExecutedModule.Should().BeSameAs(module);
        notifier.Received(1).Notify();
    }

    [Fact]
    public async Task ExecuteAsync_should_skip_disabled_command_without_notifying()
    {
        IGitUICommands uiCommands = Substitute.For<IGitUICommands>();
        ILockableNotifier notifier = Substitute.For<ILockableNotifier>();
        uiCommands.RepoChangedNotifier.Returns(notifier);
        RepositoryPluginCommand command = new() { CanRun = false, ChangesRepository = true };

        await RepositoryPluginCommandRunner.ExecuteAsync(uiCommands, command);

        command.ExecutionCount.Should().Be(0);
        notifier.DidNotReceive().Notify();
    }

    [Fact]
    public async Task ExecuteAsync_should_notify_when_a_state_changing_command_fails()
    {
        IGitUICommands uiCommands = Substitute.For<IGitUICommands>();
        ILockableNotifier notifier = Substitute.For<ILockableNotifier>();
        uiCommands.RepoChangedNotifier.Returns(notifier);
        RepositoryPluginCommand command = new()
        {
            ChangesRepository = true,
            Exception = new InvalidOperationException("plugin failure"),
        };

        Func<Task> execute = () => RepositoryPluginCommandRunner.ExecuteAsync(uiCommands, command);

        await execute.Should().ThrowAsync<InvalidOperationException>();
        notifier.Received(1).Notify();
    }

    private sealed class RepositoryPluginCommand : IRepositoryPluginCommand
    {
        public string Id => "test-command";
        public string Name => "Test command";
        public bool ChangesRepository { get; init; }
        public bool CanRun { get; init; } = true;
        public Exception? Exception { get; init; }
        public int ExecutionCount { get; private set; }
        public IGitModule? ExecutedModule { get; private set; }

        public bool CanExecute(IGitModule module) => CanRun;

        public Task ExecuteAsync(IGitModule module, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            ExecutedModule = module;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }
}
