namespace GitUI.Interops;

internal static partial class NativeMethods
{
    /// <summary>
    ///     Scroll bar values (SB_) that indicates the user's scrolling request in a horizontal scrollbar.
    ///     Used by WM_HSCROLL message.
    /// </summary>
    public enum Sbh
    {
        Lineleft = 0,
        Lineright = 1,
        Pageleft = 2,
        Pageright = 3,
        Thumbposition = 4,
        Thumbtrack = 5,
        Left = 6,
        Right = 7,
        Endscroll = 8
    }
}
