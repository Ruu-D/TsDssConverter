using System.Text;

namespace TsDssConverter.Core;

/// <summary>
/// Which header name in the TopSolid XLSX belongs to which field of the program.
/// The names are in two small text files that the customer can edit (columns-li.txt and columns-lp.txt),
/// because TopSolid may rename a column one day and the converter must not break on that.
/// The ORDER of the columns in the XLSX never matters: columns are always found by name.
///
/// A file looks like this (one field per line, several names for one field are separated by |):
///   SheetName = Naam_Plaat
///   Project   = Test | Project
/// </summary>
public class ColumnMap
{
    public const string InfoFileName = "columns-li.txt";
    public const string PositionFileName = "columns-lp.txt";

    /// <summary>One field: its key, the built-in header name and whether the file cannot do without it.</summary>
    private record Field(string Key, string DefaultHeader, bool Required = true);

    /// <summary>
    /// The ten extra description fields (Desc1 = header "DESC1", ...). Optional in both files: a file without
    /// them still converts, and the label CSV then has empty DESC columns.
    /// </summary>
    private static Field[] DescriptionFields()
    {
        return Enumerable.Range(1, ColumnKeys.DescriptionCount)
            .Select(number => new Field(ColumnKeys.Desc(number), "DESC" + number, Required: false))
            .ToArray();
    }

    private static readonly Field[] InfoFields = new Field[]
    {
        new(ColumnKeys.Info.SheetName, "Naam_Plaat"),
        new(ColumnKeys.Info.Description, "Omschrijving"),
        new(ColumnKeys.Info.Dimensions, "Afmetingen"),
        new(ColumnKeys.Info.MaterialText, "Materiaal"),
        new(ColumnKeys.Info.EdgeL1, "L1"),
        new(ColumnKeys.Info.EdgeL2, "L2"),
        new(ColumnKeys.Info.EdgeB1, "B1"),
        new(ColumnKeys.Info.EdgeB2, "B2"),
        new(ColumnKeys.Info.Cam2, "CAM_2"),
        new(ColumnKeys.Info.Opleg2, "Opleg_2_?"),
        new(ColumnKeys.Info.Project, "Test"), // actually the project number (badly named header, open point 5)
    }.Concat(DescriptionFields()).ToArray();

    private static readonly Field[] PositionFields = new Field[]
    {
        new(ColumnKeys.Position.SheetName, "SP"),
        new(ColumnKeys.Position.Description, "ID"),
        new(ColumnKeys.Position.SheetLength, "SUP_L"),
        new(ColumnKeys.Position.SheetWidth, "SUP_B"),
        new(ColumnKeys.Position.LabelX, "LABEL_X"),
        new(ColumnKeys.Position.LabelY, "LABEL_Y"),
        new(ColumnKeys.Position.LabelAngle, "LABEL_ANGLE"),
        new(ColumnKeys.Position.Designation, "SUP_DESIGNATION", Required: false),
    }.Concat(DescriptionFields()).ToArray();

    private readonly Field[] _fields;

    // Key -> the header names that are accepted, in order of preference.
    private readonly Dictionary<string, List<string>> _headers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>"LI" or "LP", only used in messages.</summary>
    public string FileKind { get; }

    /// <summary>"columns-li.txt" or "columns-lp.txt".</summary>
    public string FileName { get; }

    private ColumnMap(string fileKind, string fileName, Field[] fields)
    {
        FileKind = fileKind;
        FileName = fileName;
        _fields = fields;

        foreach (Field field in fields)
        {
            _headers[field.Key] = new List<string> { field.DefaultHeader };
        }
    }

    /// <summary>The header names of the sample files. Used when there is no columns-li.txt.</summary>
    public static ColumnMap DefaultLabelInfo() => new("LI", InfoFileName, InfoFields);

    /// <summary>The header names of the sample files. Used when there is no columns-lp.txt.</summary>
    public static ColumnMap DefaultLabelPosition() => new("LP", PositionFileName, PositionFields);

    // ------------------------------------------------------------------ reading the file

    /// <summary>
    /// The map for the LI file: the names in columns-li.txt, or the built-in names if there is no such file
    /// (a deleted file must never stop the program). Null as path also gives the built-in names.
    /// </summary>
    public static ColumnMap LoadLabelInfo(string? path) => ApplyFile(DefaultLabelInfo(), path);

    /// <summary>The map for the LP file: the names in columns-lp.txt, or the built-in names if there is no such file.</summary>
    public static ColumnMap LoadLabelPosition(string? path) => ApplyFile(DefaultLabelPosition(), path);

    /// <summary>
    /// A file that cannot be understood is a TEMPORARY problem: the settings are wrong, not the TopSolid files,
    /// so those stay where they are until the file is fixed.
    /// </summary>
    private static ColumnMap ApplyFile(ColumnMap map, string? path)
    {
        if (path == null || !File.Exists(path))
        {
            return map;
        }

        try
        {
            map.ApplyText(TextFile.ReadAllText(path));
        }
        catch (ConversionException error)
        {
            throw new ConversionException(error.Problems) { IsTemporary = true };
        }

        return map;
    }

    /// <summary>
    /// Changes the map according to the text of a column file. A field that is not in the text (a deleted line)
    /// keeps its name. Throws a ConversionException that holds EVERY problem in the text, not just the first.
    /// </summary>
    public void ApplyText(string text)
    {
        List<string> problems = ReadLines(text);
        if (problems.Count > 0)
        {
            throw new ConversionException(problems);
        }
    }

    private List<string> ReadLines(string text)
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim(); // also removes the '\r' of a Windows line ending
            if (line == "" || line.StartsWith('#'))
            {
                continue;
            }

            // Line numbers are counted from 1, like in Notepad.
            int lineNumber = i + 1;
            int equals = line.IndexOf('=');
            if (equals < 0)
            {
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsNotALine(line)));
                continue;
            }

            string key = line.Substring(0, equals).Trim();
            List<string> names = line.Substring(equals + 1).Split('|')
                .Select(name => name.Trim())
                .Where(name => name != "")
                .ToList();

            if (!_headers.ContainsKey(key))
            {
                string known = string.Join(", ", _fields.Select(field => field.Key));
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsUnknownField(key, known)));
            }
            else if (names.Count == 0)
            {
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsNoName(key)));
            }
            else if (!seen.Add(key))
            {
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsDuplicateField(key)));
            }
            else
            {
                _headers[key] = names;
            }
        }

        return problems;
    }

    // ------------------------------------------------------------------ finding the columns in an XLSX

    /// <summary>The header names accepted for a field, in order of preference.</summary>
    public IReadOnlyList<string> HeadersFor(string key)
    {
        return _headers[key];
    }

    /// <summary>
    /// Finds every field in the table and returns key -> the header name that is really in the file.
    /// A required field that is missing is an error: ALL missing fields are named at once, followed by a tip
    /// about the column file. An optional field that is missing is simply not in the result.
    /// </summary>
    public Dictionary<string, string> Resolve(XlsxTable table)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();

        foreach (Field field in _fields)
        {
            List<string> names = _headers[field.Key];
            string? header = table.FindHeader(names);

            if (header != null)
            {
                found[field.Key] = header;
            }
            else if (field.Required)
            {
                problems.Add(Messages.MissingColumn(FileKind, string.Join("' / '", names)));
            }
        }

        if (problems.Count > 0)
        {
            problems.Add(Messages.MissingColumnTip(FileKind, FileName));
            throw new ConversionException(problems);
        }

        return found;
    }

    // ------------------------------------------------------------------ writing the file

    /// <summary>
    /// The text of a new column file: a short explanation and one line per field with the default header name.
    /// Written once when the file does not exist yet, and never over a file the customer may have changed.
    /// </summary>
    public string ToText()
    {
        int width = _fields.Max(field => field.Key.Length);
        var text = new StringBuilder();

        // The example in the explanation uses the first field of THIS file, with a made-up second name.
        Field first = _fields[0];
        string example = $"{first.Key} = {first.DefaultHeader} | {first.DefaultHeader}_2";

        foreach (string line in Messages.ColumnsFileIntro(FileKind, example).Split('\n'))
        {
            text.Append("# ").AppendLine(line.TrimEnd('\r'));
        }

        text.AppendLine();
        foreach (Field field in _fields)
        {
            // A blank line and a note above the first field of each group of optional fields:
            // the description fields have a note of their own, the other optional fields the general one.
            if (field.Key == ColumnKeys.Desc(1))
            {
                text.AppendLine();
                foreach (string line in Messages.ColumnsFileDescriptionNote(LabelColumnNames.FileName).Split('\n'))
                {
                    text.Append("# ").AppendLine(line);
                }
            }
            else if (!field.Required && !ColumnKeys.IsDesc(field.Key))
            {
                text.AppendLine();
                text.Append("# ").AppendLine(Messages.ColumnsFileOptional);
            }

            text.Append(field.Key.PadRight(width)).Append(" = ").AppendLine(string.Join(" | ", _headers[field.Key]));
        }

        return text.ToString(); // AppendLine uses the Windows line ending (CRLF)
    }

    /// <summary>Writes a new column file, with a byte order mark so an old Notepad also shows accents correctly.</summary>
    public void WriteTemplate(string path)
    {
        File.WriteAllText(path, ToText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
