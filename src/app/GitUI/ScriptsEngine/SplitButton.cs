using System.ComponentModel;
using System.Windows.Forms.VisualStyles;
using ContentAlignment = System.Drawing.ContentAlignment;

/* Get the latest version of SplitButton at: http://wyday.com/splitbutton/
 *
 * Copyright (c) 2010, wyDay
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 *  * Redistributions of source code must retain the above copyright notice,
 *    this list of conditions and the following disclaimer.
 *  * Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
 * ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace GitUI.ScriptsEngine;

public class SplitButton : Button
{
    private const int _splitSectionWidth = 18;

    private static readonly int _borderSize = SystemInformation.Border3DSize.Width * 2;
    private Rectangle _dropDownRectangle;

    private bool _isSplitMenuVisible;
    private bool _showSplit;
    private bool _skipNextOpen;

    private ContextMenuStrip? _splitMenuStrip;
    private PushButtonState _state;

    private TextFormatFlags _textFormatFlags = TextFormatFlags.Default;

    public SplitButton()
    {
        AutoSize = true;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if (keyData.Equals(Keys.Down) && _showSplit)
        {
            return true;
        }

        return base.IsInputKey(keyData);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        if (!_showSplit)
        {
            base.OnGotFocus(e);
            return;
        }

        if (!State.Equals(PushButtonState.Pressed) && !State.Equals(PushButtonState.Disabled))
        {
            State = PushButtonState.Default;
        }
    }

    protected override void OnKeyDown(KeyEventArgs kevent)
    {
        if (_showSplit)
        {
            if (kevent.KeyCode.Equals(Keys.Down) && !_isSplitMenuVisible)
            {
                ShowContextMenuStrip();
            }
            else if (kevent.KeyCode.Equals(Keys.Space) && kevent.Modifiers == Keys.None)
            {
                State = PushButtonState.Pressed;
            }
        }

        base.OnKeyDown(kevent);
    }

    protected override void OnKeyUp(KeyEventArgs kevent)
    {
        if (kevent.KeyCode.Equals(Keys.Space))
        {
            if (MouseButtons == MouseButtons.None)
            {
                State = PushButtonState.Normal;
            }
        }
        else if (kevent.KeyCode.Equals(Keys.Apps))
        {
            if (MouseButtons == MouseButtons.None && !_isSplitMenuVisible)
            {
                ShowContextMenuStrip();
            }
        }

        base.OnKeyUp(kevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        State = Enabled ? PushButtonState.Normal : PushButtonState.Disabled;

        base.OnEnabledChanged(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        if (!_showSplit)
        {
            base.OnLostFocus(e);
            return;
        }

        if (!State.Equals(PushButtonState.Pressed) && !State.Equals(PushButtonState.Disabled))
        {
            State = PushButtonState.Normal;
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        if (!_showSplit)
        {
            base.OnMouseEnter(e);
            return;
        }

        if (!State.Equals(PushButtonState.Pressed) && !State.Equals(PushButtonState.Disabled))
        {
            State = PushButtonState.Hot;
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (!_showSplit)
        {
            base.OnMouseLeave(e);
            return;
        }

        if (!State.Equals(PushButtonState.Pressed) && !State.Equals(PushButtonState.Disabled))
        {
            State = Focused ? PushButtonState.Default : PushButtonState.Normal;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!_showSplit)
        {
            base.OnMouseDown(e);
            return;
        }

        if ((_dropDownRectangle.Contains(e.Location) || WholeButtonDropdown) &&
            !_isSplitMenuVisible && e.Button == MouseButtons.Left)
        {
            ShowContextMenuStrip();
        }
        else
        {
            State = PushButtonState.Pressed;
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        if (!_showSplit)
        {
            base.OnMouseUp(mevent);
            return;
        }

        // if the right button was released inside the button
        if (mevent.Button == MouseButtons.Right && ClientRectangle.Contains(mevent.Location) && !_isSplitMenuVisible)
        {
            ShowContextMenuStrip();
        }
        else if (_splitMenuStrip is null || !_isSplitMenuVisible)
        {
            SetButtonDrawState();

            if (ClientRectangle.Contains(mevent.Location) && !_dropDownRectangle.Contains(mevent.Location))
            {
                OnClick(EventArgs.Empty);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);

        if (!_showSplit)
        {
            return;
        }

        var g = pevent.Graphics;
        var bounds = ClientRectangle;
        pevent.Graphics.Clear(SystemColors.Window);

        // draw the button background as according to the current state.
        if (State != PushButtonState.Pressed && IsDefault && !Application.RenderWithVisualStyles)
        {
            var backgroundBounds = bounds;
            backgroundBounds.Inflate(-1, -1);

            // button renderer doesn't draw the black frame when themes are off
            g.DrawRectangle(SystemPens.WindowFrame, 0, 0, bounds.Width - 1, bounds.Height - 1);
        }

        // calculate the current dropdown rectangle.
        _dropDownRectangle = new Rectangle(bounds.Right - _splitSectionWidth, 0, _splitSectionWidth, bounds.Height);

        var internalBorder = _borderSize;
        Rectangle focusRect =
            new(internalBorder - 1,
                internalBorder - 1,
                bounds.Width - _dropDownRectangle.Width - internalBorder,
                bounds.Height - (internalBorder * 2) + 2);

        var drawSplitLine = State is PushButtonState.Hot or PushButtonState.Pressed ||
                            !Application.RenderWithVisualStyles;

        if (RightToLeft == RightToLeft.Yes)
        {
            _dropDownRectangle.X = bounds.Left + 1;
            focusRect.X = _dropDownRectangle.Right;

            if (drawSplitLine)
            {
                // draw two lines at the edge of the dropdown button
                g.DrawLine(SystemPens.ButtonShadow, bounds.Left + _splitSectionWidth, _borderSize,
                    bounds.Left + _splitSectionWidth, bounds.Bottom - _borderSize);
                g.DrawLine(SystemPens.ButtonFace, bounds.Left + _splitSectionWidth + 1, _borderSize,
                    bounds.Left + _splitSectionWidth + 1, bounds.Bottom - _borderSize);
            }
        }
        else
        {
            if (drawSplitLine)
            {
                // draw two lines at the edge of the dropdown button
                g.DrawLine(SystemPens.ButtonShadow, bounds.Right - _splitSectionWidth, _borderSize,
                    bounds.Right - _splitSectionWidth, bounds.Bottom - _borderSize);
                g.DrawLine(SystemPens.ButtonFace, bounds.Right - _splitSectionWidth - 1, _borderSize,
                    bounds.Right - _splitSectionWidth - 1, bounds.Bottom - _borderSize);
            }
        }

        // Draw an arrow in the correct location
        PaintArrow(g, _dropDownRectangle);

        // paint the image and text in the "button" part of the splitButton
        PaintTextandImage(g, new Rectangle(0, 0, ClientRectangle.Width - _splitSectionWidth, ClientRectangle.Height));

        // draw the focus rectangle.
        if (State != PushButtonState.Pressed && Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(g, focusRect);
        }
    }

    private void PaintTextandImage(Graphics g, Rectangle bounds)
    {
        // Figure out where our text and image should go

        CalculateButtonTextAndImageLayout(ref bounds, out var textRectangle, out var imageRectangle);

        // draw the image
        if (Image is not null)
        {
            if (Enabled)
            {
                g.DrawImage(Image, imageRectangle.X, imageRectangle.Y, Image.Width, Image.Height);
            }
            else
            {
                ControlPaint.DrawImageDisabled(g, Image, imageRectangle.X, imageRectangle.Y, BackColor);
            }
        }

        // If we don't use mnemonic, set formatFlag to NoPrefix as this will show ampersand.
        if (!UseMnemonic)
        {
            _textFormatFlags |= TextFormatFlags.NoPrefix;
        }
        else if (!ShowKeyboardCues)
        {
            _textFormatFlags |= TextFormatFlags.HidePrefix;
        }

        // draw the text
        if (!string.IsNullOrEmpty(Text))
        {
            if (Enabled)
            {
                TextRenderer.DrawText(g, Text, Font, textRectangle, ForeColor, _textFormatFlags);
            }
            else
            {
                ControlPaint.DrawStringDisabled(g, Text, Font, BackColor, textRectangle, _textFormatFlags);
            }
        }
    }

    private void PaintArrow(Graphics g, Rectangle dropDownRect)
    {
        Point middle = new(Convert.ToInt32(dropDownRect.Left + (dropDownRect.Width / 2)),
            Convert.ToInt32(dropDownRect.Top + (dropDownRect.Height / 2)));

        // if the width is odd - favor pushing it over one pixel right.
        middle.X += dropDownRect.Width % 2;

        Point[] arrow = [new(middle.X - 2, middle.Y - 1), new(middle.X + 3, middle.Y - 1), new(middle.X, middle.Y + 2)];

        var brush = Enabled
            ? SystemBrushes.ControlText
            : SystemBrushes.ButtonShadow;

        g.FillPolygon(brush, arrow);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var preferredSize = base.GetPreferredSize(proposedSize);

        // autosize correctly for splitbuttons
        if (_showSplit)
        {
            if (AutoSize)
            {
                return CalculateButtonAutoSize();
            }

            if (!string.IsNullOrEmpty(Text) &&
                TextRenderer.MeasureText(Text, Font).Width + _splitSectionWidth > preferredSize.Width)
            {
                return preferredSize + new Size(_splitSectionWidth + (_borderSize * 2), 0);
            }
        }

        return preferredSize;
    }

    private Size CalculateButtonAutoSize()
    {
        var retSize = Size.Empty;
        var textSize = TextRenderer.MeasureText(Text, Font);
        var imageSize = Image?.Size ?? Size.Empty;

        // Pad the text size
        if (Text.Length != 0)
        {
            textSize.Height += 4;
            textSize.Width += 4;
        }

        switch (TextImageRelation)
        {
            case TextImageRelation.Overlay:
                retSize.Height = Math.Max(Text.Length == 0 ? 0 : textSize.Height, imageSize.Height);
                retSize.Width = Math.Max(textSize.Width, imageSize.Width);
                break;
            case TextImageRelation.ImageAboveText:
            case TextImageRelation.TextAboveImage:
                retSize.Height = textSize.Height + imageSize.Height;
                retSize.Width = Math.Max(textSize.Width, imageSize.Width);
                break;
            case TextImageRelation.ImageBeforeText:
            case TextImageRelation.TextBeforeImage:
                retSize.Height = Math.Max(textSize.Height, imageSize.Height);
                retSize.Width = textSize.Width + imageSize.Width;
                break;
        }

        // Pad the result
        retSize.Height += Padding.Vertical + 6;
        retSize.Width += Padding.Horizontal + 6;

        // pad the splitButton arrow region
        if (_showSplit)
        {
            retSize.Width += _splitSectionWidth;
        }

        return retSize;
    }

    private void ShowContextMenuStrip()
    {
        if (_skipNextOpen)
        {
            // we were called because we're closing the context menu strip
            // when clicking the dropdown button.
            _skipNextOpen = false;
            return;
        }

        State = PushButtonState.Pressed;

        _splitMenuStrip?.Show(this, new Point(0, Height), ToolStripDropDownDirection.BelowRight);
    }

    private void SplitMenuStrip_Opening(object? sender, CancelEventArgs e)
    {
        _isSplitMenuVisible = true;
    }

    private void SplitMenuStrip_Closing(object? sender, ToolStripDropDownClosingEventArgs e)
    {
        _isSplitMenuVisible = false;

        SetButtonDrawState();

        if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked)
        {
            _skipNextOpen = _dropDownRectangle.Contains(PointToClient(Cursor.Position)) &&
                            MouseButtons == MouseButtons.Left;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // 0x0212 == WM_EXITMENULOOP
        if (m.Msg == 0x0212)
        {
            // this message is only sent when a ContextMenu is closed (not a ContextMenuStrip)
            _isSplitMenuVisible = false;
            SetButtonDrawState();
        }

        base.WndProc(ref m);
    }

    private void SetButtonDrawState()
    {
        if (Bounds.Contains(Parent!.PointToClient(Cursor.Position)))
        {
            State = PushButtonState.Hot;
        }
        else if (Focused)
        {
            State = PushButtonState.Default;
        }
        else if (!Enabled)
        {
            State = PushButtonState.Disabled;
        }
        else
        {
            State = PushButtonState.Normal;
        }
    }

    #region Properties

    [Browsable(false)]
    public override ContextMenuStrip? ContextMenuStrip
    {
        get => SplitMenuStrip;
        set => SplitMenuStrip = value;
    }

    [DefaultValue(null)]
    public ContextMenuStrip? SplitMenuStrip
    {
        get => _splitMenuStrip;
        set
        {
            // remove the event handlers for the old SplitMenuStrip
            if (_splitMenuStrip is not null)
            {
                _splitMenuStrip.Closing -= SplitMenuStrip_Closing;
                _splitMenuStrip.Opening -= SplitMenuStrip_Opening;
            }

            // add the event handlers for the new SplitMenuStrip
            if (value is not null)
            {
                ShowSplit = true;
                value.Closing += SplitMenuStrip_Closing;
                value.Opening += SplitMenuStrip_Opening;
            }
            else
            {
                ShowSplit = false;
            }

            _splitMenuStrip = value;
        }
    }

    [DefaultValue(false)]
    public bool ShowSplit
    {
        set
        {
            if (value != _showSplit)
            {
                _showSplit = value;
                Invalidate();

                Parent?.PerformLayout();
            }
        }
    }

    [DefaultValue(false)] public bool WholeButtonDropdown { get; set; }

    private PushButtonState State
    {
        get => _state;
        set
        {
            if (!_state.Equals(value))
            {
                _state = value;
                Invalidate();
            }
        }
    }

    #endregion

    #region Button Layout Calculations

    // The following layout functions were taken from Mono's Windows.Forms
    // implementation, specifically "ThemeWin32Classic.cs",
    // then modified to fit the context of this splitButton

    private void CalculateButtonTextAndImageLayout(ref Rectangle contentRect, out Rectangle textRectangle,
        out Rectangle imageRectangle)
    {
        var textSize = TextRenderer.MeasureText(Text, Font, contentRect.Size, _textFormatFlags);
        var imageSize = Image?.Size ?? Size.Empty;

        textRectangle = Rectangle.Empty;
        imageRectangle = Rectangle.Empty;

        switch (TextImageRelation)
        {
            case TextImageRelation.Overlay:
                // Overlay is easy, text always goes here
                textRectangle =
                    OverlayObjectRect(ref contentRect, ref textSize,
                        TextAlign); // Rectangle.Inflate(content_rect, -4, -4);

                // Offset on Windows 98 style when button is pressed
                if (_state == PushButtonState.Pressed && !Application.RenderWithVisualStyles)
                {
                    textRectangle.Offset(1, 1);
                }

                // Image is dependent on ImageAlign
                if (Image is not null)
                {
                    imageRectangle = OverlayObjectRect(ref contentRect, ref imageSize, ImageAlign);
                }

                break;
            case TextImageRelation.ImageAboveText:
                contentRect.Inflate(-4, -4);
                LayoutTextAboveOrBelowImage(contentRect, false, textSize, imageSize, out textRectangle,
                    out imageRectangle);
                break;
            case TextImageRelation.TextAboveImage:
                contentRect.Inflate(-4, -4);
                LayoutTextAboveOrBelowImage(contentRect, true, textSize, imageSize, out textRectangle,
                    out imageRectangle);
                break;
            case TextImageRelation.ImageBeforeText:
                contentRect.Inflate(-4, -4);
                LayoutTextBeforeOrAfterImage(contentRect, false, textSize, imageSize, out textRectangle,
                    out imageRectangle);
                break;
            case TextImageRelation.TextBeforeImage:
                contentRect.Inflate(-4, -4);
                LayoutTextBeforeOrAfterImage(contentRect, true, textSize, imageSize, out textRectangle,
                    out imageRectangle);
                break;
        }
    }

    private static Rectangle OverlayObjectRect(ref Rectangle container, ref Size sizeOfObject,
        ContentAlignment alignment)
    {
        int x, y;

        switch (alignment)
        {
            case ContentAlignment.TopLeft:
                x = 4;
                y = 4;
                break;
            case ContentAlignment.TopCenter:
                x = (container.Width - sizeOfObject.Width) / 2;
                y = 4;
                break;
            case ContentAlignment.TopRight:
                x = container.Width - sizeOfObject.Width - 4;
                y = 4;
                break;
            case ContentAlignment.MiddleLeft:
                x = 4;
                y = (container.Height - sizeOfObject.Height) / 2;
                break;
            case ContentAlignment.MiddleCenter:
                x = (container.Width - sizeOfObject.Width) / 2;
                y = (container.Height - sizeOfObject.Height) / 2;
                break;
            case ContentAlignment.MiddleRight:
                x = container.Width - sizeOfObject.Width - 4;
                y = (container.Height - sizeOfObject.Height) / 2;
                break;
            case ContentAlignment.BottomLeft:
                x = 4;
                y = container.Height - sizeOfObject.Height - 4;
                break;
            case ContentAlignment.BottomCenter:
                x = (container.Width - sizeOfObject.Width) / 2;
                y = container.Height - sizeOfObject.Height - 4;
                break;
            case ContentAlignment.BottomRight:
                x = container.Width - sizeOfObject.Width - 4;
                y = container.Height - sizeOfObject.Height - 4;
                break;
            default:
                x = 4;
                y = 4;
                break;
        }

        return new Rectangle(x, y, sizeOfObject.Width, sizeOfObject.Height);
    }

    private void LayoutTextBeforeOrAfterImage(Rectangle totalArea, bool textFirst, Size textSize, Size imageSize,
        out Rectangle textRect, out Rectangle imageRect)
    {
        var elementSpacing = 0; // Spacing between the Text and the Image
        var totalWidth = textSize.Width + elementSpacing + imageSize.Width;

        if (!textFirst)
        {
            elementSpacing += 2;
        }

        // If the text is too big, chop it down to the size we have available to it
        if (totalWidth > totalArea.Width)
        {
            textSize.Width = totalArea.Width - elementSpacing - imageSize.Width;
            totalWidth = totalArea.Width;
        }

        var excessWidth = totalArea.Width - totalWidth;
        var offset = 0;

        Rectangle finalTextRect;
        Rectangle finalImageRect;

        var hText = GetHorizontalAlignment(TextAlign);
        var hImage = GetHorizontalAlignment(ImageAlign);

        if (hImage == HorizontalAlignment.Left)
        {
            offset = 0;
        }
        else if (hImage == HorizontalAlignment.Right && hText == HorizontalAlignment.Right)
        {
            offset = excessWidth;
        }
        else if (hImage == HorizontalAlignment.Center &&
                 hText is HorizontalAlignment.Left or HorizontalAlignment.Center)
        {
            offset += excessWidth / 3;
        }
        else
        {
            offset += 2 * (excessWidth / 3);
        }

        if (textFirst)
        {
            finalTextRect = new Rectangle(totalArea.Left + offset, AlignInRectangle(totalArea, textSize, TextAlign).Top,
                textSize.Width, textSize.Height);
            finalImageRect = new Rectangle(finalTextRect.Right + elementSpacing,
                AlignInRectangle(totalArea, imageSize, ImageAlign).Top, imageSize.Width, imageSize.Height);
        }
        else
        {
            finalImageRect = new Rectangle(totalArea.Left + offset,
                AlignInRectangle(totalArea, imageSize, ImageAlign).Top, imageSize.Width, imageSize.Height);
            finalTextRect = new Rectangle(finalImageRect.Right + elementSpacing,
                AlignInRectangle(totalArea, textSize, TextAlign).Top, textSize.Width, textSize.Height);
        }

        textRect = finalTextRect;
        imageRect = finalImageRect;
    }

    private void LayoutTextAboveOrBelowImage(Rectangle totalArea, bool textFirst, Size textSize, Size imageSize,
        out Rectangle textRect, out Rectangle imageRect)
    {
        var elementSpacing = 0; // Spacing between the Text and the Image
        var totalHeight = textSize.Height + elementSpacing + imageSize.Height;

        if (textFirst)
        {
            elementSpacing += 2;
        }

        if (textSize.Width > totalArea.Width)
        {
            textSize.Width = totalArea.Width;
        }

        // If the there isn't enough room and we're text first, cut out the image
        if (totalHeight > totalArea.Height && textFirst)
        {
            imageSize = Size.Empty;
            totalHeight = totalArea.Height;
        }

        var excessHeight = totalArea.Height - totalHeight;
        var offset = 0;

        Rectangle finalTextRect;
        Rectangle finalImageRect;

        var vText = GetVerticalAlignment(TextAlign);
        var vImage = GetVerticalAlignment(ImageAlign);

        if (vImage == VerticalAlignment.Top)
        {
            offset = 0;
        }
        else if (vImage == VerticalAlignment.Bottom && vText == VerticalAlignment.Bottom)
        {
            offset = excessHeight;
        }
        else if (vImage == VerticalAlignment.Center && vText is VerticalAlignment.Top or VerticalAlignment.Center)
        {
            offset += excessHeight / 3;
        }
        else
        {
            offset += 2 * (excessHeight / 3);
        }

        if (textFirst)
        {
            finalTextRect = new Rectangle(AlignInRectangle(totalArea, textSize, TextAlign).Left, totalArea.Top + offset,
                textSize.Width, textSize.Height);
            finalImageRect = new Rectangle(AlignInRectangle(totalArea, imageSize, ImageAlign).Left,
                finalTextRect.Bottom + elementSpacing, imageSize.Width, imageSize.Height);
        }
        else
        {
            finalImageRect = new Rectangle(AlignInRectangle(totalArea, imageSize, ImageAlign).Left,
                totalArea.Top + offset, imageSize.Width, imageSize.Height);
            finalTextRect = new Rectangle(AlignInRectangle(totalArea, textSize, TextAlign).Left,
                finalImageRect.Bottom + elementSpacing, textSize.Width, textSize.Height);

            if (finalTextRect.Bottom > totalArea.Bottom)
            {
                finalTextRect.Y = totalArea.Top;
            }
        }

        textRect = finalTextRect;
        imageRect = finalImageRect;
    }

    private static HorizontalAlignment GetHorizontalAlignment(ContentAlignment align)
    {
        switch (align)
        {
            case ContentAlignment.BottomLeft:
            case ContentAlignment.MiddleLeft:
            case ContentAlignment.TopLeft:
                return HorizontalAlignment.Left;
            case ContentAlignment.BottomCenter:
            case ContentAlignment.MiddleCenter:
            case ContentAlignment.TopCenter:
                return HorizontalAlignment.Center;
            case ContentAlignment.BottomRight:
            case ContentAlignment.MiddleRight:
            case ContentAlignment.TopRight:
                return HorizontalAlignment.Right;
        }

        return HorizontalAlignment.Left;
    }

    private static VerticalAlignment GetVerticalAlignment(ContentAlignment align)
    {
        switch (align)
        {
            case ContentAlignment.TopLeft:
            case ContentAlignment.TopCenter:
            case ContentAlignment.TopRight:
                return VerticalAlignment.Top;
            case ContentAlignment.MiddleLeft:
            case ContentAlignment.MiddleCenter:
            case ContentAlignment.MiddleRight:
                return VerticalAlignment.Center;
            case ContentAlignment.BottomLeft:
            case ContentAlignment.BottomCenter:
            case ContentAlignment.BottomRight:
                return VerticalAlignment.Bottom;
        }

        return VerticalAlignment.Top;
    }

    private static Rectangle AlignInRectangle(Rectangle outer, Size inner, ContentAlignment align)
    {
        var x = 0;
        var y = 0;

        x = align switch
        {
            ContentAlignment.BottomLeft or ContentAlignment.MiddleLeft or ContentAlignment.TopLeft => outer.X,
            ContentAlignment.BottomCenter or ContentAlignment.MiddleCenter or ContentAlignment.TopCenter => Math.Max(
                outer.X + ((outer.Width - inner.Width) / 2), outer.Left),
            ContentAlignment.BottomRight or ContentAlignment.MiddleRight or ContentAlignment.TopRight => outer.Right -
                inner.Width,
            _ => x
        };

        y = align switch
        {
            ContentAlignment.TopCenter or ContentAlignment.TopLeft or ContentAlignment.TopRight => outer.Y,
            ContentAlignment.MiddleCenter or ContentAlignment.MiddleLeft or ContentAlignment.MiddleRight => outer.Y +
                ((outer.Height - inner.Height) / 2),
            ContentAlignment.BottomCenter or ContentAlignment.BottomRight or ContentAlignment.BottomLeft =>
                outer.Bottom - inner.Height,
            _ => y
        };

        return new Rectangle(x, y, Math.Min(inner.Width, outer.Width), Math.Min(inner.Height, outer.Height));
    }

    #endregion
}
