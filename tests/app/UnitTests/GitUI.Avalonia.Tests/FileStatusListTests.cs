using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls;

namespace GitUI.AvaloniaTests;

public sealed class FileStatusListTests
{
    [AvaloniaFact]
    public void Clear_should_not_throw_after_SetDiffs()
    {
        FileStatusList list = new();
        Window window = new() { Content = list };
        window.Show();

        list.SetDiffs([new GitItemStatus("a.txt"), new GitItemStatus("b.txt")]);
        Dispatcher.UIThread.RunJobs();

        var act = () =>
        {
            list.Clear();
            Dispatcher.UIThread.RunJobs();
        };

        act.Should().NotThrow();
    }
}
