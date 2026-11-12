using System.Runtime.InteropServices;

namespace GitUI.Interops.WinInet;

internal static class NativeMethods
{
    [DllImport(Libraries.WinInet)]
    public static extern bool InternetGetConnectedState(out int description, int reservedValue);
}
