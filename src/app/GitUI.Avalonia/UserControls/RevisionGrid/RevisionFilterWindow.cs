using Avalonia.Controls;
using Avalonia.Layout;

namespace GitUI;

/// <summary>
///  Edits the <see cref="RevisionFilterOptions"/>; closes with the new options, or <see langword="null"/> when cancelled.
/// </summary>
public sealed class RevisionFilterWindow : Window
{
    private readonly CalendarDatePicker _from = new() { Name = "dtpFilterFrom" };
    private readonly CalendarDatePicker _to = new() { Name = "dtpFilterTo" };
    private readonly TextBox _path = new() { Name = "txtFilterPath" };
    private readonly CheckBox _hideMerges = new() { Name = "chkFilterHideMerges", Content = GitUI.TranslatedStrings.FilterHideMerges };
    private readonly CheckBox _firstParent = new() { Name = "chkFilterFirstParent", Content = GitUI.TranslatedStrings.FilterFirstParent };
    private readonly CheckBox _currentBranch = new() { Name = "chkFilterCurrentBranch", Content = GitUI.TranslatedStrings.FilterCurrentBranchOnly };
    private readonly CheckBox _reflog = new() { Name = "chkFilterReflog", Content = GitUI.TranslatedStrings.FilterReflog };
    private readonly CheckBox _decoration = new() { Name = "chkFilterDecoration", Content = GitUI.TranslatedStrings.FilterSimplifyByDecoration };

    /// <summary>
    ///  Initializes a new instance of the <see cref="RevisionFilterWindow"/> class.
    /// </summary>
    public RevisionFilterWindow(RevisionFilterOptions current)
    {
        ArgumentNullException.ThrowIfNull(current);

        Title = GitUI.TranslatedStrings.MoreFiltersTitle;
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

        Button ok = new() { Content = GitUI.TranslatedStrings.ApplyFilter, IsDefault = true };
        ok.Click += (_, _) => Close(ReadOptions());
        Button reset = new() { Content = GitUI.TranslatedStrings.ClearFilter };
        reset.Click += (_, _) => Close(new RevisionFilterOptions());
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                Labeled(GitUI.TranslatedStrings.FilterDateFrom, _from),
                Labeled(GitUI.TranslatedStrings.FilterDateTo, _to),
                Labeled(GitUI.TranslatedStrings.FilterPath, _path),
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
                    Children = { ok, reset, cancel },
                },
            },
        };
    }

    private static StackPanel Labeled(string label, Control control)
        => new() { Spacing = 2, Children = { new TextBlock { Text = label }, control } };

    private RevisionFilterOptions ReadOptions() => new()
    {
        DateFrom = _from.SelectedDate?.Date,
        DateTo = _to.SelectedDate?.Date,
        PathFilter = _path.Text?.Trim() ?? string.Empty,
        HideMergeCommits = _hideMerges.IsChecked == true,
        ShowOnlyFirstParent = _firstParent.IsChecked == true,
        ShowCurrentBranchOnly = _currentBranch.IsChecked == true,
        ShowReflogReferences = _reflog.IsChecked == true,
        SimplifyByDecoration = _decoration.IsChecked == true,
    };
}
