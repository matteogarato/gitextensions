using System.ComponentModel.Design;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;

namespace GitExtensionsTests;

public sealed class GitConfigWorkflowTests
{
    [Fact]
    public void BuildGet_and_BuildSet_should_target_the_requested_level()
    {
        GitConfigWorkflow.BuildGet(GitConfigScope.Global, "user.name").ToString().Should().Be("config --global --get user.name");
        GitConfigWorkflow.BuildSet(GitConfigScope.Local, "user.name", "A B").ToString().Should().Be("config --local user.name \"A B\"");
        GitConfigWorkflow.BuildSet(GitConfigScope.Local, "user.name", "").ToString().Should().Be("config --local --unset user.name");
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
        StackPanel content = (StackPanel)dialog.Content!;
        TabControl tabs = content.Children.OfType<TabControl>().Single();
        ((IEnumerable<TabItem>)tabs.ItemsSource!).Should().HaveCount(2);

        ComboBox scope = (ComboBox)((StackPanel)((ScrollViewer)((IEnumerable<TabItem>)tabs.ItemsSource!).Last().Content!).Content!).Children.OfType<ComboBox>().First();
        scope.SelectedIndex = 1;
        StackPanel gitPage = (StackPanel)((ScrollViewer)((IEnumerable<TabItem>)tabs.ItemsSource!).Last().Content!).Content!;
        gitPage.Children.OfType<TextBox>().First().Text.Should().Be("Local Tester");
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task PullOptionsWindow_and_PushOptionsWindow_should_return_chosen_options()
    {
        Window owner = new();
        owner.Show();

        PullOptionsWindow pull = new(["origin", "fork"], "fork", autoStash: true);
        Task<PullOptions?> pullResult = pull.ShowDialog<PullOptions?>(owner);
        StackPanel pullContent = (StackPanel)pull.Content!;
        pullContent.Children.OfType<RadioButton>().Last().IsChecked = true;
        pullContent.Children.OfType<StackPanel>().Single().Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PullOptions? pullOptions = await pullResult;
        pullOptions.Should().NotBeNull();
        pullOptions!.Remote.Should().Be("fork");
        pullOptions.Mode.Should().Be(PullMode.FetchOnly);
        pullOptions.AutoStash.Should().BeFalse();

        PushOptionsWindow push = new(["origin"], "origin", "main", trackByDefault: true);
        Task<PushOptions?> pushResult = push.ShowDialog<PushOptions?>(owner);
        StackPanel pushContent = (StackPanel)push.Content!;
        pushContent.Children.OfType<StackPanel>().Single().Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PushOptions? pushOptions = await pushResult;
        pushOptions.Should().NotBeNull();
        pushOptions!.Scope.Should().Be(PushScope.CurrentBranch);
        pushOptions.TrackUpstream.Should().BeTrue();
    }

    private sealed class RepositoryFixture : IDisposable
    {
        private readonly ServiceContainer _services = new();
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ge-config-{Guid.NewGuid():N}");

        public RepositoryFixture()
        {
            Directory.CreateDirectory(_directory);
            GitExtUtils.ServiceContainerRegistry.RegisterServices(_services);
            System.IO.Abstractions.FileSystem fileSystem = new();
            _services.AddService<System.IO.Abstractions.IFileSystem>(fileSystem);
            _services.AddService<IGitDirectoryResolver>(new GitDirectoryResolver(fileSystem));
            GitCommands.ServiceContainerRegistry.RegisterServices(_services);
            GitUI.ServiceContainerRegistry.RegisterServices(_services);
            using Process process = Process.Start(new ProcessStartInfo("git", "init -q -b main") { WorkingDirectory = _directory })!;
            process.WaitForExit();
            Module = new GitModule(_services.GetRequiredService<IGitExecutorProvider>(), _directory);
        }

        public GitModule Module { get; }

        public void Dispose()
        {
            _services.Dispose();
            foreach (string file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_directory, recursive: true);
        }
    }
}
