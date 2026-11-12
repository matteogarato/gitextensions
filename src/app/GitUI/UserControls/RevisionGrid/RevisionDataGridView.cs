using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using GitUI.UserControls.RevisionGrid.Columns;
using GitUI.UserControls.RevisionGrid.Graph;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.UserControls.RevisionGrid;

public sealed partial class RevisionDataGridView : DataGridView
{
    private const int _backgroundThreadUpdatePeriod = 25;

    private const int
        _mouseWheelDeltaTimeout =
            1500; // Mouse wheel idle time in milliseconds after which unconsumed wheel delta will be dropped.

    private const int _rowCountUpdateCoolDown = 300;

    private static readonly AccessibleDataGridViewTextBoxCell _accessibleDataGridViewTextBoxCell = new();
    private readonly SolidBrush _alternatingRowBackgroundBrush;
    private readonly SolidBrush _authoredHighlightBrush;
    private readonly List<ColumnProvider> _columnProviders = [];
    private readonly SolidBrush _inactiveSelectionHighlightBrush;
    private readonly Color _nonRelativeNonSelectedBodyColor;
    private readonly Color _nonRelativeNonSelectedSubjectColor;
    private readonly Color _nonRelativeSelectedBodyColor;
    private readonly Color _nonRelativeSelectedSubjectColor;
    private readonly Color _relativeNonSelectedSubjectColor;
    private readonly Color _relativeSelectedBodyColor;

    private readonly SolidBrush _rowBackgroundBrush;

    private readonly BackgroundUpdater _rowCountUpdater;
    private readonly TaskManager _taskManager = ThreadHelper.CreateTaskManager();
    private readonly CancellationTokenSequence _updateVisibleRowRangeSequence = new();
    private readonly BackgroundUpdater _visibleRowRangeUpdater;

    internal RevisionGraph RevisionGraph = new();

    private int _backgroundScrollTo;
    private Font _boldFont;

    private CellStyle? _cellStyle;
    private bool _highlightAuthoredRevisions;
    private bool _isFocused;
    private bool _isSelected;
    private long _lastMouseWheelTickCount; // Timestamp of the last vertical scroll via mouse wheel.
    private int _loadedToBeSelectedRevisionsCount;
    private Font _monospaceFont;

    private int
        _mouseWheelDeltaRemainder; // Corresponds to unconsumed scroll distance while scrolling via mouse wheel, see OnMouseWheel().

    private Font _normalFont;

    /// <summary>
    ///     Force refresh the gridview, set when revision graph is changed while loading revisions.
    /// </summary>
    private RedrawMode _redrawMode;

    private GitRevision? _revision;
    private bool _revisionGraphDrawAlternateBackColor;

    private bool _revisionGraphDrawNonRelativesTextGray;
    private int _rowHeight; // Height of elements in the cache. Is equal to the control's row height.

    // Set while loading the revisions and data grid, see also ToBeSelectedObjectIds.
    private Lazy<IList<int>> _toBeSelectedGraphIndexesCache;

    private VisibleRowRange _visibleRowRange;

    // _toBeSelectedGraphIndexesCache is init in Clear()
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public RevisionDataGridView()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        InitFonts();

        _rowCountUpdater = new BackgroundUpdater(_taskManager, UpdateRowCountAsync, _rowCountUpdateCoolDown);
        _visibleRowRangeUpdater = new BackgroundUpdater(_taskManager, UpdateVisibleRowRangeInternalAsync,
            _backgroundThreadUpdatePeriod);

        InitializeComponent();
        DoubleBuffered = true;

        _rowBackgroundBrush = new SolidBrush(AppColor.PanelBackground.GetThemeColor());
        _alternatingRowBackgroundBrush =
            new SolidBrush(_rowBackgroundBrush.Color.MakeDarkerBy(Application.IsDarkModeEnabled ? -0.018 : 0.025));
        _authoredHighlightBrush = new SolidBrush(AppColor.AuthoredHighlight.GetThemeColor());
        _inactiveSelectionHighlightBrush = new SolidBrush(AppColor.InactiveSelectionHighlight.GetThemeColor());

        // subject/body colors are hardcoded to be correct relative each other,
        // subject color should be emphasied compared to body, selected vs unselected etc.

        // relativeNonSelectedSubject: SystemColors.ControlText
        _relativeNonSelectedSubjectColor = Application.IsDarkModeEnabled
            ? SystemColors.ControlText
            : SystemColors.HighlightText;
        _nonRelativeNonSelectedSubjectColor = Application.IsDarkModeEnabled
            ? Color.FromArgb(192, 192, 192) // de-emphasised light grey on dark background
            : SystemColors.GrayText;
        _nonRelativeSelectedSubjectColor = Application.IsDarkModeEnabled
            ? Color.FromArgb(235, 235, 215) // near-white with warm tint on blue selection
            : Color.FromArgb(188, 188, 188);

        // relativeNonSelectedBody: SystemColors.GrayText
        _relativeSelectedBodyColor = Application.IsDarkModeEnabled
            ? Color.FromArgb(170, 170, 150) // warm mid-grey on blue selection
            : Color.FromArgb(188, 188, 188); // same as _nonRelativeSelectedSubjectColor
        _nonRelativeNonSelectedBodyColor = Application.IsDarkModeEnabled
            ? Color.FromArgb(130, 130, 130) // darker grey than subject, further de-emphasised
            : Color.FromArgb(152, 152, 152);
        _nonRelativeSelectedBodyColor = Application.IsDarkModeEnabled
            ? Color.FromArgb(170, 170, 150) // same as relativeSelectedBody — consistent on selection
            : Color.FromArgb(161, 161, 161);

        UpdateRowHeight();

        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        ColumnWidthChanged += (s, e) =>
        {
            if (e.Column.Tag is ColumnProvider provider)
            {
                provider.OnColumnWidthChanged(e);
            }
        };

        Scroll += (_, _) => UpdateVisibleRowRange();
        Resize += (_, _) => UpdateVisibleRowRange();
        GotFocus += (_, _) => InvalidateSelectedRows();
        LostFocus += (_, _) => InvalidateSelectedRows();

        CellPainting += OnCellPainting;
        CellFormatting += (_, e) =>
        {
            if (Columns[e.ColumnIndex].Tag is ColumnProvider provider)
            {
                var revision = GetRevision(e.RowIndex);
                if (revision is not null)
                {
                    provider.OnCellFormatting(e, revision);
                }
            }
        };

        VirtualMode = true;
        Clear();

        return;

        void InitializeComponent()
        {
            ((ISupportInitialize)this).BeginInit();
            SuspendLayout();
            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
            BackgroundColor = AppColor.PanelBackground.GetThemeColor();
            CellBorderStyle = DataGridViewCellBorderStyle.None;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = AppColor.PanelBackground.GetThemeColor(),
                ForeColor = SystemColors.ControlText,
                SelectionBackColor = SystemColors.Highlight,
                SelectionForeColor = SystemColors.HighlightText,
                WrapMode = DataGridViewTriState.False
            };
            Dock = DockStyle.Fill;
            GridColor = AppColor.PanelBackground.GetThemeColor();
            ReadOnly = true;
            RowHeadersVisible = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            StandardTab = true;
            ((ISupportInitialize)this).EndInit();
            ResumeLayout(false);
        }

        void InvalidateSelectedRows()
        {
            for (var index = 0; index < SelectedRows.Count; ++index)
            {
                InvalidateRow(SelectedRows[index].Index);
            }
        }
    }

    /// <summary>
    ///     Indicates whether 'interesting' rows in the data grid is currently being loaded.
    ///     The property is set to true when all revisions have been read (including
    ///     artificial commits that are inserted before HEAD) and the data grid rows for
    ///     selected revisions have been loaded.
    ///     Revisions are handled directly when git-log delivers them.
    ///     All revisions are always saved.
    ///     GridRows are loaded when needed, starting when revisions are loaded.
    ///     If the top revision is selected then only the first rows need to be loaded.
    ///     If one of the bottom rows are selected, all rows must be loaded.
    ///     (https://github.com/gitextensions/gitextensions/wiki/Revision-Graph#revisiongraphrow describes this some.)
    ///     So all grid rows are not forced to be loaded.
    ///     The code now forces loading more rows to be able to select them.
    ///     This property is intended to be used by tests.
    /// </summary>
    public bool IsDataLoadComplete { get; private set; } = true;

    public bool UpdatingVisibleRows { get; private set; }

    internal AuthorRevisionHighlighting? AuthorHighlighting { get; set; }

    // Contains the object Id's that will be selected as soon as all of them have been loaded.
    // The object Id's are in the order in which they were originally selected.
    public IReadOnlyList<ObjectId> ToBeSelectedObjectIds { get; set; } = [];

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    public IReadOnlyList<ObjectId>? SelectedObjectIds
    {
        get
        {
            if (SelectedRows.Count == 0)
            {
                return null;
            }

            var data = new ObjectId[SelectedRows.Count];

            for (var i = 0; i < SelectedRows.Count; i++)
            {
                var row = RevisionGraph.GetNodeForRow(SelectedRows[i].Index);

                if (row?.GitRevision is not null)
                {
                    // NOTE returned collection has reverse order of SelectedRows
                    data[SelectedRows.Count - 1 - i] = row.GitRevision.ObjectId;
                }
            }

            return data;
        }
    }

    internal FrozenDictionary<string, Color> RemoteColors { get; set; }

    /// <summary>
    ///     Returns if any of the to-be-selected was found in the loaded revisions but are not yet selected.
    /// </summary>
    public bool PendingToBeSelected
    {
        get => _loadedToBeSelectedRevisionsCount > 0;
    }

    internal void CancelBackgroundTasks()
    {
        _columnProviders.Clear();
        _backgroundScrollTo = -1;
        _updateVisibleRowRangeSequence.CancelCurrent();
        _taskManager.JoinPendingOperations();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelBackgroundTasks();
        }

        base.Dispose(disposing);
    }

    internal void AddColumn(ColumnProvider columnProvider)
    {
        _columnProviders.Add(columnProvider);

        columnProvider.Column.Tag = columnProvider;
        columnProvider.Column.CellTemplate = _accessibleDataGridViewTextBoxCell;

        Columns.Add(columnProvider.Column);
    }

    private Color GetForeground(bool isSelected, bool isFocused, bool isNonRelativeGray)
    {
        return (isNonRelativeGray, isSelectedAndFocused: isSelected && isFocused) switch
        {
            // Unselected revisions of the currently checked out branch and all revisions reachable from HEAD
            (isNonRelativeGray: false, isSelectedAndFocused: false) => SystemColors.ControlText,

            // Selected revisions of the currently checked out branch and all revisions reachable from HEAD
            (isNonRelativeGray: false, isSelectedAndFocused: true) => _relativeNonSelectedSubjectColor,

            // All revisions not reachable from HEAD, revisions aren't selected
            (isNonRelativeGray: true, isSelectedAndFocused: false) => _nonRelativeNonSelectedSubjectColor,

            // (isNonRelativeGray: true, isSelected: true)
            _ => _nonRelativeSelectedSubjectColor
        };
    }

    private Color GetCommitBodyForeground(bool isSelected, bool isNonRelativeGray)
    {
        return (isNonRelativeGray, isSelected) switch
        {
            // Unselected revisions of the currently checked out branch and all revisions reachable from HEAD
            (isNonRelativeGray: false, isSelected: false) => SystemColors.GrayText,

            // Selected revisions of the currently checked out branch and all revisions reachable from HEAD
            (isNonRelativeGray: false, isSelected: true) => _relativeSelectedBodyColor,

            // All revisions not reachable from HEAD, revisions aren't selected
            (isNonRelativeGray: true, isSelected: false) => _nonRelativeNonSelectedBodyColor,

            // (isNonRelativeGray: true, isSelected: true)
            _ => _nonRelativeSelectedBodyColor
        };
    }

    private Brush GetBackground(bool isSelected, bool isFocused, int rowIndex, GitRevision? revision)
    {
        if (isSelected)
        {
            return isFocused ? SystemBrushes.Highlight : _inactiveSelectionHighlightBrush;
        }

        if (_highlightAuthoredRevisions && revision?.IsArtificial is false &&
            AuthorHighlighting?.IsHighlighted(revision) is true)
        {
            return _authoredHighlightBrush;
        }

        if (_revisionGraphDrawAlternateBackColor && rowIndex % 2 == 0)
        {
            return _alternatingRowBackgroundBrush;
        }

        return _rowBackgroundBrush;
    }

    private void OnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        DebugHelpers.Assert(_rowHeight != 0, "_rowHeight != 0");

        if (e.RowIndex < 0 ||
            e.RowIndex >= RowCount ||
            !e.State.HasFlag(DataGridViewElementStates.Visible))
        {
            return;
        }

        var revision = GetRevision(e.RowIndex);
        var isSelected = e.State.HasFlag(DataGridViewElementStates.Selected);
        if (revision != _revision || _isSelected != isSelected || _isFocused != Focused)
        {
            _revision = revision;
            _isSelected = isSelected;
            _isFocused = Focused;
            if (_revision is null)
            {
                _cellStyle = null;

                return;
            }

            var backBrush = GetBackground(_isSelected, _isFocused, e.RowIndex, _revision);
            var isNonRelativeGray = _revisionGraphDrawNonRelativesTextGray && !RowIsRelative(e.RowIndex);
            var foreColor = GetForeground(_isSelected, _isFocused, isNonRelativeGray);
            var commitBodyForeColor = GetCommitBodyForeground(_isSelected, isNonRelativeGray);
            _cellStyle = new CellStyle(backBrush, foreColor, commitBodyForeColor, _normalFont, _boldFont,
                _monospaceFont, RemoteColors);
        }

        if (_cellStyle is null)
        {
            e.Handled = false;
            return;
        }

        e.Graphics!.FillRectangle(_cellStyle.Value.BackBrush, e.CellBounds);

        e.Handled = true;

        if (Columns[e.ColumnIndex].Tag is ColumnProvider provider)
        {
            provider.OnCellPainting(e, _revision!, _rowHeight, _cellStyle.Value);
        }
    }

    /// <summary>
    ///     Add revisions from the git log to the graph, including segments to parents.
    ///     Update visible rows if needed.
    /// </summary>
    /// <param name="revisions">The revisions to add.</param>
    public void AddRange(IEnumerable<GitRevision> revisions)
    {
        foreach (var revision in revisions)
        {
            if (RevisionGraph.Add(revision))
            {
                _redrawMode = RedrawMode.RedrawWithClear;
            }

            if (ToBeSelectedObjectIds.Contains(revision.ObjectId))
            {
                ++_loadedToBeSelectedRevisionsCount;
            }
        }

        TriggerRowCountUpdate();
    }

    /// <summary>
    ///     Insert workTree and index revisions to the graph, offset existing revisions.
    /// </summary>
    /// <param name="workTreeRev">The workTree revision to add.</param>
    /// <param name="indexRev">The index revision to add.</param>
    /// <param name="parents">Parent ids for the revision to find (and insert before).</param>
    public void Insert(GitRevision workTreeRev, GitRevision indexRev, IReadOnlyList<ObjectId> parents)
    {
        if (_loadedToBeSelectedRevisionsCount == 0
            && ToBeSelectedObjectIds.Count == 0
            && SelectedRows?.Count is > 0)
        {
            // (GraphIndex) selection in grid was 'premature'
            ToBeSelectedObjectIds = SelectedObjectIds ?? [];
            _loadedToBeSelectedRevisionsCount = ToBeSelectedObjectIds.Count;
            ResetGraphIndices();
        }

        // Insert at matching parent.
        if (RevisionGraph.Insert(workTreeRev, indexRev, parents))
        {
            _redrawMode = RedrawMode.RedrawWithClear;
        }

        if (ToBeSelectedObjectIds.Contains(workTreeRev.ObjectId))
        {
            ++_loadedToBeSelectedRevisionsCount;
        }

        if (ToBeSelectedObjectIds.Contains(indexRev.ObjectId))
        {
            ++_loadedToBeSelectedRevisionsCount;
        }

        TriggerRowCountUpdate();
    }

    public void Clear()
    {
        ThreadHelper.AssertOnUIThread();

        _updateVisibleRowRangeSequence.CancelCurrent();
        _backgroundScrollTo = -1;
        _redrawMode = RedrawMode.NoRedrawNeeded;
        _visibleRowRange = new VisibleRowRange(0, 0);

        // Set rowcount to 0 first, to ensure it is not possible to select or redraw, since we are about to delete the data
        SetRowCount(0);
        RevisionGraph.Clear();
        ClearToBeSelected();

        // The graphdata is stored in one of the columnproviders, clear this last
        foreach (var columnProvider in _columnProviders)
        {
            columnProvider.Clear();
        }

        // Reload settings that will be used during drawing.
        // Theme related settings are only loaded at startup.
        _revisionGraphDrawNonRelativesTextGray = AppSettings.RevisionGraphDrawNonRelativesTextGray;

        _highlightAuthoredRevisions = AppSettings.HighlightAuthoredRevisions;
        _revisionGraphDrawAlternateBackColor = AppSettings.RevisionGraphDrawAlternateBackColor;

        // Redraw
        Invalidate(true);
    }

    public void ClearToBeSelected()
    {
        _loadedToBeSelectedRevisionsCount = 0;
        ToBeSelectedObjectIds = [];
        ResetGraphIndices();
    }

    public void EnsureRowVisible(int row)
    {
        var countVisible = DisplayedRowCount(false);
        var firstVisible = FirstDisplayedScrollingRowIndex;
        if (row >= 0 && (row < firstVisible || firstVisible + countVisible <= row))
        {
            FirstDisplayedScrollingRowIndex = row;
        }
    }

    /// <summary>
    ///     Set the first objectid in the parent list that is found in loaded revisions
    ///     as the to-be-selected objectid in the grid.
    /// </summary>
    /// <param name="parents">List with parents to the objectid initially intended to be selected.</param>
    public void SetToBeSelectedFromParents(IEnumerable<ObjectId>? parents)
    {
        if (parents is null)
        {
            return;
        }

        foreach (var parentId in parents)
        {
            if (RevisionGraph.TryGetRowIndex(parentId, out _))
            {
                ToBeSelectedObjectIds = new[] { parentId };
                _loadedToBeSelectedRevisionsCount = ToBeSelectedObjectIds.Count;
                break;
            }
        }
    }

    public void LoadingCompleted()
    {
        if (_loadedToBeSelectedRevisionsCount < ToBeSelectedObjectIds.Count)
        {
            // All expected revisions not found, settle with partial (empty) match
            _loadedToBeSelectedRevisionsCount = ToBeSelectedObjectIds.Count;
        }

        RevisionGraph.LoadingCompleted();
        if (RevisionGraph.Count == 0)
        {
            MarkAsDataLoadingComplete();
            return;
        }

        // Rows have not been selected yet
        this.InvokeAndForget(async () =>
        {
            SetRowCountAndSelectRowsIfReady();

            if (_toBeSelectedGraphIndexesCache.Value.Count == 0)
            {
                // Nothing to select or interrupted
                LoadingFinishedWithRevisions();
                return;
            }

            int rowCount;

            // Wait for the periodic background thread to load the first selected grid row, stop if aborted
            var firstGraphIndex = _toBeSelectedGraphIndexesCache.Value[0];
            do
            {
                rowCount = RowCount;
                if (firstGraphIndex < rowCount)
                {
                    break;
                }

                // Scroll to currently last loaded row
                SetRowCount(rowCount);
                EnsureRowVisible(rowCount - 1);

                // Wait for background thread to load grid rows
                await Task.Delay(_backgroundThreadUpdatePeriod);
            } while (_loadedToBeSelectedRevisionsCount > 0);

            // Scroll to first selected only if selection is not changed
            if (firstGraphIndex >= 0 && firstGraphIndex < rowCount && Rows[firstGraphIndex].Selected)
            {
                EnsureRowVisible(firstGraphIndex);
            }

            LoadingFinishedWithRevisions();
        });

        return;

        void LoadingFinishedWithRevisions()
        {
            // As fallback, select the first shown real commit or the first row (which exists here)
            if (SelectedRows.Count == 0)
            {
                var index = GetFallbackRowIndexToSelect();
                Rows[index].Selected = true;
                CurrentCell = Rows[index].Cells[1];
                EnsureRowVisible(index);
            }

            MarkAsDataLoadingComplete();
        }

        int GetFallbackRowIndexToSelect()
        {
            for (var index = 0; index < RevisionGraph.Count; ++index)
            {
                if (RevisionGraph.GetNodeForRow(index)?.GitRevision?.IsArtificial is false)
                {
                    return index;
                }
            }

            return 0;
        }
    }

    public void MarkAsDataLoadingComplete()
    {
        DebugHelpers.Assert(!IsDataLoadComplete, "The grid is already marked as 'data load complete'.");
        IsDataLoadComplete = true;
    }

    public void MarkAsDataLoading()
    {
        DebugHelpers.Assert(IsDataLoadComplete, "The grid is already marked as 'data load in process'.");
        IsDataLoadComplete = false;
    }

    /// <summary>
    ///     Checks whether the given hash is present in the graph.
    /// </summary>
    /// <param name="objectId">The hash to find.</param>
    /// <returns><see langword="true" />, if the given hash if found; otherwise <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="objectId" /> is <see langword="null" />.</exception>
    public bool Contains(ObjectId objectId)
    {
        return RevisionGraph.Contains(objectId);
    }

    public bool RowIsRelative(int rowIndex)
    {
        return RevisionGraph.IsRowRelative(rowIndex);
    }

    public GitRevision? GetRevision(int rowIndex)
    {
        return RevisionGraph.GetNodeForRow(rowIndex)?.GitRevision;
    }

    private void SetRowCount(int count)
    {
        if (InvokeRequired)
        {
            // DO NOT INVOKE! The RowCount is fixed at other strategic points in time.
            // -Doing this in synch can lock up the application
            // -Doing this async causes the scroll bar to flicker and eats performance
            // -At first I was concerned that returning might lead to some cases where
            //  we have more items in the list than we're showing, but I'm pretty sure
            //  when we're done processing we'll update with the final count, so the
            //  problem will only be temporary, and not able to distinguish it from
            //  just git giving us data slowly.
            ////Invoke(new MethodInvoker(delegate { setRowCount(count); }));
            return;
        }

        UpdatingVisibleRows = true;

        try
        {
            if (CurrentCell is null)
            {
                RowCount = count;
                CurrentCell = null;
            }
            else
            {
                RowCount = count;
            }
        }
        finally
        {
            UpdatingVisibleRows = false;
            UpdateVisibleRowRange();
        }
    }

    /// <summary>
    ///     Reset the calculated indices.
    /// </summary>
    private void ResetGraphIndices()
    {
        _toBeSelectedGraphIndexesCache = new Lazy<IList<int>>(CalculateGraphIndices);
        return;

        // Get the revision graph row indexes for the ToBeSelectedObjectIds.
        // (In filtering situations, all previous commits may no longer be in the grid).
        IList<int> CalculateGraphIndices()
        {
            List<int> toBeSelectedGraphIndexes = [];
            foreach (var objectId in ToBeSelectedObjectIds)
            {
                if (RevisionGraph.TryGetRowIndex(objectId, out var rowIndexToBeSelected))
                {
                    toBeSelectedGraphIndexes.Add(rowIndexToBeSelected);
                }
            }

            return toBeSelectedGraphIndexes;
        }
    }

    private void SelectRowsIfReady(int rowCount)
    {
        // Wait till we have all the row indexes to be selected
        if (_loadedToBeSelectedRevisionsCount == 0
            || _loadedToBeSelectedRevisionsCount < ToBeSelectedObjectIds.Count)
        {
            return;
        }

        // All grid rows must be loaded before they are shown
        if (_toBeSelectedGraphIndexesCache.Value.Any(i => i > rowCount - 1))
        {
            return;
        }

        // If updating selection, clear is required first
        ClearSelection();
        var first = true;
        foreach (var index in _toBeSelectedGraphIndexesCache.Value)
        {
            Rows[index].Selected = true;

            if (first)
            {
                first = false;
                CurrentCell = Rows[index].Cells[Math.Min(1, Rows[index].Cells.Count - 1)];
            }
        }

        // The to-be-selected are handled. Prevent from selecting them again.
        ClearToBeSelected();
    }

    private void SetRowCountAndSelectRowsIfReady()
    {
        var rowCount = RevisionGraph.Count;
        if (RowCount < rowCount)
        {
            SetRowCount(rowCount);
            SelectRowsIfReady(rowCount);
        }
    }

    private void TriggerRowCountUpdate()
    {
        _rowCountUpdater.ScheduleExecution();
    }

    private async Task UpdateRowCountAsync()
    {
        await _taskManager.JoinableTaskFactory.SwitchToMainThreadAsync();
        SetRowCountAndSelectRowsIfReady();
    }

    private void UpdateVisibleRowRange()
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            // TODO: Switch to IsDesignMode? See Github discussion in #8809
            // Don't run background operations in the designer.
            return;
        }

        if (_redrawMode == RedrawMode.RedrawWithClear)
        {
            // Always set _backgroundScrollTo in order to stop the background thread
            _backgroundScrollTo = -1;

            // The graph cache must be cleared at once
            foreach (var columnProvider in _columnProviders)
            {
                columnProvider.Clear();
            }
        }

        _visibleRowRangeUpdater.ScheduleExecution();
    }

    private async Task UpdateVisibleRowRangeInternalAsync()
    {
        if (!AppSettings.ShowRevisionGridGraphColumn
            || _columnProviders.Count == 0
            || _columnProviders[0] is not RevisionGraphColumnProvider revisionGraphColumnProvider)
        {
            return;
        }

        var cancellationToken = _updateVisibleRowRangeSequence.Next();

        var fromIndex = Math.Max(0, FirstDisplayedScrollingRowIndex);
        var visibleRowCount = DisplayedRowCount(true);
        visibleRowCount = Math.Min(RevisionGraph.Count - fromIndex, visibleRowCount);

        if (_redrawMode == RedrawMode.NoRedrawNeeded && _visibleRowRange.FromIndex == fromIndex &&
            _visibleRowRange.Count == visibleRowCount)
        {
            return;
        }

        _redrawMode = RedrawMode.NoRedrawNeeded;
        VisibleRowRange visibleRowRange = new(fromIndex, visibleRowCount);
        _visibleRowRange = visibleRowRange;

        if (visibleRowCount == 0)
        {
            return;
        }

        try
        {
            // Preload the next page, too, in order to avoid delayed display of the graph when scrolling down
            var newBackgroundScrollTo = fromIndex + (2 * visibleRowCount) - 1;

            // We always want to set _backgroundScrollTo. Because we want the backgroundthread to stop working when we scroll up
            _backgroundScrollTo = newBackgroundScrollTo;

            int curCount;
            do
            {
                curCount = RevisionGraph.GetCachedCount();
                RevisionGraph.CacheTo(curCount, newBackgroundScrollTo, cancellationToken);

                // Take changes to _backgroundScrollTo and IsDataLoadComplete by another thread into account
                if (IsDataLoadComplete)
                {
                    _backgroundScrollTo = Math.Min(_backgroundScrollTo, RevisionGraph.Count - 1);
                }
            } while (curCount <= _backgroundScrollTo);

            cancellationToken.ThrowIfCancellationRequested();

            await revisionGraphColumnProvider.RenderGraphToCacheAsync(visibleRowRange, newBackgroundScrollTo,
                _rowHeight, cancellationToken);
        }
        catch (Exception exception)
        {
            _visibleRowRange = new VisibleRowRange(0, 0);
            Trace.WriteLineIf(exception is not OperationCanceledException, exception);
        }
    }

    public void ApplySettings()
    {
        InitFonts();
        UpdateRowHeight();

        foreach (var columnProvider in _columnProviders)
        {
            columnProvider.ApplySettings();
        }

        Refresh();
    }

    public override void Refresh()
    {
        _redrawMode = RedrawMode.RedrawWithClear;
        UpdateVisibleRowRange();

        base.Refresh();
    }

    /// <summary>
    ///     Schedules a graph re-render without clearing the display cache, keeping the currently
    ///     cached display visible during re-rendering. Use this for lightweight state changes such
    ///     as hover highlighting, where the stale cache can be shown while the new render is computed.
    /// </summary>
    public void RequestRedrawWithoutClear()
    {
        if (_redrawMode < RedrawMode.RedrawOnly)
        {
            _redrawMode = RedrawMode.RedrawOnly;
        }

        _visibleRowRangeUpdater.ScheduleExecution();
    }

    private void UpdateRowHeight()
    {
        // TODO allow custom grid row spacing
        using var g = Graphics.FromHwnd(Handle);
        _rowHeight = (int)g.MeasureString("By", _normalFont).Height + DpiUtil.Scale(9);
        //// + AppSettings.GridRowSpacing
        RowTemplate.Height = _rowHeight;
    }

    public GitRevision? GetRevision(ObjectId objectId)
    {
        return RevisionGraph.TryGetNode(objectId, out var node) ? node.GitRevision : null;
    }

    public int? TryGetRevisionIndex(ObjectId objectId)
    {
        return !objectId.IsZero && RevisionGraph.TryGetRowIndex(objectId, out var index) ? index : null;
    }

    public IReadOnlyList<ObjectId> GetRevisionChildren(ObjectId objectId)
    {
        // We do not need a lock here since we load the data from the first commit and walk through all
        // parents. Children are always loaded, since we start at the newest commit.
        // With lock, loading the commit info slows down terribly.
        if (RevisionGraph.TryGetNode(objectId, out var node))
        {
            List<ObjectId> children = [.. node.Children.Select(d => d.GitRevision!.ObjectId)];
            children.Reverse();
            return children;
        }

        return [];
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyData)
        {
            case Keys.Home:
                if (RowCount != 0)
                {
                    ClearSelection();
                    Rows[0].Selected = true;
                    CurrentCell = Rows[0].Cells[1];
                }

                break;
            case Keys.End:
                if (RowCount != 0)
                {
                    ClearSelection();
                    Rows[RowCount - 1].Selected = true;
                    CurrentCell = Rows[RowCount - 1].Cells[1];
                }

                break;
            case Keys.Control | Keys.C:
                var selectedObjectIds = SelectedObjectIds;
                if (selectedObjectIds?.Count is > 0)
                {
                    ClipboardUtil.TrySetText(string.Join(Environment.NewLine, selectedObjectIds));
                }

                break;
            default:
                base.OnKeyDown(e);
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);

        if (hit.Type == DataGridViewHitTestType.None)
        {
            // Work around the fact that clicking in the space to the right of the last column does not
            // actually select the row. Instead, we test if the click would hit if done to the far left
            // of the row, and if so, pretend that's what happened.
            const int fakeX = 5;

            hit = HitTest(fakeX, e.Y);

            if (hit.Type == DataGridViewHitTestType.Cell && hit.RowIndex != -1)
            {
                base.OnMouseDown(new MouseEventArgs(e.Button, e.Clicks, fakeX, e.Y, e.Delta));
                return;
            }
        }

        base.OnMouseDown(e);

        // If clicking while loading, cancel load-select
        ClearToBeSelected();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var maxRowIndex = RowCount - 1;
        if (maxRowIndex < 0)
        {
            return;
        }

        if (ModifierKeys.HasFlag(Keys.Shift))
        {
            var currentIndex = HorizontalScrollingOffset;
            var scrollLines = DpiUtil.Scale(32);

            HorizontalScrollingOffset = e.Delta switch
            {
                > 0 => Math.Max(0, currentIndex - scrollLines),
                < 0 => currentIndex + scrollLines,
                _ => HorizontalScrollingOffset
            };
        }
        else
        {
            // Reset unconsumed wheel delta when the mouse wheel is idle, because there are at least
            // two situations in which unconsumed wheel delta causes an issue:
            // - When switching back to a notched mouse wheel. Whenever the scroll direction is changed,
            //   unconsumed delta will reduce the absolute value of the total delta so that the threshold
            //   for scrolling one row is never reached on the first wheel rotation.
            // - When using a precision scrolling device, the unconsumed delta will offset the first scroll,
            //   which makes the user experience a subtle "lag" or "leap" at beginning of a scroll.
            var currentTickCount = Environment.TickCount64;
            if (currentTickCount - _lastMouseWheelTickCount > _mouseWheelDeltaTimeout)
            {
                _mouseWheelDeltaRemainder = 0;
            }

            _lastMouseWheelTickCount = currentTickCount;

            var visibleCompleteRowsCount = _rowHeight > 0 ? Height / _rowHeight : 0;

            // The wheel might be configured to scroll more than one row at once.
            // Respect this by scaling MouseEventArgs.Delta accordingly.
            var scrollLines = SystemInformation.MouseWheelScrollLines;

            // Value of -1 indicates the "One screen at a time" mouse option.
            if (scrollLines == -1)
            {
                scrollLines = visibleCompleteRowsCount;
            }

            scrollLines = Math.Max(1, scrollLines);

            // Calculate the total wheel delta, which corresponds to the intended scrolling distance, from
            // MouseEventArgs.Delta, which is usually a multiple of SystemInformation.MouseWheelScrollDelta
            // for notched mouse wheels, but can be an arbitrary number in the case of precision scrolling
            // devices like free-spinning mouse wheels or touchpads.
            // Consume the total wheel delta in multiples of SystemInformation.MouseWheelScrollDelta, which
            // is the wheel delta threshold for scrolling one row, and save the remainder for consumption
            // during the next MouseWheel event.
            var totalWheelDelta = (scrollLines * e.Delta) + _mouseWheelDeltaRemainder;
            var wheelDeltaPerRow = SystemInformation.MouseWheelScrollDelta;
            _mouseWheelDeltaRemainder = totalWheelDelta % wheelDeltaPerRow;
            var rowDelta = -(totalWheelDelta - _mouseWheelDeltaRemainder) / wheelDeltaPerRow;
            if (rowDelta != 0)
            {
                var toRowIndex = Math.Clamp(FirstDisplayedScrollingRowIndex + rowDelta, 0, maxRowIndex);

                // Drop unconsumed wheel delta when reaching the upper or lower bound of the grid
                // to prevent the grid being stuck there for a moment.
                if (toRowIndex == 0 || toRowIndex + visibleCompleteRowsCount > maxRowIndex)
                {
                    _mouseWheelDeltaRemainder = 0;
                }

                try
                {
                    // This will raise the Scroll event.
                    FirstDisplayedScrollingRowIndex = toRowIndex;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Tried to scroll to nonexistent row.
                }
            }
        }
    }

    [MemberNotNull(nameof(_normalFont))]
    [MemberNotNull(nameof(_boldFont))]
    [MemberNotNull(nameof(_monospaceFont))]
    private void InitFonts()
    {
        _normalFont = AppSettings.Font;
        _boldFont = new Font(_normalFont, FontStyle.Bold);
        _monospaceFont = AppSettings.MonospaceFont;
    }

    private enum RedrawMode
    {
        NoRedrawNeeded,
        RedrawOnly,
        RedrawWithClear
    }
}
