namespace GitUI.UserControls;

internal sealed class TextBoxEx : TextBox
{
    private Color _borderDefaultColor = SystemColors.WindowFrame;
    private Color _borderFocusedColor = SystemColors.HotTrack;
    private Color _borderHoveredColor = SystemColors.Highlight;
    private bool _hovered;

    public TextBoxEx()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    public Color BorderDefaultColor
    {
        get => _borderDefaultColor;
        set
        {
            _borderDefaultColor = value;
            Invalidate();
        }
    }

    public Color BorderHoveredColor
    {
        get => _borderHoveredColor;
        set
        {
            _borderHoveredColor = value;
            Invalidate();
        }
    }

    public Color BorderFocusedColor
    {
        get => _borderFocusedColor;
        set
        {
            _borderFocusedColor = value;
            Invalidate();
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (BorderStyle != BorderStyle.FixedSingle)
        {
            return;
        }

        switch (m.Msg)
        {
            case NativeMethods.WmNcpaint:
            case NativeMethods.WmPaint:
                var penColor = Focused ? _borderFocusedColor
                    : _hovered ? _borderHoveredColor
                    : _borderDefaultColor;

                var windowDc = NativeMethods.GetWindowDC(Handle);
                try
                {
                    using var graphics = Graphics.FromHdc(windowDc);
                    using Pen pen = new(penColor);

                    ControlPaint.DrawBorder(graphics, ClientRectangle, penColor, ButtonBorderStyle.Solid);
                }
                finally
                {
                    NativeMethods.ReleaseDC(Handle, windowDc);
                }

                break;

            case NativeMethods.WmNcmousehover when !_hovered:
            case NativeMethods.WmMousehover when !_hovered:
                _hovered = true;

                Invalidate();

                break;
            case NativeMethods.WmNcmouseleave:
            case NativeMethods.WmMouseleave:
                _hovered = false;

                Invalidate();

                break;
        }
    }
}
