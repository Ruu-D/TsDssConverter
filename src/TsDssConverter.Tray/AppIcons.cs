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

    // The banner is drawn at twice its size at 100% (1080 x 168 = 2 x 540 x 84) and scaled down by the window, so it
    // stays sharp on high-DPI screens. It is drawn exactly like the banner of the sister program DssLabelTemplate (same
    // fonts, sizes and places), so the two programs look alike.
    private const int BannerPictureWidth = 1080;
    private const int BannerPictureHeight = 168;
    private const int BannerLogoWidth = 180;     // the logo is the left part of header@2x.png; its old text starts at x = 194
    private const int BannerTextLeft = 194;      // where the title starts (the same in DssLabelTemplate)

    /// <summary>
    /// The banner: the logo on the brand colour, with the name of the program and one line below it. The text is in
    /// English in every language. Only the logo is taken from the picture header@2x.png (whose text is not used any more).
    /// </summary>
    public static Image Banner()
    {
        var banner = new Bitmap(BannerPictureWidth, BannerPictureHeight);
        using var graphics = Graphics.FromImage(banner);
        graphics.Clear(Theme.Brand);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using (var stream = OpenResource("header.png"))
        using (var picture = new Bitmap(stream))
        {
            var logo = new Rectangle(0, 0, BannerLogoWidth, BannerPictureHeight);
            graphics.DrawImage(picture, logo, logo, GraphicsUnit.Pixel); // 1 to 1: the picture is already 1080 x 168
        }

        // The same places as in DssLabelTemplate: the title's baseline at y = 82, the subtitle's at y = 128.
        using var titleFont = Theme.CreateBannerTitleFont(54);
        using var subtitleFont = Theme.CreateBannerSubtitleFont(27);
        using var navy = new SolidBrush(Theme.Navy);
        DrawOnBaseline(graphics, Strings.AppName, titleFont, navy, BannerTextLeft, 82);
        DrawOnBaseline(graphics, Strings.BannerSubtitle, subtitleFont, navy, BannerTextLeft + 2, 128);
        return banner;
    }

    /// <summary>Draws a text so that the bottom of its letters (the baseline) is on the given y.</summary>
    private static void DrawOnBaseline(Graphics graphics, string text, Font font, Brush brush, float left, float baseline)
    {
        FontFamily family = font.FontFamily;
        float ascent = font.Size * family.GetCellAscent(font.Style) / family.GetEmHeight(font.Style);
        graphics.DrawString(text, font, brush, left, baseline - ascent, StringFormat.GenericTypographic);
    }

    /// <summary>
    /// The small logo of ROGIERS for the footer. The picture file has a transparent margin around the logo; that is
    /// cut off, so the logo itself lines up with the other controls. It is made smaller once, with high quality:
    /// the window then only scales a small picture (a big picture scaled down by a PictureBox looks jagged).
    /// </summary>
    public static Image CompanyLogo()
    {
        return TrimmedAndScaled("rogiers-logo.png", 128); // enough for a screen at 250%: shown about 44 pixels wide at 100%
    }

    /// <summary>
    /// The small "user guide" icon (a book with a question mark) next to the app version. Made smaller once like the
    /// company logo: shown 16 pixels high at 100%, so 64 is enough for a screen at 400%.
    /// </summary>
    public static Image UserGuide()
    {
        return TrimmedAndScaled("user-guide.png", 64);
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
