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

    /// <summary>The banner with the name of the program (2x version, scaled down: sharp on high-DPI screens).</summary>
    public static Image Banner()
    {
        using var stream = OpenResource("header.png");
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return Image.FromStream(copy); // the MemoryStream must stay open as long as the image is used
    }

    /// <summary>
    /// The small logo of ROGIERS for the footer. The picture file has a transparent margin around the logo; that is
    /// cut off, so the logo itself lines up with the other controls. It is made smaller once, with high quality:
    /// the window then only scales a small picture (a big picture scaled down by a PictureBox looks jagged).
    /// </summary>
    public static Image CompanyLogo()
    {
        const int LongSide = 128; // enough for a screen at 250%: the logo is shown about 44 pixels wide at 100%

        using var stream = OpenResource("rogiers-logo.png");
        using var original = new Bitmap(stream);
        Rectangle visible = FindVisibleArea(original);

        double scale = (double)LongSide / Math.Max(visible.Width, visible.Height);
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
