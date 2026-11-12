namespace GitUI.Interops;

internal static partial class NativeMethods
{
    public struct Formatrange
    {
        public IntPtr Hdc; // Actual DC to draw on
        public IntPtr HdcTarget; // Target DC for determining text formatting
        public Rect Rc; // Region of the DC to draw to (in twips)
        public Rect RcPage; // Region of the whole DC (page size) (in twips)
        public Charrange Chrg; // Range of text to draw (see earlier declaration)
    }
}
