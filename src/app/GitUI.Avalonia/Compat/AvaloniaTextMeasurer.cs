using System.Globalization;
using Avalonia.Media;
using GitExtensions.Shims.WinForms;
using Font = GitExtensions.Shims.WinForms.Font;
using FontStyle = Avalonia.Media.FontStyle;
using Size = System.Drawing.Size;

namespace GitUI.Compat;

/// <summary>
///     Implements the shim <see cref="ITextMeasurer" /> with real Avalonia text shaping,
///     replacing the shim's built-in approximation.
/// </summary>
public sealed class AvaloniaTextMeasurer : ITextMeasurer
{
    Size ITextMeasurer.MeasureText(string? text, Font? font)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Size.Empty;
        }

        Typeface typeface = new(
            font?.Name ?? "sans-serif",
            font?.Italic is true ? FontStyle.Italic : FontStyle.Normal,
            font?.Bold is true ? FontWeight.Bold : FontWeight.Normal);

        // WinForms font sizes are points (1/72 inch); Avalonia takes device-independent pixels (1/96 inch).
        const double pixelsPerPoint = 96.0 / 72;
        var fontSizePixels = (font?.Size ?? 9F) * pixelsPerPoint;

        FormattedText formatted = new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSizePixels,
            null);

        return new Size((int)Math.Ceiling(formatted.WidthIncludingTrailingWhitespace),
            (int)Math.Ceiling(formatted.Height));
    }
}
