using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using GitExtensions.Shims.WinForms;

namespace GitUI.Compat;

/// <summary>
///     Implements the shim <see cref="IMessageBoxHost" /> with an Avalonia dialog window,
///     preserving the blocking semantics shared code expects from WinForms
///     <c>MessageBox.Show</c>.
/// </summary>
public sealed class AvaloniaMessageBoxHost(IClassicDesktopStyleApplicationLifetime desktop) : IMessageBoxHost
{
    DialogResult IMessageBoxHost.Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons,
        MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        return DispatcherPump.Wait(ShowDialogAsync);

        async Task<DialogResult> ShowDialogAsync()
        {
            var ownerWindow = desktop.MainWindow;

            Window dialog = new()
            {
                Title = caption ?? string.Empty,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation =
                    ownerWindow is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                MaxWidth = 700
            };

            var result = GetCancelResult(buttons);

            StackPanel buttonPanel = new()
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8
            };

            var choices = GetChoices(buttons);
            for (var index = 0; index < choices.Length; index++)
            {
                var choice = choices[index];
                Button button = new()
                {
                    Content = choice.ToString(),
                    MinWidth = 80,
                    IsDefault = index == GetDefaultIndex(defaultButton, choices.Length),
                    IsCancel = choice is DialogResult.Cancel or DialogResult.No && choices.Length > 0 &&
                               choice == GetCancelResult(buttons)
                };
                button.Click += (_, _) =>
                {
                    result = choice;
                    dialog.Close();
                };
                buttonPanel.Children.Add(button);
            }

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new SelectableTextBlock { Text = text ?? string.Empty, TextWrapping = TextWrapping.Wrap },
                    buttonPanel
                }
            };

            if (ownerWindow is not null && ownerWindow.IsVisible)
            {
                await dialog.ShowDialog(ownerWindow);
            }
            else
            {
                // No owner yet (e.g. during startup): show as a floating window and wait for close.
                TaskCompletionSource closed = new();
                dialog.Closed += (_, _) => closed.SetResult();
                dialog.Show();
                await closed.Task;
            }

            return result;
        }
    }

    private static DialogResult[] GetChoices(MessageBoxButtons buttons)
    {
        return buttons switch
        {
            MessageBoxButtons.OkCancel => [DialogResult.Ok, DialogResult.Cancel],
            MessageBoxButtons.AbortRetryIgnore => [DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore],
            MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            MessageBoxButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
            MessageBoxButtons.CancelTryContinue => [DialogResult.Cancel, DialogResult.TryAgain, DialogResult.Continue],
            _ => [DialogResult.Ok]
        };
    }

    /// <summary>
    ///     The result to report when the window is closed without pressing a button,
    ///     mirroring the WinForms close-box behavior.
    /// </summary>
    private static DialogResult GetCancelResult(MessageBoxButtons buttons)
    {
        return buttons switch
        {
            MessageBoxButtons.Ok => DialogResult.Ok,
            MessageBoxButtons.YesNo => DialogResult.No,
            MessageBoxButtons.AbortRetryIgnore => DialogResult.Abort,
            _ => DialogResult.Cancel
        };
    }

    private static int GetDefaultIndex(MessageBoxDefaultButton defaultButton, int buttonCount)
    {
        var index = (int)defaultButton >> 8;
        return index < buttonCount ? index : 0;
    }
}
