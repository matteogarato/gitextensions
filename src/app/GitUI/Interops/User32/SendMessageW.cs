using System.Runtime.InteropServices;

namespace GitUI.Interops.User32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.User32, ExactSpelling = true)]
    public static extern IntPtr SendMessageW(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam = default,
        IntPtr lParam = default);

    public static IntPtr SendMessageW(
        HandleRef hWnd,
        uint msg,
        IntPtr wParam = default,
        IntPtr lParam = default)
    {
        var result = SendMessageW(hWnd.Handle, msg, wParam, lParam);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }

    public static unsafe IntPtr SendMessageW<T>(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        ref T lParam) where T : unmanaged
    {
        fixed (void* l = &lParam)
        {
            return SendMessageW(hWnd, msg, wParam, (IntPtr)l);
        }
    }
}
