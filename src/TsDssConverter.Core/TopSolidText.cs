using System.Text.RegularExpressions;

namespace TsDssConverter.Core;

/// <summary>
/// Reads the values that TopSolid packs into text: "L: 660.0mm X B: 590.0mm", "K1 - Front - 3575",
/// "White_18#01".
/// </summary>
public static class TopSolidText
{
    // "L: 660.0mm X B: 590.0mm" - dot decimals, the decimals are optional.
    private static readonly Regex DimensionsPattern = new(
        @"^\s*L:\s*(?<length>\d+(\.\d+)?)\s*mm\s*X\s*B:\s*(?<width>\d+(\.\d+)?)\s*mm\s*$",
        RegexOptions.IgnoreCase);

    // The part number is the number at the very end of the description.
    private static readonly Regex TrailingNumberPattern = new(@"(\d+)\s*$");

    /// <summary>
    /// Reads panel length (L) and width (B) from the Afmetingen text. L and B are passed as-is, never swapped.
    /// </summary>
    public static void ParseDimensions(string text, string part, out double length, out double width)
    {
        var match = DimensionsPattern.Match(text);
        if (!match.Success)
        {
            throw new ConversionException(Messages.CannotParseDimensions(text, part));
        }

        // The pattern guarantees a valid number, so TryParseInvariant cannot fail here.
        NumberFormat.TryParseInvariant(match.Groups["length"].Value, out length);
        NumberFormat.TryParseInvariant(match.Groups["width"].Value, out width);
    }

    // The number at the very start: "18.0_panel 18mm" gives 18.0.
    private static readonly Regex LeadingNumberPattern = new(@"^\s*(\d+(\.\d+)?)");

    /// <summary>
    /// The leading number of SUP_DESIGNATION ("18.0_panel 18mm" gives 18): the real thickness in TopSolid.
    /// Returns false if the text does not start with a number.
    /// </summary>
    public static bool TryGetLeadingNumber(string text, out double number)
    {
        number = 0;
        var match = LeadingNumberPattern.Match(text);
        return match.Success && NumberFormat.TryParseInvariant(match.Groups[1].Value, out number);
    }

    /// <summary>The trailing number of the description: "... - 19587" gives "19587".</summary>
    public static string GetPartId(string description)
    {
        var match = TrailingNumberPattern.Match(description);
        if (!match.Success)
        {
            throw new ConversionException(Messages.NoPartId(description));
        }

        return match.Groups[1].Value;
    }

    /// <summary>
    /// The sheet number at the end of a sheet name: "White_18#01" gives 1, "P2026_(Keuken)_MELA_BLANC_18#02" gives 2.
    /// Whatever is in front of the '#' (material, project prefix, ...) does not matter here: the material comes from
    /// the Matériau column of the LI file, not from the sheet name. Returns false when the name does not end in '#number'.
    /// </summary>
    public static bool TryGetSheetNumber(string sheetName, out int sheetNumber)
    {
        sheetNumber = 0;

        int hashPosition = sheetName.LastIndexOf('#');
        if (hashPosition <= 0)
        {
            return false; // no '#', or nothing in front of it
        }

        return int.TryParse(sheetName.Substring(hashPosition + 1), out sheetNumber);
    }
}
