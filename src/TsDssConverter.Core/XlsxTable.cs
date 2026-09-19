using ClosedXML.Excel;

namespace TsDssConverter.Core;

/// <summary>
/// The first worksheet of an XLSX file as a simple table: one dictionary per row, keyed by HEADER NAME.
/// Unnamed columns are ignored. Reading stops at the first fully empty row.
/// Uses ClosedXML, so Excel is not needed on the PC.
/// </summary>
public class XlsxTable
{
    private readonly HashSet<string> _headers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One entry per data row. Keys are header names (case-insensitive), values are trimmed text.</summary>
    public List<Dictionary<string, string>> Rows { get; } = new();

    public static XlsxTable Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new ConversionException(Messages.FileNotFound(path));
        }

        var table = new XlsxTable();

        // FileShare.ReadWrite: the file may still be open in Excel or just closed by TopSolid.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        var headerRow = sheet.FirstRowUsed();
        if (headerRow == null)
        {
            return table; // empty sheet
        }

        int lastColumn = sheet.LastColumnUsed()!.ColumnNumber();
        int lastRow = sheet.LastRowUsed()!.RowNumber();

        // Remember which column number belongs to which header name. Columns without a name are skipped.
        var headerByColumn = new Dictionary<int, string>();
        for (int col = 1; col <= lastColumn; col++)
        {
            string name = CellText(headerRow.Cell(col)).Trim();
            if (name != "")
            {
                headerByColumn[col] = name;
                table._headers.Add(name);
            }
        }

        for (int rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);

            if (IsEmptyRow(row, lastColumn))
            {
                break;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headerByColumn)
            {
                values.TryAdd(pair.Value, CellText(row.Cell(pair.Key)).Trim());
            }

            table.Rows.Add(values);
        }

        return table;
    }

    /// <summary>Throws an error that names the first missing column.</summary>
    /// <param name="fileKind">"LI" or "LP", only used in the message.</param>
    public void RequireColumns(string fileKind, params string[] columns)
    {
        foreach (string column in columns)
        {
            if (!_headers.Contains(column))
            {
                throw new ConversionException(Messages.MissingColumn(fileKind, column));
            }
        }
    }

    private static bool IsEmptyRow(IXLRow row, int lastColumn)
    {
        for (int col = 1; col <= lastColumn; col++)
        {
            if (CellText(row.Cell(col)) != "")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The value of a cell as text, WITHOUT using the PC's regional settings.
    /// Numbers are written with a dot ("2055.9699999999998"), exactly as they are stored in the file.
    /// </summary>
    private static string CellText(IXLCell cell)
    {
        var value = cell.Value;

        if (value.IsBlank)
        {
            return "";
        }

        if (value.IsNumber)
        {
            return value.GetNumber().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value.IsText)
        {
            return value.GetText();
        }

        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
