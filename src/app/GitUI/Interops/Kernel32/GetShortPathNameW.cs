using System.Runtime.InteropServices;
using System.Text;

namespace GitUI.Interops.Kernel32;

internal static partial class NativeMethods
{
    [DllImport(Libraries.Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetShortPathNameW(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);
}
