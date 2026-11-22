using System.Drawing.Drawing2D;

namespace TsDssConverter.Tray;

// The three controls in this file paint themselves, so the settings window gets its own look instead of the old square
// WinForms one: since 2.0.0 the "Maestro" look (navy and grey buttons, flat grey text fields, a navy check box). They are
// still a normal Button, CheckBox and TextBox for the rest of the program (Text, Checked, Click, ...).
// All colours come from Theme, and every size is written for 96 dpi and scaled with LogicalToDeviceUnits.

/// <summary>Small drawing helper shared by the controls below.</summary>
internal static class FluentDrawing
{
    /// <summary>The usual corner radius, in pixels at 100% scaling.</summary>
    public const int CornerRadius = 4;

    /// <summary>A rectangle with rounded corners, as a path that can be filled and drawn. Radius 0 = square corners.</summary>
    public static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        float diameter = radius * 2;
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

    /// <summary>
    /// The folder sign of the mockup (on the Browse buttons), as lines on a grid of 24 in <paramref name="box"/>: a tab
    /// at the top left, then the body.
    /// </summary>
    public static void DrawFolder(Graphics g, RectangleF box, Color colour)
    {
        float unit = box.Width / 24;
        PointF P(float x, float y) => new(box.Left + x * unit, box.Top + y * unit);
        using var pen = new Pen(colour, 2 * unit) { LineJoin = LineJoin.Round };
        g.DrawPolygon(pen, new[] { P(3, 6), P(9, 6), P(11, 8), P(21, 8), P(21, 19), P(3, 19) });
    }
}

/// <summary>
/// A button with (almost) square or rounded corners. The colours are set by Theme.StyleNavyButton / StyleGreyButton.
/// Keyboard focus is shown with a ring, so the window can still be used without a mouse.
/// </summary>
internal class RoundedButton : Button
{
    private bool _hover;
    private bool _pressed;

    internal Color NormalFill = Theme.White;
    internal Color HoverFill = Theme.Field;
    internal Color PressedFill = Theme.Selection;

    /// <summary>Border colour; Color.Empty = no border.</summary>
    internal Color BorderColor = Color.Empty;

    internal Color NormalText = Theme.Text;
    internal Color HoverText = Theme.Text;

    /// <summary>The corner radius in pixels at 100% (the Maestro buttons are almost square: 2, the grey ones 0).</summary>
    internal int Radius = FluentDrawing.CornerRadius;

    /// <summary>True: a folder sign before the text (the Browse buttons, like the mockup).</summary>
    internal bool ShowFolderIcon;

    private const int IconSize = 18; // the folder sign, at 100%
    private const int IconGap = 8;   // between the sign and the text, at 100%

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    /// <summary>The width this button needs for its text (and folder sign), with room on both sides, in pixels.</summary>
    internal int WidthNeeded()
    {
        int text = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        int icon = ShowFolderIcon ? LogicalToDeviceUnits(IconSize + IconGap) : 0;
        return icon + text + LogicalToDeviceUnits(2 * 14);
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
        float radius = LogicalToDeviceUnits(Radius);
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

        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        if (ShowFolderIcon)
        {
            // The sign and the text together in the middle of the button.
            int icon = LogicalToDeviceUnits(IconSize), gap = LogicalToDeviceUnits(IconGap);
            int textWidth = TextRenderer.MeasureText(g, Text, Font, new Size(int.MaxValue, int.MaxValue), flags).Width;
            int left = Math.Max(0, (Width - icon - gap - textWidth) / 2);
            FluentDrawing.DrawFolder(g, new RectangleF(left, (Height - icon) / 2f, icon, icon), text);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(left + icon + gap, 0, Math.Max(0, Width - left - icon - gap), Height), text, flags);
        }
        else
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, flags | TextFormatFlags.HorizontalCenter);
        }

        if (Focused && ShowFocusCues)
        {
            float inset = LogicalToDeviceUnits(2);
            var ringRect = new RectangleF(inset, inset, Width - 1 - 2 * inset, Height - 1 - 2 * inset);
            using GraphicsPath ring = FluentDrawing.RoundedRectangle(ringRect, radius);
            using var ringPen = new Pen(text, 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawPath(ringPen, ring);
        }
    }
}

/// <summary>
/// A check box in the Maestro look: a white square with a navy border that fills navy with a white tick when checked.
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
        float radius = LogicalToDeviceUnits(2);
        float top = (Height - box) / 2f;
        var outline = new RectangleF(0.5f, top + 0.5f, box - 1, box - 1);

        // Unchecked: a white box with a navy border. Checked: navy with a white tick (lighter navy under the mouse).
        Color fill = Checked ? (_hover ? Theme.ButtonNavyHover : Theme.ButtonNavy) : (_hover ? Theme.Field : Theme.White);
        Color border = Checked ? fill : Theme.ButtonNavy;

        using (GraphicsPath path = FluentDrawing.RoundedRectangle(outline, radius))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(border, Math.Max(1, LogicalToDeviceUnits(1))))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        if (Checked)
        {
            using var tickPen = new Pen(Theme.White, Math.Max(1.5f, box / 8f))
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
            using var ringPen = new Pen(Theme.ButtonNavy, 1.5f);
            g.DrawPath(ringPen, ring);
        }
    }
}

/// <summary>
/// A frame around a TextBox: in the Maestro look a flat light-grey field without a border, with a navy line at the bottom
/// while the box has the focus. The TextBox itself stays a normal TextBox (its own border is switched off), so Text, Leave
/// and so on work as before.
/// </summary>
internal class TextBoxFrame : Panel
{
    private const int SidePadding = 10; // between the edge and the text, at 100%

    private readonly TextBox _box;
    private bool _focused;

    public TextBoxFrame(TextBox box)
    {
        _box = box;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _box.BorderStyle = BorderStyle.None;
        _box.BackColor = Theme.Field;
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
        g.Clear(Theme.Field);

        if (_focused)
        {
            int line = LogicalToDeviceUnits(2);
            using var accent = new SolidBrush(Theme.ButtonNavy);
            g.FillRectangle(accent, 0, Height - line, Width, line);
        }
    }
}
