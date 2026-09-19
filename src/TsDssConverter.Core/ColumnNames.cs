namespace TsDssConverter.Core;

/// <summary>
/// The header names in the TopSolid XLSX files. Columns are always found by name, never by position.
/// If TopSolid renames a header (open point 5: "Test" -> "Project"), change it here only.
/// </summary>
public static class ColumnNames
{
    // ---- LI (label info) ----
    public const string InfoSheetName = "Naam_Plaat";
    public const string InfoDescription = "Omschrijving";
    public const string InfoDimensions = "Afmetingen";
    public const string InfoMaterialText = "Materiaal";
    public const string InfoEdgeL1 = "L1";
    public const string InfoEdgeL2 = "L2";
    public const string InfoEdgeB1 = "B1";
    public const string InfoEdgeB2 = "B2";
    public const string InfoCam2 = "CAM_2";
    public const string InfoOpleg2 = "Opleg_2_?";
    public const string InfoProject = "Test";

    // ---- LP (label position) ----
    public const string PositionSheetName = "SP";
    public const string PositionDescription = "ID";
    public const string PositionSheetLength = "SUP_L";
    public const string PositionSheetWidth = "SUP_B";
    public const string PositionLabelX = "LABEL_X";
    public const string PositionLabelY = "LABEL_Y";
    public const string PositionLabelAngle = "LABEL_ANGLE";
    public const string PositionDesignation = "SUP_DESIGNATION"; // optional: only used for a warning
    // SUP_D is unreliable and not used.
}
