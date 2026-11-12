using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using GitUI.Compat;
using ResourceManager;
using Point = System.Drawing.Point;

namespace GitUI.UserControls.RevisionGrid;

internal class FormQuickItemSelector : GitExtensionsFormBase
{
    private readonly Button _actionButton = new();
    private readonly TranslationString _cancelText = new("Cancel");
    private readonly ListBox _itemsList = new();
    private readonly TranslationString _selectText = new("Select");

    public FormQuickItemSelector()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        InitializeComplete();
        Title = _selectText.Text;

        _itemsList.ItemTemplate = new FuncDataTemplate<ItemData>(
            (item, _) => new TextBlock { Text = item.Label },
            false);
        _itemsList.MinWidth = 300;
        _itemsList.MaxHeight = 320;
        _itemsList.DoubleTapped += (_, _) => AcceptSelection();
        _itemsList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                AcceptSelection();
            }
        };

        _actionButton.Click += (_, _) => AcceptSelection();
        Button cancelButton = new() { Content = _cancelText.Text, IsCancel = true };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                _itemsList,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { _actionButton, cancelButton }
                }
            }
        };
    }

    public object? SelectedItem
    {
        get => (_itemsList.SelectedItem as ItemData)?.Item;
    }

    public Point Location
    {
        get => new(Position.X, Position.Y);
        set
        {
            // These dialogs are centered on their owner; the WinForms grid anchor is not meaningful in Avalonia.
        }
    }

    protected void Init(IReadOnlyList<ItemData> items, string buttonText, int selectedIndex = 0)
    {
        _actionButton.Content = buttonText;
        _itemsList.ItemsSource = items;
        _itemsList.SelectedIndex = items.Count == 0 ? -1 : Math.Clamp(selectedIndex, 0, items.Count - 1);
        _actionButton.IsEnabled = items.Count > 0;
    }

    private void AcceptSelection()
    {
        DialogResult = SelectedItem is null ? DialogResult.Cancel : DialogResult.Ok;
    }

    internal sealed class ItemData(string label, object item)
    {
        public string Label { get; } = label;

        public object Item { get; } = item;
    }
}
