using System.Runtime.InteropServices;

namespace GitUI.Interops.User32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.User32, ExactSpelling = true)]
    public static extern IntPtr GetActiveWindow();
}
