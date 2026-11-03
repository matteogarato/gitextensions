using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtUtils;
using ResourceManager;

namespace GitUI.ScriptsEngine;

internal sealed class FormFilePrompt : GitExtensionsFormBase, IUserInputPrompt
{
    private readonly TextBox _filePaths = new();
    private readonly TranslationString _okText = new("OK");
    private readonly TranslationString _cancelText = new("Cancel");

    public string UserInput { get; private set; } = string.Empty;

    public FormFilePrompt()
    {
        Title = GitUI.TranslatedStrings.Open;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        InitializeComplete();
        _filePaths.MinWidth = 340;

        Button browseButton = new() { Content = GitUI.TranslatedStrings.Open };
        browseButton.Click += BrowseButton_Click;
        Button acceptButton = new() { Content = _okText.Text, IsDefault = true };
        acceptButton.Click += (_, _) =>
        {
            UserInput = _filePaths.Text ?? string.Empty;
            DialogResult = string.IsNullOrWhiteSpace(UserInput) ? DialogResult.Cancel : DialogResult.OK;
        };
        Button cancelButton = new() { Content = _cancelText.Text, IsCancel = true };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = GitUI.TranslatedStrings.ScriptText },
                _filePaths,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { browseButton, acceptButton, cancelButton },
                },
            },
        };
    }

    private void BrowseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ThreadHelper.FileAndForget(BrowseForFilesAsync);

    private async Task BrowseForFilesAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = GitUI.TranslatedStrings.Open,
        });

        if (files.Count > 0)
        {
            _filePaths.Text = string.Join(' ', files.Select(file => file.Path.LocalPath.Quote()));
        }
    }
}
