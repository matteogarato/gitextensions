using System.Runtime.InteropServices;

namespace GitUI.Interops;

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Dtbgopts
    {
        public uint dwSize;
        public uint dwFlags;
        public Rect rcClip;
    }
}
