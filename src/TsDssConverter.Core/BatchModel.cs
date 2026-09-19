namespace TsDssConverter.Core;

// The result of the conversion, before anything is written to disk:
//   Batch -> Plans (one per material) -> Sheets (one per physical board) -> Labels (one per part)

/// <summary>One part on a sheet = one row in the label CSV.</summary>
public class Label
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Rotation { get; set; }

    /// <summary>Sequence number of the part on this sheet, 1..n, in LP order.</summary>
    public int N { get; set; }

    /// <summary>Trailing number of the description, e.g. "19587".</summary>
    public string Id { get; set; } = "";

    public double PanelLength { get; set; }
    public double PanelWidth { get; set; }
    public string Description { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string EdgeL1 { get; set; } = "";
    public string EdgeL2 { get; set; } = "";
    public string EdgeB1 { get; set; } = "";
    public string EdgeB2 { get; set; } = "";
    public string Cam2 { get; set; } = "";
    public string Opleg2 { get; set; } = "";
    public string Project { get; set; } = "";

    /// <summary>TopSolid sheet name, e.g. "White_18#01".</summary>
    public string SheetName { get; set; } = "";
}

/// <summary>One physical board: gets one label CSV and one CNC program.</summary>
public class Sheet
{
    public string Name { get; set; } = "";
    public int Number { get; set; }

    /// <summary>File name only, e.g. "Verschuren-P-20_001.csv".</summary>
    public string LabelFileName { get; set; } = "";

    /// <summary>Full path as written in the XML.</summary>
    public string CncPath { get; set; } = "";

    public List<Label> Labels { get; } = new();
}

/// <summary>All sheets of one material: one Plan element in the XML.</summary>
public class Plan
{
    /// <summary>"001", "002", ...</summary>
    public string PlanName { get; set; } = "";

    public Material Material { get; set; } = new();
    public double SheetLength { get; set; }
    public double SheetWidth { get; set; }
    public List<Sheet> Sheets { get; } = new();
}

public class Batch
{
    public string Name { get; set; } = "";
    public DateTime PlanDate { get; set; }
    public List<Plan> Plans { get; } = new();

    /// <summary>Things that are not wrong enough to stop the conversion (plain Dutch text).</summary>
    public List<string> Warnings { get; } = new();
}
