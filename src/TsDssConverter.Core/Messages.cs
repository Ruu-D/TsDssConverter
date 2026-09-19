namespace TsDssConverter.Core;

/// <summary>
/// All texts that the user can see (error messages), in Dutch, in one place so they are easy to change.
/// </summary>
public static class Messages
{
    // ---- Files and folders ----
    public static string FileNotFound(string path) => $"Bestand niet gevonden: {path}";

    public static string FolderNotFound(string path) => $"Map niet bereikbaar: {path}";

    public static string BatchAlreadyExists(string batchName) =>
        $"Batch bestaat al in Duivestein-map: {batchName}.xml";

    public static string BadInfoFileName(string fileName) =>
        $"Bestandsnaam moet eindigen op '-LI.xlsx' (label-info): {fileName}";

    // ---- XLSX content ----
    public static string MissingColumn(string fileKind, string column) =>
        $"Kolom '{column}' ontbreekt in het {fileKind}-bestand.";

    public static string NotANumber(string fileKind, string column, string value, string part) =>
        $"{fileKind}: waarde '{value}' in kolom '{column}' is geen getal (onderdeel: {part}).";

    public static string CannotParseDimensions(string value, string part) =>
        $"Afmetingen niet leesbaar: '{value}' (onderdeel: {part}). Verwacht: 'L: 660.0mm X B: 590.0mm'.";

    public static string NoPartId(string description) =>
        $"Geen onderdeelnummer achteraan de omschrijving: '{description}'.";

    public static string BadSheetName(string sheetName) =>
        $"Plaatnaam heeft niet de vorm 'materiaal#nummer' (bv. White_18#01): '{sheetName}'.";

    public static string AngleNotMultipleOf90(double angle, string part) =>
        $"Hoek {NumberFormat.ToInvariantText(angle)} is geen veelvoud van 90 (onderdeel: {part}).";

    // ---- Joining LI and LP ----
    public static string DuplicateKey(string fileKind, string sheetName, string description) =>
        $"{fileKind}: dubbele combinatie plaat/omschrijving: {sheetName} / {description}";

    public static string PartOnlyInPositionFile(string sheetName, string description) =>
        $"Onderdeel staat in LP maar niet in LI: {sheetName} / {description}";

    public static string PartOnlyInInfoFile(string sheetName, string description) =>
        $"Onderdeel staat in LI maar niet in LP: {sheetName} / {description}";

    public static string DifferentSheetSizes(string material) =>
        $"Materiaal '{material}' heeft platen met verschillende afmetingen (SUP_L / SUP_B).";

    // ---- Materials ----
    public static string UnknownMaterial(string material) =>
        $"Materiaal '{material}' staat niet in materials.csv.";

    public static string MaterialsMissingColumn(string column) =>
        $"materials.csv: kolom '{column}' ontbreekt.";

    public static string MaterialsBadLine(int lineNumber, string reason) =>
        $"materials.csv, regel {lineNumber}: {reason}";

    public static string MaterialsKeyHasSheetNumber(string key) =>
        $"'{key}' bevat '#': zet enkel de materiaalnaam in materials.csv, zonder plaatnummer (bv. White_18, niet White_18#01).";
}
