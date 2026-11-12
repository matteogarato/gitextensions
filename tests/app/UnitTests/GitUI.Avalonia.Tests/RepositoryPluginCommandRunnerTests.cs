using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitUI.Plugin;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class RepositoryPluginCommandRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_should_run_enabled_command_and_notify_repository_change()
    {
        var module = Substitute.For<IGitModule>();
        var notifier = Substitute.For<ILockableNotifier>();
        var uiCommands = Substitute.For<IGitUiCommands>();
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
        var uiCommands = Substitute.For<IGitUiCommands>();
        var notifier = Substitute.For<ILockableNotifier>();
        uiCommands.RepoChangedNotifier.Returns(notifier);
        RepositoryPluginCommand command = new() { CanRun = false, ChangesRepository = true };

        await RepositoryPluginCommandRunner.ExecuteAsync(uiCommands, command);

        command.ExecutionCount.Should().Be(0);
        notifier.DidNotReceive().Notify();
    }

    [Fact]
    public async Task ExecuteAsync_should_notify_when_a_state_changing_command_fails()
    {
        var uiCommands = Substitute.For<IGitUiCommands>();
        var notifier = Substitute.For<ILockableNotifier>();
        uiCommands.RepoChangedNotifier.Returns(notifier);
        RepositoryPluginCommand command = new()
        {
            ChangesRepository = true, Exception = new InvalidOperationException("plugin failure")
        };

        var execute = () => RepositoryPluginCommandRunner.ExecuteAsync(uiCommands, command);

        await execute.Should().ThrowAsync<InvalidOperationException>();
        notifier.Received(1).Notify();
    }

    private sealed class RepositoryPluginCommand : IRepositoryPluginCommand
    {
        private bool ChangesRepository { get; init; }

        public bool CanRun { get; init; } = true;
        public Exception? Exception { get; init; }
        public int ExecutionCount { get; private set; }
        public IGitModule? ExecutedModule { get; private set; }

        string IRepositoryPluginCommand.Id
        {
            get => "test-command";
        }

        string IRepositoryPluginCommand.Name
        {
            get => "Test command";
        }

        bool IRepositoryPluginCommand.ChangesRepository
        {
            get => ChangesRepository;
        }

        bool IRepositoryPluginCommand.CanExecute(IGitModule module)
        {
            return CanRun;
        }

        Task IRepositoryPluginCommand.ExecuteAsync(IGitModule module, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            ExecutedModule = module;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }
}
