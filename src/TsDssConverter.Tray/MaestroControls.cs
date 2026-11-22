using System.Drawing.Drawing2D;

namespace TsDssConverter.Tray;

// The controls of the "Maestro" skin (2.0.0, Daan 2026-11-21), the look of DssLabelTemplate 2.0.0: the customer works all
// day with the SCM machine software (Maestro). Like FluentControls.cs these controls paint themselves and are still a
// normal Button / Control / ListView for the rest of the program (Text, Click, PerformClick, Items, ...). They were copied
// from DssLabelTemplate and adapted (the two programs share no code). All colours come from Theme, every size is written
// for 96 dpi and scaled with LogicalToDeviceUnits.

/// <summary>Drawing helpers shared by the controls below.</summary>
internal static class MaestroDrawing
{
    /// <summary>
    /// A round disc that is lighter at the top left, like the round buttons of Maestro: <paramref name="light"/> at that
    /// point, <paramref name="middle"/> about halfway and <paramref name="edge"/> at the rim.
    /// </summary>
    public static void FillDisc(Graphics g, RectangleF circle, Color light, Color middle, Color edge)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(circle);
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(circle.Left + circle.Width * 0.35f, circle.Top + circle.Height * 0.30f),
            // Positions run from the rim (0) to the centre point (1).
            InterpolationColors = new ColorBlend { Colors = new[] { edge, middle, light }, Positions = new[] { 0f, 0.45f, 1f } },
        };
        g.FillPath(brush, path);
    }

    /// <summary>A rectangle whose two short ends are half circles (Save, Cancel, the end of the bottom bar).</summary>
    public static GraphicsPath Pill(RectangleF rect)
    {
        float diameter = Math.Min(rect.Width, rect.Height);
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 90, 180);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 180);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// A cog wheel (the Magic parameters, 2.0.1): eight flat teeth around a wheel, with a round hole in the middle in
    /// <paramref name="hole"/> (the colour behind the cog).
    /// </summary>
    public static void DrawCog(Graphics g, RectangleF box, Color ink, Color hole)
    {
        float centreX = box.Left + box.Width / 2;
        float centreY = box.Top + box.Height / 2;
        float outer = Math.Min(box.Width, box.Height) / 2;
        float inner = outer * 0.74f;

        // Every tooth: from the wheel up to the tip and down again, a little narrower at the tip than at its foot.
        var points = new List<PointF>();
        for (int tooth = 0; tooth < 8; tooth++)
        {
            double middle = tooth * Math.PI / 4;
            foreach (var (turn, radius) in new[] { (-0.30, inner), (-0.17, outer), (0.17, outer), (0.30, inner) })
            {
                points.Add(new PointF(centreX + radius * (float)Math.Cos(middle + turn), centreY + radius * (float)Math.Sin(middle + turn)));
            }
        }

        using (var brush = new SolidBrush(ink))
        {
            g.FillPolygon(brush, points.ToArray());
        }

        float hollow = outer * 0.36f;
        using var holeBrush = new SolidBrush(hole);
        g.FillEllipse(holeBrush, centreX - hollow, centreY - hollow, 2 * hollow, 2 * hollow);
    }

    /// <summary>A soft shadow below a shape: the same shape a little lower, in see-through black.</summary>
    public static void FillShadow(Graphics g, GraphicsPath shape, float down, int alpha)
    {
        using var shadow = (GraphicsPath)shape.Clone();
        using var move = new Matrix();
        move.Translate(0, down);
        shadow.Transform(move);
        using var brush = new SolidBrush(Color.FromArgb(alpha, Color.Black));
        g.FillPath(brush, shadow);
    }
}

/// <summary>
/// A control that paints a background its round children must show around their shape (the bottom bar's gradient).
/// </summary>
internal interface IPaintsBackgroundForChildren
{
    /// <summary>Paints this control's background into <paramref name="g"/>, which is the graphics of <paramref name="child"/>.</summary>
    void PaintBackgroundFor(Control child, Graphics g);
}

/// <summary>
/// A button with a round shape that shows the background of its parent around it (the gradient of the bottom bar). It
/// asks the parent to paint that background into the button itself (<see cref="IPaintsBackgroundForChildren"/>): with
/// BackColor = Transparent DssLabelTemplate's round buttons showed grey squares around them on Daan's screen. Tracks the
/// mouse for the hover and pressed look.
/// </summary>
internal abstract class ShapedButton : Button
{
    protected bool Hover;
    protected bool Pressed;

    protected ShapedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        Cursor = Cursors.Hand;
    }

    /// <summary>First the parent's background (the band), then the button: no square around the round shape.</summary>
    protected void PaintParentBackground(Graphics g)
    {
        if (Parent is IPaintsBackgroundForChildren painter)
        {
            painter.PaintBackgroundFor(this, g);
        }
        else
        {
            g.Clear(Parent?.BackColor ?? Theme.White);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Nothing here: OnPaint paints the parent's background itself (Opaque: Windows does not erase it either), so the
        // button is drawn in one go into its double buffer, without a flash of another colour.
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        Hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        Hover = false;
        Pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Pressed = e.Button == MouseButtons.Left;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        Pressed = false;
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
}

/// <summary>What a <see cref="BarButton"/> shows on its disc.</summary>
internal enum BarGlyph
{
    Scan,      // a magnifying glass: scan now (Daan, 2026-11-22; was an arrow going round)
    Retry,     // an arrow going round anticlockwise: retry the failed projects
    Pause,     // two bars
    Resume,    // a triangle pointing right
    LogFolder, // a folder with two lines of text in it
    Materials, // a wooden board with its grain and an edge band (the material list, materials.csv; Daan's picture, 2026-11-22)
}

/// <summary>
/// A round button of the bottom bar: a light-blue disc with a pale ring and a navy sign, and below it its name in white
/// bold letters (the <see cref="Control.Text"/>), so it is clear what it does without hovering (Daan's mockup, 2026-11-21).
/// The name is part of the button: a click on it works too.
/// Every bar button is <see cref="SlotWidth"/> wide, whatever its name (Daan, 2026-11-22): the discs then always sit the
/// same distance apart, in every language. A name that is wider than the slot ("Analyser maintenant") wraps onto a
/// second line under its disc instead of pushing the other buttons away.
/// </summary>
internal class BarButton : ShapedButton
{
    /// <summary>The width of every bar button at 100% = the distance between the middles of two discs.</summary>
    internal const int SlotWidth = 122; // 140 in 2.0.0-alpha.2-3; Daan asked for the buttons 10 to 15% closer (alpha.4)

    private const int DiscSize = 50;   // at 100%
    private const int Shadow = 2;
    private const int CaptionGap = 4;  // between the disc and its name
    private const int SidePadding = 5; // room left and right of the name, so two names never touch
    private const int GlyphSize = 26;

    private const TextFormatFlags CaptionFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;

    private Font? _captionFont;

    /// <summary>The sign on the disc.</summary>
    internal BarGlyph Glyph = BarGlyph.Scan;

    internal Font CaptionFont => _captionFont ??= Theme.CreateFont(Font, Font.SizeInPoints, FontStyle.Bold);

    /// <summary>The width the name may use: the slot minus a little room on both sides, in pixels.</summary>
    internal int CaptionWidth(int slot) => slot - LogicalToDeviceUnits(2 * SidePadding);

    /// <summary>On how many lines the name is written in a slot of <paramref name="slot"/> pixels (1, or 2 when it is long).</summary>
    internal int CaptionLines(int slot)
    {
        int height = TextRenderer.MeasureText(Text, CaptionFont, new Size(CaptionWidth(slot), int.MaxValue), CaptionFlags).Height;
        return Math.Max(1, (int)Math.Round((double)height / CaptionFont.Height));
    }

    /// <summary>The height of a bar button whose name takes <paramref name="lines"/> lines, in pixels.</summary>
    internal int HeightFor(int lines) => LogicalToDeviceUnits(DiscSize + Shadow + CaptionGap) + lines * CaptionFont.Height;

    /// <summary>The disc, in this button's pixels (the tests look at it).</summary>
    internal RectangleF DiscBounds()
    {
        float disc = LogicalToDeviceUnits(DiscSize);
        return new RectangleF((Width - disc) / 2f, 0, disc, disc);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        _captionFont?.Dispose();
        _captionFont = null;
        base.OnFontChanged(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        Parent?.PerformLayout(); // a name of another length can need another number of lines
        base.OnTextChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        PaintParentBackground(g);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        RectangleF circle = DiscBounds();
        using (var outline = new GraphicsPath())
        {
            outline.AddEllipse(circle);
            MaestroDrawing.FillShadow(g, outline, LogicalToDeviceUnits(Shadow), 90);
        }

        // A thin pale ring, a thicker dark one while it has the keyboard focus.
        float ring = LogicalToDeviceUnits(Focused && ShowFocusCues ? 4 : 3);
        using (var ringBrush = new SolidBrush(Focused && ShowFocusCues ? Theme.ButtonNavyPressed : Theme.DiscRing))
        {
            g.FillEllipse(ringBrush, circle);
        }

        RectangleF disc = RectangleF.Inflate(circle, -ring, -ring);
        if (Pressed)
        {
            MaestroDrawing.FillDisc(g, disc, Theme.DiscMiddle, Theme.DiscEdge, Theme.DiscEdge);
        }
        else
        {
            MaestroDrawing.FillDisc(g, disc, Hover ? Theme.DiscLight : Theme.DiscSoftLight, Theme.DiscMiddle, Theme.DiscEdge);
        }

        float size = LogicalToDeviceUnits(GlyphSize);
        DrawGlyph(g, new RectangleF(disc.Left + (disc.Width - size) / 2, disc.Top + (disc.Height - size) / 2, size, size), Theme.ButtonNavy);

        int captionTop = (int)(circle.Bottom + LogicalToDeviceUnits(Shadow + CaptionGap));
        var captionArea = new Rectangle((Width - CaptionWidth(Width)) / 2, captionTop, CaptionWidth(Width), Math.Max(0, Height - captionTop));
        TextRenderer.DrawText(g, Text, CaptionFont, captionArea, Theme.White, CaptionFlags);
    }

    /// <summary>The signs of the mockup, as lines on a grid of 24 in <paramref name="box"/>.</summary>
    private void DrawGlyph(Graphics g, RectangleF box, Color ink)
    {
        float unit = box.Width / 24;
        PointF P(float x, float y) => new(box.Left + x * unit, box.Top + y * unit);
        using var pen = new Pen(ink, 2.4f * unit) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(ink);
        var round = new RectangleF(box.Left + 4 * unit, box.Top + 4 * unit, 16 * unit, 16 * unit); // a circle of 8 around (12, 12)

        switch (Glyph)
        {
            case BarGlyph.Scan:
                // A magnifying glass: a round lens at the top left and a thicker handle to the bottom right.
                g.DrawEllipse(pen, box.Left + 3.5f * unit, box.Top + 3.5f * unit, 11 * unit, 11 * unit);
                pen.Width = 3.4f * unit;
                g.DrawLine(pen, P(13.4f, 13.4f), P(20, 20));
                break;
            case BarGlyph.Retry:
                // From nine o'clock almost all the way round, anticlockwise, the arrow head at the top left.
                g.DrawArc(pen, round, 180, -315);
                g.DrawLines(pen, new[] { P(4, 4), P(4, 9), P(9, 9) });
                break;
            case BarGlyph.Pause:
                g.FillRectangle(brush, box.Left + 6 * unit, box.Top + 4 * unit, 4 * unit, 16 * unit);
                g.FillRectangle(brush, box.Left + 14 * unit, box.Top + 4 * unit, 4 * unit, 16 * unit);
                break;
            case BarGlyph.Resume:
                g.FillPolygon(brush, new[] { P(8, 4), P(20, 12), P(8, 20) });
                break;
            case BarGlyph.LogFolder:
                pen.Width = 2.2f * unit;
                g.DrawPolygon(pen, new[] { P(3, 6), P(9, 6), P(11, 8), P(21, 8), P(21, 19), P(3, 19) });
                g.DrawLine(pen, P(7, 13), P(17, 13));
                g.DrawLine(pen, P(7, 16), P(13, 16));
                break;
            case BarGlyph.Materials:
                // A standing wooden board (like Daan's picture): the outline, a narrow edge band at the right and the
                // wavy grain: two long lines and a knot at the left edge. Few, thin lines: on a disc of 50 px more lines
                // ran together into stripes.
                pen.Width = 1.9f * unit;
                g.DrawRectangle(pen, box.Left + 4 * unit, box.Top + 2 * unit, 16 * unit, 20 * unit);
                g.DrawLine(pen, P(16.5f, 2), P(16.5f, 22));
                pen.Width = 1.4f * unit;
                g.DrawBezier(pen, P(8, 2), P(5, 8), P(11.5f, 13), P(8.5f, 22));
                g.DrawBezier(pen, P(12.5f, 2), P(10, 9), P(15.5f, 13), P(12.5f, 22));
                g.DrawBezier(pen, P(4, 14), P(6.3f, 14.5f), P(7, 17.5f), P(6, 20));
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captionFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Save and Cancel at the bottom right, like Maestro's "MDI" / "Manual" buttons (and DssLabelTemplate's Print): a
/// light-grey pill. With <see cref="ShowCheck"/> (Save) a navy circle with a white tick before the text in bold;
/// without it (Cancel) only the text, in the middle.
/// </summary>
internal class PillButton : ShapedButton
{
    private const int CircleMargin = 6; // between the edge of the pill and the navy circle, at 100%
    private const int TextGap = 12;     // between the circle and the text, at 100%
    private const int SidePadding = 26;

    private Font? _boldFont;

    /// <summary>True: the navy circle with the tick, and the text in bold (Save).</summary>
    internal bool ShowCheck;

    private Font TextFont => ShowCheck ? _boldFont ??= new Font(Font, FontStyle.Bold) : Font;

    /// <summary>The width this button needs for its circle and its text, in pixels (its height must be set first).</summary>
    internal int WidthNeeded()
    {
        int text = TextRenderer.MeasureText(Text, TextFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        int circle = ShowCheck ? Height + LogicalToDeviceUnits(TextGap - CircleMargin) - LogicalToDeviceUnits(2) : LogicalToDeviceUnits(SidePadding);
        return circle + text + LogicalToDeviceUnits(SidePadding);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        _boldFont?.Dispose();
        _boldFont = null;
        base.OnFontChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        PaintParentBackground(g);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float shadow = LogicalToDeviceUnits(2);
        var pill = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1 - shadow);
        using (GraphicsPath path = MaestroDrawing.Pill(pill))
        {
            MaestroDrawing.FillShadow(g, path, shadow, 80);
            using var fill = new SolidBrush(Pressed ? Theme.GreyButton : Hover ? Theme.White : Theme.Pill);
            g.FillPath(fill, path);

            if (Focused && ShowFocusCues)
            {
                using var ring = new Pen(Theme.ButtonNavy, 1.5f) { DashStyle = DashStyle.Dot };
                g.DrawPath(ring, path);
            }
        }

        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        if (!ShowCheck)
        {
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(pill), Theme.ButtonNavy, flags | TextFormatFlags.HorizontalCenter);
            return;
        }

        float margin = LogicalToDeviceUnits(CircleMargin);
        float diameter = pill.Height - 2 * margin;
        var circle = new RectangleF(pill.Left + margin, pill.Top + margin, diameter, diameter);
        using (var navy = new SolidBrush(Theme.ButtonNavy))
        {
            g.FillEllipse(navy, circle);
        }

        // The tick of the mockup (on a grid of 24): from the left, down, then up to the right.
        float unit = diameter * 0.5f / 24;
        PointF P(float x, float y) => new(circle.Left + diameter / 4 + x * unit, circle.Top + diameter / 4 + y * unit);
        using (var tick = new Pen(Theme.White, 2.8f * unit) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            g.DrawLines(tick, new[] { P(5, 12.5f), P(9.5f, 17), P(19, 7.5f) });
        }

        int textLeft = (int)(circle.Right + LogicalToDeviceUnits(TextGap));
        TextRenderer.DrawText(g, Text, TextFont, new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), (int)pill.Height), Theme.ButtonNavy, flags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boldFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// The "v" before the title of the folders panel (Daan, 2026-11-22, like the file lines of DssLabelTemplate): a click
/// folds the panel up to one line (the sign then points right, ">") or opens it again. Navy on the white panel.
/// </summary>
internal class FoldButton : Button
{
    private bool _folded;
    private bool _hover;

    public FoldButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }

    /// <summary>True: the panel is folded up (the sign points right).</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Folded
    {
        get => _folded;
        set
        {
            _folded = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(_hover ? Theme.Field : Parent?.BackColor ?? Theme.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float size = Math.Min(Width, Height) * 0.42f, middleX = Width / 2f, middleY = Height / 2f;
        using var pen = new Pen(Theme.ButtonNavy, Math.Max(2, size * 0.28f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        PointF[] sign = _folded
            ? new[] { new PointF(middleX - size / 4, middleY - size / 2), new PointF(middleX + size / 4, middleY), new PointF(middleX - size / 4, middleY + size / 2) }
            : new[] { new PointF(middleX - size / 2, middleY - size / 4), new PointF(middleX, middleY + size / 4), new PointF(middleX + size / 2, middleY - size / 4) };
        g.DrawLines(pen, sign);

        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.ButtonNavy, 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(ring, 1, 1, Width - 3, Height - 3);
        }
    }
}

/// <summary>
/// The folded folders panel's one line: "TopSolid: Z:\TopSolid\Export\   Batch: Z:\Duivestein\Batch   Labels: …",
/// read only (to change a folder, unfold the panel). The paths are bold (Daan, 2026-11-22), the names before them not. A
/// folder that is not reachable is written in red, so a problem is never hidden by folding. Each folder gets a third of the
/// width; a long path ends in "…" in its middle.
/// </summary>
internal class FolderSummary : Control
{
    private const int Gap = 24; // between the three folders, at 100%
    private readonly List<(string Name, string Path, bool Reachable)> _entries = new();
    private Font? _pathFont;

    /// <summary>The font of the paths: the text font in bold.</summary>
    internal Font PathFont => _pathFont ??= new Font(Font, FontStyle.Bold);

    protected override void OnFontChanged(EventArgs e)
    {
        _pathFont?.Dispose();
        _pathFont = null;
        base.OnFontChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pathFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    public FolderSummary()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    /// <summary>The three folders as shown (the tests read them).</summary>
    internal IReadOnlyList<(string Name, string Path, bool Reachable)> Entries => _entries;

    /// <summary>Shows these folders: name, path and whether it is reachable.</summary>
    internal void Show(IEnumerable<(string Name, string Path, bool Reachable)> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.White);
        if (_entries.Count == 0)
        {
            return;
        }

        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        int gap = LogicalToDeviceUnits(Gap);
        int each = Math.Max(0, (Width - gap * (_entries.Count - 1)) / _entries.Count);
        int left = 0;
        foreach (var (name, path, reachable) in _entries)
        {
            string label = name + ": ";
            int labelWidth = Math.Min(each, TextRenderer.MeasureText(g, label, Font, new Size(int.MaxValue, Height), flags).Width);
            TextRenderer.DrawText(g, label, Font, new Rectangle(left, 0, labelWidth, Height), Theme.Text, flags);
            TextRenderer.DrawText(g, path, PathFont, new Rectangle(left + labelWidth, 0, Math.Max(0, each - labelWidth), Height),
                reachable ? Theme.TextSoft : Theme.Error, flags | TextFormatFlags.PathEllipsis);
            left += each + gap;
        }
    }
}

/// <summary>
/// The user-guide "?" in the top bar: a flat dark-grey disc with the "?" in the bar's grey, like DssLabelTemplate's (and
/// its clock sign). The button is bigger than the disc, so it is easy to hit.
/// </summary>
internal class HelpButton : Button
{
    internal const int DiscSize = 20; // at 100%; the ROGIERS logo beside it is as high
    private bool _hover;

    /// <summary>True: a cog on the disc instead of the "?" (the Magic parameters button, 2.0.1).</summary>
    internal bool ShowCog;

    public HelpButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Text = "?";
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.TopBar);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float disc = LogicalToDeviceUnits(DiscSize);
        var round = new RectangleF((Width - disc) / 2f, (Height - disc) / 2f, disc, disc);
        using (var grey = new SolidBrush(_hover ? Theme.DarkGreyHover : Theme.DarkGrey))
        {
            g.FillEllipse(grey, round);
        }

        if (ShowCog)
        {
            MaestroDrawing.DrawCog(g, RectangleF.Inflate(round, -disc * 0.19f, -disc * 0.19f), Theme.TopBar, _hover ? Theme.DarkGreyHover : Theme.DarkGrey);
        }
        else
        {
            using var sign = new Font(Font, FontStyle.Bold);
            TextRenderer.DrawText(g, "?", sign, Rectangle.Round(round), Theme.TopBar,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.ButtonNavy, 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawEllipse(ring, RectangleF.Inflate(round, LogicalToDeviceUnits(2), LogicalToDeviceUnits(2)));
        }
    }
}

/// <summary>
/// The top left of the window, like Maestro's white tab with the round check: a white tab with a round right end and a
/// shadow, holding the app logo on a round blue disc, the program name and one line below it. It replaces the blue
/// banner of 1.x. When the window is narrow the line below the name ends in "…".
/// </summary>
internal class TitleTab : Control
{
    private const int LeftPadding = 14;
    private const int DiscSize = 50;
    private const int TextGap = 14;
    private const int RightPadding = 34;
    private const int TabBottom = 8; // room below the tab, for its shadow

    private Font? _titleFont;
    private Font? _subtitleFont;

    /// <summary>The line below the program name.</summary>
    internal string Subtitle = "";

    public TitleTab()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    // The name big and bold, the line below it a little smaller and bold (like DssLabelTemplate 2.0.0).
    internal Font TitleFont => _titleFont ??= Theme.CreateFont(Font, Font.SizeInPoints + 7, FontStyle.Bold);
    internal Font SubtitleFont => _subtitleFont ??= Theme.CreateFont(Font, Font.SizeInPoints - 1, FontStyle.Bold);

    protected override void OnFontChanged(EventArgs e)
    {
        _titleFont?.Dispose();
        _subtitleFont?.Dispose();
        _titleFont = null;
        _subtitleFont = null;
        base.OnFontChanged(e);
    }

    /// <summary>The width of the white tab when everything fits, in pixels.</summary>
    internal int WidthNeeded()
    {
        int title = TextRenderer.MeasureText(Text, TitleFont, new Size(int.MaxValue, int.MaxValue), TextFlags).Width;
        int subtitle = TextRenderer.MeasureText(Subtitle, SubtitleFont, new Size(int.MaxValue, int.MaxValue), TextFlags).Width;
        return LogicalToDeviceUnits(LeftPadding + DiscSize + TextGap + RightPadding) + Math.Max(title, subtitle);
    }

    /// <summary>The white tab: as wide as its content, or as the control when that is narrower.</summary>
    internal Rectangle TabBounds()
    {
        return new Rectangle(0, 0, Math.Min(Width, WidthNeeded()), Math.Max(1, Height - LogicalToDeviceUnits(TabBottom)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.TopBar);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // The tab: square on the left (it starts at the window's edge), a half circle on the right.
        Rectangle tab = TabBounds();
        using (var path = new GraphicsPath())
        {
            float diameter = tab.Height;
            path.AddLine(tab.Left - 1, tab.Top, tab.Right - diameter / 2, tab.Top);
            path.AddArc(tab.Right - diameter, tab.Top, diameter, diameter, 270, 180);
            path.AddLine(tab.Right - diameter / 2, tab.Bottom, tab.Left - 1, tab.Bottom);
            path.CloseFigure();
            MaestroDrawing.FillShadow(g, path, LogicalToDeviceUnits(3), 60);
            using var white = new SolidBrush(Theme.White);
            g.FillPath(white, path);
        }

        // The logo on its round blue disc; the ring around the badge in the disc's blue.
        float disc = LogicalToDeviceUnits(DiscSize);
        var circle = new RectangleF(LogicalToDeviceUnits(LeftPadding), tab.Top + (tab.Height - disc) / 2f, disc, disc);
        MaestroDrawing.FillDisc(g, circle, Theme.LogoDiscLight, Theme.LogoDiscMiddle, Theme.LogoDiscEdge);
        AppIcons.DrawLogo(g, RectangleF.Inflate(circle, -disc * 0.16f, -disc * 0.16f), Theme.LogoDiscMiddle);

        // The name and the line below it, together in the middle of the tab's height.
        int textLeft = (int)(circle.Right + LogicalToDeviceUnits(TextGap));
        int textWidth = Math.Max(0, tab.Right - LogicalToDeviceUnits(RightPadding) / 2 - textLeft);
        int titleHeight = TitleFont.Height, subtitleHeight = SubtitleFont.Height;
        int top = tab.Top + (tab.Height - titleHeight - subtitleHeight) / 2;
        TextRenderer.DrawText(g, Text, TitleFont, new Rectangle(textLeft, top, textWidth, titleHeight), Theme.ButtonNavy, TextFlags);
        TextRenderer.DrawText(g, Subtitle, SubtitleFont, new Rectangle(textLeft, top + titleHeight, textWidth, subtitleHeight), Theme.Text, TextFlags);
    }

    // NoPadding: without it Windows puts room before a text that grows with the font, so the big name started further
    // right than the line below it. Now both start at the same left edge.
    private const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _subtitleFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// A white tile, like Maestro's speed tiles: a title, a big value and two small lines (e.g. "Status" / "Actief" /
/// "Bewaakt Z:\TopSolid\Export\" / "elke 30 s en bij elke wijziging"). Its height follows its fonts.
/// </summary>
internal class InfoTile : Control
{
    private const int PaddingX = 16;
    private const int PaddingY = 12;
    private const int LineGap = 3;

    private string _caption = "", _value = "", _line1 = "", _line2 = "";
    private Color _valueColor = Theme.TileValue;
    private Font? _captionFont;
    private Font? _valueFont;

    public InfoTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        BackColor = Theme.White;
    }

    // (The two attributes tell the Visual Studio designer to ignore these properties.)

    /// <summary>The title of the tile ("Status").</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string Caption { get => _caption; set { _caption = value; Invalidate(); } }

    /// <summary>The big value ("Actief").</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string Value { get => _value; set { _value = value; Invalidate(); } }

    /// <summary>The colour of the big value: light blue, red for an error.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Color ValueColor { get => _valueColor; set { _valueColor = value; Invalidate(); } }

    /// <summary>The first small line.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string Line1 { get => _line1; set { _line1 = value; Invalidate(); } }

    /// <summary>The second small line; may be empty.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string Line2 { get => _line2; set { _line2 = value; Invalidate(); } }

    /// <summary>
    /// On how many lines the second small line may wrap (1 = one line that ends in "…"). The Last conversion tile uses
    /// more (Daan, 2026-11-22: a message must be readable, not "…" after a few words). The text then also uses any extra
    /// height of the tile, and the room of the first line when that one is empty; only what still does not fit ends in "…".
    /// </summary>
    internal int Line2Lines = 1;

    private const int RingSize = 36;   // the countdown ring, at 100%
    private const int RingWidth = 4;   // the thickness of its line
    private const int RingGap = 10;    // between the title / value and the ring
    private (double Remaining, double Total)? _countdown;
    private Font? _countdownFont;

    /// <summary>
    /// A countdown in the top right corner (the Status tile: the next scan, Daan 2.0.0): a navy ring that empties as the
    /// time runs out, the seconds left in the middle. Null = no ring.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal (double Remaining, double Total)? Countdown
    {
        get => _countdown;
        set
        {
            if (_countdown != value)
            {
                // Only the ring changes 10 times a second: repaint only its corner, unless the ring appears or goes
                // (the title and the value get another width) or the last picture was not finished.
                bool wholeTile = _countdown == null || value == null || _paintFailed;
                _countdown = value;
                _paintFailed = false;
                if (wholeTile)
                {
                    Invalidate();
                }
                else
                {
                    Invalidate(RingBounds());
                }
            }
        }
    }

    /// <summary>The number in the ring: the whole seconds left (rounded up), or minutes ("5m") from 100 s on, so it fits.</summary>
    internal static string CountdownText(double remaining)
    {
        int seconds = double.IsFinite(remaining) ? (int)Math.Ceiling(Math.Clamp(remaining, 0, 1_000_000)) : 0;
        return seconds < 100 ? seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                             : ((seconds + 59) / 60).ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
    }

    /// <summary>The place of the countdown ring, in this tile's pixels (the tests look at it).</summary>
    internal Rectangle RingBounds()
    {
        int size = LogicalToDeviceUnits(RingSize);
        return new Rectangle(Width - LogicalToDeviceUnits(PaddingX) - size, LogicalToDeviceUnits(PaddingY), size, size);
    }

    private Font CountdownFont => _countdownFont ??= Theme.CreateFont(Font, Font.SizeInPoints, FontStyle.Bold);

    internal Font CaptionFont => _captionFont ??= Theme.CreateFont(Font, Font.SizeInPoints + 1);
    internal Font ValueFont => _valueFont ??= Theme.CreateFont(Font, Font.SizeInPoints + 6, FontStyle.Bold);

    /// <summary>The height of the tile for its fonts: the title, the value and the small lines, in pixels.</summary>
    internal int HeightNeeded() =>
        2 * LogicalToDeviceUnits(PaddingY) + CaptionFont.Height + ValueFont.Height + (1 + Math.Max(1, Line2Lines)) * Font.Height + 3 * LogicalToDeviceUnits(LineGap);

    protected override void OnFontChanged(EventArgs e)
    {
        _captionFont?.Dispose();
        _valueFont?.Dispose();
        _countdownFont?.Dispose();
        _captionFont = null;
        _valueFont = null;
        _countdownFont = null;
        base.OnFontChanged(e);
    }

    /// <summary>
    /// Paints the tile. GDI+ can refuse a drawing now and then (alpha.6 on Daan's PC: an ExternalException "An object could
    /// not be created" in the countdown ring). An exception that leaves OnPaint makes WinForms give up on the control: a red
    /// cross until the window is built again. So such a refusal only skips this one picture; the whole tile is painted
    /// again at the next change of the countdown (10 times a second) or the next normal repaint.
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            PaintTile(e.Graphics);
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.ExternalException || error is OutOfMemoryException ||
                                      error is ArgumentException || error is InvalidOperationException)
        {
            _paintFailed = true;
        }
    }

    private bool _paintFailed; // the last picture was not finished: paint the whole tile again

    private void PaintTile(Graphics g)
    {
        g.Clear(Theme.White);
        const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

        int left = LogicalToDeviceUnits(PaddingX), width = Math.Max(0, Width - 2 * left), gap = LogicalToDeviceUnits(LineGap);
        int top = LogicalToDeviceUnits(PaddingY);

        // The title and the value stop before the ring; the small lines below it have the whole width.
        int topWidth = width;
        if (_countdown is { } countdown)
        {
            Rectangle ring = RingBounds();
            topWidth = Math.Max(0, ring.Left - LogicalToDeviceUnits(RingGap) - left);
            DrawCountdown(g, ring, countdown.Remaining, countdown.Total);
        }

        TextRenderer.DrawText(g, _caption, CaptionFont, new Rectangle(left, top, topWidth, CaptionFont.Height), Theme.Text, flags);
        top += CaptionFont.Height + gap;
        TextRenderer.DrawText(g, _value, ValueFont, new Rectangle(left, top, topWidth, ValueFont.Height), _valueColor, flags | TextFormatFlags.VerticalCenter);
        top += ValueFont.Height + gap;
        if (Line2Lines <= 1)
        {
            // A long path in the first line ends in "…" in the middle of the path, so its end (the folder name) stays visible.
            TextRenderer.DrawText(g, _line1, Font, new Rectangle(left, top, width, Font.Height), Theme.TileLine,
                TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
            top += Font.Height + gap;
            TextRenderer.DrawText(g, _line2, Font, new Rectangle(left, top, width, Font.Height), Theme.TileLine, flags);
            return;
        }

        if (_line1 != "")
        {
            TextRenderer.DrawText(g, _line1, Font, new Rectangle(left, top, width, Font.Height), Theme.TileLine, flags);
            top += Font.Height + gap;
        }

        // The rest of the tile, in whole lines. TextBoxControl: no half-visible last line, and "…" at the end of the
        // last whole line when the text is longer (also a long word without spaces, such as a path, is broken).
        int lines = Math.Max(1, (Height - LogicalToDeviceUnits(PaddingY) - top) / Font.Height);
        TextRenderer.DrawText(g, _line2, Font, new Rectangle(left, top, width, lines * Font.Height), Theme.TileLine,
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    /// <summary>
    /// The countdown ring (like Daan's picture: a navy ring with the number in it): a pale full circle, over it the navy
    /// part that is still left, from twelve o'clock clockwise, so it empties towards the top as the time runs out.
    /// </summary>
    private void DrawCountdown(Graphics g, Rectangle ring, double remaining, double total)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float thickness = LogicalToDeviceUnits(RingWidth);
        RectangleF circle = RectangleF.Inflate(ring, -thickness / 2, -thickness / 2);
        using (var track = new Pen(Theme.GridLine, thickness))
        {
            g.DrawEllipse(track, circle);
        }

        // No arc under 1 degree: GDI+ refuses (some) very small arcs with round ends ("An object could not be created",
        // alpha.6, just before the ring reaches 0), and so little navy is invisible anyway.
        double part = total <= 0 ? 0 : Math.Clamp(remaining / total, 0, 1);
        if (double.IsFinite(part) && 360 * part >= 1)
        {
            using var rest = new Pen(Theme.ButtonNavy, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(rest, circle, -90, (float)(360 * part));
        }

        TextRenderer.DrawText(g, CountdownText(remaining), CountdownFont, ring, Theme.ButtonNavy,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        g.SmoothingMode = SmoothingMode.Default;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captionFont?.Dispose();
            _valueFont?.Dispose();
            _countdownFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// The bar at the bottom of the window, like Maestro's: a blue band (a gradient, with a round right end) holding the round
/// buttons (scan now, retry failed, pause, open log folder) and, on the grey to the right of it, Cancel and Save. The
/// buttons are placed here in code (<see cref="OnLayout"/>), not by a table: the band's end depends on where Cancel is.
/// </summary>
internal class BottomBar : Control, IPaintsBackgroundForChildren
{
    /// <summary>The height of the bar at 100%.</summary>
    public const int BarHeight = 100;

    private const int PillHeight = 50;  // Save and Cancel, at 100%
    private const int Padding24 = 24;   // at the right end, and between the band and Cancel
    private const int PillGap = 14;     // between Cancel and Save
    private const int EndGap = 24;      // at least this much room between the last left button and the one at the right end

    private readonly BarButton[] _buttons;
    private readonly BarButton? _end;
    private readonly PillButton _cancel, _save;

    /// <param name="buttons">The round buttons from the left edge.</param>
    /// <param name="end">One round button at the right end of the band, just before its round end (the material list).</param>
    public BottomBar(BarButton[] buttons, BarButton? end, PillButton cancel, PillButton save)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        (_buttons, _end, _cancel, _save) = (buttons, end, cancel, save);
        Controls.AddRange(buttons);
        if (end != null)
        {
            Controls.Add(end);
        }

        Controls.Add(cancel);
        Controls.Add(save);
    }

    /// <summary>The narrowest the band may be: the left buttons, the room, the button at the end and the round end.</summary>
    private int SmallestBandRight(int leftButtonsRight) =>
        _end == null ? leftButtonsRight + Height / 2 : leftButtonsRight + LogicalToDeviceUnits(EndGap) + Slot + Height / 2;

    /// <summary>Where the blue band ends (its round end included), in the bar's pixels.</summary>
    internal int BandRight { get; private set; }

    /// <summary>The width of one round button = the distance between two discs, in pixels.</summary>
    private int Slot => LogicalToDeviceUnits(BarButton.SlotWidth);

    /// <summary>The narrowest width at which nothing overlaps: the round buttons, the round end, Cancel and Save.</summary>
    internal int WidthNeeded()
    {
        int pad = LogicalToDeviceUnits(Padding24);
        int buttons = _buttons.Length * Slot;
        int height = LogicalToDeviceUnits(BarHeight);
        int end = _end == null ? 0 : LogicalToDeviceUnits(EndGap) + Slot;
        int pillHeight = LogicalToDeviceUnits(PillHeight);
        _cancel.Height = pillHeight;
        _save.Height = pillHeight;
        return buttons + end + height / 2 + pad + _cancel.WidthNeeded() + LogicalToDeviceUnits(PillGap) + _save.WidthNeeded() + pad;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int pad = LogicalToDeviceUnits(Padding24);
        int middle = Height / 2;

        // Save against the right edge, Cancel left of it.
        int pillHeight = LogicalToDeviceUnits(PillHeight);
        _save.Height = pillHeight;
        _cancel.Height = pillHeight;
        _save.Width = _save.WidthNeeded();
        _cancel.Width = _cancel.WidthNeeded();
        _save.Location = new Point(Width - pad - _save.Width, middle - pillHeight / 2);
        _cancel.Location = new Point(_save.Left - LogicalToDeviceUnits(PillGap) - _cancel.Width, _save.Top);

        // The round buttons from the left edge, all as wide as one slot, so their discs are always the same distance apart
        // (Daan, 2026-11-22: in French the long names pushed them apart). All as high as the one whose name needs the most
        // lines, so the discs stay in one line.
        int slot = Slot;
        BarButton[] round = _end == null ? _buttons : _buttons.Append(_end).ToArray();
        int lines = round.Length == 0 ? 1 : round.Max(button => button.CaptionLines(slot));
        int x = 0;
        foreach (BarButton button in _buttons)
        {
            int height = button.HeightFor(lines);
            button.Bounds = new Rectangle(x, middle - height / 2, slot, height);
            x += slot;
        }

        int lastRight = _buttons.Length == 0 ? pad : _buttons[^1].Right;
        BandRight = Math.Max(SmallestBandRight(lastRight), _cancel.Left - pad);

        // The button at the end sits where the band's round end begins: its name stays on the blue (on the round end it
        // would run onto the grey, white on grey is not readable).
        if (_end != null)
        {
            int height = _end.HeightFor(lines);
            _end.Bounds = new Rectangle(BandRight - Height / 2 - slot, middle - height / 2, slot, height);
        }

        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        DrawBar(e.Graphics);
    }

    /// <summary>The bar's background inside one of its buttons: the same drawing, moved to the button's place.</summary>
    public void PaintBackgroundFor(Control child, Graphics g)
    {
        GraphicsState state = g.Save();
        g.TranslateTransform(-child.Left, -child.Top);
        DrawBar(g);
        g.Restore(state);
    }

    /// <summary>The grey bar with the blue band (a gradient, its right end a half circle).</summary>
    private void DrawBar(Graphics g)
    {
        using (var grey = new SolidBrush(Theme.WorkArea))
        {
            g.FillRectangle(grey, 0, 0, Width, Height); // not Clear: that would ignore the move for a child
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;

        float diameter = Height;
        using var band = new GraphicsPath();
        band.AddLine(-1, 0, BandRight - diameter / 2, 0);
        band.AddArc(BandRight - diameter, 0, diameter, diameter - 1, 270, 180);
        band.AddLine(BandRight - diameter / 2, Height, -1, Height);
        band.CloseFigure();
        using var gradient = new LinearGradientBrush(new Point(0, -1), new Point(0, Height + 1), Theme.BarTop, Theme.BarBottom);
        g.FillPath(gradient, band);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Everything is in the background (the buttons on it paint themselves).
    }
}

/// <summary>
/// The list of conversions in the table look of the mockup: grey column titles in bold, white rows with a thin line
/// between them, the chosen row in light blue, an error row in red (not bold since 2.0.0-alpha.5, Daan). Drawn by the list itself (OwnerDraw); the
/// items are still normal ListViewItems (the window and the tests fill and read them as before).
/// </summary>
internal class HistoryListView : ListView
{
    private const int TextPadding = 10; // left of the text in every cell, at 100%
    private Font? _boldFont;

    public HistoryListView()
    {
        OwnerDraw = true;
        DoubleBuffered = true; // no flicker while the list is refreshed
    }

    private Font BoldFont => _boldFont ??= new Font(Font, FontStyle.Bold);

    protected override void OnFontChanged(EventArgs e)
    {
        _boldFont?.Dispose();
        _boldFont = null;
        base.OnFontChanged(e);
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using (var fill = new SolidBrush(Theme.Field))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        var text = Rectangle.FromLTRB(e.Bounds.Left + LogicalToDeviceUnits(TextPadding), e.Bounds.Top, e.Bounds.Right, e.Bounds.Bottom);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, BoldFont, text, Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        // Nothing: every cell is drawn in OnDrawSubItem.
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null || e.SubItem == null)
        {
            return;
        }

        bool selected = e.Item.Selected;
        bool error = e.Item.ForeColor == Theme.Error;
        using (var fill = new SolidBrush(selected ? Theme.Selection : Theme.White))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        using (var line = new SolidBrush(Theme.GridLine))
        {
            e.Graphics.FillRectangle(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Width, 1);
        }

        // Red on the light-blue selection is too pale (2:1): a chosen row is navy, also an error row.
        Color ink = selected ? Theme.ButtonNavy : error ? Theme.Error : Theme.TableText;
        var text = Rectangle.FromLTRB(e.Bounds.Left + LogicalToDeviceUnits(TextPadding), e.Bounds.Top, e.Bounds.Right, e.Bounds.Bottom);
        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font, text, ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boldFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// A label that writes some parts of its text in bold (Daan, 2026-11-22: "-LI.xlsx", "-LP.xlsx" and "(DESC1 – DESC10)" in
/// the hints of the folders panel). <see cref="BoldParts"/> are pieces of the text that are the same in every language, so
/// the texts in Strings stay plain sentences. It wraps onto more lines like a normal label, word by word, and its
/// <see cref="GetPreferredSize"/> gives the height for a width (the window sizes the hints with it, see FitHints).
/// </summary>
internal class BoldPartsLabel : Label
{
    private const TextFormatFlags Flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    private Font? _boldFont;

    /// <summary>The pieces of the text to write in bold (each one wherever it appears).</summary>
    internal string[] BoldParts = Array.Empty<string>();

    internal Font BoldFont => _boldFont ??= new Font(Font, FontStyle.Bold);

    private int LineHeight => Math.Max(Font.Height, BoldFont.Height);

    /// <summary>The words of the text: each with whether it is bold and whether a space comes before it.</summary>
    internal List<(string Word, bool Bold, bool SpaceBefore)> Words()
    {
        var words = new List<(string, bool, bool)>();
        string text = Text;
        bool space = false;
        int i = 0;
        while (i < text.Length)
        {
            // The first bold part from here on.
            int start = -1;
            string? part = null;
            foreach (string candidate in BoldParts)
            {
                int at = candidate.Length == 0 ? -1 : text.IndexOf(candidate, i, StringComparison.Ordinal);
                if (at >= 0 && (start < 0 || at < start))
                {
                    start = at;
                    part = candidate;
                }
            }

            int end = part == null ? text.Length : start;
            AddWords(text.Substring(i, end - i), false);
            if (part == null)
            {
                break;
            }

            // A bold part is one "word": it is never split over two lines ("(DESC1 –" / "DESC10)" looked broken).
            words.Add((part, true, space || (start > 0 && char.IsWhiteSpace(text[start - 1]))));
            space = false;
            i = start + part.Length;
        }

        return words;

        void AddWords(string piece, bool bold)
        {
            int k = 0;
            while (k < piece.Length)
            {
                if (char.IsWhiteSpace(piece[k]))
                {
                    space = true;
                    k++;
                    continue;
                }

                int wordStart = k;
                while (k < piece.Length && !char.IsWhiteSpace(piece[k]))
                {
                    k++;
                }

                words.Add((piece.Substring(wordStart, k - wordStart), bold, space));
                space = false;
            }
        }
    }

    /// <summary>Where every word goes for this width: its place and font. Returns the number of lines and the widest line.</summary>
    private (int Lines, int Widest) PlaceWords(int width, Action<string, Font, Point>? draw)
    {
        int spaceWidth = TextRenderer.MeasureText("x x", Font, Size.Empty, Flags).Width - TextRenderer.MeasureText("xx", Font, Size.Empty, Flags).Width;
        int x = 0, line = 0, widest = 0;
        foreach (var (word, bold, spaceBefore) in Words())
        {
            Font font = bold ? BoldFont : Font;
            int wordWidth = TextRenderer.MeasureText(word, font, Size.Empty, Flags).Width;
            int start = x > 0 && spaceBefore ? x + spaceWidth : x;
            if (x > 0 && start + wordWidth > width)
            {
                line++; // does not fit: on the next line (a word longer than the whole line stays alone on its line)
                start = 0;
            }

            draw?.Invoke(word, font, new Point(start, line * LineHeight));
            x = start + wordWidth;
            widest = Math.Max(widest, x);
        }

        return (x == 0 && line == 0 ? 0 : line + 1, widest);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 1 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : int.MaxValue;
        var (lines, widest) = PlaceWords(width, null);
        return new Size(widest + Padding.Horizontal, Math.Max(1, lines) * LineHeight + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Not base.OnPaint: that would write the whole text in one font. The background is painted as usual.
        Graphics g = e.Graphics;
        PlaceWords(Math.Max(1, ClientSize.Width - Padding.Horizontal), (word, font, place) =>
            TextRenderer.DrawText(g, word, font, new Point(place.X + Padding.Left, place.Y + Padding.Top), ForeColor, Flags));
    }

    protected override void OnFontChanged(EventArgs e)
    {
        _boldFont?.Dispose();
        _boldFont = null;
        base.OnFontChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boldFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
