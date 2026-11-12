using System.ComponentModel;
using System.Drawing.Drawing2D;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Plugins.GitImpact;

public partial class ImpactControl : UserControl
{
    private const int _linesFontSize = 10;
    private const int _weekFontSize = 8;
    private static readonly int _blockWidth = DpiUtil.Scale(60);
    private static readonly int _blockHalfWidth = _blockWidth / 2;
    private static readonly int _transitionWidth = DpiUtil.Scale(50);
    private static readonly int _transitionHalfWidth = _transitionWidth / 2;

    // List of authors that determines the drawing order

    // <Author, <Commits, Added Lines, Deleted Lines>>
    private readonly Dictionary<string, ImpactLoader.DataPoint> _authors = [];

    // The brush for each author
    private readonly Dictionary<string, SolidBrush> _brushes = [];

    private readonly Lock _dataLock = new();

    // The changed-lines-labels for each author
    private readonly Dictionary<string, List<(PointF point, string changeCount)>> _lineLabels = [];
    private readonly Brush _linesBrush = Brushes.White;
    private readonly Font _linesFont = new("Arial", _linesFontSize);

    // The paths for each author
    private readonly Dictionary<string, GraphicsPath> _paths = [];
    private readonly Pen _selectedAuthorPen = new(SystemColors.WindowText, 2);
    private readonly Brush _weekBrush = Brushes.Gray;

    private readonly Font _weekFont = new("Arial", _weekFontSize);

    // The week-labels
    private readonly List<(PointF point, string date)> _weekLabels = [];

    // <First weekday of commit date, <Author, <Commits, Added Lines, Deleted Lines>>>
    private SortedDictionary<DateOnly, Dictionary<string, ImpactLoader.DataPoint>> _impact = [];

    private ImpactLoader? _impactLoader;

    private bool _showSubmodules;

    public ImpactControl()
    {
        Clear();

        InitializeComponent();

        // Set DoubleBuffer flag for flicker-free drawing
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);

        MouseWheel += ImpactControl_MouseWheel;
        Disposed += ImpactControl_Disposed;
    }

    public string SelectedAuthor { get; private set; } = string.Empty;

    [DefaultValue(false)]
    public bool ShowSubmodules
    {
        get => _showSubmodules;
        set
        {
            _showSubmodules = value;
            Stop();
            Clear();
            UpdateData();
        }
    }

    [Browsable(false)] public List<string> Authors { get; } = [];

    public void Init(IGitModule module)
    {
        _impactLoader = new ImpactLoader(module)
        {
            // respect the .mailmap file
            RespectMailmap = true
        };

        _impactLoader.CommitLoaded += OnImpactUpdate;
    }

    private void Clear()
    {
        lock (_dataLock)
        {
            _authors.Clear();
            _impact.Clear();

            Authors.Clear();
            ClearPaths();
            ClearBrushes();
            _lineLabels.Clear();
            _weekLabels.Clear();
        }
    }

    public void Stop()
    {
        _impactLoader?.Stop();
    }

    private void ImpactControl_MouseWheel(object? sender, MouseEventArgs e)
    {
        _scrollBar.Value = Math.Min(_scrollBar.Maximum, Math.Max(_scrollBar.Minimum, _scrollBar.Value + e.Delta));

        // Redraw when we've scrolled
        Invalidate();
    }

    private void OnImpactUpdate(IList<ImpactLoader.Commit> commits)
    {
        lock (_dataLock)
        {
            foreach (var commit in commits)
            {
                // UPDATE IMPACT

                // If week does not exist yet in the impact dictionary
                if (!_impact.TryGetValue(commit.Week, out var weekData))
                {
                    // Create it
                    _impact.Add(commit.Week, weekData = []);
                }

                // If author does not exist yet for this week in the impact dictionary
                if (!weekData.TryGetValue(commit.Author, out var authorWeekData))
                {
                    // Create it
                    weekData.Add(commit.Author, commit.Data);
                }
                else
                {
                    // Otherwise just add the changes
                    weekData[commit.Author] = authorWeekData + commit.Data;
                }

                // UPDATE AUTHORS

                // If author does not exist yet in the authors dictionary
                if (!_authors.TryGetValue(commit.Author, out var authorData))
                {
                    // Create it
                    _authors.Add(commit.Author, commit.Data);
                }
                else
                {
                    // Otherwise just add the changes
                    _authors[commit.Author] = authorData + commit.Data;
                }

                // UPDATE AUTHOR STACK

                // If author does not exist yet in the author_stack
                if (!Authors.Contains(commit.Author))
                {
                    // Add it to the front (drawn first)
                    Authors.Insert(0, commit.Author);
                }
            }

            // Add authors to intermediate weeks where they didn't create commits
            ImpactLoader.AddIntermediateEmptyWeeks(ref _impact, _authors.Keys);
        }

        UpdatePathsAndLabels();
        Invalidate();
    }

    public void UpdateData()
    {
        if (_impactLoader is not null)
        {
            _impactLoader.ShowSubmodules = _showSubmodules;
            _impactLoader.Execute();
        }
    }

    private int GetGraphWidth()
    {
        return Math.Max(0, (_impact.Count * (_blockWidth + _transitionWidth)) - _transitionWidth);
    }

    private void UpdateScrollbar()
    {
        var rightValue = Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange - _scrollBar.Value);

        _scrollBar.Minimum = 0;
        _scrollBar.Maximum = (int)(Math.Max(0, GetGraphWidth() - ClientSize.Width) * 1.1);
        _scrollBar.SmallChange = _scrollBar.Maximum / 22;
        _scrollBar.LargeChange = _scrollBar.Maximum / 11;

        _scrollBar.Value = Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange - rightValue);
    }

    private void OnPaint(object sender, PaintEventArgs e)
    {
        // White background
        e.Graphics.Clear(SystemColors.Window);
        UpdateScrollbar();

        // Nothing to draw
        if (_impact.Count == 0)
        {
            // Show this cursor until we get some results painted
            UseWaitCursor = true;
            return;
        }

        // Now we have results, don't show waiting cursor
        UseWaitCursor = false;

        // Activate AntiAliasing
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // "Scroll" to the right position
        e.Graphics.TranslateTransform(-_scrollBar.Value, 0);

        lock (_dataLock)
        {
            // Draw paths in order of the author_stack
            // Default: person with least number of changed lines first, others on top
            foreach (var author in Authors)
            {
                if (author == SelectedAuthor)
                {
                    continue;
                }

                DrawAuthorContribution(author);
            }

            // Draw selected author data
            DrawAuthorContribution(SelectedAuthor);
            if (_paths.TryGetValue(SelectedAuthor, out var selectedAuthorPath))
            {
                e.Graphics.DrawPath(_selectedAuthorPen, selectedAuthorPath);
            }

            foreach (var author in Authors)
            {
                DrawAuthorLinesLabels(author);
            }

            void DrawAuthorContribution(string author)
            {
                if (_brushes.TryGetValue(author, out var authorBrush) &&
                    _paths.TryGetValue(author, out var authorPath))
                {
                    e.Graphics.FillPath(authorBrush, authorPath);
                }
            }
        }

        DrawWeekLabels();

        void DrawAuthorLinesLabels(string author)
        {
            if (!_lineLabels.TryGetValue(author, out var authorData))
            {
                return;
            }

            foreach ((var position, var changeCount) in authorData)
            {
                e.Graphics.DrawString(changeCount, _linesFont, _linesBrush, position);
            }
        }

        void DrawWeekLabels()
        {
            foreach ((var point, var date) in _weekLabels)
            {
                e.Graphics.DrawString(date, _weekFont, _weekBrush, point);
            }
        }
    }

    private void OnResize(object sender, EventArgs e)
    {
        UpdatePathsAndLabels();
        UpdateScrollbar();
        Invalidate();
    }

    private void UpdatePathsAndLabels()
    {
        var hMax = 0;
        var x = 0;
        Dictionary<string, List<(Rectangle, int changeCount)>> authorPointsDict = [];

        using var g = CreateGraphics();

        lock (_dataLock)
        {
            // Clear previous week labels
            _weekLabels.Clear();

            // Iterate through weeks
            foreach ((var weekDate, var dataByAuthor) in _impact)
            {
                var y = 0;

                // Iterate through authors
                foreach ((var author, var data) in from entry in dataByAuthor
                         orderby entry.Value.ChangedLines descending
                         select entry)
                {
                    // Calculate week-author-rectangle
                    var changedLines = Math.Max(1, data.ChangedLines);
                    var height = Math.Max(1, (int)Math.Round(Math.Pow(Math.Log(changedLines), 1.5) * 4));
                    Rectangle rc = new(x, y, _blockWidth, height);

                    // Add rectangle to temporary list
                    if (!authorPointsDict.TryGetValue(author, out var rect))
                    {
                        rect = [];
                        authorPointsDict.Add(author, rect);
                    }

                    rect.Add((rc, data.ChangedLines));

                    // Create a new random brush for the author if none exists yet
                    if (!_brushes.ContainsKey(author))
                    {
                        var color = Color.FromArgb((int)(author.GetHashCode() | 0xFF000000)).AdaptBackColor();
                        _brushes.Add(author, new SolidBrush(color));
                    }

                    // Increase y for next block
                    y += rc.Height + 2;
                }

                // Remember total height of largest week
                hMax = Math.Max(hMax, y);

                // Add week date label
                var formatedWeekDate = weekDate.ToShortDateString();

                _weekLabels.Add((new PointF(x + _blockHalfWidth, y), formatedWeekDate));

                // Increase x for next week
                x += _blockWidth + _transitionWidth;
            }

            // Pre-calculate height scale factor
            var heightFactor = hMax > 0 ? 0.9 * Height / hMax : 1.0;

            // Scale week label coordinates
            for (var i = 0; i < _weekLabels.Count; i++)
            {
                (var point, var formatedWeekDate) = _weekLabels[i];

                PointF adjustedPoint = new(point.X, point.Y * (float)heightFactor);

                var sz = g.MeasureString(formatedWeekDate, _weekFont);
                PointF centeredAdjustedPoint = new(adjustedPoint.X - (sz.Width / 2), adjustedPoint.Y + (sz.Height / 2));

                _weekLabels[i] = (centeredAdjustedPoint, formatedWeekDate);
            }

            // Clear previous paths
            ClearPaths();

            // Clear previous labels
            _lineLabels.Clear();

            // Add points to each author's GraphicsPath
            foreach ((var author, var points) in authorPointsDict)
            {
                // Scale heights
                for (var i = 0; i < points.Count; i++)
                {
                    (var unscaledRect, var changeCount) = points[i];

                    Rectangle rect = new(unscaledRect.Left, (int)(unscaledRect.Top * heightFactor),
                        unscaledRect.Width, Math.Max(1, (int)(unscaledRect.Height * heightFactor)));

                    points[i] = (rect, changeCount);

                    // Add lines-changed-labels
                    if (!_lineLabels.TryGetValue(author,
                            out var authorLineLabels))
                    {
                        _lineLabels.Add(author, authorLineLabels = []);
                    }

                    if (rect.Height > _linesFontSize * 1.5)
                    {
                        PointF adjustedPoint = new(rect.Left + _blockHalfWidth, rect.Top + (rect.Height / 2));

                        var changeCountText = changeCount.ToString();
                        var sz = g.MeasureString(changeCountText, _linesFont);
                        PointF centeredAdjustedPosition = new(adjustedPoint.X - (sz.Width / 2),
                            adjustedPoint.Y - (sz.Height / 2));

                        authorLineLabels.Add((centeredAdjustedPosition, changeCountText));
                    }
                }

                // Will be disposed when ClearPaths() is called
                GraphicsPath authorGraphicsPath = new();
                _paths.Add(author, authorGraphicsPath);

                (var firstRect, _) = points[0];

                // Left border
                authorGraphicsPath.AddLine(firstRect.Left, firstRect.Bottom,
                    firstRect.Left, firstRect.Top);

                // Top borders
                for (var i = 0; i < points.Count; i++)
                {
                    (var rect, _) = points[i];

                    authorGraphicsPath.AddLine(rect.Left, rect.Top,
                        rect.Right, rect.Top);

                    if (i < points.Count - 1)
                    {
                        (var nextRect, _) = points[i + 1];

                        authorGraphicsPath.AddBezier(rect.Right, rect.Top,
                            rect.Right + _transitionHalfWidth, rect.Top,
                            rect.Right + _transitionHalfWidth, nextRect.Top,
                            nextRect.Left, nextRect.Top);
                    }
                }

                (var lastRect, _) = points[^1];

                // Right border
                authorGraphicsPath.AddLine(lastRect.Right, lastRect.Top,
                    lastRect.Right, lastRect.Bottom);

                // Bottom borders
                for (var i = points.Count - 1; i >= 0; i--)
                {
                    (var rect, _) = points[i];

                    authorGraphicsPath.AddLine(rect.Right, rect.Bottom,
                        rect.Left, rect.Bottom);

                    if (i > 0)
                    {
                        (var prevRect, _) = points[i - 1];

                        authorGraphicsPath.AddBezier(rect.Left, rect.Bottom,
                            rect.Left - _transitionHalfWidth, rect.Bottom,
                            rect.Left - _transitionHalfWidth, prevRect.Bottom,
                            prevRect.Right, prevRect.Bottom);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Determines if the given coordinates are belonging to any author
    /// </summary>
    /// <param name="x">x coordinate</param>
    /// <param name="y">y coordinate</param>
    /// <returns>
    ///     true if author has changed and graph should be redrawn
    ///     false, otherwise
    /// </returns>
    public bool TrySetAuthorByScreenPosition(int x, int y)
    {
        lock (_dataLock)
        {
            for (var i = Authors.Count - 1; i >= 0; i--)
            {
                var author = Authors[i];
                if (_paths.TryGetValue(author, out var authorGraphicsPath)
                    && authorGraphicsPath.IsVisible(x + _scrollBar.Value, y))
                {
                    if (SelectedAuthor != author)
                    {
                        SelectedAuthor = author;
                        return true;
                    }

                    return false;
                }
            }
        }

        return false;
    }

    private void OnScroll(object sender, ScrollEventArgs e)
    {
        // Redraw when we've scrolled
        Invalidate();
    }

    public Color GetAuthorColor(string author)
    {
        lock (_dataLock)
        {
            if (_brushes.TryGetValue(author, out var brush))
            {
                return brush.Color;
            }
        }

        return Color.Transparent;
    }

    public ImpactLoader.DataPoint GetAuthorInfo(string author)
    {
        lock (_dataLock)
        {
            if (_authors.TryGetValue(author, out var info))
            {
                return info;
            }

            return new ImpactLoader.DataPoint(0, 0, 0);
        }
    }

    private void ImpactControl_Disposed(object? sender, EventArgs e)
    {
        lock (_dataLock)
        {
            _impactLoader?.Dispose();

            ClearBrushes();
            ClearPaths();
        }
    }

    private void ClearBrushes()
    {
        foreach (var brush in _brushes.Values)
        {
            brush.Dispose();
        }

        _brushes.Clear();
    }

    private void ClearPaths()
    {
        foreach (var path in _paths.Values)
        {
            path.Dispose();
        }

        _paths.Clear();
    }
}
