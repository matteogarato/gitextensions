namespace GitUI.Interops;

internal static partial class NativeMethods
{
    public const int WmUser = 0x0400;
    public const int WmLbuttondown = 0x0201;
    public const int WmLbuttonup = 0x0202;
    public const int WmRbuttondown = 0x0204;
    public const int WmRbuttonup = 0x0205;
    public const int WmMouseactivate = 0x21;

    public const int WmHscroll = 276;
    public const int WmVscroll = 0x115;
    public const int WmMousewheel = 0x020A;
    public const int WmMousehwheel = 0x020E;
    public const int WmKeydown = 0x0100;

    public const int LvmFirst = 0x1000;
    public const int LvmHittest = LvmFirst + 18;
    public const int LvmSetgroupinfo = LvmFirst + 147;
    public const int LvmSubitemhittest = LvmFirst + 57;
    public const int LvmInsertgroup = LvmFirst + 145;

    public const uint MaActivate = 1;
    public const uint MaActivateandeat = 2;
    public const uint MaNoactivate = 3;
    public const uint MaNoactivateandeat = 4;
    public const int CpNocloseButton = 0x200;

    // from vsstyle.h
    public const int TextMaininstruction = 1;

    // from vssym32.h
    public const int TmtTextcolor = 3803;
    public const int TmtFont = 210;

    public const int EmFormatrange = WmUser + 57;
    public const int EmLineindex = 0x00BB;
    public const int EmLinelength = 0x00C1;
    public const int EmPosfromchar = 0x00D6;
    public const int EmCharfrompos = 0x00D7;
    public const int EmGetfirstvisibleline = 0xCE;

    /// <summary>
    ///     The WM_NULL message performs no operation. An application sends the WM_NULL message if it wants to post a message
    ///     that the recipient window will ignore.
    /// </summary>
    public const int WmNull = 0x0000;

    /// <summary>
    ///     The WM_SETREDRAW message is sent to a window to allow changes in that window to be redrawn, or to prevent changes
    ///     in that window from being redrawn.
    /// </summary>
    public const int WmSetredraw = 0x000B;

    /// <summary>
    ///     The WM_PAINT message is sent when the system or another application makes a request to paint a portion of an
    ///     application's window. The message is sent when the UpdateWindow or RedrawWindow function is called, or by the
    ///     DispatchMessage function when the application obtains a WM_PAINT message by using the GetMessage or PeekMessage
    ///     function.
    /// </summary>
    public const int WmPaint = 0x000F;

    /// <summary>
    ///     The WM_NCPAINT message is sent to a window when its frame must be painted.
    /// </summary>
    public const int WmNcpaint = 0x0085;

    /// <summary>
    ///     The WM_SYSCOMMAND message is sent when the user chooses a command from the Window menu
    ///     or when the user chooses the maximize button, minimize button, restore button, or close button.
    /// </summary>
    public const int WmSyscommand = 0x0112;

    /// <summary>
    ///     The WM_CTLCOLORSCROLLBAR message is sent to the parent window of a scroll bar control when the control is about to
    ///     be drawn.
    /// </summary>
    public const int WmCtlcolorscrollbar = 0x0137;

    /// <summary>
    ///     The WM_MOUSEHOVER message is posted to a window when the cursor hovers over the client area of the window for the
    ///     period of time specified in a prior call to TrackMouseEvent.
    /// </summary>
    public const int WmMousehover = 0x02A1;

    /// <summary>
    ///     The WM_MOUSELEAVE message is posted to a window when the cursor leaves the client area of the window specified in a
    ///     prior call to TrackMouseEvent.
    /// </summary>
    public const int WmMouseleave = 0x02A3;

    /// <summary>
    ///     The WM_NCMOUSEHOVER message is posted to a window when the cursor hovers over the nonclient area of the window for
    ///     the period of time specified in a prior call to TrackMouseEvent.
    /// </summary>
    public const int WmNcmousehover = 0x02A0;

    /// <summary>
    ///     The WM_NCMOUSELEAVE message is posted to a window when the cursor leaves the nonclient area of the window specified
    ///     in a prior call to TrackMouseEvent.
    /// </summary>
    public const int WmNcmouseleave = 0x02A2;

    /// <summary>
    ///     The type of a WM_SYSCOMMAND (given in WParam).
    /// </summary>
    public const nint ScClose = 0xf060;

    public static readonly IntPtr False = IntPtr.Zero;
    public static readonly IntPtr True = new(1);
}
