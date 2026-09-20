using System.Text;
using System.Text.RegularExpressions;

namespace TsDssConverter.Core;

/// <summary>
/// The names of the ten extra description columns at the end of the label CSV (DESC1 .. DESC10 by default).
/// The label template in Duivestein refers to these names, so they can be changed in columns-label.txt:
///   Desc1 = DESC1
///   Desc2 = DESC2
/// Left of the '=' is the field (the same key as in columns-li.txt / columns-lp.txt, where the header of the
/// Excel column is set), right of it the name of the column in the label CSV.
/// The 23 columns before them are fixed: Duivestein reads the first 13 by name, so those names cannot be changed.
/// </summary>
public class LabelColumnNames
{
    public const string FileName = "columns-label.txt";

    // Duivestein: column names may only have letters (no accents), digits, underscore and dash.
    private static readonly Regex ValidName = new(@"^[A-Za-z0-9_-]+$");

    private readonly string[] _names = new string[ColumnKeys.DescriptionCount];

    /// <summary>The default names: DESC1 .. DESC10.</summary>
    public LabelColumnNames()
    {
        for (int i = 0; i < _names.Length; i++)
        {
            _names[i] = "DESC" + (i + 1);
        }
    }

    /// <summary>The ten names, index 0 = Desc1.</summary>
    public IReadOnlyList<string> Names => _names;

    // ------------------------------------------------------------------ reading the file

    /// <summary>
    /// The names in columns-label.txt, or the default names if there is no such file (a deleted file must never stop
    /// the program). A file with mistakes is a TEMPORARY problem, like the other column files: the TopSolid files
    /// are fine, so they stay where they are until the file is fixed.
    /// </summary>
    public static LabelColumnNames Load(string? path)
    {
        var names = new LabelColumnNames();
        if (path == null || !File.Exists(path))
        {
            return names;
        }

        try
        {
            names.ApplyText(TextFile.ReadAllText(path));
        }
        catch (ConversionException error)
        {
            throw new ConversionException(error.Problems) { IsTemporary = true };
        }

        return names;
    }

    /// <summary>
    /// Changes the names according to the text of the file. A field that is not in the text keeps its name.
    /// Throws a ConversionException that holds EVERY problem in the text, not just the first.
    /// </summary>
    public void ApplyText(string text)
    {
        var problems = new List<string>();
        var lineOfField = new int[_names.Length];   // 0 = not in the file
        var seen = new HashSet<int>();
        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line == "" || line.StartsWith('#'))
            {
                continue;
            }

            int lineNumber = i + 1;
            int equals = line.IndexOf('=');
            if (equals < 0)
            {
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsNotALine(line)));
                continue;
            }

            string key = line.Substring(0, equals).Trim();
            string name = line.Substring(equals + 1).Trim();
            int field = FindField(key);

            if (field < 0)
            {
                string known = string.Join(", ", Enumerable.Range(1, ColumnKeys.DescriptionCount).Select(ColumnKeys.Desc));
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsUnknownField(key, known)));
            }
            else if (name == "")
            {
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsNoName(key)));
            }
            else if (!ValidName.IsMatch(name))
            {
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.LabelColumnBadName(name)));
            }
            else if (!seen.Add(field))
            {
                problems.Add(Messages.ColumnsBadLine(FileName, lineNumber, Messages.ColumnsDuplicateField(key)));
            }
            else
            {
                _names[field] = name;
                lineOfField[field] = lineNumber;
            }
        }

        CheckForClashes(lineOfField, problems);

        if (problems.Count > 0)
        {
            throw new ConversionException(problems);
        }
    }

    /// <summary>
    /// A description column must not have the name of one of the fixed columns (MATERIAL, ID, X, ...) or of another
    /// description column: Duivestein would not know which one the label template means.
    /// </summary>
    private void CheckForClashes(int[] lineOfField, List<string> problems)
    {
        var fixedNames = new HashSet<string>(LabelCsvWriter.FixedColumnNames, StringComparer.OrdinalIgnoreCase);
        var owner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int field = 0; field < _names.Length; field++)
        {
            string name = _names[field];
            int blame = field; // the field whose line in the file caused the clash

            if (!fixedNames.Contains(name) && owner.TryAdd(name, field))
            {
                continue;
            }

            if (!fixedNames.Contains(name) && lineOfField[field] == 0)
            {
                blame = owner[name]; // this one is a default name: the earlier one was changed to it
            }

            problems.Add(Messages.ColumnsBadLine(FileName, lineOfField[blame], Messages.LabelColumnClash(name)));
        }
    }

    private static int FindField(string key)
    {
        for (int i = 0; i < ColumnKeys.DescriptionCount; i++)
        {
            if (ColumnKeys.Desc(i + 1).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // ------------------------------------------------------------------ writing the file

    /// <summary>The text of a new columns-label.txt: an explanation and one line per description column.</summary>
    public string ToText()
    {
        var text = new StringBuilder();

        foreach (string line in Messages.LabelColumnsFileIntro.Split('\n'))
        {
            text.Append("# ").AppendLine(line.TrimEnd('\r'));
        }

        text.AppendLine();
        int width = ColumnKeys.Desc(ColumnKeys.DescriptionCount).Length;
        for (int i = 0; i < _names.Length; i++)
        {
            text.Append(ColumnKeys.Desc(i + 1).PadRight(width)).Append(" = ").AppendLine(_names[i]);
        }

        return text.ToString(); // AppendLine uses the Windows line ending (CRLF)
    }

    /// <summary>Writes a new column file, with a byte order mark so an old Notepad also shows accents correctly.</summary>
    public void WriteTemplate(string path)
    {
        File.WriteAllText(path, ToText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
