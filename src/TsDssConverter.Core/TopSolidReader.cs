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
}

/// <summary>Reads the two TopSolid XLSX files into simple row objects.</summary>
public static class TopSolidReader
{
    public static List<LabelInfoRow> ReadLabelInfo(string path)
    {
        var table = XlsxTable.Load(path);
        table.RequireColumns("LI",
            ColumnNames.InfoSheetName, ColumnNames.InfoDescription, ColumnNames.InfoDimensions,
            ColumnNames.InfoMaterialText, ColumnNames.InfoEdgeL1, ColumnNames.InfoEdgeL2,
            ColumnNames.InfoEdgeB1, ColumnNames.InfoEdgeB2, ColumnNames.InfoCam2,
            ColumnNames.InfoOpleg2, ColumnNames.InfoProject);

        var rows = new List<LabelInfoRow>();
        foreach (var values in table.Rows)
        {
            rows.Add(new LabelInfoRow
            {
                SheetName = values[ColumnNames.InfoSheetName],
                Description = values[ColumnNames.InfoDescription],
                Dimensions = values[ColumnNames.InfoDimensions],
                MaterialText = values[ColumnNames.InfoMaterialText],
                EdgeL1 = values[ColumnNames.InfoEdgeL1],
                EdgeL2 = values[ColumnNames.InfoEdgeL2],
                EdgeB1 = values[ColumnNames.InfoEdgeB1],
                EdgeB2 = values[ColumnNames.InfoEdgeB2],
                Cam2 = values[ColumnNames.InfoCam2],
                Opleg2 = values[ColumnNames.InfoOpleg2],
                Project = values[ColumnNames.InfoProject],
            });
        }

        return rows;
    }

    public static List<LabelPositionRow> ReadLabelPositions(string path)
    {
        var table = XlsxTable.Load(path);
        table.RequireColumns("LP",
            ColumnNames.PositionSheetName, ColumnNames.PositionDescription,
            ColumnNames.PositionSheetLength, ColumnNames.PositionSheetWidth,
            ColumnNames.PositionLabelX, ColumnNames.PositionLabelY, ColumnNames.PositionLabelAngle);

        var problems = new List<string>();
        var rows = new List<LabelPositionRow>();
        foreach (var values in table.Rows)
        {
            string sheetName = values[ColumnNames.PositionSheetName];
            string description = values[ColumnNames.PositionDescription];
            string part = sheetName + " / " + description;

            values.TryGetValue(ColumnNames.PositionDesignation, out string? designation);

            rows.Add(new LabelPositionRow
            {
                SheetName = sheetName,
                Description = description,
                SheetLength = ReadNumber(values, ColumnNames.PositionSheetLength, part, problems),
                SheetWidth = ReadNumber(values, ColumnNames.PositionSheetWidth, part, problems),
                LabelX = ReadNumber(values, ColumnNames.PositionLabelX, part, problems),
                LabelY = ReadNumber(values, ColumnNames.PositionLabelY, part, problems),
                LabelAngle = ReadNumber(values, ColumnNames.PositionLabelAngle, part, problems),
                Designation = designation ?? "",
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
