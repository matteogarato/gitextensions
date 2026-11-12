using System.Runtime.InteropServices;

namespace GitUI.Interops.Gdi32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.Gdi32, ExactSpelling = true, SetLastError = true)]
    public static extern IntPtr CreateSolidBrush(int crColor);
}
