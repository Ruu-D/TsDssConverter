using System.Text;

namespace TsDssConverter.Core;

/// <summary>What the conversion did, including the warnings (things that did not stop the conversion).</summary>
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
    /// <param name="infoColumns">The header names in the LI file (columns-li.txt). Null = the built-in names.</param>
    /// <param name="positionColumns">The header names in the LP file (columns-lp.txt). Null = the built-in names.</param>
    /// <param name="labelColumns">The names of the ten DESC columns in the label CSV (columns-label.txt). Null = DESC1 .. DESC10.</param>
    public ConversionResult Convert(
        string infoPath, string positionPath, string materialsPath,
        string batchFolder, string labelFolder,
        ConverterSettings settings, DateTime planDate,
        ColumnMap? infoColumns = null, ColumnMap? positionColumns = null, LabelColumnNames? labelColumns = null)
    {
        string batchName = GetBatchName(infoPath);

        // Read all three input files first and report EVERY problem in them together
        // (for example a missing column in LI, a missing column in LP and a bad materials.csv).
        var readProblems = new List<string>();
        var infoRows = TryRead(() => TopSolidReader.ReadLabelInfo(infoPath, infoColumns), readProblems);
        var positionRows = TryRead(() => TopSolidReader.ReadLabelPositions(positionPath, positionColumns), readProblems);
        var materials = TryRead(() => MaterialTable.Load(materialsPath), readProblems);

        if (readProblems.Count > 0)
        {
            throw new ConversionException(readProblems);
        }

        Batch batch = BatchBuilder.Build(batchName, infoRows!, positionRows!, materials!, settings, planDate);

        // Turn the batch into bytes: label CSVs first, then the XML.
        Encoding csvEncoding = settings.GetCsvEncoding();
        var result = new ConversionResult { BatchName = batchName };
        result.Warnings.AddRange(batch.Warnings);
        AddCncWarnings(batch, settings, result.Warnings);
        var labelFiles = new List<PendingFile>();

        foreach (var plan in batch.Plans)
        {
            foreach (var sheet in plan.Sheets)
            {
                string path = Path.Combine(labelFolder, sheet.LabelFileName);
                labelFiles.Add(new PendingFile { Path = path, Content = csvEncoding.GetBytes(LabelCsvWriter.BuildText(plan, sheet, labelColumns)) });
                result.LabelPaths.Add(path);
                result.PartCount += sheet.Labels.Count;
            }
        }

        string xmlText = BatchXmlWriter.BuildText(batch, labelFolder);
        var xmlFile = new PendingFile
        {
            Path = Path.Combine(batchFolder, batchName + ".xml"),
            Content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xmlText),
        };
        result.XmlPath = xmlFile.Path;

        OutputWriter.Write(batchFolder, labelFolder, labelFiles, xmlFile, batchName);
        return result;
    }

    /// <summary>Runs a read action. A problem is added to the list and null is returned instead of stopping.</summary>
    private static T? TryRead<T>(Func<T> read, List<string> problems) where T : class
    {
        try
        {
            return read();
        }
        catch (ConversionException error)
        {
            problems.AddRange(error.Problems);
            return null;
        }
    }

    /// <summary>
    /// A CNC program that does not exist yet is only a warning: TopSolid may still be writing the CAM files.
    /// The check is done in the export folder on THIS PC (not in the prefix that is written in the XML).
    /// </summary>
    private static void AddCncWarnings(Batch batch, ConverterSettings settings, List<string> warnings)
    {
        if (!Directory.Exists(settings.TopSolidExportPath))
        {
            // One warning is clearer than one per sheet.
            warnings.Add(Messages.CncFolderNotReachable(settings.TopSolidExportPath));
            return;
        }

        foreach (var plan in batch.Plans)
        {
            foreach (var sheet in plan.Sheets)
            {
                string path = CncPathBuilder.BuildLocalPath(settings, batch.Name, sheet.Name);
                if (!File.Exists(path))
                {
                    warnings.Add(Messages.CncFileNotFound(path));
                }
            }
        }
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
