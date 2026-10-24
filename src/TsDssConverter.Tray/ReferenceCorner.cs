using System.Drawing.Drawing2D;

namespace TsDssConverter.Tray;

/// <summary>
/// Which corner of the panel the label X/Y position is measured from. This mirrors the "Oriëntatie" drop-down in
/// Duivestein's own DSSClient (Linksonder / Linksboven / Rechtsonder / Rechtsboven), so the customer can pick the
/// same corner here as in DSSClient, without doing any flip arithmetic themselves.
///
/// Internally this is still just AppSettings.FlipX / FlipY (unchanged, so old settings.json files keep working):
/// TopSolid always measures from the bottom-left, so BottomLeft needs no flip and the other three corners flip X,
/// Y or both. See <see cref="ReferenceCornerMapping"/>.
/// </summary>
public enum ReferenceCorner
{
    BottomLeft,
    TopLeft,
    BottomRight,
    TopRight,
}

/// <summary>Converts between <see cref="ReferenceCorner"/> and the FlipX / FlipY booleans that AppSettings stores.</summary>
internal static class ReferenceCornerMapping
{
    public static ReferenceCorner FromFlips(bool flipX, bool flipY)
    {
        if (flipX && flipY) return ReferenceCorner.TopRight;
        if (flipX) return ReferenceCorner.BottomRight;
        if (flipY) return ReferenceCorner.TopLeft;
        return ReferenceCorner.BottomLeft;
    }

    public static (bool FlipX, bool FlipY) ToFlips(ReferenceCorner corner)
    {
        return corner switch
        {
            ReferenceCorner.TopLeft => (false, true),
            ReferenceCorner.BottomRight => (true, false),
            ReferenceCorner.TopRight => (true, true),
            _ => (false, false), // BottomLeft
        };
    }
}

/// <summary>
/// A small schematic next to the reference-corner drop-down: a "sheet" (dark blue) with a marker at each of its four
/// corners (light blue). The corner that matches <see cref="SelectedCorner"/> is drawn bigger and in the brand colour,
/// so the user sees at a glance which corner is selected, without reading the drop-down text. Purely decorative
/// (no Click, no tab stop): the drop-down itself is still what is saved.
/// </summary>
internal class ReferenceCornerPreview : Control
{
    // Everything below is at 100% scaling; LogicalToDeviceUnits converts it when painting (same as FluentControls.cs).
    private const int PanelMargin = 14;  // room around the "sheet" rectangle, for the corner markers to sit in
    private const int SmallMarker = 9;   // the three corners that are not selected
    private const int BigMarker = 21;    // the selected corner: 1/3 bigger than the first version (was 16, Daan 2026-09-23)

    private ReferenceCorner _selectedCorner = ReferenceCorner.BottomLeft;

    public ReferenceCornerPreview()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false; // decorative: nothing to focus
    }

    /// <summary>Which corner to draw bigger. Set this whenever the drop-down's selection changes.</summary>
    [System.ComponentModel.Browsable(false)] // not a designer property: set only from code (SettingsForm)
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ReferenceCorner SelectedCorner
    {
        get => _selectedCorner;
        set
        {
            if (_selectedCorner != value)
            {
                _selectedCorner = value;
                Invalidate();
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(FluentDrawing.BehindColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int margin = LogicalToDeviceUnits(PanelMargin);
        var sheet = new RectangleF(margin, margin, Width - 2 * margin, Height - 2 * margin);
        if (sheet.Width <= 0 || sheet.Height <= 0)
        {
            return; // the control is too small to draw meaningfully (should not happen: it has a fixed size)
        }

        using (var sheetBrush = new SolidBrush(Theme.DarkBlue))
        {
            g.FillRectangle(sheetBrush, sheet);
        }

        // The four corners of the sheet, matched to the ReferenceCorner they represent.
        var corners = new (ReferenceCorner Corner, PointF Point)[]
        {
            (ReferenceCorner.BottomLeft, new PointF(sheet.Left, sheet.Bottom)),
            (ReferenceCorner.TopLeft, new PointF(sheet.Left, sheet.Top)),
            (ReferenceCorner.BottomRight, new PointF(sheet.Right, sheet.Bottom)),
            (ReferenceCorner.TopRight, new PointF(sheet.Right, sheet.Top)),
        };

        using var markerBrush = new SolidBrush(Theme.LightBlue);
        using var selectedBrush = new SolidBrush(Theme.Brand);

        foreach (var (corner, point) in corners)
        {
            bool selected = corner == _selectedCorner;
            int size = LogicalToDeviceUnits(selected ? BigMarker : SmallMarker);
            var marker = new RectangleF(point.X - size / 2f, point.Y - size / 2f, size, size);
            g.FillRectangle(selected ? selectedBrush : markerBrush, marker);
        }
    }
}
