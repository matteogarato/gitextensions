using GitCommands.Settings;
using GitCommands.Utils;
using GitExtensions.Extensibility;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.Editor.Diff;
using GitUI.Theming;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Document.HighlightingStrategy;
using ICSharpCode.TextEditor.Document.LineManager;
using ICSharpCode.TextEditor.Document.MarkerStrategy;
using ICSharpCode.TextEditor.Gui;
using KeyEventHandler = System.Windows.Forms.KeyEventHandler;

namespace GitUI.Editor;

public partial class FileViewerInternal : GitModuleControl, IFileViewer
{
    private readonly int _bottomBlankHeight = DpiUtil.Scale(300);
    private readonly CurrentViewPositionCache _currentViewPositionCache;

    private readonly FindAndReplaceForm _findAndReplaceForm = new();
    private BlameAuthorMargin? _authorsAvatarMargin;
    private ContinuousScrollEventManager? _continuousScrollEventManager;
    private DiffViewerLineNumberControl _lineNumbersControl;
    private bool _shouldScrollToBottom;
    private bool _shouldScrollToTop;
    private bool _showGutterAvatars;
    private ITextHighlightService _textHighlightService = TextHighlightService.Instance;

    public FileViewerInternal()
    {
        InitializeComponent();
        InitializeComplete();

        Disposed += (sender, e) =>
        {
            //// _textHighlightService not disposable
            //// _lineNumbersControl not disposable
            //// _currentViewPositionCache not disposable
            _findAndReplaceForm.Dispose();
        };

        FirstVisibleLine = 0;
        _currentViewPositionCache = new CurrentViewPositionCache(this);
        TextEditor.ActiveTextAreaControl.TextArea.SelectionManager.SelectionChanged += SelectionManagerSelectionChanged;

        TextEditor.ActiveTextAreaControl.TextArea.PreviewKeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape && !TextEditor.ActiveTextAreaControl.SelectionManager.HasSomethingSelected)
            {
                EscapePressed?.Invoke();
            }
        };

        TextEditor.TextChanged += (s, e) => TextChanged?.Invoke(s, e);
        TextEditor.ActiveTextAreaControl.HScrollBar.ValueChanged += (s, e) => OnHScrollPositionChanged(EventArgs.Empty);
        TextEditor.ActiveTextAreaControl.VScrollBar.ValueChanged += (s, e) => OnVScrollPositionChanged(EventArgs.Empty);
        TextEditor.ActiveTextAreaControl.TextArea.KeyUp += (s, e) => KeyUp?.Invoke(s, e);
        TextEditor.ActiveTextAreaControl.TextArea.DoubleClick += (s, e) => DoubleClick?.Invoke(s, e);
        TextEditor.ActiveTextAreaControl.TextArea.MouseMove += (s, e) => MouseMove?.Invoke(s, e);
        TextEditor.ActiveTextAreaControl.TextArea.MouseEnter += (s, e) => MouseEnter?.Invoke(s, e);
        TextEditor.ActiveTextAreaControl.TextArea.MouseLeave += (s, e) => MouseLeave?.Invoke(s, e);
        TextEditor.ActiveTextAreaControl.TextArea.MouseDown += (s, e) =>
        {
            SelectedLineChanged?.Invoke(
                this,
                new SelectedLineEventArgs(
                    TextEditor.ActiveTextAreaControl.TextArea.TextView.GetLogicalLine(e.Y)));
        };
        TextEditor.ActiveTextAreaControl.TextArea.MouseWheel += TextArea_MouseWheel;

        TextEditor.ActiveTextAreaControl.TextEditorProperties.EnableFolding = false;
        _lineNumbersControl = new DiffViewerLineNumberControl(TextEditor.ActiveTextAreaControl.TextArea);
        VRulerPosition = AppSettings.DiffVerticalRulerPosition;
        TextEditor.ActiveTextAreaControl.Caret.PositionChanged += GutterSelectedLineChanged;
    }

    private Action? OpenWithDifftool { get; set; }

    public bool ShowGutterAvatars
    {
        get => _showGutterAvatars;
        set
        {
            _showGutterAvatars = value;
            if (!_showGutterAvatars)
            {
                _authorsAvatarMargin?.SetVisiblity(false);

                return;
            }

            if (_authorsAvatarMargin is null)
            {
                _authorsAvatarMargin = new BlameAuthorMargin(TextEditor.ActiveTextAreaControl.TextArea);
                TextEditor.ActiveTextAreaControl.TextArea.InsertLeftMargin(0, _authorsAvatarMargin);
            }
            else
            {
                _authorsAvatarMargin.SetVisiblity(true);
            }
        }
    }

    event EventHandler<SelectedLineEventArgs>? IFileViewer.SelectedLineChanged
    {
        add => SelectedLineChanged += value;
        remove => SelectedLineChanged -= value;
    }

    event MouseEventHandler? IFileViewer.MouseMove
    {
        add => MouseMove += value;
        remove => MouseMove -= value;
    }

    event EventHandler? IFileViewer.MouseEnter
    {
        add => MouseEnter += value;
        remove => MouseEnter -= value;
    }

    event EventHandler? IFileViewer.MouseLeave
    {
        add => MouseLeave += value;
        remove => MouseLeave -= value;
    }

    event KeyEventHandler? IFileViewer.KeyUp
    {
        add => KeyUp += value;
        remove => KeyUp -= value;
    }

    event EventHandler? IFileViewer.DoubleClick
    {
        add => DoubleClick += value;
        remove => DoubleClick -= value;
    }

    Font IFileViewer.Font
    {
        get => TextEditor.Font;
        set => TextEditor.Font = value;
    }

    Action? IFileViewer.OpenWithDifftool
    {
        get => OpenWithDifftool;
    }

    /// <summary>
    ///     Move the file viewer cursor position to the next TextMarker found in the document that matches the
    ///     AppColor.HighlightAllOccurences.
    /// </summary>
    void IFileViewer.GoToNextOccurrence()
    {
        var offset = TextEditor.ActiveTextAreaControl.TextArea.Caret.Offset;

        var markers = TextEditor.Document.MarkerStrategy.GetMarkers(offset,
            TextEditor.Document.TextLength - offset);

        var marker =
            markers.FirstOrDefault(x =>
                x.Offset > offset && x.Color == AppColor.HighlightAllOccurences.GetThemeColor());
        if (marker is not null)
        {
            var position = TextEditor.ActiveTextAreaControl.TextArea.Document.OffsetToPosition(marker.Offset);
            TextEditor.ActiveTextAreaControl.Caret.Position = position;
        }
    }

    /// <summary>
    ///     Move the file viewer cursor position to the previous TextMarker found in the document that matches the
    ///     AppColor.HighlightAllOccurences.
    /// </summary>
    void IFileViewer.GoToPreviousOccurrence()
    {
        var offset = TextEditor.ActiveTextAreaControl.TextArea.Caret.Offset;

        var markers = TextEditor.Document.MarkerStrategy.GetMarkers(0, offset);

        var marker =
            markers.LastOrDefault(x => x.Offset < offset && x.Color == AppColor.HighlightAllOccurences.GetThemeColor());
        if (marker is not null)
        {
            var position = TextEditor.ActiveTextAreaControl.TextArea.Document.OffsetToPosition(marker.Offset);
            TextEditor.ActiveTextAreaControl.Caret.Position = position;
        }
    }

    void IFileViewer.Find(bool replace)
    {
        _findAndReplaceForm.ShowFor(TextEditor, replace && !IsReadOnly);
        OnVScrollPositionChanged(EventArgs.Empty);
    }

    async Task IFileViewer.FindNextAsync(bool searchForwardOrOpenWithDifftool)
    {
        if (searchForwardOrOpenWithDifftool && OpenWithDifftool is not null &&
            string.IsNullOrEmpty(_findAndReplaceForm.LookFor))
        {
            OpenWithDifftool();
            return;
        }

        await _findAndReplaceForm.FindNextAsync(true, !searchForwardOrOpenWithDifftool, "Text not found");
        OnVScrollPositionChanged(EventArgs.Empty);
    }

    /// <summary>
    ///     Raised when the Escape key is pressed (and only when no selection exists, as the default behaviour of escape is to
    ///     clear the selection).
    /// </summary>
    public event Action? EscapePressed;

    private event EventHandler<SelectedLineEventArgs>? SelectedLineChanged;

    private new event MouseEventHandler? MouseMove;

    private new event EventHandler? MouseEnter;

    private new event EventHandler? MouseLeave;

    private new event KeyEventHandler? KeyUp;

    private new event EventHandler? DoubleClick;

    public void DontMarkGutterSelectedLine()
    {
        TextEditor.ActiveTextAreaControl.Caret.PositionChanged -= GutterSelectedLineChanged;
        TextEditor.ActiveTextAreaControl.TextArea.GutterMargin.MarkSelectedLine = false;
    }

    public void SetContinuousScrollManager(ContinuousScrollEventManager continuousScrollEventManager)
    {
        _continuousScrollEventManager = continuousScrollEventManager;
    }

    internal void GutterSelectedLineChanged(object? sender, EventArgs e)
    {
        GutterSelectedLineChanged(TextEditor.ActiveTextAreaControl.Caret.Line);
    }

    internal void GutterSelectedLineChanged(int lineNo)
    {
        _lineNumbersControl.SelectedLineChanged(lineNo);
        TextEditor.ActiveTextAreaControl.TextArea.GutterMargin.SelectedLineChanged(lineNo);
    }

    private void SelectionManagerSelectionChanged(object? sender, EventArgs e)
    {
        var text = TextEditor.ActiveTextAreaControl.TextArea.SelectionManager.SelectedText;
        TextEditor.Document.MarkerStrategy.RemoveAll(m => true);

        var selectionMarkers = GetTextMarkersMatchingWord(text);
        TextEditor.Document.MarkerStrategy.AddMarkers(selectionMarkers);

        _textHighlightService.AddTextHighlighting(TextEditor.Document);
        TextEditor.ActiveTextAreaControl.TextArea.Invalidate();
    }

    /// <summary>
    ///     Create a list of TextMarker instances in the Document that match the given text.
    /// </summary>
    /// <param name="word">The text to match.</param>
    private IList<TextMarker> GetTextMarkersMatchingWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return [];
        }

        List<TextMarker> selectionMarkers = [];

        var textContent = TextEditor.Document.TextContent;
        var indexMatch = -1;
        do
        {
            indexMatch = textContent.IndexOf(word, indexMatch + 1, StringComparison.OrdinalIgnoreCase);
            if (indexMatch >= 0)
            {
                var highlightColor = AppColor.HighlightAllOccurences.GetThemeColor();

                TextMarker textMarker = new(indexMatch,
                    word.Length, TextMarkerType.SolidBlock, highlightColor,
                    highlightColor.GetTextColor());

                selectionMarkers.Add(textMarker);
            }
        } while (indexMatch >= 0 && indexMatch < textContent.Length - 1);

        return selectionMarkers;
    }

    public void SetGitBlameGutter(IEnumerable<GitBlameEntry> gitBlameEntries)
    {
        if (_showGutterAvatars)
        {
            _authorsAvatarMargin?.Initialize(gitBlameEntries);
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal sealed class CurrentViewPositionCache(FileViewerInternal viewer)
    {
        private readonly FileViewerInternal _viewer = viewer;
        public string? CapturedIdentification;
        public string? CurrentIdentification;
        private ViewPosition _currentViewPosition;

        public void Capture()
        {
            if (_viewer.TotalNumberOfLines <= 1 || string.IsNullOrEmpty(CurrentIdentification))
            {
                return;
            }

            // store the previous view position
            CapturedIdentification = CurrentIdentification;
            ViewPosition currentViewPosition = new()
            {
                ActiveLineNum = null,
                FirstLine = _viewer.GetLineText(0),
                TotalNumberOfLines = _viewer.TotalNumberOfLines,
                CaretPosition = _viewer.TextEditor.ActiveTextAreaControl.Caret.Position,
                FirstVisibleLine = _viewer.FirstVisibleLine
            };
            currentViewPosition.CaretVisible =
                currentViewPosition.CaretPosition.Line >= currentViewPosition.FirstVisibleLine &&
                currentViewPosition.CaretPosition.Line < currentViewPosition.FirstVisibleLine +
                _viewer.TextEditor.ActiveTextAreaControl.TextArea.TextView.VisibleLineCount;

            if (_viewer.TextEditor.ShowLineNumbers)
            {
                // Full contents is shown, just set the line number
                _currentViewPosition = currentViewPosition;
                return;
            }

            var initialActiveLine = currentViewPosition.CaretVisible
                ? currentViewPosition.CaretPosition.Line
                : currentViewPosition.FirstVisibleLine;

            // search downwards for a text line, i.e. a line with line numbers
            var activeLine = initialActiveLine;
            while (activeLine < currentViewPosition.TotalNumberOfLines && currentViewPosition.ActiveLineNum is null)
            {
                SetActiveLineNum(activeLine);
                ++activeLine;
            }

            // if none found, search upwards
            activeLine = initialActiveLine - 1;
            while (activeLine >= 0 && currentViewPosition.ActiveLineNum is null)
            {
                SetActiveLineNum(activeLine);
                --activeLine;
            }

            _currentViewPosition = currentViewPosition;
            return;

            void SetActiveLineNum(int line)
            {
                currentViewPosition.ActiveLineNum = _viewer._lineNumbersControl.GetLineInfo(line);
                if (currentViewPosition.ActiveLineNum is null)
                {
                    return;
                }

                if (currentViewPosition.ActiveLineNum.LeftLineNumber == DiffLineInfo.NotApplicableLineNum &&
                    currentViewPosition.ActiveLineNum.RightLineNumber == DiffLineInfo.NotApplicableLineNum)
                {
                    currentViewPosition.ActiveLineNum = null;
                }
            }
        }

        public bool Restore(string? contentIdentification)
        {
            CurrentIdentification = contentIdentification;
            if (_viewer.TotalNumberOfLines <= 1 || string.IsNullOrEmpty(contentIdentification) ||
                string.IsNullOrEmpty(CurrentIdentification))
            {
                return false;
            }

            var sameIdentification = contentIdentification == CapturedIdentification;
            if (!sameIdentification)
            {
                return false;
            }

            var viewPosition = _currentViewPosition;
            if (viewPosition.ActiveLineNum is not null)
            {
                // prefer the LeftLineNum because the base revision will not change
                var line = viewPosition.ActiveLineNum.LeftLineNumber != DiffLineInfo.NotApplicableLineNum
                    ? _viewer.GetCaretOffset(viewPosition.ActiveLineNum.LeftLineNumber, false)
                    : _viewer.GetCaretOffset(viewPosition.ActiveLineNum.RightLineNumber, true);
                _viewer.TextEditor.ActiveTextAreaControl.Caret.Position =
                    new TextLocation(viewPosition.CaretPosition.Column, line);
                if (viewPosition.CaretVisible)
                {
                    _viewer.TextEditor.ActiveTextAreaControl.CenterViewOn(line, 5);
                }
                else
                {
                    _viewer.FirstVisibleLine = line;
                }
            }
            else
            {
                _viewer.FirstVisibleLine = viewPosition.FirstVisibleLine;
                _viewer.TextEditor.ActiveTextAreaControl.Caret.Position = viewPosition.CaretPosition;
                if (!viewPosition.CaretVisible)
                {
                    _viewer.FirstVisibleLine = viewPosition.FirstVisibleLine;
                }
            }

            return true;
        }

        /// <summary>
        ///     Get the line number at the current view position offset.
        /// </summary>
        /// <param name="isDiff">If the current contents is a file diff.</param>
        /// <returns>The current line number at the caret offset</returns>
        public int CurrentFileLine(bool isDiff)
        {
            var viewPosition = _currentViewPosition;
            if (isDiff && _viewer.GetLineText(0) == viewPosition.FirstLine && viewPosition.ActiveLineNum is not null)
            {
                // prefer the RightLineNum that is for the current revision
                return viewPosition.ActiveLineNum.RightLineNumber != DiffLineInfo.NotApplicableLineNum
                    ? viewPosition.ActiveLineNum.RightLineNumber
                    : viewPosition.ActiveLineNum.LeftLineNumber;
            }

            // Convert from offset to line number
            return viewPosition.CaretPosition.Line + 1;
        }

        internal TestAccessor GetTestAccessor()
        {
            return new TestAccessor(this);
        }

        internal readonly struct TestAccessor(CurrentViewPositionCache viewPositionCache)
        {
            public ViewPosition ViewPosition
            {
                get => viewPositionCache._currentViewPosition;
                set => viewPositionCache._currentViewPosition = value;
            }

            public TextEditorControl TextEditor
            {
                get => viewPositionCache._viewer.TextEditor;
            }

            public DiffViewerLineNumberControl LineNumberControl
            {
                get => viewPositionCache._viewer._lineNumbersControl;
                set => viewPositionCache._viewer._lineNumbersControl = value;
            }

            public void SetCapturedIdentification(string? contentIdentification)
            {
                viewPositionCache.CapturedIdentification = contentIdentification;
            }

            public void SetCurrentIdentification(string? contentIdentification)
            {
                viewPositionCache.CurrentIdentification = contentIdentification;
            }
        }
    }

    internal struct ViewPosition
    {
        internal string FirstLine; // contains the file names in case of a diff

        internal int
            TotalNumberOfLines; // if changed, CaretPosition and FirstVisibleLine must be ignored and the line number must be searched

        internal TextLocation CaretPosition;
        internal int FirstVisibleLine;
        internal bool CaretVisible; // if not, FirstVisibleLine has priority for restoring
        internal DiffLineInfo? ActiveLineNum;
    }

    internal readonly struct TestAccessor(FileViewerInternal control)
    {
        public TextEditorControl TextEditor
        {
            get => control.TextEditor;
        }

        public CurrentViewPositionCache CurrentViewPositionCache
        {
            get => control._currentViewPositionCache;
        }
    }

    #region IFileViewer Members

    private event EventHandler? HScrollPositionChanged;

    event EventHandler? IFileViewer.HScrollPositionChanged
    {
        add => HScrollPositionChanged += value;
        remove => HScrollPositionChanged -= value;
    }

    private event EventHandler? VScrollPositionChanged;

    event EventHandler? IFileViewer.VScrollPositionChanged
    {
        add => VScrollPositionChanged += value;
        remove => VScrollPositionChanged -= value;
    }

    private new event EventHandler? TextChanged;

    event EventHandler? IFileViewer.TextChanged
    {
        add => TextChanged += value;
        remove => TextChanged -= value;
    }

    public void ScrollToTop()
    {
        _shouldScrollToTop = true;
    }

    public void ScrollToBottom()
    {
        _shouldScrollToBottom = true;
    }

    string IFileViewer.GetText()
    {
        return TextEditor.Text;
    }

    bool? IFileViewer.ShowLineNumbers { get; set; }

    /// <summary>
    ///     Set plain text in the editor.
    /// </summary>
    /// <param name="text">The text to set in the editor.</param>
    /// <param name="openWithDifftool">The command to open the difftool.</param>
    void IFileViewer.SetText(string text, Action? openWithDifftool)
    {
        SetText(text, openWithDifftool, ViewMode.Text, false, null);
    }

    /// <summary>
    ///     Set plain text in the editor.
    /// </summary>
    /// <param name="text">The text to set in the editor.</param>
    /// <param name="openWithDifftool">The command to open the difftool.</param>
    /// <param name="viewMode">the view viewMode in the file viewer, the kind of info shown</param>
    /// <returns><see langword="true" /> if a position was set.</returns>
    public bool SetText(string text, Action? openWithDifftool, ViewMode viewMode, bool useGitColoring,
        string? contentIdentification)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        _currentViewPositionCache.Capture();

        OpenWithDifftool = openWithDifftool;

        // Get the highlight service, possibly clean escape sequences in the text and create highlight.
        _lineNumbersControl.Clear();
        var vrulerpos = -1;
        _textHighlightService = viewMode switch
        {
            ViewMode.Text => TextHighlightService.Instance,
            ViewMode.Diff or ViewMode.FixedDiff => new PatchHighlightService(ref text, useGitColoring,
                _lineNumbersControl),
            ViewMode.CombinedDiff => new CombinedDiffHighlightService(ref text, useGitColoring, _lineNumbersControl),
            ViewMode.Difftastic => new DifftasticHighlightService(ref text, _lineNumbersControl, out vrulerpos),
            ViewMode.RangeDiff => new RangeDiffHighlightService(ref text, _lineNumbersControl),
            ViewMode.Grep => new GrepHighlightService(ref text, _lineNumbersControl),
            _ => throw new ArgumentException($"Unexpected viewMode: {viewMode}", nameof(viewMode))
        };

        if (vrulerpos >= 0)
        {
            // Difftastic set the position (0 to hide)
            VRulerPosition = vrulerpos;
        }
        else if (VRulerPosition != AppSettings.DiffVerticalRulerPosition)
        {
            // Reset if Difftastic changed the position
            VRulerPosition = AppSettings.DiffVerticalRulerPosition;
        }

        TextEditor.Text = text;
        var hasLineNumberControl = viewMode.IsPartialTextView();
        _lineNumbersControl.SetVisibility(hasLineNumberControl);

        if (hasLineNumberControl)
        {
            var index = TextEditor.ActiveTextAreaControl.TextArea.LeftMargins.IndexOf(_lineNumbersControl);
            if (index == -1)
            {
                TextEditor.ActiveTextAreaControl.TextArea.InsertLeftMargin(0, _lineNumbersControl);
            }
        }

        // important to set after the text was changed
        // otherwise the may be rendering artifacts as noted in #5568
        TextEditor.ShowLineNumbers = ShowLineNumbers ?? !hasLineNumberControl;
        GutterSelectedLineChanged(-1);
        if (ShowLineNumbers.HasValue && !ShowLineNumbers.Value)
        {
            Padding = new Padding(DpiUtil.Scale(5), Padding.Top, Padding.Right, Padding.Bottom);
        }

        TextEditor.Refresh();

        // Restore position if contentIdentification matches the capture
        var positionSet = _currentViewPositionCache.Restore(contentIdentification) &&
                          LineAtCaret > FirstLineAfterHeader;

        if (_shouldScrollToBottom || _shouldScrollToTop)
        {
            VScrollBar scrollBar = TextEditor.ActiveTextAreaControl.VScrollBar;
            if (scrollBar.Visible)
            {
                scrollBar.Value = _shouldScrollToTop
                    ? 0
                    : Math.Max(0, scrollBar.Maximum - scrollBar.Height - _bottomBlankHeight);
                positionSet = true;
            }

            _shouldScrollToTop = false;
            _shouldScrollToBottom = false;
        }

        return positionSet;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ShowLineNumbers.HasValue && !ShowLineNumbers.Value)
        {
            e.Graphics.FillRectangle(SystemBrushes.Window, e.ClipRectangle);
        }
        else
        {
            base.OnPaintBackground(e);
        }
    }

    void IFileViewer.SetHighlighting(string syntax)
    {
        SetHighlightingStrategy(HighlightingStrategyFactory.CreateHighlightingStrategy(syntax));
    }

    void IFileViewer.SetHighlightingForFile(string filename)
    {
        IHighlightingStrategy highlightingStrategy;
        if (filename.EndsWith("git-rebase-todo"))
        {
            highlightingStrategy = new RebaseTodoHighlightingStrategy(Module);
        }
        else if (filename.EndsWith("COMMIT_EDITMSG"))
        {
            highlightingStrategy = new CommitMessageHighlightingStrategy(Module);
        }
        else
        {
            highlightingStrategy = HighlightingManager.Manager.FindHighlighterForFile(filename);
        }

        SetHighlightingStrategy(highlightingStrategy);
    }

    private void SetHighlightingStrategy(IHighlightingStrategy highlightingStrategy)
    {
        TextEditor.Document.HighlightingStrategy =
            ThemeModule.Settings.UseSystemVisualStyle
                ? highlightingStrategy
                : new ThemeBasedHighlighting(highlightingStrategy);
        TextEditor.Refresh();
    }

    string IFileViewer.GetSelectedText()
    {
        return TextEditor.ActiveTextAreaControl.SelectionManager.SelectedText;
    }

    int IFileViewer.GetSelectionPosition()
    {
        if (TextEditor.ActiveTextAreaControl.SelectionManager.SelectionCollection.Count > 0)
        {
            return TextEditor.ActiveTextAreaControl.SelectionManager.SelectionCollection[0].Offset;
        }

        return TextEditor.ActiveTextAreaControl.Caret.Offset;
    }

    int IFileViewer.GetSelectionLength()
    {
        if (TextEditor.ActiveTextAreaControl.SelectionManager.SelectionCollection.Count > 0)
        {
            return TextEditor.ActiveTextAreaControl.SelectionManager.SelectionCollection[0].Length;
        }

        return 0;
    }

    void IFileViewer.EnableScrollBars(bool enable)
    {
        TextEditor.ActiveTextAreaControl.VScrollBar.Width = 0;
        TextEditor.ActiveTextAreaControl.VScrollBar.Visible = enable;
        TextEditor.ActiveTextAreaControl.TextArea.Dock = DockStyle.Fill;
    }

    /// <summary>
    ///     Add text highlighting to the document.
    ///     This is primarily used to highlight changed files for diffs.
    /// </summary>
    public void AddTextHighlighting()
    {
        TextEditor.Document.MarkerStrategy.RemoveAll(_ => true);
        _textHighlightService.AddTextHighlighting(TextEditor.Document);
    }

    /// <summary>
    ///     Get the full prefix for lines with differences.
    /// </summary>
    /// <returns>An array with the prefixes.</returns>
    /// <exception cref="ArgumentException"></exception>
    public string[] GetFullDiffPrefixes()
    {
        if (_textHighlightService is not DiffHighlightService highlightService)
        {
            throw new ArgumentException(
                $"Unexpected highlight service {_textHighlightService.GetType()}, not a diff type.");
        }

        return highlightService.GetFullDiffPrefixes();
    }

    /// <summary>
    ///     Check if the line is a search match.
    ///     For normal diff, this a added deleted line.
    ///     For range-diff, this is a header.
    /// </summary>
    /// <returns>An array with the prefixes.</returns>
    /// <param name="indexInText">The line to check.</param>
    private bool IsSearchMatch(int indexInText)
    {
        return _textHighlightService.IsSearchMatch(_lineNumbersControl, indexInText);
    }

    private int FirstLineAfterHeader
    {
        get
        {
            var hasDiffHeader = _textHighlightService is PatchHighlightService or CombinedDiffHighlightService;
            return hasDiffHeader ? 5 : 0;
        }
    }

    /// <summary>
    ///     Go to the first change.
    ///     For normal diffs, this is the first diff.
    ///     For range-diff, it is the first block of commit summary header.
    /// </summary>
    public void GoToFirstChange(int contextLines)
    {
        GoToNextChange(contextLines, true);
    }

    /// <summary>
    ///     Go to the next change.
    ///     For normal diffs, this is the next diff.
    ///     For range-diff, it is the next block of commit summary header.
    /// </summary>
    /// <param name="contextLines">Number of context lines, to include header for new diff.</param>
    public void GoToNextChange(int contextLines)
    {
        GoToNextChange(contextLines, false);
    }

    private void GoToNextChange(int contextLines, bool fromTop)
    {
        // Skip the file header
        var firstValidIndex = FirstLineAfterHeader;
        var startIndex = fromTop ? firstValidIndex : Math.Max(firstValidIndex, LineAtCaret);
        var totalNumberOfLines = TotalNumberOfLines;

        var emptyLineCheck = fromTop;
        for (var line = startIndex; line < totalNumberOfLines; line++)
        {
            if (IsSearchMatch(line))
            {
                if (emptyLineCheck)
                {
                    if (fromTop && IsLineVisible(line))
                    {
                        // Keep FirstVisibleLine, but let it be clamped in order to avoid scrolling the text out of view
                        FirstVisibleLine = FirstVisibleLine;
                    }
                    else
                    {
                        // Include the header with the (possible) function summary line
                        FirstVisibleLine = Math.Max(line - contextLines - 1, 0);
                    }

                    LineAtCaret = line;
                    return;
                }
            }
            else
            {
                emptyLineCheck = true;
            }
        }

        return;

        bool IsLineVisible(int line)
        {
            var firstVisibleLine = FirstVisibleLine;
            return firstVisibleLine <= line &&
                   line < firstVisibleLine + TextEditor.ActiveTextAreaControl.TextArea.TextView.VisibleLineCount;
        }
    }

    public void GoToPreviousChange(int contextLines)
    {
        // Skip the file header
        var hasDiffHeader = _textHighlightService is PatchHighlightService or CombinedDiffHighlightService;
        var firstValidIndex = hasDiffHeader ? 4 : 0;
        var startIndex = LineAtCaret;
        while (startIndex > firstValidIndex && IsSearchMatch(startIndex))
        {
            // Go to line before of current diff block
            startIndex--;
        }

        // Find the start of previous diff
        var emptyLineCheck = false;
        for (var line = startIndex; line >= firstValidIndex; line--)
        {
            if (IsSearchMatch(line) && line > firstValidIndex)
            {
                emptyLineCheck = true;
                continue;
            }

            if (!emptyLineCheck)
            {
                continue;
            }

            if (!IsSearchMatch(line))
            {
                // Restore to last diff
                line++;
            }

            // Include the header with the (possible) function summary line
            FirstVisibleLine = Math.Max(line - contextLines - 1, 0);
            LineAtCaret = line;
            return;
        }
    }

    /// <summary>
    ///     Copy the the text selected in the editor, filtering out lines starting with the given character.
    /// </summary>
    /// <param name="startChar">The start character to ignore for diffs.</param>
    public void CopyNotStartingWith(char startChar)
    {
        var text = GetSelectedText();
        var noSelection = false;

        if (string.IsNullOrEmpty(text))
        {
            text = GetText();
            noSelection = true;
        }

        var isDiff = _textHighlightService is DiffHighlightService;
        if (isDiff)
        {
            // add artificial space if selected text is not starting from line beginning, it will be removed later
            var pos = noSelection ? 0 : GetSelectionPosition();
            var fileText = GetText();

            if (pos > 0 && fileText[pos - 1] != '\n')
            {
                text = " " + text;
            }

            var lines = text.LazySplit('\n')
                .Where(s => s.Length == 0 || s[0] != startChar || (s.Length > 2 && s[1] == s[0] && s[2] == s[0]));
            var hpos = fileText.IndexOf("\n@@");

            // if header is selected then don't remove diff extra chars
            if (hpos <= pos)
            {
                const string specials = " -+";
                lines = lines.Select(s => s.Length > 0 && specials.Contains(s[0]) ? s[1..] : s);
            }

            text = string.Join("\n", lines);
        }

        ClipboardUtil.TrySetText(text.AdjustLineEndings(Module.GetEffectiveSetting<AutoCrlfType>("core.autocrlf")));
    }

    public int HScrollPosition
    {
        get => TextEditor.ActiveTextAreaControl.HScrollBar?.Value ?? 0;
        set
        {
            var scrollBar = TextEditor.ActiveTextAreaControl.HScrollBar;
            if (scrollBar is null)
            {
                return;
            }

            var max = scrollBar.Maximum - scrollBar.LargeChange;
            max = Math.Max(max, scrollBar.Minimum);
            scrollBar.Value = max > value ? value : max;
        }
    }

    int IFileViewer.VScrollPosition
    {
        get => TextEditor.ActiveTextAreaControl.VScrollBar?.Value ?? 0;
        set
        {
            VScrollBar scrollBar = TextEditor.ActiveTextAreaControl.VScrollBar;
            if (scrollBar is null)
            {
                return;
            }

            var max = scrollBar.Maximum - scrollBar.LargeChange;
            max = Math.Max(max, scrollBar.Minimum);
            scrollBar.Value = max > value ? value : max;
        }
    }

    EolMarkerStyle IFileViewer.EolMarkerStyle
    {
        get => TextEditor.EolMarkerStyle;
        set => TextEditor.EolMarkerStyle = value;
    }

    bool IFileViewer.ShowSpaces
    {
        get => TextEditor.ShowSpaces;
        set => TextEditor.ShowSpaces = value;
    }

    bool IFileViewer.ShowTabs
    {
        get => TextEditor.ShowTabs;
        set => TextEditor.ShowTabs = value;
    }

    int IFileViewer.VRulerPosition
    {
        get => TextEditor.VRulerRow;
        set => TextEditor.VRulerRow = value;
    }

    private int FirstVisibleLine
    {
        get => TextEditor.ActiveTextAreaControl.TextArea.TextView.FirstVisibleLine;
        set => TextEditor.ActiveTextAreaControl.TextArea.TextView.SetFirstVisibleLine(value,
            TextEditor.ActiveTextAreaControl.HScrollBar.Visible ? 0 : SystemInformation.HorizontalScrollBarHeight);
    }

    int IFileViewer.GetLineFromVisualPosY(int visualPosY)
    {
        return TextEditor.ActiveTextAreaControl.TextArea.TextView.GetLogicalLine(visualPosY);
    }

    private string GetLineText(int line)
    {
        if (line >= TextEditor.Document.TotalNumberOfLines)
        {
            return string.Empty;
        }

        return TextEditor.Document.GetText(TextEditor.Document.GetLineSegment(line));
    }

    void IFileViewer.GoToLine(int lineNumber)
    {
        TextEditor.ActiveTextAreaControl.Caret.Position = new TextLocation(0, GetCaretOffset(lineNumber, true));
        TextEditor.ActiveTextAreaControl.CenterViewOn(TextEditor.ActiveTextAreaControl.Caret.Position.Line, 5);
    }

    /// <summary>
    ///     Convert the line number to the offset for the caret.
    /// </summary>
    /// <param name="lineNumber">The line number seen in the editor.</param>
    /// <param name="rightFile">If the line number for the right file is preferred.</param>
    /// <returns>The caret offset</returns>
    private int GetCaretOffset(int lineNumber, bool rightFile)
    {
        if (TextEditor.ShowLineNumbers)
        {
            // All contents is shown, just translate the lineNumber (that is an offset)
            return lineNumber - 1;
        }

        for (var offset = 0; offset < TotalNumberOfLines; ++offset)
        {
            var diffLineNum = _lineNumbersControl.GetLineInfo(offset);
            if (diffLineNum is not null)
            {
                var diffLine = rightFile ? diffLineNum.RightLineNumber : diffLineNum.LeftLineNumber;
                if (diffLine != DiffLineInfo.NotApplicableLineNum && diffLine >= lineNumber)
                {
                    return offset;
                }
            }
        }

        return 0;
    }

    int IFileViewer.MaxLineNumber
    {
        get => TextEditor.ShowLineNumbers ? TotalNumberOfLines : _lineNumbersControl.MaxLineNumber;
    }

    public int CurrentFileColumn
    {
        get => TextEditor.ActiveTextAreaControl.Caret.Position.Column;
    }

    public int CurrentFileLine()
    {
        var isPartial =
            _textHighlightService is DiffHighlightService or GrepHighlightService or DifftasticHighlightService;
        _currentViewPositionCache.Capture();
        return _currentViewPositionCache.CurrentFileLine(isPartial);
    }

    private int LineAtCaret
    {
        get => TextEditor.ActiveTextAreaControl.Caret.Position.Line;
        set => TextEditor.ActiveTextAreaControl.Caret.Position =
            new TextLocation(TextEditor.ActiveTextAreaControl.Caret.Position.Column, value);
    }

    void IFileViewer.HighlightLines(int startLine, int endLine, Color color)
    {
        var document = TextEditor.Document;
        if (startLine > endLine || endLine >= document.TotalNumberOfLines)
        {
            return;
        }

        var markerStrategy = document.MarkerStrategy;
        var startLineSegment = document.GetLineSegment(startLine);
        var endLineSegment = document.GetLineSegment(endLine);
        markerStrategy.AddMarker(new TextMarker(startLineSegment.Offset,
            endLineSegment.Offset - startLineSegment.Offset + endLineSegment.Length,
            TextMarkerType.SolidBlock, color));
    }

    void IFileViewer.ClearHighlighting()
    {
        var document = TextEditor.Document;
        document.MarkerStrategy.RemoveAll(_ => true);
    }

    int IFileViewer.TotalNumberOfLines
    {
        get => TextEditor.Document.TotalNumberOfLines;
    }

    bool IFileViewer.IsReadOnly
    {
        get => TextEditor.IsReadOnly;
        set => TextEditor.IsReadOnly = value;
    }

    void IFileViewer.SetFileLoader(GetNextFileFnc fileLoader)
    {
        _findAndReplaceForm.SetFileLoader(fileLoader);
    }

    private void TextArea_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (ModifierKeys.HasFlag(Keys.Shift))
        {
            var scrollAmount = DpiUtil.Scale(8);
            HScrollPosition = e.Delta switch
            {
                > 0 => Math.Max(0, HScrollPosition - scrollAmount),
                < 0 => HScrollPosition + scrollAmount,
                _ => HScrollPosition
            };

            if (e is HandledMouseEventArgs handled)
            {
                handled.Handled = true;
            }

            return;
        }

        var isScrollingTowardTop = e.Delta > 0;
        var isScrollingTowardBottom = e.Delta < 0;
        VScrollBar scrollBar = TextEditor.ActiveTextAreaControl.VScrollBar;

        if (isScrollingTowardTop && scrollBar.Value == 0)
        {
            _continuousScrollEventManager?.RaiseTopScrollReached(sender!, e);
        }

        if (isScrollingTowardBottom && (!scrollBar.Visible || scrollBar.Value + scrollBar.Height > scrollBar.Maximum))
        {
            _continuousScrollEventManager?.RaiseBottomScrollReached(sender!, e);
        }
    }

    private void OnHScrollPositionChanged(EventArgs e)
    {
        HScrollPositionChanged?.Invoke(this, e);
    }

    private void OnVScrollPositionChanged(EventArgs e)
    {
        VScrollPositionChanged?.Invoke(this, e);
    }

    #endregion
}
