namespace TsDssConverter.Tray;

/// <summary>Loads the icons and the banner that are built into the exe (from the media folder).</summary>
internal static class AppIcons
{
    /// <summary>The icon for the tray, in the size that fits the current screen scaling.</summary>
    public static Icon ForState(TrayState state)
    {
        string name = state switch
        {
            TrayState.Busy => "app-busy.ico",
            TrayState.Error => "app-error.ico",
            TrayState.Paused => "app-paused.ico",
            _ => "app.ico",
        };

        using var stream = OpenResource(name);
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    /// <summary>The icon for the window (title bar, taskbar): all sizes, Windows picks the right one.</summary>
    public static Icon ForWindow()
    {
        using var stream = OpenResource("app.ico");
        return new Icon(stream);
    }

    /// <summary>
    /// The logo (three sheets with nested parts, and the round "convert" badge), drawn in code so it is sharp at every
    /// size: the title tab of the window shows it on a blue disc. The shapes are those of media/header.svg (the area
    /// x 26 – 240, y 46 – 240 of its drawing). <paramref name="gapColour"/> is the ring around the badge: the colour
    /// behind the logo, so the badge looks cut out of the sheets.
    /// </summary>
    public static void DrawLogo(Graphics g, RectangleF box, Color gapColour)
    {
        const float left = 26, top = 46, size = 214, height = 194;
        float scale = Math.Min(box.Width / size, box.Height / height);
        var state = g.Save();
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TranslateTransform(box.Left + (box.Width - size * scale) / 2, box.Top + (box.Height - height * scale) / 2);
        g.ScaleTransform(scale, scale);
        g.TranslateTransform(-left, -top);

        void Fill(Color colour, params float[] xy)
        {
            var points = new PointF[xy.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
            }

            using var brush = new SolidBrush(colour);
            g.FillPolygon(brush, points);
        }

        // Three sheets, the bottom one first: front, side, top.
        for (int layer = 0; layer < 3; layer++)
        {
            float up = -20f * layer;
            Fill(Theme.VeryPaleBlue, 32, 134 + up, 116, 176 + up, 116, 190 + up, 32, 148 + up);
            Fill(Theme.PaleBlue, 116, 176 + up, 200, 134 + up, 200, 148 + up, 116, 190 + up);
            Fill(layer == 2 ? Theme.White : Theme.DarkBlue, 32, 134 + up, 116, 92 + up, 200, 134 + up, 116, 176 + up);
        }

        // The nested parts on the top sheet (the sheet's own skewed grid).
        var sheet = g.Transform;
        using (var skew = new System.Drawing.Drawing2D.Matrix(84, -42, 84, 42, 32, 94))
        {
            g.MultiplyTransform(skew);
        }

        using (var parts = new SolidBrush(Theme.LightBlue))
        {
            g.FillRectangle(parts, 0.07f, 0.07f, 0.51f, 0.35f);
            g.FillRectangle(parts, 0.63f, 0.07f, 0.30f, 0.45f);
            g.FillRectangle(parts, 0.07f, 0.47f, 0.26f, 0.46f);
            g.FillRectangle(parts, 0.38f, 0.47f, 0.20f, 0.23f);
            g.FillRectangle(parts, 0.38f, 0.75f, 0.20f, 0.18f);
            g.FillRectangle(parts, 0.63f, 0.57f, 0.30f, 0.36f);
        }

        g.Transform = sheet;

        // The badge: a gap in the colour behind the logo, the navy disc and the white arrow going round.
        using (var gap = new SolidBrush(gapColour))
        {
            g.FillEllipse(gap, 190 - 44, 190 - 44, 88, 88);
        }

        using (var navy = new SolidBrush(Theme.ButtonNavy))
        {
            g.FillEllipse(navy, 190 - 37, 190 - 37, 74, 74);
        }

        using (var arc = new Pen(Theme.White, 7.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
        {
            g.DrawArc(arc, 190 - 17, 190 - 17, 34, 34, -50f, 253f);
        }

        var head = new[] { new PointF(181.24f, 172.88f), new PointF(165.88f, 177.38f), new PointF(182.26f, 188.85f) };
        using (var white = new SolidBrush(Theme.White))
        {
            g.FillPolygon(white, head);
        }

        g.Restore(state);
    }

    /// <summary>
    /// The small logo of ROGIERS in the top bar. The picture file has a transparent margin around the logo; that is
    /// cut off, so the logo itself lines up with the other controls. It is made smaller once, with high quality:
    /// the window then only scales a small picture (a big picture scaled down by a PictureBox looks jagged).
    /// </summary>
    public static Image CompanyLogo()
    {
        return TrimmedAndScaled("rogiers-logo.png", 128); // enough for a screen at 250%: shown about 34 pixels wide at 100%
    }

    /// <summary>A built-in picture without its transparent margin, made smaller once (high quality) to the given long side.</summary>
    private static Image TrimmedAndScaled(string resourceName, int longSide)
    {
        using var stream = OpenResource(resourceName);
        using var original = new Bitmap(stream);
        Rectangle visible = FindVisibleArea(original);

        double scale = (double)longSide / Math.Max(visible.Width, visible.Height);
        var logo = new Bitmap((int)Math.Round(visible.Width * scale), (int)Math.Round(visible.Height * scale));

        using var graphics = Graphics.FromImage(logo);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.DrawImage(original, new Rectangle(Point.Empty, logo.Size), visible, GraphicsUnit.Pixel);
        return logo;
    }

    /// <summary>The smallest rectangle that holds every pixel that is not (almost) fully transparent.</summary>
    private static Rectangle FindVisibleArea(Bitmap picture)
    {
        var all = new Rectangle(0, 0, picture.Width, picture.Height);
        var data = picture.LockBits(all, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            int left = picture.Width, top = picture.Height, right = -1, bottom = -1;
            var row = new byte[data.Stride];

            for (int y = 0; y < picture.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);

                for (int x = 0; x < picture.Width; x++)
                {
                    bool visiblePixel = row[x * 4 + 3] > 16; // byte 3 of a pixel (B, G, R, A) is the alpha: 0 = transparent
                    if (visiblePixel)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            // A picture without any visible pixel: use it whole.
            return right < 0 ? all : new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
        finally
        {
            picture.UnlockBits(data);
        }
    }

    private static Stream OpenResource(string name)
    {
        return typeof(AppIcons).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Ingebouwd bestand niet gevonden: " + name);
    }
}
