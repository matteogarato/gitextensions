namespace GitExtUtils.GitUI.Interops;

internal static partial class NativeMethods
{
    internal enum ComboBoxButtonState : uint
    {
        StateSystemNone = 0,
        StateSystemInvisible = 0x00008000,
        StateSystemPressed = 0x00000008
    }
}
