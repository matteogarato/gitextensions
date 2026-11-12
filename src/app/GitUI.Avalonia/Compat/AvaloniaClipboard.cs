using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using IShimClipboard = GitExtensions.Shims.WinForms.IClipboard;

namespace GitUI.Compat;

/// <summary>
///     Implements the shim <see cref="IShimClipboard" /> over the main window's Avalonia clipboard.
/// </summary>
public sealed class AvaloniaClipboard(IClassicDesktopStyleApplicationLifetime desktop) : IShimClipboard
{
    private IClipboard? PlatformClipboard
    {
        get => desktop.MainWindow?.Clipboard;
    }

    void IShimClipboard.SetText(string text)
    {
        DispatcherPump.Wait(async () =>
        {
            if (PlatformClipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }

            return true;
        });
    }

    string IShimClipboard.GetText()
    {
        return DispatcherPump.Wait(async () =>
        {
            if (PlatformClipboard is { } clipboard)
            {
                return await clipboard.TryGetTextAsync() ?? string.Empty;
            }

            return string.Empty;
        });
    }

    bool IShimClipboard.ContainsText()
    {
        return GetText().Length > 0;
    }
}
