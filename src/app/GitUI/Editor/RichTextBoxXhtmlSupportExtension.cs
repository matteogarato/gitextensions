using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using GitExtensions.Extensibility;

#pragma warning disable SA1305 // Field names should not use Hungarian notation

namespace GitUI.Editor;

internal static class RichTextBoxXhtmlSupportExtension
{
    // Defines for STRUCT_CHARFORMAT member dwEffects
    [Flags]
    public enum Cfe : uint
    {
        Bold = 0x00000001,
        Italic = 0x00000002,
        Underline = 0x00000004,
        Strikeout = 0x00000008,
        Protected = 0x00000010,
        Link = 0x00000020,
        Autocolor = 0x40000000,
        Subscript = 0x00010000, /* Superscript and subscript are */
        Superscript = 0x00020000, /*  mutually exclusive             */

        Smallcaps = 0x0040, /* (*)  */
        Allcaps = 0x0080, /* Displayed by 3.0 */
        Hidden = 0x0100, /* Hidden by 3.0 */
        Outline = 0x0200, /* (*)  */
        Shadow = 0x0400, /* (*)  */
        Emboss = 0x0800, /* (*)  */
        Imprint = 0x1000, /* (*)  */
        Disabled = 0x2000,
        Revised = 0x4000,

        // CFE.AUTOCOLOR and CFE.AUTOBACKCOLOR correspond to CFM.COLOR and
        // CFM.BACKCOLOR, respectively, which control them
        Autobackcolor = 0x04000000
    }

    // Defines for STRUCT_CHARFORMAT member dwMask
    [Flags]
    public enum Cfm : uint
    {
        Bold = 0x00000001,
        Italic = 0x00000002,
        Underline = 0x00000004,
        Strikeout = 0x00000008,
        Protected = 0x00000010,
        Link = 0x00000020,
        Size = 0x80000000,
        Color = 0x40000000,
        Face = 0x20000000,
        Offset = 0x10000000,
        Charset = 0x08000000,
        Subscript = Cfe.Subscript | Cfe.Superscript,
        Superscript = Subscript,

        Smallcaps = 0x0040, /* (*)  */
        Allcaps = 0x0080, /* Displayed by 3.0 */
        Hidden = 0x0100, /* Hidden by 3.0 */
        Outline = 0x0200, /* (*)  */
        Shadow = 0x0400, /* (*)  */
        Emboss = 0x0800, /* (*)  */
        Imprint = 0x1000, /* (*)  */
        Disabled = 0x2000,
        Revised = 0x4000,

        Backcolor = 0x04000000,
        Lcid = 0x02000000,
        Underlinetype = 0x00800000, /* Many displayed by 3.0 */
        Weight = 0x00400000,
        Spacing = 0x00200000, /* Displayed by 3.0 */
        Kerning = 0x00100000, /* (*)  */
        Style = 0x00080000, /* (*)  */
        Animation = 0x00040000, /* (*)  */
        Revauthor = 0x00008000
    }

    public enum Cfu : byte
    {
        Underlinenone = 0x00,
        Underline = 0x01,
        Underlineword = 0x02, /* (*) displayed as ordinary underline    */
        Underlinedouble = 0x03, /* (*) displayed as ordinary underline  */
        Underlinedotted = 0x04,
        Underlinedash = 0x05,
        Underlinedashdot = 0x06,
        Underlinedashdotdot = 0x07,
        Underlinewave = 0x08,
        Underlinethick = 0x09,
        Underlinehairline = 0x0A /* (*) displayed as ordinary underline */
    }

    // Font Weights
    public enum Fw : short
    {
        Dontcare = 0,
        Thin = 100,
        Extralight = 200,
        Light = 300,
        Normal = 400,
        Medium = 500,
        Semibold = 600,
        Bold = 700,
        Extrabold = 800,
        Heavy = 900,

        Ultralight = Extralight,
        Regular = Normal,
        Demibold = Semibold,
        Ultrabold = Extrabold,
        Black = Heavy
    }

    // PARAFORMAT alignment options
    public enum Pfa : ushort
    {
        Left = 0x0001,
        Right = 0x0002,
        Center = 0x0003
    }

    // PARAFORMAT mask values
    [Flags]
    public enum Pfm : uint
    {
        // PARAFORMAT mask values
        Startindent = 0x00000001,
        Rightindent = 0x00000002,
        Offset = 0x00000004,
        Alignment = 0x00000008,
        Tabstops = 0x00000010,
        Numbering = 0x00000020,
        Offsetindent = 0x80000000,

        // PARAFORMAT 2.0 masks and effects
        Spacebefore = 0x00000040,
        Spaceafter = 0x00000080,
        Linespacing = 0x00000100,
        Style = 0x00000400,
        Border = 0x00000800, // (*)
        Shading = 0x00001000, // (*)
        Numberingstyle = 0x00002000, // RE 3.0
        Numberingtab = 0x00004000, // RE 3.0
        Numberingstart = 0x00008000, // RE 3.0

        Rtlpara = 0x00010000,
        Keep = 0x00020000, // (*)
        Keepnext = 0x00040000, // (*)
        Pagebreakbefore = 0x00080000, // (*)
        Nolinenumber = 0x00100000, // (*)
        Nowidowcontrol = 0x00200000, // (*)
        Donothyphen = 0x00400000, // (*)
        Sidebyside = 0x00800000, // (*)
        Table = 0x40000000, // RE 3.0
        Textwrappingbreak = 0x20000000, // RE 3.0
        Tablerowdelimiter = 0x10000000, // RE 4.0

        // The following three properties are read only
        Collapsed = 0x01000000, // RE 3.0
        Outlinelevel = 0x02000000, // RE 3.0
        Box = 0x04000000, // RE 3.0
        Reserved2 = 0x08000000 // RE 4.0
    }

    // PARAFORMAT numbering options
    public enum Pfn : ushort
    {
        Bullet = 0x0001
    }

    private const string _linkSeparator = "|||";

    /// <summary>
    ///     Maintains performance while updating.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         It is recommended to call this method before doing
    ///         any major updates that you do not wish the user to
    ///         see. Remember to call EndUpdate when you are finished
    ///         with the update. Nested calls are supported.
    ///     </para>
    ///     <para>
    ///         Calling this method will prevent redrawing. It will
    ///         also setup the event mask of the underlying richedit
    ///         control so that no events are sent.
    ///     </para>
    /// </remarks>
    private static IntPtr BeginUpdate(HandleRef handleRef)
    {
        // Prevent the control from raising any events.
        var oldEventMask = NativeMethods.SendMessage(handleRef,
            NativeMethods.EmSeteventmask, IntPtr.Zero, IntPtr.Zero);

        // Prevent the control from redrawing itself.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.WmSetredraw, IntPtr.Zero, IntPtr.Zero);

        return oldEventMask;
    }

    public static IntPtr BeginUpdate(this RichTextBox rtb)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        return BeginUpdate(handleRef);
    }

    /// <summary>
    ///     Resumes drawing and event handling.
    /// </summary>
    /// <remarks>
    ///     This method should be called every time a call is made
    ///     made to BeginUpdate. It resets the event mask to it's
    ///     original value and enables redrawing of the control.
    /// </remarks>
    private static void EndUpdate(HandleRef handleRef, IntPtr oldEventMask)
    {
        // Allow the control to redraw itself.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.WmSetredraw, 1, IntPtr.Zero);

        // Allow the control to raise event messages.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmSeteventmask, IntPtr.Zero, oldEventMask);
    }

    public static void EndUpdate(this RichTextBox rtb, IntPtr oldEventMask)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        EndUpdate(handleRef, oldEventMask);
    }

    public static void SetSuperScript(this RichTextBox rtb, bool bSet)
    {
        rtb.SetCharFormat(Cfm.Superscript, bSet ? Cfe.Superscript : 0);
    }

    public static void SetSubScript(this RichTextBox rtb, bool bSet)
    {
        rtb.SetCharFormat(Cfm.Subscript, bSet ? Cfe.Subscript : 0);
    }

    public static void SetLink(this RichTextBox rtb, bool bSet)
    {
        rtb.SetCharFormat(Cfm.Link, bSet ? Cfe.Link : 0);
    }

    public static bool IsSuperScript(this RichTextBox rtb)
    {
        var cf = rtb.GetCharFormat();
        return (cf.dwEffects & Cfe.Superscript) == Cfe.Superscript;
    }

    public static bool IsSubScript(this RichTextBox rtb)
    {
        var cf = rtb.GetCharFormat();
        return (cf.dwEffects & Cfe.Subscript) == Cfe.Subscript;
    }

    public static bool IsLink(this RichTextBox rtb)
    {
        var cf = rtb.GetCharFormat();
        return (cf.dwEffects & Cfe.Link) == Cfe.Link;
    }

    private static void RtbSetSelectedRtf(RichTextBox rtb, string str)
    {
        // Work around bug in DotNet.
        // Basically it assumes that incoming text is always in the default
        // encoding so giving it a unicode string throws an exception.
        // (Bug #5005)
        try
        {
            rtb.SelectedRtf = str;
        }
        catch (ArgumentException)
        {
            // NOTE: This will break any text which actually contains UTF-8 characters!
            if (str.StartsWith(@"{\urtf"))
            {
                // Convert "urtf" -> "rtf"
                str = str.Remove(2, 1);

                // Encode unicode characters
                StringBuilder sb = new();
                foreach (var c in str)
                {
                    if (c < 0x7f)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append(@"\u").Append(Convert.ToUInt32(c)).Append('?');
                    }
                }

                rtb.SelectedRtf = sb.ToString();
            }
        }
    }

    private static Paraformat GetParaFormat(HandleRef handleRef)
    {
        Paraformat pf = new();
        pf.cbSize = Marshal.SizeOf(pf);

        // Get the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmGetparaformat,
            NativeMethods.ScfSelection, ref pf);

        return pf;
    }

    public static Paraformat GetParaFormat(this RichTextBox rtb)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        return GetParaFormat(handleRef);
    }

    private static void SetParaFormat(HandleRef handleRef, Paraformat value)
    {
        DebugHelpers.Assert(value.cbSize == Marshal.SizeOf(value), "value.cbSize == Marshal.SizeOf(value)");

        // Set the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmSetparaformat,
            NativeMethods.ScfSelection, ref value);
    }

    public static void SetParaFormat(this RichTextBox rtb, Paraformat value)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        SetParaFormat(handleRef, value);
    }

    private static Paraformat GetDefaultParaFormat(HandleRef handleRef)
    {
        Paraformat pf = new();
        pf.cbSize = Marshal.SizeOf(pf);

        // Get the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmGetparaformat,
            NativeMethods.ScfAll, ref pf);

        return pf;
    }

    public static Paraformat GetDefaultParaFormat(this RichTextBox rtb)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        return GetDefaultParaFormat(handleRef);
    }

    private static void SetDefaultParaFormat(HandleRef handleRef, Paraformat value)
    {
        DebugHelpers.Assert(value.cbSize == Marshal.SizeOf(value), "value.cbSize == Marshal.SizeOf(value)");

        // Set the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmSetparaformat,
            NativeMethods.ScfAll, ref value);
    }

    public static void SetDefaultParaFormat(this RichTextBox rtb, Paraformat value)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        SetDefaultParaFormat(handleRef, value);
    }

    private static Charformat GetCharFormat(HandleRef handleRef)
    {
        Charformat cf = new();
        cf.cbSize = Marshal.SizeOf(cf);

        // Get the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmGetcharformat,
            NativeMethods.ScfSelection, ref cf);

        return cf;
    }

    public static Charformat GetCharFormat(this RichTextBox rtb)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        return GetCharFormat(handleRef);
    }

    private static void SetCharFormat(HandleRef handleRef, Charformat value)
    {
        DebugHelpers.Assert(value.cbSize == Marshal.SizeOf(value), "value.cbSize == Marshal.SizeOf(value)");

        // Set the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmSetcharformat,
            NativeMethods.ScfSelection, ref value);
    }

    public static void SetCharFormat(this RichTextBox rtb, Charformat value)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        SetCharFormat(handleRef, value);
    }

    public static void SetCharFormat(this RichTextBox rtb, Cfm mask, Cfe effects)
    {
        Charformat cf = new(mask, effects);
        rtb.SetCharFormat(cf);
    }

    private static Charformat GetDefaultCharFormat(HandleRef handleRef)
    {
        Charformat cf = new();
        cf.cbSize = Marshal.SizeOf(cf);

        // Get the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmGetcharformat,
            NativeMethods.ScfAll, ref cf);

        return cf;
    }

    public static Charformat GetDefaultCharFormat(this RichTextBox rtb)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        return GetDefaultCharFormat(handleRef);
    }

    private static void SetDefaultCharFormat(HandleRef handleRef, Charformat value)
    {
        DebugHelpers.Assert(value.cbSize == Marshal.SizeOf(value), "value.cbSize == Marshal.SizeOf(value)");

        // Set the alignment.
        NativeMethods.SendMessage(handleRef,
            NativeMethods.EmSetcharformat,
            NativeMethods.ScfAll, ref value);
    }

    public static void SetDefaultCharFormat(this RichTextBox rtb, Charformat value)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        SetDefaultCharFormat(handleRef, value);
    }

    public static void SetDefaultCharFormat(this RichTextBox rtb, Cfm mask, Cfe effects)
    {
        Charformat cf = new(mask, effects);
        rtb.SetDefaultCharFormat(cf);
    }

    private static Point GetScrollPoint(HandleRef handleRef)
    {
        Point scrollPoint = new();
        NativeMethods.SendMessage(handleRef, NativeMethods.EmGetscrollpos, IntPtr.Zero, ref scrollPoint);
        return scrollPoint;
    }

    public static Point GetScrollPoint(this RichTextBox rtb)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        return GetScrollPoint(handleRef);
    }

    private static void SetScrollPoint(HandleRef handleRef, Point scrollPoint)
    {
        NativeMethods.SendMessage(handleRef, NativeMethods.EmSetscrollpos, IntPtr.Zero, ref scrollPoint);
    }

    public static void SetScrollPoint(this RichTextBox rtb, Point scrollPoint)
    {
        HandleRef handleRef = new(rtb, rtb.Handle);
        SetScrollPoint(handleRef, scrollPoint);
    }

    public static string GetUrl(this LinkClickedEventArgs e)
    {
        var v = e.LinkText!.Split(Delimiters.Hash, 2);
        return v.Length switch
        {
            0 => "",
            1 => v[0],
            _ => v[1]
        };
    }

    public static void GetLinkText(this LinkClickedEventArgs e, out string url, out string text)
    {
        var v = e.LinkText!.Split(Delimiters.Hash, 2);
        if (v.Length == 0)
        {
            url = "";
            text = "";
            return;
        }

        text = v[0];

        url = v.Length == 1 ? v[0] : v[1];
    }

    public static string GetXhtmlText(this RichTextBox rtb, bool bParaFormat)
    {
        StringBuilder strHtml = new();

        rtb.HideSelection = true;
        var oldMask = rtb.BeginUpdate();

        var nStart = rtb.SelectionStart;
        var nEnd = rtb.SelectionLength;

        try
        {
            // to store formatting
            List<KeyValuePair<int, string>> colFormat = [];
            var strT = ProcessTags(rtb, colFormat, bParaFormat);

            // apply format by replacing and inserting HTML tags
            // stored in the Format Array
            var nAcum = 0;
            for (var i = 0; i < colFormat.Count; i++)
            {
                (var pos, var markup) = colFormat[i];
                strHtml.Append(WebUtility.HtmlEncode(strT[nAcum..pos])).Append(markup);
                nAcum = pos;
            }

            if (nAcum < strT.Length)
            {
                strHtml.Append(strT.AsSpan(nAcum));
            }
        }
        catch (Exception /*ex*/)
        {
        }
        finally
        {
            // finish, restore
            rtb.SelectionStart = nStart;
            rtb.SelectionLength = nEnd;

            rtb.EndUpdate(oldMask);
            rtb.HideSelection = false;
        }

        return strHtml.ToString();
    }

    private static string ProcessTags(RichTextBox rtb, List<KeyValuePair<int, string>> colFormat, bool bParaFormat)
    {
        StringBuilder sbT = new();

        var bold = CtformatStates.NctNone;
        var bitalic = CtformatStates.NctNone;
        var bstrikeout = CtformatStates.NctNone;
        var bunderline = CtformatStates.NctNone;
        var super = CtformatStates.NctNone;
        var sub = CtformatStates.NctNone;

        var bacenter = CtformatStates.NctNone;
        var baleft = CtformatStates.NctNone;
        var baright = CtformatStates.NctNone;
        var bnumbering = CtformatStates.NctNone;
        var fontSet = false;
        var strFont = "";
        var crFont = 0;
        var yHeight = 0;

        int i;
        var pos = 0;
        var k = rtb.TextLength;
        char[] chtrim = [' ', '\x0000'];

        //--------------------------------
        // this is an inefficient method to get text format
        // but RichTextBox doesn't provide another method to
        // get something like an array of charformat and paraformat
        //--------------------------------
        for (i = 0; i < k; i++)
        {
            // select one character
            rtb.Select(i, 1);
            var strChar = rtb.SelectedText;

            // get format for this character
            var cf = rtb.GetCharFormat();
            var pf = rtb.GetParaFormat();

            var strfname = cf.szFaceName;
            strfname = strfname.Trim(chtrim);

            // new font format ?
            if (strFont != strfname || crFont != cf.crTextColor || yHeight != cf.yHeight)
            {
                KeyValuePair<int, string> mfr;
                if (strFont != "")
                {
                    // close previous <font> tag
                    mfr = new KeyValuePair<int, string>(pos, "</font>");
                    colFormat.Add(mfr);
                }

                // save this for cache
                strFont = strfname;
                crFont = cf.crTextColor;
                yHeight = cf.yHeight;

                fontSet = strFont != "";

                // font size should be translate to
                // html size (Approximately)
                var fsize = yHeight / (20 * 5);

                // color object from COLORREF
                var color = GetColor(crFont);

                // add <font> tag
                var strcolor = string.Concat("#", (color.ToArgb() & 0x00FFFFFF).ToString("X6"));

                mfr = new KeyValuePair<int, string>(pos,
                    "<font face=\"" + strFont + "\" color=\"" + strcolor + "\" size=\"" + fsize + "\">");
                colFormat.Add(mfr);
            }

            // are we in another line ?
            if (strChar == "\r" || strChar == "\n")
            {
                // yes?
                // then, we need to reset paragraph format
                // and character format
                if (bParaFormat)
                {
                    bnumbering = CtformatStates.NctNone;
                    baleft = CtformatStates.NctNone;
                    baright = CtformatStates.NctNone;
                    bacenter = CtformatStates.NctNone;
                }

                // close previous tags

                // is italic? => close it
                if (bitalic != CtformatStates.NctNone)
                {
                    KeyValuePair<int, string> mfr = new(pos, "</i>");
                    colFormat.Add(mfr);
                    bitalic = CtformatStates.NctNone;
                }

                // is bold? => close it
                if (bold != CtformatStates.NctNone)
                {
                    KeyValuePair<int, string> mfr = new(pos, "</b>");
                    colFormat.Add(mfr);
                    bold = CtformatStates.NctNone;
                }

                // is underline? => close it
                if (bunderline != CtformatStates.NctNone)
                {
                    KeyValuePair<int, string> mfr = new(pos, "</u>");
                    colFormat.Add(mfr);
                    bunderline = CtformatStates.NctNone;
                }

                // is strikeout? => close it
                if (bstrikeout != CtformatStates.NctNone)
                {
                    KeyValuePair<int, string> mfr = new(pos, "</s>");
                    colFormat.Add(mfr);
                    bstrikeout = CtformatStates.NctNone;
                }

                // is super? => close it
                if (super != CtformatStates.NctNone)
                {
                    KeyValuePair<int, string> mfr = new(pos, "</sup>");
                    colFormat.Add(mfr);
                    super = CtformatStates.NctNone;
                }

                // is sub? => close it
                if (sub != CtformatStates.NctNone)
                {
                    KeyValuePair<int, string> mfr = new(pos, "</sub>");
                    colFormat.Add(mfr);
                    sub = CtformatStates.NctNone;
                }
            }

            // now, process the paragraph format,
            // managing states: none, new, continue {with previous}, reset
            if (bParaFormat)
            {
                // align to center?
                UpdateState(pf.wAlignment == Pfa.Center, ref bacenter);

                if (bacenter == CtformatStates.NctNew)
                {
                    KeyValuePair<int, string> mfr = new(pos, "<p align=\"center\">");
                    colFormat.Add(mfr);
                }
                else if (bacenter == CtformatStates.NctReset)
                {
                    bacenter = CtformatStates.NctNone;
                }

                // align to left
                UpdateState(pf.wAlignment == Pfa.Left, ref baleft);

                if (baleft == CtformatStates.NctNew)
                {
                    KeyValuePair<int, string> mfr = new(pos, "<p align=\"left\">");
                    colFormat.Add(mfr);
                }
                else if (baleft == CtformatStates.NctReset)
                {
                    baleft = CtformatStates.NctNone;
                }

                // align to right
                UpdateState(pf.wAlignment == Pfa.Right, ref baright);

                if (baright == CtformatStates.NctNew)
                {
                    KeyValuePair<int, string> mfr = new(pos, "<p align=\"right\">");
                    colFormat.Add(mfr);
                }
                else if (baright == CtformatStates.NctReset)
                {
                    baright = CtformatStates.NctNone;
                }

                // bullet
                UpdateState(pf.wNumbering == Pfn.Bullet, ref bnumbering);

                if (bnumbering == CtformatStates.NctNew)
                {
                    KeyValuePair<int, string> mfr = new(pos, "<li>");
                    colFormat.Add(mfr);
                }
                else if (bnumbering == CtformatStates.NctReset)
                {
                    bnumbering = CtformatStates.NctNone;
                }
            }

            // bold
            UpdateState((cf.dwEffects & Cfe.Bold) == Cfe.Bold, ref bold);
            AddTag(pos, "b", colFormat, ref bold);

            // Italic
            UpdateState((cf.dwEffects & Cfe.Italic) == Cfe.Italic, ref bitalic);
            AddTag(pos, "i", colFormat, ref bitalic);

            // strikeout
            UpdateState((cf.dwEffects & Cfe.Strikeout) == Cfe.Strikeout, ref bstrikeout);
            AddTag(pos, "s", colFormat, ref bstrikeout);

            // underline
            UpdateState((cf.dwEffects & Cfe.Underline) == Cfe.Underline, ref bunderline);
            AddTag(pos, "u", colFormat, ref bunderline);

            // superscript
            UpdateState((cf.dwEffects & Cfe.Superscript) == Cfe.Superscript, ref super);
            AddTag(pos, "sup", colFormat, ref super);

            // subscript
            UpdateState((cf.dwEffects & Cfe.Subscript) == Cfe.Subscript, ref sub);
            AddTag(pos, "sub", colFormat, ref sub);

            sbT.Append(strChar);
            pos = sbT.Length;
        }

        // close pending tags
        if (bold != CtformatStates.NctNone)
        {
            KeyValuePair<int, string> mfr = new(pos, "</b>");
            colFormat.Add(mfr);
        }

        if (bitalic != CtformatStates.NctNone)
        {
            KeyValuePair<int, string> mfr = new(pos, "</i>");
            colFormat.Add(mfr);
        }

        if (bstrikeout != CtformatStates.NctNone)
        {
            KeyValuePair<int, string> mfr = new(pos, "</s>");
            colFormat.Add(mfr);
        }

        if (bunderline != CtformatStates.NctNone)
        {
            KeyValuePair<int, string> mfr = new(pos, "</u>");
            colFormat.Add(mfr);
        }

        if (super != CtformatStates.NctNone)
        {
            KeyValuePair<int, string> mfr = new(pos, "</sup>");
            colFormat.Add(mfr);
        }

        if (sub != CtformatStates.NctNone)
        {
            KeyValuePair<int, string> mfr = new(pos, "</sub>");
            colFormat.Add(mfr);
        }

        if (fontSet)
        {
            // close pending font format
            KeyValuePair<int, string> mfr = new(pos, "</font>");
            colFormat.Add(mfr);
        }

        // now, reorder the formatting array
        k = colFormat.Count;
        for (i = 0; i < k - 1; i++)
        {
            for (var j = i + 1; j < k; j++)
            {
                var mfr = colFormat[i];
                var mfr2 = colFormat[j];

                if (mfr2.Key < mfr.Key)
                {
                    colFormat.RemoveAt(j);
                    colFormat.Insert(i, mfr2);
                    j--;
                }
            }
        }

        return sbT.ToString();
    }

    private static void UpdateState(bool value, ref CtformatStates state)
    {
        if (value)
        {
            state = state == CtformatStates.NctNone
                ? CtformatStates.NctNew
                : CtformatStates.NctContinue;
        }
        else
        {
            if (state != CtformatStates.NctNone)
            {
                state = CtformatStates.NctReset;
            }
        }
    }

    private static void AddTag(int pos, string tag, List<KeyValuePair<int, string>> colFormat, ref CtformatStates state)
    {
        if (state == CtformatStates.NctNew)
        {
            KeyValuePair<int, string> mfr = new(pos, "<" + tag + ">");
            colFormat.Add(mfr);
        }
        else if (state == CtformatStates.NctReset)
        {
            KeyValuePair<int, string> mfr = new(pos, "</" + tag + ">");
            colFormat.Add(mfr);
            state = CtformatStates.NctNone;
        }
    }

    public static string GetPlainText(this RichTextBox rtb)
    {
        return rtb.GetPlainText(0, rtb.TextLength);
    }

    public static string GetSelectionPlainText(this RichTextBox rtb)
    {
        return rtb.GetPlainText(rtb.SelectionStart, rtb.SelectionStart + rtb.SelectionLength);
    }

    public static string GetPlainText(this RichTextBox rtb, int from, int to)
    {
        if (to == 0)
        {
            return string.Empty;
        }

        var oldMask = rtb.BeginUpdate();

        var nStart = rtb.SelectionStart;
        var nEnd = rtb.SelectionLength;
        StringBuilder text = new();

        try
        {
            //--------------------------------
            // This is an inefficient method to get text format.
            // But RichTextBox doesn't provide another method to
            // get something like an array of charformat and paraformat.
            //--------------------------------
            for (var i = from; i < to; i++)
            {
                // Try to select one visible character (RichTextBox returns a string, can be empty, may contain more than what is visible)
                rtb.Select(i, 1);
                var selectedText = rtb.SelectedText;

                // For every character of a link, the current RichTextBox returns the entire link (but should be the empty string). So, skip the links.
                if (!selectedText.StartsWith(_linkSeparator))
                {
                    text.Append(selectedText);
                }
            }
        }
        catch (Exception /*ex*/)
        {
        }
        finally
        {
            // finish, restore
            rtb.SelectionStart = nStart;
            rtb.SelectionLength = nEnd;

            rtb.EndUpdate(oldMask);
            rtb.Invalidate();
        }

        return text.ToString();
    }

    public static string? GetLink(this RichTextBox rtb, int charIndex)
    {
        var text = rtb.Text;
        if (charIndex < 0 || text.Length <= charIndex)
        {
            return null;
        }

        var oldMask = rtb.BeginUpdate();

        var nStart = rtb.SelectionStart;
        var nEnd = rtb.SelectionLength;

        try
        {
            //--------------------------------
            // this is an inefficient method to get text format
            // but RichTextBox doesn't provide another method to
            // get something like an array of charformat and paraformat
            //--------------------------------
            rtb.Select(charIndex, 1);
            if (!rtb.IsLink())
            {
                return null;
            }

            var from = charIndex;
            while (from > 0)
            {
                rtb.Select(from - 1, 1);
                if (!rtb.IsLink())
                {
                    break;
                }

                --from;
            }

            var to = charIndex + 1;
            while (to < text.Length)
            {
                rtb.Select(to, 1);
                if (!rtb.IsLink())
                {
                    break;
                }

                ++to;
            }

            if (to < text.Length && DoesMatchLinkSeparatorPattern(text, to))
            {
                from = to + _linkSeparator.Length;
                while (to < text.Length && !char.IsWhiteSpace(text[to]) && text[to] != ',')
                {
                    ++to;
                }
            }

            // prior to net47 links were created via hidden text, and had the following format: "text#link"
            // extract the link portion only
            var linkOldFormat = text.AsSpan(from, to - from);
            return linkOldFormat[(linkOldFormat.IndexOf(_linkSeparator) + _linkSeparator.Length)..].ToString();
        }
        catch
        {
            return null;
        }
        finally
        {
            // finish, restore
            rtb.SelectionStart = nStart;
            rtb.SelectionLength = nEnd;

            rtb.EndUpdate(oldMask);
            rtb.Invalidate();
        }
    }

    private static bool DoesMatchLinkSeparatorPattern(string text, int index)
    {
        for (var ls = 0; ls < _linkSeparator.Length && index < text.Length; ls++, index++)
        {
            if (text[index] != _linkSeparator[ls])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Returns input text with characters disallowed by XML spec (e.g. most control codes in 0x00-0x20 range)
    ///     replaced with equivalent character references or question marks if there is an unrecoverable error.
    ///     Although they are disallowed even when escaped, this step seems necessary to make them acceptable
    ///     by XmlReader with CheckCharacters disabled.
    /// </summary>
    private static string EscapeNonXmlChars(string input)
    {
        StringBuilder result = new();
        foreach (var ch in input)
        {
            if (XmlConvert.IsXmlChar(ch))
            {
                result.Append(ch);
            }
            else
            {
                try
                {
                    result.Append("&#").Append((int)ch).Append(';');
                }
                catch (ArgumentException)
                {
                    result.Append('?');
                }
            }
        }

        return result.ToString();
    }

    public static void SetXhtmlText(this RichTextBox rtb, string xhtmlText)
    {
        rtb.DetectUrls = false;

        rtb.Clear();
        RtfCurrentState cs = new();

        HandleRef handleRef = new(rtb, rtb.Handle);
        cs.Cf = GetDefaultCharFormat(handleRef); // to apply character formatting
        cs.Pf = GetDefaultParaFormat(handleRef); // to apply paragraph formatting

        var oldMask = BeginUpdate(handleRef);

        XmlReaderSettings settings = new() { ConformanceLevel = ConformanceLevel.Fragment, CheckCharacters = false };

        try
        {
            using StringReader stringreader = new(EscapeNonXmlChars(xhtmlText));
            var reader = XmlReader.Create(stringreader, settings);
            while (reader.Read())
            {
                ProcessNode(rtb, handleRef, reader, cs);
            }
        }
        catch (XmlException ex)
        {
            Debug.WriteLine(ex.Message);
        }

        // apply links style
        Charformat ncf = new(Cfm.Link, Cfe.Link);
        ncf.cbSize = Marshal.SizeOf(ncf);
        foreach ((var start, var length) in cs.Links)
        {
            rtb.Select(start, length);
            SetCharFormat(handleRef, ncf);
        }

        // reposition to first
        rtb.Select(0, 0);
        EndUpdate(handleRef, oldMask);
        rtb.Invalidate();
    }

    private static void ProcessNode(RichTextBox rtb, HandleRef handleRef, XmlReader reader, RtfCurrentState cs)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                ProcessElement(reader, cs, rtb);
                break;
            case XmlNodeType.EndElement:
                ProcessEndElement(reader, cs, rtb);
                break;
            case XmlNodeType.Text:
                var strData = reader.Value;
                var bNewParagraph = strData.IndexOf("\r\n", 0) >= 0 || strData.IndexOf('\n', 0) >= 0;

                if (strData.Length > 0)
                {
                    // now, add text to control
                    var nStartCache = rtb.SelectionStart;
                    rtb.SelectedText = strData;
                    rtb.Select(nStartCache, strData.Length);

                    // apply format
                    if (cs.ParaFormatChanged)
                    {
                        SetParaFormat(handleRef, cs.Pf);
                    }

                    if (cs.CharFormatChanged)
                    {
                        SetCharFormat(handleRef, cs.Cf);
                    }

                    cs.CharFormatChanged = false;
                    cs.ParaFormatChanged = false;

                    // reposition to final
                    rtb.Select(rtb.TextLength + 1, 0);

                    // new paragraph requires to reset alignment
                    if (bNewParagraph)
                    {
                        cs.Pf.dwMask = Pfm.Alignment | Pfm.Numbering;
                        cs.Pf.wAlignment = Pfa.Left;
                        cs.Pf.wNumbering = 0;
                        cs.ParaFormatChanged = true;
                    }
                }

                break;
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                rtb.SelectedText = reader.Value;
                break;
            case XmlNodeType.XmlDeclaration:
            case XmlNodeType.ProcessingInstruction:
                break;
            case XmlNodeType.Comment:
                break;
        }
    }

    private static void ProcessElement(XmlReader reader, RtfCurrentState cs, RichTextBox rtb)
    {
        switch (reader.Name.ToLower())
        {
            case "b":
                cs.Cf.dwMask |= Cfm.Weight | Cfm.Bold;
                cs.Cf.dwEffects |= Cfe.Bold;
                cs.Cf.wWeight = Fw.Bold;
                cs.CharFormatChanged = true;
                break;
            case "i":
                cs.Cf.dwMask |= Cfm.Italic;
                cs.Cf.dwEffects |= Cfe.Italic;
                cs.CharFormatChanged = true;
                break;
            case "u":
                cs.Cf.dwMask |= Cfm.Underline | Cfm.Underlinetype;
                cs.Cf.dwEffects |= Cfe.Underline;
                cs.Cf.bUnderlineType = Cfu.Underline;
                cs.CharFormatChanged = true;
                break;
            case "s":
                cs.Cf.dwMask |= Cfm.Strikeout;
                cs.Cf.dwEffects |= Cfe.Strikeout;
                cs.CharFormatChanged = true;
                break;
            case "sup":
                cs.Cf.dwMask |= Cfm.Superscript;
                cs.Cf.dwEffects |= Cfe.Superscript;
                cs.CharFormatChanged = true;
                break;
            case "sub":
                cs.Cf.dwMask |= Cfm.Subscript;
                cs.Cf.dwEffects |= Cfe.Subscript;
                cs.CharFormatChanged = true;
                break;
            case "a":
                cs.HyperlinkStart = rtb.TextLength;
                cs.Hyperlink = null;
                while (reader.MoveToNextAttribute())
                {
                    if (reader.Name.ToLower() == "href")
                    {
                        cs.Hyperlink = reader.Value;
                    }
                }

                reader.MoveToElement();
                break;
            case "p":
                cs.Spf.Push(cs.Pf);
                while (reader.MoveToNextAttribute())
                {
                    if (reader.Name.ToLower() == "align")
                    {
                        if (reader.Value == "left")
                        {
                            cs.Pf.dwMask |= Pfm.Alignment;
                            cs.Pf.wAlignment = Pfa.Left;
                            cs.ParaFormatChanged = true;
                        }
                        else if (reader.Value == "right")
                        {
                            cs.Pf.dwMask |= Pfm.Alignment;
                            cs.Pf.wAlignment = Pfa.Right;
                            cs.ParaFormatChanged = true;
                        }
                        else if (reader.Value == "center")
                        {
                            cs.Pf.dwMask |= Pfm.Alignment;
                            cs.Pf.wAlignment = Pfa.Center;
                            cs.ParaFormatChanged = true;
                        }
                    }
                }

                reader.MoveToElement();
                break;
            case "li":
                cs.Spf.Push(cs.Pf);
                if (cs.Pf.wNumbering != Pfn.Bullet)
                {
                    cs.Pf.dwMask |= Pfm.Numbering;
                    cs.Pf.wNumbering = Pfn.Bullet;
                    cs.ParaFormatChanged = true;
                }

                break;
            case "font":
                cs.Scf.Push(cs.Cf);
                var strFont = cs.Cf.szFaceName;
                var crFont = cs.Cf.crTextColor;
                var yHeight = cs.Cf.yHeight;

                while (reader.MoveToNextAttribute())
                {
                    switch (reader.Name.ToLower())
                    {
                        case "face":
                            cs.Cf.dwMask |= Cfm.Face;
                            strFont = reader.Value;
                            break;
                        case "size":
                            cs.Cf.dwMask |= Cfm.Size;
                            yHeight = int.Parse(reader.Value);
                            yHeight *= 20 * 5;
                            break;
                        case "color":
                            cs.Cf.dwMask |= Cfm.Color;
                            var text = reader.Value;
                            if (text.StartsWith('#'))
                            {
                                var strCr = text[1..];
                                var nCr = Convert.ToInt32(strCr, 16);
                                var color = Color.FromArgb(nCr);
                                crFont = GetColorref(color);
                            }
                            else if (!int.TryParse(text, out crFont))
                            {
                                var color = Color.FromName(text);
                                crFont = GetColorref(color);
                            }

                            break;
                    }
                }

                reader.MoveToElement();

                cs.Cf.szFaceName = strFont;
                cs.Cf.crTextColor = crFont;
                cs.Cf.yHeight = yHeight;

                cs.Cf.dwEffects &= ~Cfe.Autocolor;
                cs.CharFormatChanged = true;
                break;
        }
    }

    private static void ProcessEndElement(XmlReader reader, RtfCurrentState cs, RichTextBox rtb)
    {
        switch (reader.Name)
        {
            case "b":
                cs.Cf.dwEffects &= ~Cfe.Bold;
                cs.Cf.wWeight = Fw.Normal;
                cs.CharFormatChanged = true;
                break;
            case "i":
                cs.Cf.dwEffects &= ~Cfe.Italic;
                cs.CharFormatChanged = true;
                break;
            case "u":
                cs.Cf.dwEffects &= ~Cfe.Underline;
                cs.CharFormatChanged = true;
                break;
            case "s":
                cs.Cf.dwEffects &= ~Cfe.Strikeout;
                cs.CharFormatChanged = true;
                break;
            case "sup":
                cs.Cf.dwEffects &= ~Cfe.Superscript;
                cs.CharFormatChanged = true;
                break;
            case "sub":
                cs.Cf.dwEffects &= ~Cfe.Subscript;
                cs.CharFormatChanged = true;
                break;
            case "a":
                var length = rtb.TextLength - cs.HyperlinkStart;

                if (cs.Hyperlink is not null)
                {
                    rtb.Select(cs.HyperlinkStart, length);
                    if (cs.Hyperlink != rtb.SelectedText)
                    {
                        var rtfText = rtb.SelectedRtf;
                        var idx = rtfText.LastIndexOf('}');
                        if (idx != -1)
                        {
                            var head = rtfText[..idx];
                            var tail = rtfText[idx..];
                            RtbSetSelectedRtf(rtb, $@"{head}\v {_linkSeparator}{cs.Hyperlink}\v0{tail}");
                            length = rtb.TextLength - cs.HyperlinkStart;
                        }
                    }

                    // reposition to final
                    rtb.Select(rtb.TextLength + 1, 0);
                }

                cs.Links.Add(new KeyValuePair<int, int>(cs.HyperlinkStart, length));

                cs.HyperlinkStart = -1;
                break;
            case "p":
                cs.Pf = cs.Spf.Pop();
                cs.ParaFormatChanged = true;
                break;
            case "li":
                cs.Pf = cs.Spf.Pop();
                cs.ParaFormatChanged = true;
                break;
            case "font":
                cs.Cf = cs.Scf.Pop();
                cs.CharFormatChanged = true;
                break;
        }
    }

    public static void SetXhtmlTextAsPlainText(this RichTextBox rtb, string xhtmlText)
    {
        rtb.Clear();

        rtb.HideSelection = true;

        XmlReaderSettings settings = new() { ConformanceLevel = ConformanceLevel.Fragment };

        try
        {
            using StringReader strReader = new(xhtmlText);
            var reader = XmlReader.Create(strReader, settings);
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Text:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        rtb.SelectedText = reader.Value;
                        break;
                    case XmlNodeType.Element:
                    case XmlNodeType.EndElement:
                        break;
                    case XmlNodeType.XmlDeclaration:
                    case XmlNodeType.ProcessingInstruction:
                        break;
                    case XmlNodeType.Comment:
                        break;
                }
            }
        }
        catch (XmlException ex)
        {
            Debug.WriteLine(ex.Message);
        }

        rtb.HideSelection = false;

        // reposition to final
        rtb.Select(rtb.TextLength + 1, 0);
    }

    // It makes no difference if we use PARAFORMAT or
    // PARAFORMAT2 here, so I have opted for PARAFORMAT2.
    [StructLayout(LayoutKind.Sequential)]
    public struct Paraformat
    {
        public int cbSize;
        public Pfm dwMask;
        public Pfn wNumbering;
        public short wReserved;
        public int dxStartIndent;
        public int dxRightIndent;
        public int dxOffset;
        public Pfa wAlignment;
        public short cTabCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public int[] rgxTabs;

        // PARAFORMAT2 from here onwards.
        public int dySpaceBefore;
        public int dySpaceAfter;
        public int dyLineSpacing;
        public short sStyle;
        public byte bLineSpacingRule;
        public byte bOutlineLevel;
        public short wShadingWeight;
        public short wShadingStyle;
        public short wNumberingStart;
        public short wNumberingStyle;
        public short wNumberingTab;
        public short wBorderSpace;
        public short wBorderWidth;
        public short wBorders;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct Charformat
    {
        public Charformat(Cfm mask, Cfe effects)
            : this()
        {
            cbSize = Marshal.SizeOf(this);
            dwMask = mask;
            dwEffects = effects;
            szFaceName = "";
        }

        public int cbSize;
        public Cfm dwMask;
        public Cfe dwEffects;
        public int yHeight;
        public int yOffset;
        public int crTextColor;
        public byte bCharSet;
        public byte bPitchAndFamily;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szFaceName;

        // CHARFORMAT2 from here onwards.
        public Fw wWeight;
        public short sSpacing;
        public int crBackColor;
        public uint lcid;
        public uint dwReserved;
        public short sStyle;
        public short wKerning;
        public Cfu bUnderlineType;
        public byte bAnimation;
        public byte bRevAuthor;
        public byte bReserved1;
    }

    #region Win32 Apis

    internal static class NativeMethods
    {
        // Constants from the Platform SDK.
        internal const int WmUser = 0x0400;
        internal const int EmGetcharformat = WmUser + 58;
        internal const int EmSetcharformat = WmUser + 68;
        internal const int EmHideselection = WmUser + 63;
        internal const int EmGetscrollpos = WmUser + 221;
        internal const int EmSetscrollpos = WmUser + 222;

        internal const int EmSeteventmask = 1073;
        internal const int EmGetparaformat = 1085;
        internal const int EmSetparaformat = 1095;
        internal const int WmSetredraw = 11;

        // Defines for EM_SETCHARFORMAT/EM_GETCHARFORMAT
        internal const int ScfSelection = 0x0001;
        internal const int ScfWord = 0x0002;
        internal const int ScfAll = 0x0004;

        internal const int LfFacesize = 32;

        [DllImport("user32", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(HandleRef hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(HandleRef hWnd,
            int msg,
            IntPtr wParam,
            ref Point lParam);

        [DllImport("user32", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(HandleRef hWnd,
            int msg,
            IntPtr wParam,
            ref Paraformat lp);

        [DllImport("user32", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(HandleRef hWnd,
            int msg,
            IntPtr wParam,
            ref Charformat lp);
    }

    #endregion

    // format states
    private enum CtformatStates
    {
        NctNone = 0, // none format applied
        NctNew = 1, // new format
        NctContinue = 2, // continue with previous format
        NctReset = 3 // reset format (close this tag)
    }

    private sealed class RtfCurrentState
    {
        public readonly List<KeyValuePair<int, int>> Links = [];
        public readonly Stack<Charformat> Scf = new();
        public readonly Stack<Paraformat> Spf = new();
        public Charformat Cf;
        public bool CharFormatChanged;
        public string? Hyperlink;
        public int HyperlinkStart = -1;
        public bool ParaFormatChanged;
        public Paraformat Pf;
    }

    #region COLORREF helper functions

    // convert COLORREF to Color
    private static Color GetColor(int crColor)
    {
        var r = (byte)crColor;
        var g = (byte)(crColor >> 8);
        var b = (byte)(crColor >> 16);

        return Color.FromArgb(r, g, b);
    }

    // convert COLORREF to Color
    private static int GetColorref(int r, int g, int b)
    {
        var r2 = r;
        var g2 = g << 8;
        var b2 = b << 16;

        var result = r2 | g2 | b2;

        return result;
    }

    private static int GetColorref(Color color)
    {
        int r = color.R;
        int g = color.G;
        int b = color.B;

        return GetColorref(r, g, b);
    }

    #endregion
}
