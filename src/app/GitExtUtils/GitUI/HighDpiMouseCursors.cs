using System.Reflection;
using System.Runtime.InteropServices;

namespace GitExtUtils.GitUI;

internal static class HighDpiMouseCursors
{
    /// <summary>
    ///     Replaces some .NET Framework 96-dpi .cur file mouse cursors with system cursors.
    /// </summary>
    public static void Enable()
    {
        try
        {
            SetCursor("hand", Idc.Hand);
            SetCursor("hSplit", Idc.Sizens);
            SetCursor("vSplit", Idc.Sizewe);
        }
        catch
        {
            // ignore
        }

        static void SetCursor(string fieldName, Idc idc)
        {
            var field = typeof(Cursors).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, new Cursor(NativeMethods.LoadCursor(IntPtr.Zero, idc)));
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr LoadCursor(IntPtr hInstance, Idc lpCursorName);
    }

    private enum Idc
    {
        Arrow = 32512,
        Ibeam = 32513,
        Wait = 32514,
        Cross = 32515,
        Uparrow = 32516,
        Size = 32640,
        Icon = 32641,
        Sizenwse = 32642,
        Sizenesw = 32643,
        Sizewe = 32644,
        Sizens = 32645,
        Sizeall = 32646,
        No = 32648,
        Hand = 32649,
        Appstarting = 32650,
        Help = 32651
    }
}
