using System.Runtime.InteropServices;
using static GitUI.Interops.Interop;

namespace GitUI.Interops.User32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.User32, ExactSpelling = true)]
    public static extern Bool ShowCaret(IntPtr hWnd);
}
