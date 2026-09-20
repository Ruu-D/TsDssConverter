namespace TsDssConverter.Core;

/// <summary>One row of the LI file (label info): one part.</summary>
public class LabelInfoRow
{
    public string SheetName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Dimensions { get; set; } = "";
    public string MaterialText { get; set; } = "";
    public string EdgeL1 { get; set; } = "";
    public string EdgeL2 { get; set; } = "";
    public string EdgeB1 { get; set; } = "";
    public string EdgeB2 { get; set; } = "";
    public string Cam2 { get; set; } = "";
    public string Opleg2 { get; set; } = "";
    public string Project { get; set; } = "";

    /// <summary>The ten extra description fields (DESC1..DESC10), index 0 = Desc1. Empty text if the column is not in the file.</summary>
    public string[] Descriptions { get; set; } = TopSolidReader.EmptyDescriptions();
}

/// <summary>One row of the LP file (label position): the same part, with its place on the sheet.</summary>
public class LabelPositionRow
{
    public string SheetName { get; set; } = "";
    public string Description { get; set; } = "";
    public double SheetLength { get; set; }
    public double SheetWidth { get; set; }
    public double LabelX { get; set; }
    public double LabelY { get; set; }
    public double LabelAngle { get; set; }

    /// <summary>SUP_DESIGNATION, e.g. "18.0_panel 18mm". Empty if the file has no such column.</summary>
    public string Designation { get; set; } = "";

    /// <summary>The ten extra description fields (DESC1..DESC10), index 0 = Desc1. Empty text if the column is not in the file.</summary>
    public string[] Descriptions { get; set; } = TopSolidReader.EmptyDescriptions();
}

/// <summary>Reads the two TopSolid XLSX files into simple row objects.</summary>
public static class TopSolidReader
{
    public static string[] EmptyDescriptions()
    {
        return Enumerable.Repeat("", ColumnKeys.DescriptionCount).ToArray();
    }

    /// <summary>
    /// The ten description fields of one row. A field whose column is not in the file stays empty.
    /// (The optional columns are only in <paramref name="header"/> when the file has them.)
    /// </summary>
    private static string[] ReadDescriptions(Dictionary<string, string> values, Dictionary<string, string> header)
    {
        string[] descriptions = EmptyDescriptions();

        for (int i = 0; i < descriptions.Length; i++)
        {
            if (header.TryGetValue(ColumnKeys.Desc(i + 1), out string? column))
            {
                descriptions[i] = values[column];
            }
        }

        return descriptions;
    }

    /// <param name="columns">The header names to look for. Null = the built-in names of the sample files.</param>
    public static List<LabelInfoRow> ReadLabelInfo(string path, ColumnMap? columns = null)
    {
        var table = XlsxTable.Load(path);
        Dictionary<string, string> header = (columns ?? ColumnMap.DefaultLabelInfo()).Resolve(table);

        var rows = new List<LabelInfoRow>();
        foreach (var values in table.Rows)
        {
            rows.Add(new LabelInfoRow
            {
                SheetName = values[header[ColumnKeys.Info.SheetName]],
                Description = values[header[ColumnKeys.Info.Description]],
                Dimensions = values[header[ColumnKeys.Info.Dimensions]],
                MaterialText = values[header[ColumnKeys.Info.MaterialText]],
                EdgeL1 = values[header[ColumnKeys.Info.EdgeL1]],
                EdgeL2 = values[header[ColumnKeys.Info.EdgeL2]],
                EdgeB1 = values[header[ColumnKeys.Info.EdgeB1]],
                EdgeB2 = values[header[ColumnKeys.Info.EdgeB2]],
                Cam2 = values[header[ColumnKeys.Info.Cam2]],
                Opleg2 = values[header[ColumnKeys.Info.Opleg2]],
                Project = values[header[ColumnKeys.Info.Project]],
                Descriptions = ReadDescriptions(values, header),
            });
        }

        return rows;
    }

    /// <param name="columns">The header names to look for. Null = the built-in names of the sample files.</param>
    public static List<LabelPositionRow> ReadLabelPositions(string path, ColumnMap? columns = null)
    {
        var table = XlsxTable.Load(path);
        Dictionary<string, string> header = (columns ?? ColumnMap.DefaultLabelPosition()).Resolve(table);

        var problems = new List<string>();
        var rows = new List<LabelPositionRow>();
        foreach (var values in table.Rows)
        {
            string sheetName = values[header[ColumnKeys.Position.SheetName]];
            string description = values[header[ColumnKeys.Position.Description]];
            string part = sheetName + " / " + description;

            // The designation is optional: without that column it stays empty.
            string designation = "";
            if (header.TryGetValue(ColumnKeys.Position.Designation, out string? designationHeader))
            {
                designation = values[designationHeader];
            }

            rows.Add(new LabelPositionRow
            {
                SheetName = sheetName,
                Description = description,
                SheetLength = ReadNumber(values, header[ColumnKeys.Position.SheetLength], part, problems),
                SheetWidth = ReadNumber(values, header[ColumnKeys.Position.SheetWidth], part, problems),
                LabelX = ReadNumber(values, header[ColumnKeys.Position.LabelX], part, problems),
                LabelY = ReadNumber(values, header[ColumnKeys.Position.LabelY], part, problems),
                LabelAngle = ReadNumber(values, header[ColumnKeys.Position.LabelAngle], part, problems),
                Designation = designation,
                Descriptions = ReadDescriptions(values, header),
            });
        }

        // Report every unreadable number at once, not just the first.
        if (problems.Count > 0)
        {
            throw new ConversionException(problems);
        }

        return rows;
    }

    /// <summary>Reads a number. If it is not a number the problem is added to the list and 0 is returned.</summary>
    private static double ReadNumber(Dictionary<string, string> values, string column, string part, List<string> problems)
    {
        string text = values[column];
        if (!NumberFormat.TryParseInvariant(text, out double number))
        {
            problems.Add(Messages.NotANumber("LP", column, text, part));
            return 0;
        }

        return number;
    }
}
