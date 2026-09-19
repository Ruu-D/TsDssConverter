using System.Globalization;

namespace TsDssConverter.Core;

/// <summary>
/// The ONLY place where numbers are converted to and from text.
/// We never use the PC's regional settings: a Belgian PC uses a comma as decimal separator,
/// but TopSolid texts and XLSX values use a dot.
/// </summary>
public static class NumberFormat
{
    private const string Pattern = "0.############";

    /// <summary>Reads a number with a DOT as decimal separator (TopSolid data), whatever the PC's settings are.</summary>
    public static bool TryParseInvariant(string text, out double value)
    {
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Reads a number typed by a person in materials.csv: Belgian Excel may have saved "12,5",
    /// so a comma is accepted as well as a dot. No thousands separators.
    /// </summary>
    public static bool TryParseLenient(string text, out double value)
    {
        return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Text for the Duivestein CSV files: comma as decimal separator, no thousands separator,
    /// no trailing ",0" (660 not 660,0).
    /// </summary>
    public static string ToDss(double value)
    {
        return ToInvariantText(value).Replace('.', ',');
    }

    /// <summary>Same number with a dot as decimal separator (used in the XML and in messages).</summary>
    public static string ToInvariantText(double value)
    {
        // A tiny negative number rounded to zero would be written as "-0" by .NET.
        if (value == 0)
        {
            return "0";
        }

        return value.ToString(Pattern, CultureInfo.InvariantCulture);
    }
}
