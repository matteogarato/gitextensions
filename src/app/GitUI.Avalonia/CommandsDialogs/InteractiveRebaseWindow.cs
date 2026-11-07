using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Lets the user choose an action and the order of each commit of an interactive rebase.
///  Closes with the edited list, or <see langword="null"/> when cancelled.
/// </summary>
public sealed class InteractiveRebaseWindow : Window
{
    private readonly List<RebaseTodoEntry> _entries;
    private readonly ListBox _list = new() { Name = "lstRebaseTodo" };
    private readonly ComboBox _action = new() { Name = "cbxRebaseAction", Width = 140 };
    private readonly TextBlock _error = new() { Name = "lblRebaseTodoError", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private bool _updating;

    /// <summary>
    ///  Initializes a new instance of the <see cref="InteractiveRebaseWindow"/> class.
    /// </summary>
    public InteractiveRebaseWindow(IEnumerable<RebaseTodoEntry> entries)
    {
        _entries = [.. entries];
        Title = GitUI.TranslatedStrings.InteractiveRebaseTitle;
        Width = 760;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _action.ItemsSource = Enum.GetValues<RebaseTodoAction>();
        _action.SelectionChanged += (_, _) =>
        {
            if (!_updating && _list.SelectedItem is not null && _action.SelectedItem is RebaseTodoAction action)
            {
                RebaseTodoEntry entry = _entries[_list.SelectedIndex];
                entry.Action = action;
                Refresh(entry);
            }
        };
        _list.SelectionChanged += (_, _) =>
        {
            _updating = true;
            _action.IsEnabled = _list.SelectedIndex >= 0;
            _action.SelectedItem = _list.SelectedIndex >= 0 ? _entries[_list.SelectedIndex].Action : null;
            _updating = false;
        };

        Button up = new() { Content = GitUI.TranslatedStrings.MoveUp };
        up.Click += (_, _) => Move(-1);
        Button down = new() { Content = GitUI.TranslatedStrings.MoveDown };
        down.Click += (_, _) => Move(1);
        Button start = new() { Content = GitUI.TranslatedStrings.StartRebase, IsDefault = true };
        start.Click += (_, _) => Accept();
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        StackPanel tools = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _action, up, down },
        };
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { start, cancel },
        };
        DockPanel.SetDock(tools, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(_error, Dock.Bottom);
        Content = new DockPanel
        {
            Margin = new Avalonia.Thickness(12),
            LastChildFill = true,
            Children = { tools, buttons, _error, _list },
        };

        Refresh(null);
    }

    private void Refresh(RebaseTodoEntry? selected)
    {
        _list.ItemsSource = _entries.Select(entry => $"{entry.Action.ToString().ToLowerInvariant(),-7} {entry}").ToList();
        _list.SelectedIndex = selected is null ? -1 : _entries.IndexOf(selected);
    }

    private void Move(int delta)
    {
        int index = _list.SelectedIndex;
        int target = index + delta;
        if (index < 0 || target < 0 || target >= _entries.Count)
        {
            return;
        }

        RebaseTodoEntry entry = _entries[index];
        _entries.RemoveAt(index);
        _entries.Insert(target, entry);
        Refresh(entry);
    }

    private void Accept()
    {
        string? error = InteractiveRebaseWorkflow.Validate(_entries);
        if (error is not null)
        {
            _error.Text = error;
            return;
        }

        Close(_entries.ToList());
    }
}
