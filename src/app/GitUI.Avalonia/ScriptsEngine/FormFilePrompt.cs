using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GitUI.Compat;
using ResourceManager;

namespace GitUI.ScriptsEngine;

internal sealed class FormFilePrompt : GitExtensionsFormBase, IUserInputPrompt
{
    private readonly TranslationString _cancelText = new("Cancel");
    private readonly TextBox _filePaths = new();
    private readonly TranslationString _okText = new("OK");

    public FormFilePrompt()
    {
        Title = TranslatedStrings.Open;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        InitializeComplete();
        _filePaths.MinWidth = 340;

        Button browseButton = new() { Content = TranslatedStrings.Open };
        browseButton.Click += BrowseButton_Click;
        Button acceptButton = new() { Content = _okText.Text, IsDefault = true };
        acceptButton.Click += (_, _) =>
        {
            UserInput = _filePaths.Text ?? string.Empty;
            DialogResult = string.IsNullOrWhiteSpace(UserInput) ? DialogResult.Cancel : DialogResult.Ok;
        };
        Button cancelButton = new() { Content = _cancelText.Text, IsCancel = true };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TranslatedStrings.ScriptText },
                _filePaths,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { browseButton, acceptButton, cancelButton }
                }
            }
        };
    }

    private string UserInput { get; set; } = string.Empty;

    string IUserInputPrompt.UserInput
    {
        get => UserInput;
    }

    private void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        ThreadHelper.FileAndForget(BrowseForFilesAsync);
    }

    private async Task BrowseForFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true, Title = TranslatedStrings.Open
        });

        if (files.Count > 0)
        {
            _filePaths.Text = string.Join(' ', files.Select(file => file.Path.LocalPath.Quote()));
        }
    }
}
