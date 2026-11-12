using System.Runtime.InteropServices;
using GitExtUtils.GitUI;
using ResourceManager;

namespace GitUI;

public sealed class MouseWheelRedirector : IMessageFilter
{
    private static readonly MouseWheelRedirector _instance = new();

    private bool _active;

    private MouseWheelRedirector()
    {
    }

    public static bool Active
    {
        get => _instance._active;
        set
        {
            if (_instance._active != value)
            {
                _instance._active = value;
                if (_instance._active)
                {
                    Application.AddMessageFilter(_instance);
                }
                else
                {
                    Application.RemoveMessageFilter(_instance);
                }
            }
        }
    }

    bool IMessageFilter.PreFilterMessage(ref Message m)
    {
        const int wmMousewheel = 0x20a;
        const int wmMousehwheel = 0x20e;
        if (m.Msg != wmMousewheel && m.Msg != wmMousehwheel)
        {
            return false;
        }

        // WM_MOUSEWHEEL, find the control at screen position m.LParam
        var hwnd = NativeMethods.WindowFromPoint(m.LParam.ToPoint());
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var control = Control.FromHandle(hwnd);
        if (control is null)
        {
            return false;
        }

        if (hwnd == m.HWnd && !IsNonScrollableRichTextBox(control))
        {
            return false;
        }

        while (control is not (null or GitExtensionsControl))
        {
            var nonScrollableRtbx = IsNonScrollableRichTextBox(control);

            control = control.Parent;
            if (nonScrollableRtbx)
            {
                hwnd = control!.Handle;
            }
        }

        if (control is null)
        {
            return false;
        }

        NativeMethods.SendMessage(hwnd, m.Msg, m.WParam, m.LParam);
        return true;

        static bool IsNonScrollableRichTextBox(Control c)
        {
            return c is RichTextBox { ScrollBars: RichTextBoxScrollBars.None };
        }
    }

    private static class NativeMethods
    {
        // P/Invoke declarations
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Point pt);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Point(int x, int y)
        {
            public readonly int X = x;
            public readonly int Y = y;

            public static implicit operator System.Drawing.Point(Point p)
            {
                return new System.Drawing.Point(p.X, p.Y);
            }

            public static implicit operator Point(System.Drawing.Point p)
            {
                return new Point(p.X, p.Y);
            }
        }
    }
}
