using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TsDssConverter.Core;

/// <summary>Builds the text of the Duivestein batch XML: UTF-8, 2-space indentation, CRLF line endings.</summary>
public static class BatchXmlWriter
{
    /// <param name="labelFolder">
    /// The Duivestein label folder (the third path in the settings, e.g. Z:\Duivestein\Label). Every LabelFilename in
    /// the XML is this folder + the file name, so Duivestein knows where to find the label files (same drive letter
    /// as the CNC paths).
    /// </param>
    public static string BuildText(Batch batch, string labelFolder)
    {
        var plans = new XElement("Plans");
        foreach (var plan in batch.Plans)
        {
            plans.Add(BuildPlan(plan, labelFolder));
        }

        // The header is the syntax that DSSClient accepted in the test of 2026-09-21 (the "corrected file"): Date (not PlanDate),
        // no AutoExpand, and the counts. The order of the nodes is the order of that file. Confirmed by Daan:
        //   MaxStackHeight   always 1      (never more than one panel is machined at a time)
        //   EqualStackHeight always False
        //   CutCount         always 0      (not used here)
        var root = new XElement("DSSBatch",
            new XElement("BatchName", batch.Name),
            new XElement("BatchDescription", batch.Name),
            new XElement("Date", batch.PlanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement("MaxStackHeight", 1),
            new XElement("EqualStackHeight", "False"),
            new XElement("PlanCount", batch.Plans.Count),
            new XElement("BoardCount", batch.BoardCount),
            new XElement("PartCount", batch.PartCount),
            new XElement("CutCount", 0),
            plans,
            new XElement("Routes",
                new XElement("Route",
                    new XElement("ToStackPosition", "LBL"),
                    new XElement("Parameters"))));

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), // no BOM
        };

        // Write to bytes first: writing to a string would make the declaration say "utf-16".
        using var memory = new MemoryStream();
        using (var writer = XmlWriter.Create(memory, settings))
        {
            new XDocument(root).Save(writer);
        }

        string xml = Encoding.UTF8.GetString(memory.ToArray());

        // .NET writes an empty element as "<Parameters />"; the Duivestein example has "<Parameters/>".
        // Both are the same XML, but we follow the example. " />" cannot occur in text (">" is escaped).
        xml = xml.Replace(" />", "/>");

        return xml + "\r\n";
    }

    private static XElement BuildPlan(Plan plan, string labelFolder)
    {
        var labelFileNames = new XElement("LabelFilenames");
        var cncFileNames = new XElement("CNCFilenames");

        // Same order in both lists: the n-th label file belongs to the n-th CNC program.
        foreach (var sheet in plan.Sheets)
        {
            labelFileNames.Add(new XElement("LabelFilename", Path.Combine(labelFolder, sheet.LabelFileName)));
            cncFileNames.Add(new XElement("CNCFilename", sheet.CncPath));
        }

        return new XElement("Plan",
            new XElement("PlanName", plan.PlanName),
            new XElement("MaterialName", plan.Material.DssName), // "Material" in the HP002 example; MaterialName is the correct name
            new XElement("XDimSize", NumberFormat.ToInvariantText(plan.SheetLength)),
            new XElement("YDimSize", NumberFormat.ToInvariantText(plan.SheetWidth)),
            new XElement("Grain", plan.Material.Grain),
            new XElement("Quantity", plan.Sheets.Count),
            new XElement("Rotation", 0), // constant 0 for now (open point 11)
            labelFileNames,
            cncFileNames);
    }
}
