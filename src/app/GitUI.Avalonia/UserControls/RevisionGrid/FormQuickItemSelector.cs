using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using ResourceManager;

namespace GitUI.UserControls.RevisionGrid;

internal partial class FormQuickItemSelector : GitExtensionsFormBase
{
    private readonly ListBox _itemsList = new();
    private readonly Button _actionButton = new();
    private readonly TranslationString _cancelText = new("Cancel");
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
            supportsRecycling: false);
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
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                _itemsList,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { _actionButton, cancelButton },
                },
            },
        };
    }

    public object? SelectedItem => (_itemsList.SelectedItem as ItemData)?.Item;

    public System.Drawing.Point Location
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
        DialogResult = SelectedItem is null ? DialogResult.Cancel : DialogResult.OK;
    }

    internal sealed class ItemData
    {
        public string Label { get; }

        public object Item { get; }

        public ItemData(string label, object item)
        {
            Label = label;
            Item = item;
        }
    }
}
