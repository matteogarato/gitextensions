using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO.Abstractions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.CommandsDialogs;

namespace GitUI.AvaloniaTests;

public sealed class GitConfigWorkflowTests
{
    [Fact]
    public void BuildGet_and_BuildSet_should_target_the_requested_level()
    {
        GitConfigWorkflow.BuildGet(GitConfigScope.Global, "user.name").ToString().Should()
            .Be("config --global --get user.name");
        GitConfigWorkflow.BuildSet(GitConfigScope.Local, "user.name", "A B").ToString().Should()
            .Be("config --local user.name \"A B\"");
        GitConfigWorkflow.BuildSet(GitConfigScope.Local, "user.name", "").ToString().Should()
            .Be("config --local --unset user.name");
    }

    [Fact]
    public void Write_should_store_read_and_remove_local_values_in_real_repository()
    {
        using RepositoryFixture repository = new();

        GitConfigWorkflow.Write(repository.Module, GitConfigScope.Local, "merge.tool", "meld").Should().BeNull();
        GitConfigWorkflow.Read(repository.Module, GitConfigScope.Local, "merge.tool").Should().Be("meld");

        GitConfigWorkflow.Write(repository.Module, GitConfigScope.Local, "merge.tool", "").Should().BeNull();
        GitConfigWorkflow.Read(repository.Module, GitConfigScope.Local, "merge.tool").Should().BeEmpty();
        GitConfigWorkflow.Write(repository.Module, GitConfigScope.Local, "merge.tool", "").Should().BeNull();
    }

    [AvaloniaFact]
    public void SettingsWindow_should_show_application_and_git_tabs_with_stored_values()
    {
        using RepositoryFixture repository = new();
        GitConfigWorkflow.Write(repository.Module, GitConfigScope.Local, "user.name", "Local Tester");

        SettingsWindow dialog = new(repository.Module);
        dialog.Show();
        var content = (StackPanel)dialog.Content!;
        var tabs = content.Children.OfType<TabControl>().Single();
        ((IEnumerable<TabItem>)tabs.ItemsSource!).Should().HaveCount(2);

        var scope =
            ((StackPanel)((ScrollViewer)((IEnumerable<TabItem>)tabs.ItemsSource!).Last().Content!).Content!).Children
            .OfType<ComboBox>().First();
        scope.SelectedIndex = 1;
        var gitPage =
            (StackPanel)((ScrollViewer)((IEnumerable<TabItem>)tabs.ItemsSource!).Last().Content!).Content!;
        gitPage.Children.OfType<TextBox>().First().Text.Should().Be("Local Tester");
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task PullOptionsWindow_and_PushOptionsWindow_should_return_chosen_options()
    {
        Window owner = new();
        owner.Show();

        PullOptionsWindow pull = new(["origin", "fork"], "fork", true);
        var pullResult = pull.ShowDialog<PullOptions?>(owner);
        var pullContent = (StackPanel)pull.Content!;
        pullContent.Children.OfType<RadioButton>().Last().IsChecked = true;
        pullContent.Children.OfType<StackPanel>().Single().Children.OfType<Button>().First()
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var pullOptions = await pullResult;
        pullOptions.Should().NotBeNull();
        pullOptions!.Remote.Should().Be("fork");
        pullOptions.Mode.Should().Be(PullMode.FetchOnly);
        pullOptions.AutoStash.Should().BeFalse();

        PushOptionsWindow push = new(["origin"], "origin", "main", true);
        var pushResult = push.ShowDialog<PushOptions?>(owner);
        var pushContent = (StackPanel)push.Content!;
        pushContent.Children.OfType<StackPanel>().Single().Children.OfType<Button>().First()
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var pushOptions = await pushResult;
        pushOptions.Should().NotBeNull();
        pushOptions!.Scope.Should().Be(PushScope.CurrentBranch);
        pushOptions.TrackUpstream.Should().BeTrue();
    }

    private sealed class RepositoryFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ge-config-{Guid.NewGuid():N}");
        private readonly ServiceContainer _services = new();

        public RepositoryFixture()
        {
            Directory.CreateDirectory(_directory);
            GitExtUtils.ServiceContainerRegistry.RegisterServices(_services);
            FileSystem fileSystem = new();
            _services.AddService<IFileSystem>(fileSystem);
            _services.AddService<IGitDirectoryResolver>(new GitDirectoryResolver(fileSystem));
            GitCommands.ServiceContainerRegistry.RegisterServices(_services);
            ServiceContainerRegistry.RegisterServices(_services);
            using var process =
                Process.Start(new ProcessStartInfo("git", "init -q -b main") { WorkingDirectory = _directory })!;
            process.WaitForExit();
            Module = new GitCommands.GitModule(_services.GetRequiredService<IGitExecutorProvider>(), _directory);
        }

        public GitCommands.GitModule Module { get; }

        void IDisposable.Dispose()
        {
            _services.Dispose();
            foreach (var file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_directory, true);
        }
    }
}
