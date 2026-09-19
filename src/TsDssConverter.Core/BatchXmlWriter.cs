using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TsDssConverter.Core;

/// <summary>Builds the text of the Duivestein batch XML: UTF-8, 2-space indentation, CRLF line endings.</summary>
public static class BatchXmlWriter
{
    public static string BuildText(Batch batch)
    {
        var plans = new XElement("Plans");
        foreach (var plan in batch.Plans)
        {
            plans.Add(BuildPlan(plan));
        }

        var root = new XElement("DSSBatch",
            new XElement("BatchName", batch.Name),
            new XElement("BatchDescription", batch.Name),
            new XElement("PlanDate", batch.PlanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement("AutoExpand", "False"),
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

    private static XElement BuildPlan(Plan plan)
    {
        var labelFileNames = new XElement("LabelFilenames");
        var cncFileNames = new XElement("CNCFilenames");

        // Same order in both lists: the n-th label file belongs to the n-th CNC program.
        foreach (var sheet in plan.Sheets)
        {
            labelFileNames.Add(new XElement("LabelFilename", sheet.LabelFileName));
            cncFileNames.Add(new XElement("CNCFilename", sheet.CncPath));
        }

        return new XElement("Plan",
            new XElement("PlanName", plan.PlanName),
            new XElement("Material", plan.Material.DssName),
            new XElement("XDimSize", NumberFormat.ToInvariantText(plan.SheetLength)),
            new XElement("YDimSize", NumberFormat.ToInvariantText(plan.SheetWidth)),
            new XElement("Grain", plan.Material.Grain),
            new XElement("Quantity", plan.Sheets.Count),
            new XElement("Rotation", 0), // constant 0 for now (open point 11)
            labelFileNames,
            cncFileNames);
    }
}
