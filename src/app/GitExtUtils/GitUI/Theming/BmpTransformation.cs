using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GitExtUtils.GitUI.Theming;

public abstract class BmpTransformation(Bitmap bmp)
{
    protected const int B = 0;
    protected const int G = 1;
    protected const int R = 2;
    protected const int A = 3;

    private const int _bytesPerPixel = 4;
    private const PixelFormat _pixelFormat = PixelFormat.Format32bppArgb;

    protected Rectangle Rect { get; } = new(default, bmp.Size);
    protected byte[]? BgraValues { get; private set; }
    protected bool ImageChanged { get; set; }

    public void Execute()
    {
        var bmpData = bmp.LockBits(Rect, ImageLockMode.ReadWrite, _pixelFormat);

        try
        {
            var numBytes = bmpData.Stride * bmpData.Height;
            BgraValues = new byte[numBytes];
            Marshal.Copy(bmpData.Scan0, BgraValues, 0, numBytes);

            ExecuteRaw();

            if (ImageChanged)
            {
                Marshal.Copy(BgraValues, 0, bmpData.Scan0, numBytes);
            }
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }
    }

    protected abstract void ExecuteRaw();

    protected int GetLocation(int x, int y)
    {
        return _bytesPerPixel * ((Rect.Width * y) + x);
    }
}
