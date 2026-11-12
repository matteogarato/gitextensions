using System.ComponentModel;
using System.Reflection;
using GitExtUtils.GitUI;
using GitUI.Interops.User32;

namespace GitUI.UserControls;

/// <summary>
///     This class adds on to the functionality provided in System.Windows.Forms.ToolStrip.
/// </summary>
public class ToolStripEx : ToolStrip, IToolStripEx
{
    private readonly ToolStripButton _gripButton;

    public ToolStripEx()
    {
        Renderer = new ToolStripExSystemRenderer();

        var propGrip = GetType().GetProperty("Grip", BindingFlags.Instance | BindingFlags.NonPublic);
        _gripButton = (propGrip!.GetValue(this) as ToolStripButton)!;
    }

    /// <summary>
    ///     Gets or sets whether the ToolStripEx honors item clicks when its containing form does
    ///     not have input focus.
    /// </summary>
    /// <remarks>
    ///     Default value is false, which is the same behavior provided by the base ToolStrip class.
    /// </remarks>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool ClickThrough { get; set; }

    /// <summary>
    ///     Gets or sets whether the ToolStrip grip button is enabled.
    /// </summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool GripEnabled
    {
        get => _gripButton.Enabled;
        set => _gripButton.Enabled = value;
    }

    /// <inheritdoc />
    [Category("Appearance")]
    [DefaultValue(true)]
    bool IToolStripEx.DrawBorder { get; set; } = true;

    protected override void OnItemAdded(ToolStripItemEventArgs e)
    {
        base.OnItemAdded(e);

        if (e.Item is ToolStripDropDownItem item)
        {
            item.DropDownOpening += SplitButton_DropDownOpening;
            item.DropDownClosed += SplitButton_DropDownClosed;
        }
    }

    protected override void OnItemRemoved(ToolStripItemEventArgs e)
    {
        if (e.Item is ToolStripDropDownItem item)
        {
            item.DropDownOpening -= SplitButton_DropDownOpening;
            item.DropDownClosed -= SplitButton_DropDownClosed;
        }
    }

    private static void SplitButton_DropDownOpening(object? sender, EventArgs e)
    {
        if (sender is ToolStripDropDownItem { Owner: Control control })
        {
            // Suspends the control's rendering process.
            NativeMethods.SendMessageW(control.Handle, NativeMethods.WmSetredraw, NativeMethods.False, IntPtr.Zero);
        }
    }

    private static void SplitButton_DropDownClosed(object? sender, EventArgs e)
    {
        if (sender is ToolStripDropDownItem { Owner: Control control })
        {
            // Resumes the control's rendering process and trigger a redraw.
            NativeMethods.SendMessageW(control.Handle, NativeMethods.WmSetredraw, NativeMethods.True, IntPtr.Zero);
            control.Refresh();
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (ClickThrough &&
            m.Msg == NativeMethods.WmMouseactivate &&
            m.Result == (IntPtr)NativeMethods.MaActivateandeat)
        {
            m.Result = (IntPtr)NativeMethods.MaActivate;
        }
    }
}
