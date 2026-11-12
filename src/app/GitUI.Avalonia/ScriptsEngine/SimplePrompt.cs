using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GitUI.Compat;
using ResourceManager;

namespace GitUI.ScriptsEngine;

internal sealed class SimplePrompt : GitExtensionsFormBase, IUserInputPrompt
{
    private readonly TranslationString _cancelText = new("Cancel");
    private readonly TextBox _input = new();
    private readonly TranslationString _okText = new("OK");

    public SimplePrompt(string? title, string? label, string? defaultValue)
    {
        Title = title;
        UserInput = defaultValue ?? string.Empty;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        InitializeComplete();
        _input.Text = UserInput;
        _input.MinWidth = 300;

        Button acceptButton = new() { Content = _okText.Text, IsDefault = true };
        acceptButton.Click += (_, _) =>
        {
            UserInput = _input.Text ?? string.Empty;
            DialogResult = DialogResult.Ok;
        };
        Button cancelButton = new() { Content = _cancelText.Text, IsCancel = true };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label },
                _input,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { acceptButton, cancelButton }
                }
            }
        };
    }

    private string UserInput { get; set; }

    string IUserInputPrompt.UserInput
    {
        get => UserInput;
    }
}
