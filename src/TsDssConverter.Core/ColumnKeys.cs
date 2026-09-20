namespace TsDssConverter.Core;

/// <summary>
/// The fields the program reads from the two TopSolid XLSX files. A key is what the program calls a column;
/// it is also the text left of the '=' in columns-li.txt and columns-lp.txt. The header name that TopSolid uses
/// for it is on the right of the '=' (see <see cref="ColumnMap"/> for the built-in default names).
/// </summary>
public static class ColumnKeys
{
    /// <summary>
    /// The number of extra description fields (Desc1 .. Desc10). They are optional columns in the LI and the LP file
    /// and become the last columns (DESC1 .. DESC10 by default) of the label CSV, for the label template in Duivestein.
    /// </summary>
    public const int DescriptionCount = 10;

    /// <summary>The key of an extra description field: Desc(1) = "Desc1", Desc(10) = "Desc10".</summary>
    public static string Desc(int number) => "Desc" + number;

    public static bool IsDesc(string key) => key.StartsWith("Desc", StringComparison.OrdinalIgnoreCase) && key.Length > 4 && char.IsDigit(key[4]);

    // ---- LI (label info) ----
    public static class Info
    {
        public const string SheetName = "SheetName";
        public const string Description = "Description";
        public const string Dimensions = "Dimensions";
        public const string MaterialText = "MaterialText";
        public const string EdgeL1 = "EdgeL1";
        public const string EdgeL2 = "EdgeL2";
        public const string EdgeB1 = "EdgeB1";
        public const string EdgeB2 = "EdgeB2";
        public const string Cam2 = "Cam2";
        public const string Opleg2 = "Opleg2";
        public const string Project = "Project";
    }

    // ---- LP (label position) ----
    // SUP_D is unreliable and not used at all.
    public static class Position
    {
        public const string SheetName = "SheetName";
        public const string Description = "Description";
        public const string SheetLength = "SheetLength";
        public const string SheetWidth = "SheetWidth";
        public const string LabelX = "LabelX";
        public const string LabelY = "LabelY";
        public const string LabelAngle = "LabelAngle";
        public const string Designation = "Designation"; // optional: only used for a warning
    }
}
