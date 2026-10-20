using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using GitExtensions.Extensibility.Git;
using GitUI;

namespace GitExtensionsTests;

public sealed class FileStatusListTests
{
    [AvaloniaFact]
    public void Clear_should_not_throw_after_SetDiffs()
    {
        FileStatusList list = new();
        Window window = new() { Content = list };
        window.Show();

        list.SetDiffs([new GitItemStatus(name: "a.txt"), new GitItemStatus(name: "b.txt")]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Action act = () =>
        {
            list.Clear();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        };

        act.Should().NotThrow();
    }
}
