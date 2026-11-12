using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using GitExtensions.Shims.WinForms;

namespace GitUI.Compat;

/// <summary>
///     Implements the shim <see cref="IFolderPicker" /> over the Avalonia storage provider
///     (which uses the XDG desktop portal on Linux).
/// </summary>
public sealed class AvaloniaFolderPicker(IClassicDesktopStyleApplicationLifetime desktop) : IFolderPicker
{
    string? IFolderPicker.PickFolder(IWin32Window? owner, string? selectedPath)
    {
        return DispatcherPump.Wait(PickAsync);

        async Task<string?> PickAsync()
        {
            if (desktop.MainWindow?.StorageProvider is not { } storageProvider)
            {
                return null;
            }

            FolderPickerOpenOptions options = new() { AllowMultiple = false };
            if (selectedPath is not null)
            {
                options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(selectedPath);
            }

            var folders = await storageProvider.OpenFolderPickerAsync(options);
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
    }
}
