using GitExtUtils.GitUI;

namespace GitUI.SpellChecker;

/// <summary>
///     Wraps API calls for access to missing functionality
///     from the System.Windows.Forms text box.
/// </summary>
internal static class TextBoxHelper
{
    private static readonly double _anInch = 14.4 / DpiUtil.ScaleX;

    internal static int GetBaselineOffsetAtCharIndex(TextBoxBase tb, int index)
    {
        if (tb is not RichTextBox rtb || !OperatingSystem.IsWindows())
        {
            return tb.Font.Height;
        }

        var lineNumber = rtb.GetLineFromCharIndex(index);
        var lineIndex = NativeMethods
            .SendMessageW(rtb.Handle, NativeMethods.EmLineindex, (IntPtr)lineNumber, IntPtr.Zero).ToInt32();
        var lineLength = NativeMethods.SendMessageW(rtb.Handle, NativeMethods.EmLinelength, (IntPtr)index, IntPtr.Zero)
            .ToInt32();

        NativeMethods.Charrange charRange = new() { CpMin = lineIndex, CpMax = lineIndex + lineLength };

        NativeMethods.Rect rect = new()
        {
            top = 0, bottom = (int)_anInch, left = 0, right = 10000000 ////(int)(rtb.Width * anInch + 20);
        };

        NativeMethods.Rect rectPage = new()
        {
            top = 0, bottom = (int)_anInch, left = 0, right = 10000000 ////(int)(rtb.Width * anInch + 20);
        };

        var canvas = Graphics.FromHwnd(rtb.Handle);
        var canvasHdc = canvas.GetHdc();

        NativeMethods.Formatrange formatRange = new()
        {
            Chrg = charRange,
            Hdc = canvasHdc,
            HdcTarget = canvasHdc,
            Rc = rect,
            RcPage = rectPage
        };

        NativeMethods.SendMessageW(rtb.Handle, NativeMethods.EmFormatrange, IntPtr.Zero, ref formatRange);

        canvas.ReleaseHdc(canvasHdc);
        canvas.Dispose();

        return (int)((formatRange.Rc.bottom - formatRange.Rc.top) / _anInch);
    }
}
