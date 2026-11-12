using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI.UserControls.RevisionGrid;

/// <summary>
///     Edits the <see cref="RevisionFilterOptions" />; closes with the new options, or <see langword="null" /> when
///     cancelled.
/// </summary>
public sealed class RevisionFilterWindow : Window
{
    private readonly CheckBox _currentBranch = new()
    {
        Name = "chkFilterCurrentBranch", Content = TranslatedStrings.FilterCurrentBranchOnly
    };

    private readonly CheckBox _decoration = new()
    {
        Name = "chkFilterDecoration", Content = TranslatedStrings.FilterSimplifyByDecoration
    };

    private readonly CheckBox _firstParent =
        new() { Name = "chkFilterFirstParent", Content = TranslatedStrings.FilterFirstParent };

    private readonly CalendarDatePicker _from = new() { Name = "dtpFilterFrom" };

    private readonly CheckBox _hideMerges =
        new() { Name = "chkFilterHideMerges", Content = TranslatedStrings.FilterHideMerges };

    private readonly TextBox _path = new() { Name = "txtFilterPath" };
    private readonly CheckBox _reflog = new() { Name = "chkFilterReflog", Content = TranslatedStrings.FilterReflog };
    private readonly CalendarDatePicker _to = new() { Name = "dtpFilterTo" };

    /// <summary>
    ///     Initializes a new instance of the <see cref="RevisionFilterWindow" /> class.
    /// </summary>
    public RevisionFilterWindow(RevisionFilterOptions current)
    {
        ArgumentNullException.ThrowIfNull(current);

        Title = TranslatedStrings.MoreFiltersTitle;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _from.SelectedDate = current.DateFrom;
        _to.SelectedDate = current.DateTo;
        _path.Text = current.PathFilter;
        _hideMerges.IsChecked = current.HideMergeCommits;
        _firstParent.IsChecked = current.ShowOnlyFirstParent;
        _currentBranch.IsChecked = current.ShowCurrentBranchOnly;
        _reflog.IsChecked = current.ShowReflogReferences;
        _decoration.IsChecked = current.SimplifyByDecoration;

        Button ok = new() { Content = TranslatedStrings.ApplyFilter, IsDefault = true };
        ok.Click += (_, _) => Close(ReadOptions());
        Button reset = new() { Content = TranslatedStrings.ClearFilter };
        reset.Click += (_, _) => Close(new RevisionFilterOptions());
        Button cancel = new() { Content = TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                Labeled(TranslatedStrings.FilterDateFrom, _from),
                Labeled(TranslatedStrings.FilterDateTo, _to),
                Labeled(TranslatedStrings.FilterPath, _path),
                _hideMerges,
                _firstParent,
                _currentBranch,
                _reflog,
                _decoration,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { ok, reset, cancel }
                }
            }
        };
    }

    private static StackPanel Labeled(string label, Control control)
    {
        return new StackPanel { Spacing = 2, Children = { new TextBlock { Text = label }, control } };
    }

    private RevisionFilterOptions ReadOptions()
    {
        return new RevisionFilterOptions
        {
            DateFrom = _from.SelectedDate?.Date,
            DateTo = _to.SelectedDate?.Date,
            PathFilter = _path.Text?.Trim() ?? string.Empty,
            HideMergeCommits = _hideMerges.IsChecked == true,
            ShowOnlyFirstParent = _firstParent.IsChecked == true,
            ShowCurrentBranchOnly = _currentBranch.IsChecked == true,
            ShowReflogReferences = _reflog.IsChecked == true,
            SimplifyByDecoration = _decoration.IsChecked == true
        };
    }
}
