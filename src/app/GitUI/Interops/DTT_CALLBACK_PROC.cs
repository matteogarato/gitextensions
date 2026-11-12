using System.Runtime.InteropServices;

namespace GitUI.Interops;

internal static partial class NativeMethods
{
    public delegate int DttCallbackProc(IntPtr hdc,
        [MarshalAs(UnmanagedType.LPWStr)] string text, int textLen, ref Rectcls rc, int flags,
        IntPtr lParam);
}
