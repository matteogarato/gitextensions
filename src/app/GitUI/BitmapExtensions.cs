namespace GitUI;

public static class BitmapExtensions
{
    public static Icon ToIcon(this Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var handle = IntPtr.Zero;
        try
        {
            handle = bitmap.GetHicon();
            var icon = Icon.FromHandle(handle);

            return (Icon)icon.Clone();
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(handle);
            }
        }
    }
}
