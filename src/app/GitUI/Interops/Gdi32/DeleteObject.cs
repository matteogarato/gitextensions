using System.Runtime.InteropServices;
using static GitUI.Interops.Interop;

namespace GitUI.Interops.Gdi32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.Gdi32, ExactSpelling = true)]
    public static extern Bool DeleteObject(IntPtr hObject);
}
