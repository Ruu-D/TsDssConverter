namespace TsDssConverter.Tray;

/// <summary>
/// The colour theme of the application, taken from the graphics in the media folder
/// (see "Visual identity" in CLAUDE.md). All colours live here and nowhere else.
/// </summary>
internal static class Theme
{
    public static readonly Color Brand = ColorTranslator.FromHtml("#2CABE2");         // main colour, banner, primary buttons
    public static readonly Color DarkBlue = ColorTranslator.FromHtml("#1480B5");      // hover / pressed, lines
    public static readonly Color LightBlue = ColorTranslator.FromHtml("#7CCAED");     // highlights
    public static readonly Color PaleBlue = ColorTranslator.FromHtml("#A3D7EF");      // selection, borders
    public static readonly Color VeryPaleBlue = ColorTranslator.FromHtml("#D7EFFA");  // panel backgrounds
    public static readonly Color Navy = ColorTranslator.FromHtml("#0A3A56");          // text
    public static readonly Color White = Color.White;

    /// <summary>The one red of the application, only for errors. It is not part of the icon palette.</summary>
    public static readonly Color Error = ColorTranslator.FromHtml("#D6322C");

    /// <summary>
    /// Main button: brand blue with navy text (white text on brand blue is too pale to read).
    /// On hover it turns dark blue, and then the text turns white.
    /// </summary>
    public static void StylePrimaryButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Brand;
        button.ForeColor = Navy;
        button.Font = new Font(button.Font, FontStyle.Bold);
        button.FlatAppearance.MouseOverBackColor = DarkBlue;
        button.FlatAppearance.MouseDownBackColor = Navy;
        button.MouseEnter += (sender, e) => button.ForeColor = White;
        button.MouseLeave += (sender, e) => button.ForeColor = Navy;
        button.Cursor = Cursors.Hand;
    }

    /// <summary>Second button: white with a blue border and navy text.</summary>
    public static void StyleSecondaryButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = DarkBlue;
        button.BackColor = White;
        button.ForeColor = Navy;
        button.FlatAppearance.MouseOverBackColor = VeryPaleBlue;
        button.FlatAppearance.MouseDownBackColor = PaleBlue;
        button.Cursor = Cursors.Hand;
    }
}
