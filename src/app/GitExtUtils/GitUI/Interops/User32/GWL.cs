namespace GitExtUtils.GitUI.Interops.User32;

internal static partial class NativeMethods
{
    /// <summary>
    ///     Window long values for <see cref="GetWindowLongPtrW(nint, Gwl)" />.
    /// </summary>
    internal enum Gwl
    {
        Wndproc = -4,
        Hwndparent = -8,
        Style = -16,
        Exstyle = -20,
        Id = -12
    }
}
