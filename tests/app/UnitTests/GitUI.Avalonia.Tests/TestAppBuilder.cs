using Avalonia;
using Avalonia.Headless;
using GitExtensions;
using GitUI.AvaloniaTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace GitUI.AvaloniaTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                // Render real frames with Skia so tests can capture and compare pixels
                // (golden-image tests); the default headless drawing produces no pixels.
                UseHeadlessDrawing = false
            });
    }
}
