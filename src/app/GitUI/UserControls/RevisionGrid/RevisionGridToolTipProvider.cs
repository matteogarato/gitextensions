using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid.Columns;

namespace GitUI.UserControls.RevisionGrid;

internal sealed class RevisionGridToolTipProvider(RevisionDataGridView gridView)
{
    /// <summary>
    ///     Lane info and commit bodies are long, so they must stay readable much longer than the default 5 seconds.
    /// </summary>
    private const int _autoPopDelayMilliseconds = short.MaxValue;

    private readonly RevisionDataGridView _gridView = gridView;
    private readonly Dictionary<Point, bool> _isTruncatedByCellPos = [];

    private readonly ToolTip _toolTip = new() { AutoPopDelay = _autoPopDelayMilliseconds };
    private int _previousColumnIndex = -1;
    private IGitRef? _previousHighlight;
    private int _previousRowIndex = -1;

    public bool ShowRevisionGridTooltips { get; set; }

    /// <summary>
    ///     Hides the tooltip.
    /// </summary>
    /// <returns>Returns <cref>true</cref> if the tooltip was active.</returns>
    public bool Hide()
    {
        var wasActive = _toolTip.Active;
        _toolTip.Active = false;
        return wasActive;
    }

    public void OnCellMouseMove(DataGridViewCellMouseEventArgs e, RefLabelHitInfo? hitInfo)
    {
        if (hitInfo?.GitRef is { } gitRef)
        {
            if (gitRef.Equals(_previousHighlight))
            {
                return;
            }

            _previousHighlight = gitRef;
            _previousRowIndex = -1;
            UpdateToolTip(gitRef);
            return;
        }

        _previousHighlight = null;

        if (!ShowRevisionGridTooltips)
        {
            _toolTip.SetToolTip(_gridView, null);
            return;
        }

        // Always generated tooltip text of first column (graph) because it **really** depends of the pixel hovered
        if (e.ColumnIndex != 0 && _previousRowIndex == e.RowIndex && _previousColumnIndex == e.ColumnIndex)
        {
            return;
        }

        _previousRowIndex = e.RowIndex;
        _previousColumnIndex = e.ColumnIndex;
        UpdateToolTip();

        return;

        void UpdateToolTip(IGitRef? highlightRef = null)
        {
            var revision = _gridView.GetRevision(e.RowIndex);

            if (revision is null)
            {
                return;
            }

            var newText = GetToolTipText(revision, highlightRef);
            if (_toolTip.GetToolTip(_gridView) != newText)
            {
                // Do not deactivate the tooltip to force it to be shown again: deactivating resets its
                // state, so the new text would only appear once the initial delay elapsed - and as the
                // graph column builds its text for every hovered pixel, that delay would restart with
                // nearly every mouse move. Windows updates the text of a displayed tooltip in place.
                _toolTip.SetToolTip(_gridView, newText);
            }

            if (!_toolTip.Active)
            {
                _toolTip.Active = true;
            }
        }

        string GetToolTipText(GitRevision revision, IGitRef? highlightRef)
        {
            try
            {
                if (_gridView.Columns[e.ColumnIndex].Tag is ColumnProvider provider
                    && provider.TryGetToolTip(e, revision, highlightRef, out var toolTip)
                    && !string.IsNullOrWhiteSpace(toolTip))
                {
                    return toolTip;
                }

                if (_isTruncatedByCellPos.TryGetValue(new Point(e.ColumnIndex, e.RowIndex), out var showToolTip)
                    && showToolTip)
                {
                    return _gridView.Rows[e.RowIndex].Cells[e.ColumnIndex].FormattedValue?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                // Ignore exception when fetching tooltip. It's not worth crashing for.
                Trace.WriteLine(ex);
            }

            // no tooltip unless always active or truncated
            return "";
        }
    }

    public void Clear()
    {
        _isTruncatedByCellPos.Clear();

        // A row index no longer identifies the same revision, so the memorized cell must not
        // suppress the update of the tooltip for the cell the pointer is resting on.
        _previousRowIndex = -1;
        _previousColumnIndex = -1;
        _previousHighlight = null;
    }

    public void SetTruncation(int columnIndex, int rowIndex, bool truncated)
    {
        _isTruncatedByCellPos[new Point(columnIndex, rowIndex)] = truncated;
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(RevisionGridToolTipProvider provider)
    {
        public ToolTip ToolTip
        {
            get => provider._toolTip;
        }

        public int PreviousRowIndex
        {
            get => provider._previousRowIndex;
        }

        public string? GetToolTipText()
        {
            return provider._toolTip.GetToolTip(provider._gridView);
        }
    }
}
