using GitCommands.Settings;
using GitUI.Models;
using Timer = System.Windows.Forms.Timer;

namespace GitUI.UserControls;

internal class OutputHistoryPanelController : OutputHistoryControllerBase
{
    private readonly SplitContainer _horizontalSplitContainer;
    private readonly OutputHistoryControl _outputHistoryControl;
    private readonly Action _showVerticalSplitContainer1;
    private readonly Timer _timer = new() { Interval = 25 };
    private readonly SplitContainer _verticalSplitContainer1;
    private readonly SplitContainer _verticalSplitContainer2;

    internal OutputHistoryPanelController(IOutputHistoryProvider outputHistoryProvider,
        OutputHistoryControl outputHistoryControl,
        Control parent,
        Action showVerticalSplitContainer1,
        SplitContainer verticalSplitContainer1,
        SplitContainer verticalSplitContainer2,
        SplitContainer horizontalSplitContainer)
        : base(outputHistoryProvider, outputHistoryControl)
    {
        _outputHistoryControl = outputHistoryControl;
        _showVerticalSplitContainer1 = showVerticalSplitContainer1;
        _verticalSplitContainer1 = verticalSplitContainer1;
        _verticalSplitContainer2 = verticalSplitContainer2;
        _horizontalSplitContainer = horizontalSplitContainer;

        if (!outputHistoryProvider.Enabled)
        {
            _verticalSplitContainer1.Panel2Collapsed = true;
            _verticalSplitContainer2.Panel2Collapsed = true;
            return;
        }

        parent.Controls.Add(_outputHistoryControl);
        parent.Controls.SetChildIndex(_outputHistoryControl, 0);
        SetSizeByVerticalSplitContainer1(_outputHistoryControl, EventArgs.Empty);

        _timer.Tick += SetSizeByVerticalSplitContainer1;
        _verticalSplitContainer1.Invalidated += SetSizeByVerticalSplitContainer1Deferred;
        _verticalSplitContainer1.Parent!.VisibleChanged += SetSizeByVerticalSplitContainer1Deferred;
        _verticalSplitContainer1.SplitterMoved += SetSizeByVerticalSplitContainer1;
        _verticalSplitContainer2.SplitterMoved += SetSizeByVerticalSplitContainer2;
        horizontalSplitContainer.SplitterMoved += SetSizeByVerticalSplitContainer1;
        for (var splitterParent = horizontalSplitContainer.Parent;
             splitterParent is not null;
             splitterParent = splitterParent.Parent)
        {
            splitterParent.VisibleChanged += SetSizeByVerticalSplitContainer1Deferred;
        }

        _outputHistoryControl.Disposed += (_, _) =>
        {
            _timer.Tick -= SetSizeByVerticalSplitContainer1;
            _verticalSplitContainer1.Invalidated -= SetSizeByVerticalSplitContainer1Deferred;
            _verticalSplitContainer1.Parent.VisibleChanged -= SetSizeByVerticalSplitContainer1Deferred;
            _verticalSplitContainer1.SplitterMoved -= SetSizeByVerticalSplitContainer1;
            _verticalSplitContainer2.SplitterMoved -= SetSizeByVerticalSplitContainer2;
            horizontalSplitContainer.SplitterMoved -= SetSizeByVerticalSplitContainer1;
            for (var splitterParent = horizontalSplitContainer.Parent;
                 splitterParent is not null;
                 splitterParent = splitterParent.Parent)
            {
                splitterParent.VisibleChanged -= SetSizeByVerticalSplitContainer1Deferred;
            }
        };
    }

    internal override bool FocusAndToggleIfPanel()
    {
        if (!OutputHistoryProvider.Enabled)
        {
            return false;
        }

        var show = !AppSettings.OutputHistoryPanelVisible.Value;
        AppSettings.OutputHistoryPanelVisible.Value = show;
        _verticalSplitContainer1.Panel2Collapsed = !show;
        if (show && !_verticalSplitContainer1.Visible)
        {
            _showVerticalSplitContainer1();
        }

        SetSizeByVerticalSplitContainer1(_outputHistoryControl, EventArgs.Empty);

        if (show)
        {
            TextBox.FindForm()!.ActiveControl = TextBox;
        }

        return true;
    }

    private void SetSizeByVerticalSplitContainer1Deferred(object? sender, EventArgs eventArgs)
    {
        _timer.Stop();
        _timer.Start();
    }

    private void SetSizeByVerticalSplitContainer1(object? sender, EventArgs eventArgs)
    {
        _timer.Stop();
        SetSizeIgnoringEvents(_verticalSplitContainer1.Panel2.Height);
    }

    private void SetSizeByVerticalSplitContainer2(object? sender, EventArgs eventArgs)
    {
        SetSizeIgnoringEvents(_verticalSplitContainer2.Panel2.Height);
    }

    private void SetSizeIgnoringEvents(int height)
    {
        try
        {
            _verticalSplitContainer1.Invalidated -= SetSizeByVerticalSplitContainer1Deferred;
            _verticalSplitContainer1.SplitterMoved -= SetSizeByVerticalSplitContainer1;
            _verticalSplitContainer2.SplitterMoved -= SetSizeByVerticalSplitContainer2;

            SetSize(height);
        }
        finally
        {
            _verticalSplitContainer1.Invalidated += SetSizeByVerticalSplitContainer1Deferred;
            _verticalSplitContainer1.SplitterMoved += SetSizeByVerticalSplitContainer1;
            _verticalSplitContainer2.SplitterMoved += SetSizeByVerticalSplitContainer2;
        }
    }

    private void SetSize(int height)
    {
        const int margin = 6;
        const int offset = 1;
        const int h1Offset = 1;

        var visible = !_verticalSplitContainer1.Panel2Collapsed && _verticalSplitContainer1.Visible;
        _outputHistoryControl.Visible = visible;
        _verticalSplitContainer2.Panel2Collapsed = !visible;
        if (visible)
        {
            var width = (2 * offset) + GetWidth();
            _outputHistoryControl.SetBounds(margin, _outputHistoryControl.Parent!.Height - height - offset - margin,
                width, (2 * offset) + height);

            _verticalSplitContainer1.SplitterDistance = Math.Max(0,
                _verticalSplitContainer1.ClientSize.Height - _verticalSplitContainer1.SplitterWidth - height);
            _verticalSplitContainer2.SplitterDistance = Math.Max(0,
                _verticalSplitContainer2.ClientSize.Height - _verticalSplitContainer2.SplitterWidth - height);
        }

        return;

        int GetWidth()
        {
            if (!_horizontalSplitContainer.Visible)
            {
                return h1Offset + _verticalSplitContainer1.Panel2.ClientSize.Width;
            }

            var left = _verticalSplitContainer1.Panel2
                .PointToScreen(new Point(_horizontalSplitContainer.Panel1.ClientRectangle.Left, 0)).X;
            var right = _horizontalSplitContainer.Panel1
                .PointToScreen(new Point(_horizontalSplitContainer.Panel1.ClientRectangle.Right, 0)).X;
            return right - left;
        }
    }
}
