namespace GitUI.Interops;

internal static partial class NativeMethods
{
    public readonly struct Point(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;

        public static implicit operator System.Drawing.Point(Point p)
        {
            return new System.Drawing.Point(p.X, p.Y);
        }

        public static implicit operator Point(System.Drawing.Point p)
        {
            return new Point(p.X, p.Y);
        }
    }
}
