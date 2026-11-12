namespace GitUI.UserControls;

public class NativeTreeView : TreeView
{
    private const int _mkShift = 0x0004;

    public NativeTreeView()
    {
        DoubleBuffered = true;
    }

    protected override void CreateHandle()
    {
        base.CreateHandle();
        if (!Application.IsDarkModeEnabled)
        {
            // explorer style selection painting in left panel
            // Not needed in dark mode, this is the same for "DarkMode_Explorer"
            SetWindowTheme(Handle, "explorer", null);
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmMousewheel when (m.WParam.ToInt64() & _mkShift) != 0:
                {
                    int delta = (short)(m.WParam.ToInt64() >> 16);
                    if (delta != 0)
                    {
                        // Shift+wheel-up scrolls left, Shift+wheel-down scrolls right
                        ScrollHorizontally(-delta);
                    }

                    return;
                }

            case WmMousehwheel:
                {
                    int delta = (short)(m.WParam.ToInt64() >> 16);
                    if (delta != 0)
                    {
                        // Positive delta = tilt right = scroll right
                        ScrollHorizontally(delta);
                    }

                    return;
                }
        }

        base.WndProc(ref m);

        return;

        void ScrollHorizontally(int delta)
        {
            Sbh direction;
            int count;

            var scrollLines = SystemInformation.MouseWheelScrollLines;
            if (scrollLines == -1)
            {
                direction = delta > 0 ? Sbh.Pageright : Sbh.Pageleft;
                count = 1;
            }
            else
            {
                direction = delta > 0 ? Sbh.Lineright : Sbh.Lineleft;
                count = Math.Max(1, Math.Abs(delta) * scrollLines * 3 / SystemInformation.MouseWheelScrollDelta);
            }

            for (var i = 0; i < count; i++)
            {
                var hscrollMsg = Message.Create(Handle, WmHscroll, (IntPtr)direction, IntPtr.Zero);
                DefWndProc(ref hscrollMsg);
            }
        }
    }
}
