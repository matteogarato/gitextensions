using System.Runtime.InteropServices;

namespace GitUI.Interops;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect(int left, int top, int right, int bottom)
    {
        public int left = left;
        public int top = top;
        public int right = right;
        public int bottom = bottom;

        public Rect(Rectangle r) : this(r.Left, r.Top, r.Right, r.Bottom)
        {
        }

        public static implicit operator Rectangle(Rect r)
        {
            return Rectangle.FromLTRB(r.left, r.top, r.right, r.bottom);
        }

        public static implicit operator Rect(Rectangle r)
        {
            return new Rect(r);
        }

        public readonly Size Size
        {
            get => new(right - left, bottom - top);
        }
    }

    /// <summary>
    ///     Theming interop requires RECT to be class.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class Rectcls
    {
#pragma warning disable 649
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
#pragma warning restore 649

        public static implicit operator Rectangle(Rectcls r)
        {
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }
}
