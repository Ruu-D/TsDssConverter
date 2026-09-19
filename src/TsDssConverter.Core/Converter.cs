using System.Text;

namespace TsDssConverter.Core;

/// <summary>What the conversion did. Warnings stay empty until stage 2.</summary>
public class ConversionResult
{
    public string BatchName { get; set; } = "";
    public string XmlPath { get; set; } = "";
    public List<string> LabelPaths { get; } = new();
    public int PartCount { get; set; }
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// The whole conversion in one call: read LI + LP, join, map materials, write the label CSVs and the batch XML.
/// The CLI (stage 1) and the tray app (later) both call this.
/// </summary>
public class Converter
{
    /// <param name="infoPath">The "...-LI.xlsx" file. The batch name is the file name without "-LI.xlsx".</param>
    /// <param name="planDate">Written in the XML. The CLI passes today; the tests pass a fixed date.</param>
    public ConversionResult Convert(
        string infoPath, string positionPath, string materialsPath,
        string batchFolder, string labelFolder,
        ConverterSettings settings, DateTime planDate)
    {
        string batchName = GetBatchName(infoPath);

        var infoRows = TopSolidReader.ReadLabelInfo(infoPath);
        var positionRows = TopSolidReader.ReadLabelPositions(positionPath);
        var materials = MaterialTable.Load(materialsPath);

        Batch batch = BatchBuilder.Build(batchName, infoRows, positionRows, materials, settings, planDate);

        // Turn the batch into bytes: label CSVs first, then the XML.
        Encoding csvEncoding = settings.GetCsvEncoding();
        var result = new ConversionResult { BatchName = batchName };
        var labelFiles = new List<PendingFile>();

        foreach (var plan in batch.Plans)
        {
            foreach (var sheet in plan.Sheets)
            {
                string path = Path.Combine(labelFolder, sheet.LabelFileName);
                labelFiles.Add(new PendingFile { Path = path, Content = csvEncoding.GetBytes(LabelCsvWriter.BuildText(plan, sheet)) });
                result.LabelPaths.Add(path);
                result.PartCount += sheet.Labels.Count;
            }
        }

        string xmlText = BatchXmlWriter.BuildText(batch);
        var xmlFile = new PendingFile
        {
            Path = Path.Combine(batchFolder, batchName + ".xml"),
            Content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xmlText),
        };
        result.XmlPath = xmlFile.Path;

        OutputWriter.Write(batchFolder, labelFolder, labelFiles, xmlFile, batchName);
        return result;
    }

    /// <summary>"C:\...\Verschuren-P-20-LI.xlsx" gives "Verschuren-P-20".</summary>
    public static string GetBatchName(string infoPath)
    {
        string name = Path.GetFileNameWithoutExtension(infoPath);
        const string suffix = "-LI";

        if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || name.Length == suffix.Length)
        {
            throw new ConversionException(Messages.BadInfoFileName(Path.GetFileName(infoPath)));
        }

        return name.Substring(0, name.Length - suffix.Length);
    }
}
