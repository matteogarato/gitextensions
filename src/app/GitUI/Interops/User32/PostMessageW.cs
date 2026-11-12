using System.Runtime.InteropServices;

namespace GitUI.Interops.User32;

internal static partial class NativeMethods
{
    public static IntPtr HwndBroadcast
    {
        get => 0xFFFF;
    }

    [DllImport(Libraries.User32, ExactSpelling = true)]
    public static extern IntPtr PostMessageW(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam = default,
        IntPtr lParam = default);

    public static IntPtr PostMessageW(
        HandleRef hWnd,
        uint msg,
        IntPtr wParam = default,
        IntPtr lParam = default)
    {
        var result = PostMessageW(hWnd.Handle, msg, wParam, lParam);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }

    public static unsafe IntPtr PostMessageW<T>(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        ref T lParam) where T : unmanaged
    {
        fixed (void* l = &lParam)
        {
            return PostMessageW(hWnd, msg, wParam, (IntPtr)l);
        }
    }
}
