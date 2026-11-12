using System.Runtime.InteropServices;

namespace GitExtUtils.GitUI.Interops.ComCtl32;

internal static class NativeMethods
{
    internal const int ComCtl32ClrNone = unchecked((int)0xFFFFFFFF);

    [DllImport("comctl32.dll", EntryPoint = "ImageList_SetBkColor")]
    internal static extern int ImageListSetBkColor(IntPtr himl, int clrBk);
}
