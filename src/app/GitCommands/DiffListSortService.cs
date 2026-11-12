using GitCommands.Settings;

namespace GitCommands;

public class DiffListSortService : IDiffListSortService
{
    private static readonly Lazy<DiffListSortService> _lazyDiffListSorting = new(() => new DiffListSortService());

    private DiffListSortType _diffListSorting;

    public DiffListSortService()
    {
        DiffListSorting = GetSettingValueOrDefault();
    }

    public static DiffListSortService Instance
    {
        get => _lazyDiffListSorting.Value;
    }

    event EventHandler? IDiffListSortService.DiffListSortingChanged
    {
        add => DiffListSortingChanged += value;
        remove => DiffListSortingChanged -= value;
    }

    DiffListSortType IDiffListSortService.DiffListSorting
    {
        get => _diffListSorting;
        set
        {
            var previous = _diffListSorting;
            if (previous != value)
            {
                _diffListSorting = value;
                AppSettings.DiffListSorting = value;
                OnDiffListSortingChanged();
            }
        }
    }

    private event EventHandler? DiffListSortingChanged;

    private static DiffListSortType GetSettingValueOrDefault()
    {
        return AppSettings.DiffListSorting;
    }

    protected void OnDiffListSortingChanged()
    {
        DiffListSortingChanged?.Invoke(this, EventArgs.Empty);
    }
}
