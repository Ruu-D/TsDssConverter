using System.Text;
using System.Text.RegularExpressions;

namespace TsDssConverter.Core;

/// <summary>
/// Builds the text of one label CSV (one sheet): tab-separated, header row, CRLF, one row per part.
/// Numbers use a comma as decimal separator (NumberFormat.ToDss). The encoding is chosen by the caller.
/// </summary>
public static class LabelCsvWriter
{
    // Each column = a header name and a way to get its value. The header and the values can never get out of sync.
    // The first 13 columns are mandatory for Duivestein; the rest are ours (usable in their label template).
    // Column names: only letters, digits, underscore and dash.
    // After these come the ten description columns (DESC1 .. DESC10); their names are in columns-label.txt.
    private static readonly List<(string Name, Func<Plan, Sheet, Label, string> GetValue)> Columns = new()
    {
        ("MATERIAL", (plan, sheet, label) => plan.Material.DssName),
        ("SHEETLENGTH", (plan, sheet, label) => NumberFormat.ToDss(plan.SheetLength)),
        ("SHEETWIDTH", (plan, sheet, label) => NumberFormat.ToDss(plan.SheetWidth)),
        ("SHEETTHICKNESS", (plan, sheet, label) => NumberFormat.ToDss(plan.Material.Thickness)),
        ("GRAIN", (plan, sheet, label) => plan.Material.Grain.ToString()),
        ("GRAINSTR", (plan, sheet, label) => plan.Material.GrainText),
        ("X", (plan, sheet, label) => NumberFormat.ToDss(label.X)),
        ("Y", (plan, sheet, label) => NumberFormat.ToDss(label.Y)),
        ("ROTATION", (plan, sheet, label) => label.Rotation.ToString()),
        ("N", (plan, sheet, label) => label.N.ToString()),
        ("ID", (plan, sheet, label) => label.Id),
        ("PANELLENGTH", (plan, sheet, label) => NumberFormat.ToDss(label.PanelLength)),
        ("PANELWIDTH", (plan, sheet, label) => NumberFormat.ToDss(label.PanelWidth)),
        ("DESCRIPTION", (plan, sheet, label) => label.Description),
        ("MATERIALNAME", (plan, sheet, label) => label.MaterialName),
        ("EDGE_L1", (plan, sheet, label) => label.EdgeL1),
        ("EDGE_L2", (plan, sheet, label) => label.EdgeL2),
        ("EDGE_B1", (plan, sheet, label) => label.EdgeB1),
        ("EDGE_B2", (plan, sheet, label) => label.EdgeB2),
        ("CAM2", (plan, sheet, label) => label.Cam2),
        ("CAM3", (plan, sheet, label) => label.Cam3),
        ("PROJECT", (plan, sheet, label) => label.Project),
        ("SHEET", (plan, sheet, label) => label.SheetName),
    };

    /// <summary>The names of the fixed columns, in order. The description columns come after them.</summary>
    public static IReadOnlyList<string> FixedColumnNames { get; } = Columns.Select(c => c.Name).ToList();

    /// <param name="descriptionNames">The names of the ten DESC columns at the end. Null = DESC1 .. DESC10.</param>
    public static string BuildText(Plan plan, Sheet sheet, LabelColumnNames? descriptionNames = null)
    {
        const string lineEnd = "\r\n";
        var text = new StringBuilder();
        IReadOnlyList<string> extraNames = (descriptionNames ?? new LabelColumnNames()).Names;

        text.Append(string.Join("\t", FixedColumnNames.Concat(extraNames)));
        text.Append(lineEnd);

        foreach (var label in sheet.Labels)
        {
            var values = Columns.Select(c => c.GetValue(plan, sheet, label)).Concat(label.Descriptions).Select(CleanText);
            text.Append(string.Join("\t", values));
            text.Append(lineEnd);
        }

        return text.ToString();
    }

    /// <summary>Trims the text and replaces tabs and line breaks by a space (they would break the CSV).</summary>
    private static string CleanText(string value)
    {
        return Regex.Replace(value, @"[\t\r\n]", " ").Trim();
    }
}
