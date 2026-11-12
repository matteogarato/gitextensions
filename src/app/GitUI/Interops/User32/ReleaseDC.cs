using System.Runtime.InteropServices;

namespace GitUI.Interops.User32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.User32)]
    public static extern void ReleaseDC(IntPtr hwnd, IntPtr dc);
}
