using System.Drawing.Drawing2D;

namespace TsDssConverter.Tray;

// The three controls in this file give the settings window a softer, Windows 11 ("Fluent") look:
// rounded corners, a fill that changes when the mouse is over them, and a thin accent line on a text box that has
// the focus. WinForms draws its own controls in the old square style; these controls paint themselves instead.
// They are still a normal Button, CheckBox and TextBox for the rest of the program (Text, Checked, Click, ...).
// All colours come from Theme, and every size is written for 96 dpi and scaled with LogicalToDeviceUnits.

/// <summary>Small drawing helper shared by the controls below.</summary>
internal static class FluentDrawing
{
    /// <summary>The corner radius of every control, in pixels at 100% scaling.</summary>
    public const int CornerRadius = 4;

    /// <summary>A rectangle with rounded corners, as a path that can be filled and drawn.</summary>
    public static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>The colour behind a control, so the corners that are not painted look right.</summary>
    public static Color BehindColor(Control control)
    {
        return control.Parent?.BackColor ?? Theme.White;
    }
}

/// <summary>
/// A button with rounded corners. The colours are set by Theme.StylePrimaryButton / StyleSecondaryButton.
/// Keyboard focus is shown with a navy ring, so the window can still be used without a mouse.
/// </summary>
internal class RoundedButton : Button
{
    private bool _hover;
    private bool _pressed;

    internal Color NormalFill = Theme.White;
    internal Color HoverFill = Theme.VeryPaleBlue;
    internal Color PressedFill = Theme.PaleBlue;

    /// <summary>Border colour; Color.Empty = no border.</summary>
    internal Color BorderColor = Color.Empty;

    internal Color NormalText = Theme.Navy;
    internal Color HoverText = Theme.Navy;

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = e.Button == MouseButtons.Left;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(FluentDrawing.BehindColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill = _pressed ? PressedFill : _hover ? HoverFill : NormalFill;
        Color text = _pressed || _hover ? HoverText : NormalText;
        float radius = LogicalToDeviceUnits(FluentDrawing.CornerRadius);
        var outline = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1); // half a pixel in: the border stays inside

        using (GraphicsPath path = FluentDrawing.RoundedRectangle(outline, radius))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);

            if (BorderColor != Color.Empty)
            {
                using var pen = new Pen(BorderColor, 1);
                g.DrawPath(pen, path);
            }
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            float inset = LogicalToDeviceUnits(2);
            var ringRect = new RectangleF(inset, inset, Width - 1 - 2 * inset, Height - 1 - 2 * inset);
            using GraphicsPath ring = FluentDrawing.RoundedRectangle(ringRect, radius);
            using var ringPen = new Pen(Theme.Navy, 1.5f);
            g.DrawPath(ringPen, ring);
        }
    }
}

/// <summary>
/// A check box in the Windows 11 style: a rounded square that fills with the brand colour with a tick when checked.
/// </summary>
internal class FluentCheckBox : CheckBox
{
    private const int BoxSize = 18; // at 100%
    private const int TextGap = 8;  // between the box and the text, at 100%

    private const TextFormatFlags MeasureFlags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    private bool _hover;

    public FluentCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }

    /// <summary>The size for AutoSize: the box, the gap and the text. (The standard size is for the old square box.)</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
        int box = LogicalToDeviceUnits(BoxSize);
        int width = box + LogicalToDeviceUnits(TextGap) + text.Width + LogicalToDeviceUnits(2);
        int height = Math.Max(box, text.Height) + LogicalToDeviceUnits(4);
        return new Size(width, height);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(FluentDrawing.BehindColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int box = LogicalToDeviceUnits(BoxSize);
        float radius = LogicalToDeviceUnits(FluentDrawing.CornerRadius - 1);
        float top = (Height - box) / 2f;
        var outline = new RectangleF(0.5f, top + 0.5f, box - 1, box - 1);

        // Unchecked: a white box with a blue border. Checked: filled with the brand colour (dark blue under the mouse).
        Color fill = Checked ? (_hover ? Theme.DarkBlue : Theme.Brand) : (_hover ? Theme.VeryPaleBlue : Theme.White);
        Color border = Checked ? fill : Theme.DarkBlue;

        using (GraphicsPath path = FluentDrawing.RoundedRectangle(outline, radius))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(border, 1))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        if (Checked)
        {
            // The tick, as three points inside the box. Navy on the brand colour, white on dark blue (readable both ways).
            using var tickPen = new Pen(_hover ? Theme.White : Theme.Navy, Math.Max(1.5f, box / 9f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            g.DrawLines(tickPen, new[]
            {
                new PointF(box * 0.24f, top + box * 0.53f),
                new PointF(box * 0.43f, top + box * 0.72f),
                new PointF(box * 0.77f, top + box * 0.30f),
            });
        }

        int textLeft = box + LogicalToDeviceUnits(TextGap);
        var textArea = new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), Height);
        TextRenderer.DrawText(g, Text, Font, textArea, ForeColor, MeasureFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            float grow = LogicalToDeviceUnits(2);
            var ringRect = new RectangleF(outline.Left - grow, outline.Top - grow, outline.Width + 2 * grow, outline.Height + 2 * grow);
            using GraphicsPath ring = FluentDrawing.RoundedRectangle(ringRect, radius + grow);
            using var ringPen = new Pen(Theme.Navy, 1.5f);
            g.DrawPath(ringPen, ring);
        }
    }
}

/// <summary>
/// A frame around a TextBox: rounded border, and a dark blue line at the bottom while the box has the focus.
/// The TextBox itself stays a normal TextBox (its own border is switched off), so Text, Leave and so on work as before.
/// </summary>
internal class TextBoxFrame : Panel
{
    private const int SidePadding = 8; // between the border and the text, at 100%

    private readonly TextBox _box;
    private bool _focused;

    public TextBoxFrame(TextBox box)
    {
        _box = box;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _box.BorderStyle = BorderStyle.None;
        _box.BackColor = Theme.White;
        _box.Enter += (sender, e) => { _focused = true; Invalidate(); };
        _box.Leave += (sender, e) => { _focused = false; Invalidate(); };
        Controls.Add(_box);

        // A click on the frame (outside the text) also puts the cursor in the box.
        MouseDown += (sender, e) => _box.Focus();
    }

    /// <summary>The text box sits in the middle of the frame's height and fills its width.</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        int side = LogicalToDeviceUnits(SidePadding);
        int height = _box.PreferredHeight;
        _box.SetBounds(side, (Height - height) / 2, Math.Max(0, Width - 2 * side), height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(FluentDrawing.BehindColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = LogicalToDeviceUnits(FluentDrawing.CornerRadius);
        var outline = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);

        using GraphicsPath path = FluentDrawing.RoundedRectangle(outline, radius);
        using (var brush = new SolidBrush(Theme.White))
        using (var pen = new Pen(Theme.LightBlue, 1))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        if (_focused)
        {
            // Only the bottom strip of the border is drawn again, thicker and darker.
            float line = LogicalToDeviceUnits(2);
            g.SetClip(new RectangleF(0, Height - line, Width, line));
            using var accent = new Pen(Theme.DarkBlue, line);
            g.DrawPath(accent, path);
            g.ResetClip();
        }
    }
}
