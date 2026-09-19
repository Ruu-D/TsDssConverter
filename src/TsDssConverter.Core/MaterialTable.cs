using System.Text;

namespace TsDssConverter.Core;

/// <summary>One line of materials.csv: how a TopSolid material is known in the Duivestein warehouse.</summary>
public class Material
{
    /// <summary>Name as used by TopSolid (part of the sheet name before '#'), e.g. "White_18".</summary>
    public string TopSolidName { get; set; } = "";

    /// <summary>Material name for Duivestein. For now identical to the TopSolid name.</summary>
    public string DssName { get; set; } = "";

    /// <summary>Real thickness in mm (SUP_D in TopSolid is unreliable, so it comes from this file).</summary>
    public double Thickness { get; set; }

    /// <summary>0 = none, 1 = along the length, 2 = across.</summary>
    public int Grain { get; set; }

    /// <summary>Text for the GRAINSTR column.</summary>
    public string GrainText
    {
        get
        {
            if (Grain == 1) return "Langs";
            if (Grain == 2) return "Dwars";
            return "Geen";
        }
    }
}

/// <summary>
/// The customer's mapping file (materials.csv): semicolon separated, header row.
/// Never guesses: a material that is not in the file is an error, because the warehouse would pick the wrong board.
/// </summary>
public class MaterialTable
{
    private readonly Dictionary<string, Material> _byName = new(StringComparer.OrdinalIgnoreCase);

    public static MaterialTable Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new ConversionException(Messages.FileNotFound(path));
        }

        // FileShare.ReadWrite: Excel may have the file open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return FromText(DecodeText(memory.ToArray()));
    }

    /// <summary>Builds the table from the text of the file (used by Load and by the tests).</summary>
    public static MaterialTable FromText(string text)
    {
        var table = new MaterialTable();

        // Line numbers are counted from 1 for the user, including the header.
        string[] lines = text.Split('\n');
        int nameColumn = -1, dssColumn = -1, thicknessColumn = -1, grainColumn = -1;
        bool headerRead = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.Trim() == "")
            {
                continue;
            }

            string[] cells = line.Split(';');

            if (!headerRead)
            {
                nameColumn = FindColumn(cells, "TopSolidMaterial");
                dssColumn = FindColumn(cells, "DssMaterial");
                thicknessColumn = FindColumn(cells, "Thickness");
                grainColumn = FindColumn(cells, "Grain");
                headerRead = true;
                continue;
            }

            table.AddLine(i + 1, cells, nameColumn, dssColumn, thicknessColumn, grainColumn);
        }

        if (!headerRead)
        {
            throw new ConversionException(Messages.MaterialsMissingColumn("TopSolidMaterial"));
        }

        return table;
    }

    /// <summary>Finds the material. Matching is trimmed and case-insensitive. Unknown material = error.</summary>
    public Material Find(string topSolidName)
    {
        if (TryFind(topSolidName, out Material? material))
        {
            return material!;
        }

        throw new ConversionException(Messages.UnknownMaterial(topSolidName));
    }

    public bool TryFind(string topSolidName, out Material? material)
    {
        return _byName.TryGetValue(topSolidName.Trim(), out material);
    }

    private void AddLine(int lineNumber, string[] cells, int nameColumn, int dssColumn, int thicknessColumn, int grainColumn)
    {
        int highestColumn = Math.Max(Math.Max(nameColumn, dssColumn), Math.Max(thicknessColumn, grainColumn));
        if (cells.Length <= highestColumn)
        {
            throw new ConversionException(Messages.MaterialsBadLine(lineNumber, "te weinig kolommen."));
        }

        string name = cells[nameColumn].Trim();
        string dssName = cells[dssColumn].Trim();

        if (name == "")
        {
            throw new ConversionException(Messages.MaterialsBadLine(lineNumber, "TopSolidMaterial is leeg."));
        }

        if (name.Contains('#'))
        {
            throw new ConversionException(Messages.MaterialsBadLine(lineNumber, Messages.MaterialsKeyHasSheetNumber(name)));
        }

        if (dssName == "")
        {
            throw new ConversionException(Messages.MaterialsBadLine(lineNumber, "DssMaterial is leeg."));
        }

        if (!NumberFormat.TryParseLenient(cells[thicknessColumn], out double thickness) || thickness <= 0)
        {
            throw new ConversionException(Messages.MaterialsBadLine(lineNumber, $"Thickness '{cells[thicknessColumn].Trim()}' is geen geldig getal."));
        }

        if (!int.TryParse(cells[grainColumn].Trim(), out int grain) || grain < 0 || grain > 2)
        {
            throw new ConversionException(Messages.MaterialsBadLine(lineNumber, $"Grain '{cells[grainColumn].Trim()}' moet 0, 1 of 2 zijn."));
        }

        var material = new Material { TopSolidName = name, DssName = dssName, Thickness = thickness, Grain = grain };
        if (!_byName.TryAdd(name, material))
        {
            throw new ConversionException(Messages.MaterialsBadLine(lineNumber, $"'{name}' staat er twee keer in."));
        }
    }

    private static int FindColumn(string[] headerCells, string name)
    {
        for (int i = 0; i < headerCells.Length; i++)
        {
            if (headerCells[i].Trim().TrimStart('﻿').Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new ConversionException(Messages.MaterialsMissingColumn(name));
    }

    /// <summary>
    /// UTF-8 (with or without BOM) first. If the bytes are not valid UTF-8 the file was probably saved
    /// by Excel as "CSV" (ANSI), so fall back to Windows-1252. "Chêne" survives both ways.
    /// </summary>
    private static string DecodeText(byte[] bytes)
    {
        // Skip the UTF-8 byte order mark (EF BB BF) if there is one.
        int start = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            start = 3;
        }

        try
        {
            // throwOnInvalidBytes: true -> invalid UTF-8 throws instead of silently giving '?' characters.
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return strictUtf8.GetString(bytes, start, bytes.Length - start);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes, start, bytes.Length - start);
        }
    }
}
