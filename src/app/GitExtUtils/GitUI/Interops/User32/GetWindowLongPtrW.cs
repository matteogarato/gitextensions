using System.Runtime.InteropServices;

namespace GitExtUtils.GitUI.Interops.User32;

internal static partial class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
    internal static extern nint GetWindowLongPtrW(nint hWnd, Gwl nIndex);
}
