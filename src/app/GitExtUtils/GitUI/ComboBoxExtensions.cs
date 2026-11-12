using System.Reflection;
using NativeMethods = GitExtUtils.GitUI.Interops.NativeMethods;

namespace GitExtUtils.GitUI;

public static class ComboBoxExtensions
{
    public const int BranchDropDownMinWidth = 200;
    public const int BranchDropDownMaxWidth = 600;

    public static void AdjustWidthToFitContent(this ComboBox comboBox)
    {
        ArgumentNullException.ThrowIfNull(comboBox);

        var width = GetPreferredDropDownWidth(comboBox);

        comboBox.Width = width;

        if (width != 0)
        {
            comboBox.DropDownWidth = width;
        }
    }

    /// <summary>
    ///     Resizes the drop-down width of a <see cref="ComboBox" /> to fit within provided bounds.
    /// </summary>
    /// <param name="comboBox">Target control</param>
    /// <param name="minWidth">Minimum drop-down width. Defaults to <see cref="ComboBoxExtensions.BranchDropDownMinWidth" />.</param>
    /// <param name="maxWidth">Maximum drop-down width. Defaults to <see cref="ComboBoxExtensions.BranchDropDownMaxWidth" />.</param>
    /// <param name="dpiScaleBounds">
    ///     Whether to apply Dpi scaling to <paramref name="minWidth" /> and
    ///     <paramref name="maxWidth" />. Defaults to <c>true</c>.
    /// </param>
    public static void ResizeDropDownWidth(this ComboBox comboBox, int minWidth = BranchDropDownMinWidth,
        int maxWidth = BranchDropDownMaxWidth, bool dpiScaleBounds = true)
    {
        ArgumentNullException.ThrowIfNull(comboBox);

        if (dpiScaleBounds)
        {
            minWidth = DpiUtil.Scale(minWidth);
            maxWidth = DpiUtil.Scale(maxWidth);
        }

        var calculatedWidth = GetPreferredDropDownWidth(comboBox);
        comboBox.DropDownWidth = Math.Min(Math.Max(calculatedWidth, minWidth), maxWidth);
    }

    /// <summary>
    ///     Resizes the drop-down width of a <see cref="ToolStripComboBox" /> to fit within provided bounds.
    /// </summary>
    /// <param name="comboBox">Target control</param>
    /// <param name="minWidth">Minimum drop-down width. Defaults to <see cref="ComboBoxExtensions.BranchDropDownMinWidth" />.</param>
    /// <param name="maxWidth">Maximum drop-down width. Defaults to <see cref="ComboBoxExtensions.BranchDropDownMaxWidth" />.</param>
    /// <param name="dpiScaleBounds">
    ///     Whether to apply Dpi scaling to <paramref name="minWidth" /> and
    ///     <paramref name="maxWidth" />. Defaults to <c>true</c>.
    /// </param>
    public static void ResizeDropDownWidth(this ToolStripComboBox comboBox, int minWidth = BranchDropDownMinWidth,
        int maxWidth = BranchDropDownMaxWidth, bool dpiScaleBounds = true)
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        ((ComboBox)comboBox.Control).ResizeDropDownWidth(minWidth, maxWidth, dpiScaleBounds);
    }

    private static int GetPreferredDropDownWidth(ComboBox comboBox)
    {
        var calculatedWidth = 0;
        using var graphics = comboBox.CreateGraphics();

        var displayMemberName = comboBox.DisplayMember;
        var font = comboBox.Font;

        foreach (var item in comboBox.Items)
        {
            var area = graphics.MeasureString(GetDisplayValue(displayMemberName, item), font);
            calculatedWidth = Math.Max((int)area.Width, calculatedWidth);
        }

        return calculatedWidth + CalculateVerticalScrollBarWidth(comboBox);
    }

    private static string? GetDisplayValue(string displayMemberName, object item)
    {
        if (displayMemberName.Length == 0)
        {
            return item.ToString();
        }

        var displayMemberProperty = item
            .GetType()
            .GetProperty(displayMemberName);

        // When an item doesn't have a property, ComboBox falls back to ToString() - but not if the property contains null
        if (displayMemberProperty is null)
        {
            return item.ToString();
        }

        return displayMemberProperty
            .GetValue(item)
            ?.ToString();
    }

    private static unsafe int CalculateVerticalScrollBarWidth(ComboBox comboBox)
    {
        NativeMethods.Comboboxinfo cboInfo = new() { cbSize = (uint)sizeof(NativeMethods.Comboboxinfo) };

        if (Interops.User32.NativeMethods.GetComboBoxInfo(comboBox.Handle, &cboInfo) != Interop.BOOL.TRUE)
        {
            return 0;
        }

        var listStyle =
            Interops.User32.NativeMethods.GetWindowLongPtrW(cboInfo.hwndList, Interops.User32.NativeMethods.Gwl.Style);
        var hasVerticalScrollBar = (listStyle & NativeMethods.WsVscroll) == NativeMethods.WsVscroll;

        return hasVerticalScrollBar
            ? SystemInformation.VerticalScrollBarWidth
            : 0;
    }
}
