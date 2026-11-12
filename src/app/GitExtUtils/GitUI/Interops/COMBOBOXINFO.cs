using System.Runtime.InteropServices;

namespace GitExtUtils.GitUI.Interops;

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Comboboxinfo
    {
        public uint cbSize;
        public System.NativeMethods.RECT rcItem;
        public System.NativeMethods.RECT rcButton;
        public ComboBoxButtonState buttonState;
        public nint hwndCombo;
        public nint hwndEdit;
        public nint hwndList;
    }
}
