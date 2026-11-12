using System.Runtime.InteropServices;

namespace GitUI.Interops.User32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.User32, ExactSpelling = true, SetLastError = true)]
    public static extern int GetScrollPos(IntPtr hWnd, Interops.NativeMethods.Sb nBar);
}
