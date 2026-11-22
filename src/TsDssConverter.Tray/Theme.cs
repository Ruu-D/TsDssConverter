namespace TsDssConverter.Tray;

/// <summary>
/// The colour theme of the application (see "Visual identity" in CLAUDE.md). All colours live here and nowhere else.
/// </summary>
internal static class Theme
{
    // ---- The colours of the icons (1.x). Since 2.0.0 the window uses the Maestro colours below; these stay for the
    //      app and tray icons, the logo on the title tab and the README pictures.
    public static readonly Color Brand = ColorTranslator.FromHtml("#2CABE2");         // main colour of the icons
    public static readonly Color DarkBlue = ColorTranslator.FromHtml("#1480B5");      // lines in the logo
    public static readonly Color LightBlue = ColorTranslator.FromHtml("#7CCAED");     // the parts on the logo's top sheet
    public static readonly Color PaleBlue = ColorTranslator.FromHtml("#A3D7EF");      // the sides of the logo's sheets
    public static readonly Color VeryPaleBlue = ColorTranslator.FromHtml("#D7EFFA");  // the fronts of the logo's sheets
    public static readonly Color Navy = ColorTranslator.FromHtml("#0A3A56");          // the badge of the icons
    public static readonly Color White = Color.White;

    /// <summary>The one red of the application, only for errors. It is not part of the icon palette.</summary>
    public static readonly Color Error = ColorTranslator.FromHtml("#D6322C");

    // ---- 2.0.0: the "Maestro" skin (Daan, 2026-11-21), the same colours as DssLabelTemplate 2.0.0. The customer works all
    //      day with the SCM machine software (Maestro): a light grey frame, white panels with blue titles, navy buttons, a
    //      blue bar at the bottom with round light-blue buttons. From the mockup Daan approved (claude.ai artifact
    //      "TsDssConverter Maestro skin").

    public static readonly Color Frame = ColorTranslator.FromHtml("#D9DADC");        // the window around the panels
    public static readonly Color TopBar = ColorTranslator.FromHtml("#DCDDDF");       // the bar at the top
    public static readonly Color WorkArea = ColorTranslator.FromHtml("#B8BCC0");     // the grey end of the bottom bar
    public static readonly Color Field = ColorTranslator.FromHtml("#EEEFF1");        // text boxes, drop-downs, table titles
    public static readonly Color GreyButton = ColorTranslator.FromHtml("#DCDFE3");   // Config, Open materials table, Clear list
    public static readonly Color GreyButtonHover = ColorTranslator.FromHtml("#C9CDD3");
    public static readonly Color ButtonNavy = ColorTranslator.FromHtml("#0B2C6B");   // Browse, the badge of the logo
    public static readonly Color ButtonNavyHover = ColorTranslator.FromHtml("#173F86");
    public static readonly Color ButtonNavyPressed = ColorTranslator.FromHtml("#052F61");
    public static readonly Color Text = ColorTranslator.FromHtml("#1D4486");         // titles and text (9:1 on white)
    public static readonly Color TextSoft = ColorTranslator.FromHtml("#3A5A8F");     // the hints, the subtitle (7:1 on white)
    public static readonly Color TableText = ColorTranslator.FromHtml("#1D3A6E");    // the rows of the list of conversions
    public static readonly Color Selection = ColorTranslator.FromHtml("#8EB3E4");    // the chosen row of the list
    public static readonly Color GridLine = ColorTranslator.FromHtml("#E1E4E8");     // lines between rows, the line between the halves
    public static readonly Color Separator = ColorTranslator.FromHtml("#B9BDC3");    // the short lines in the top bar
    public static readonly Color TileValue = ColorTranslator.FromHtml("#4E7FC6");    // the big value of a tile (bold, large: 4:1)
    public static readonly Color TileLine = ColorTranslator.FromHtml("#3F6FB5");     // the small lines of a tile (5:1)
    public static readonly Color BarTop = ColorTranslator.FromHtml("#4C7BC0");       // the blue bottom bar, top ...
    public static readonly Color BarBottom = ColorTranslator.FromHtml("#3561A6");    // ... and bottom of its gradient
    public static readonly Color Pill = ColorTranslator.FromHtml("#E4E7EC");         // Save and Cancel at the bottom right

    // The round buttons of the bottom bar: a light-blue disc (a little lighter at the top left) with a pale ring.
    public static readonly Color DiscSoftLight = ColorTranslator.FromHtml("#DCE7F6");
    public static readonly Color DiscLight = ColorTranslator.FromHtml("#F4F8FD");     // the light spot under the mouse
    public static readonly Color DiscMiddle = ColorTranslator.FromHtml("#C9DAF0");
    public static readonly Color DiscEdge = ColorTranslator.FromHtml("#9DB9E2");
    public static readonly Color DiscRing = ColorTranslator.FromHtml("#E8EFF9");

    // The user-guide "?" in the top bar: a flat dark-grey disc (like DssLabelTemplate's).
    public static readonly Color DarkGrey = ColorTranslator.FromHtml("#3A3D42");
    public static readonly Color DarkGreyHover = ColorTranslator.FromHtml("#55595F");

    // Over the window while the Magic parameters popup is open (2.0.1, Daan chose "navy 35%" from the mockup; the
    // blurred "frozen glass" of the test build 2.1.0-alpha did not look good): the window stays sharp under this see-through navy (ButtonNavy
    // at 35%). The popup itself is WorkArea, the dark grey of the bottom bar (Daan's choice).
    public static readonly Color DimLayer = Color.FromArgb(89, 0x0B, 0x2C, 0x6B);
    public static readonly Color PopupShadow = Color.FromArgb(90, 0, 0, 0); // the soft shadow under the popup

    // The round blue disc behind the logo in the title tab: the blues of the bottom bar, lighter at the top left.
    public static readonly Color LogoDiscLight = ColorTranslator.FromHtml("#6E97D2");
    public static readonly Color LogoDiscMiddle = BarTop;
    public static readonly Color LogoDiscEdge = BarBottom;

    /// <summary>
    /// The font of the window: Verdana, like the Maestro screens and DssLabelTemplate 2.0.0; it is on every Windows PC.
    /// 8.5 pt: Verdana is wider than Segoe UI, so this is about as big on the screen as the 9 pt Segoe UI of 1.x.
    /// </summary>
    public static Font CreateBodyFont()
    {
        using var installed = new System.Drawing.Text.InstalledFontCollection();
        bool hasVerdana = installed.Families.Any(family => family.Name == "Verdana");
        return hasVerdana ? new Font("Verdana", 8.5F) : Control.DefaultFont;
    }

    /// <summary>A font of the window's family in another size and style (titles, tiles, the captions of the bar).</summary>
    public static Font CreateFont(Font body, float points, FontStyle style = FontStyle.Regular)
    {
        return new Font(body.FontFamily, points, style, GraphicsUnit.Point);
    }

    /// <summary>The font of the titles of the panels ("Mappen", "Laatste conversies"): 2 pt bigger than the text.</summary>
    public static Font CreateTitleFont(Font body) => CreateFont(body, body.SizeInPoints + 2);

    /// <summary>The navy button (Browse): white text on navy (13:1), almost square corners like the Maestro buttons.</summary>
    public static void StyleNavyButton(RoundedButton button)
    {
        button.NormalFill = ButtonNavy;
        button.HoverFill = ButtonNavyHover;
        button.PressedFill = ButtonNavyPressed;
        button.BorderColor = Color.Empty;
        button.NormalText = White;
        button.HoverText = White;
        button.Radius = 2;
        button.Cursor = Cursors.Hand;
    }

    /// <summary>The grey button (Config, Open materials table, Clear list): navy text on light grey, square.</summary>
    public static void StyleGreyButton(RoundedButton button)
    {
        button.NormalFill = GreyButton;
        button.HoverFill = GreyButtonHover;
        button.PressedFill = Selection;
        button.BorderColor = Color.Empty;
        button.NormalText = Text;
        button.HoverText = ButtonNavy;
        button.Radius = 0;
        button.Cursor = Cursors.Hand;
    }
}
