namespace TsDssConverter.Core;

/// <summary>
/// All texts of the conversion that a user can see (errors, warnings, notes), in Dutch, French and English,
/// in one place so they are easy to change. The language is chosen by <see cref="Localizer"/>.
/// The three versions of a text always sit next to each other: nl, fr, en.
/// </summary>
public static class Messages
{
    private static string T(string dutch, string french, string english) => Localizer.T(dutch, french, english);

    // ---- Files and folders ----
    public static string FileNotFound(string path) => T(
        $"Bestand niet gevonden: {path}",
        $"Fichier introuvable : {path}",
        $"File not found: {path}");

    public static string FolderNotFound(string path) => T(
        $"Map niet bereikbaar: {path}",
        $"Dossier inaccessible : {path}",
        $"Folder not reachable: {path}");

    public static string BatchAlreadyExists(string batchName) => T(
        $"Batch bestaat al in Duivestein-map: {batchName}.xml",
        $"Le lot existe déjà dans le dossier Duivestein : {batchName}.xml",
        $"Batch already exists in the Duivestein folder: {batchName}.xml");

    public static string BadInfoFileName(string fileName) => T(
        $"Bestandsnaam moet eindigen op '-LI.xlsx' (label-info): {fileName}",
        $"Le nom du fichier doit se terminer par '-LI.xlsx' (infos étiquettes) : {fileName}",
        $"File name must end with '-LI.xlsx' (label info): {fileName}");

    // ---- XLSX content ----
    public static string MissingColumn(string fileKind, string column) => T(
        $"Kolom '{column}' ontbreekt in het {fileKind}-bestand.",
        $"La colonne '{column}' est absente du fichier {fileKind}.",
        $"Column '{column}' is missing in the {fileKind} file.");

    public static string NotANumber(string fileKind, string column, string value, string part) => T(
        $"{fileKind}: waarde '{value}' in kolom '{column}' is geen getal (onderdeel: {part}).",
        $"{fileKind} : la valeur '{value}' dans la colonne '{column}' n'est pas un nombre (pièce : {part}).",
        $"{fileKind}: value '{value}' in column '{column}' is not a number (part: {part}).");

    public static string CannotParseDimensions(string value, string part) => T(
        $"Afmetingen niet leesbaar: '{value}' (onderdeel: {part}). Verwacht: 'L: 660.0mm X B: 590.0mm'.",
        $"Dimensions illisibles : '{value}' (pièce : {part}). Attendu : 'L: 660.0mm X B: 590.0mm'.",
        $"Dimensions unreadable: '{value}' (part: {part}). Expected: 'L: 660.0mm X B: 590.0mm'.");

    public static string NoPartId(string description) => T(
        $"Geen onderdeelnummer achteraan de omschrijving: '{description}'.",
        $"Aucun numéro de pièce à la fin de la description : '{description}'.",
        $"No part number at the end of the description: '{description}'.");

    public static string BadSheetName(string sheetName) => T(
        $"Plaatnaam heeft niet de vorm 'materiaal#nummer' (bv. White_18#01): '{sheetName}'.",
        $"Le nom du panneau n'a pas la forme 'matériau#numéro' (p. ex. White_18#01) : '{sheetName}'.",
        $"Sheet name does not have the form 'material#number' (e.g. White_18#01): '{sheetName}'.");

    public static string AngleNotMultipleOf90(double angle, string part)
    {
        string text = NumberFormat.ToDss(angle);
        return T(
            $"Hoek {text} is geen veelvoud van 90 (onderdeel: {part}).",
            $"L'angle {text} n'est pas un multiple de 90 (pièce : {part}).",
            $"Angle {text} is not a multiple of 90 (part: {part}).");
    }

    public static string LabelOutsideSheet(double x, double y, double sheetLength, double sheetWidth, string part)
    {
        string xs = NumberFormat.ToDss(x), ys = NumberFormat.ToDss(y);
        string length = NumberFormat.ToDss(sheetLength), width = NumberFormat.ToDss(sheetWidth);
        return T(
            $"Label ligt buiten de plaat: X={xs}, Y={ys} bij een plaat van {length} x {width} mm (onderdeel: {part}).",
            $"L'étiquette se trouve en dehors du panneau : X={xs}, Y={ys} pour un panneau de {length} x {width} mm (pièce : {part}).",
            $"Label lies outside the sheet: X={xs}, Y={ys} on a sheet of {length} x {width} mm (part: {part}).");
    }

    /// <summary>Last line of a long list of problems, when the list is cut off.</summary>
    public static string AndMoreProblems(int count) => T(
        $"... en nog {count} andere problemen.",
        $"... et {count} autres problèmes.",
        $"... and {count} more problems.");

    // ---- Joining LI and LP ----
    public static string DuplicateKey(string fileKind, string sheetName, string description) => T(
        $"{fileKind}: dubbele combinatie plaat/omschrijving: {sheetName} / {description}",
        $"{fileKind} : combinaison panneau/description en double : {sheetName} / {description}",
        $"{fileKind}: duplicate combination of sheet/description: {sheetName} / {description}");

    public static string PartOnlyInPositionFile(string sheetName, string description) => T(
        $"Onderdeel staat in LP maar niet in LI: {sheetName} / {description}",
        $"La pièce figure dans LP mais pas dans LI : {sheetName} / {description}",
        $"Part is in LP but not in LI: {sheetName} / {description}");

    public static string PartOnlyInInfoFile(string sheetName, string description) => T(
        $"Onderdeel staat in LI maar niet in LP: {sheetName} / {description}",
        $"La pièce figure dans LI mais pas dans LP : {sheetName} / {description}",
        $"Part is in LI but not in LP: {sheetName} / {description}");

    public static string DifferentSheetSizes(string material) => T(
        $"Materiaal '{material}' heeft platen met verschillende afmetingen (SUP_L / SUP_B).",
        $"Le matériau '{material}' a des panneaux de dimensions différentes (SUP_L / SUP_B).",
        $"Material '{material}' has sheets with different sizes (SUP_L / SUP_B).");

    // ---- Materials ----
    public static string UnknownMaterial(string material) => T(
        $"Materiaal '{material}' staat niet in materials.csv.",
        $"Le matériau '{material}' ne figure pas dans materials.csv.",
        $"Material '{material}' is not in materials.csv.");

    public static string MaterialsMissingColumn(string column) => T(
        $"materials.csv: kolom '{column}' ontbreekt.",
        $"materials.csv : la colonne '{column}' est absente.",
        $"materials.csv: column '{column}' is missing.");

    public static string MaterialsBadLine(int lineNumber, string reason) => T(
        $"materials.csv, regel {lineNumber}: {reason}",
        $"materials.csv, ligne {lineNumber} : {reason}",
        $"materials.csv, line {lineNumber}: {reason}");

    public static string MaterialsKeyHasSheetNumber(string key) => T(
        $"'{key}' bevat '#': zet enkel de materiaalnaam in materials.csv, zonder plaatnummer (bv. White_18, niet White_18#01).",
        $"'{key}' contient '#' : n'indiquez que le nom du matériau dans materials.csv, sans numéro de panneau (p. ex. White_18, pas White_18#01).",
        $"'{key}' contains '#': put only the material name in materials.csv, without the sheet number (e.g. White_18, not White_18#01).");

    // The reasons that go behind "materials.csv, line N:"
    public static string MaterialsTooFewColumns => T(
        "te weinig kolommen.", "trop peu de colonnes.", "too few columns.");

    public static string MaterialsEmptyName => T(
        "TopSolidMaterial is leeg.", "TopSolidMaterial est vide.", "TopSolidMaterial is empty.");

    public static string MaterialsEmptyDssName => T(
        "DssMaterial is leeg.", "DssMaterial est vide.", "DssMaterial is empty.");

    public static string MaterialsBadThickness(string value) => T(
        $"Thickness '{value}' is geen geldig getal.",
        $"Thickness '{value}' n'est pas un nombre valide.",
        $"Thickness '{value}' is not a valid number.");

    public static string MaterialsBadGrain(string value) => T(
        $"Grain '{value}' moet 0, 1 of 2 zijn.",
        $"Grain '{value}' doit être 0, 1 ou 2.",
        $"Grain '{value}' must be 0, 1 or 2.");

    public static string MaterialsDuplicate(string name) => T(
        $"'{name}' staat er twee keer in.",
        $"'{name}' apparaît deux fois.",
        $"'{name}' appears twice.");

    // ---- settings.json ----
    public static string SettingsUnreadable(string path, string reason) => T(
        $"settings.json kon niet gelezen worden ({reason}). De standaardinstellingen worden gebruikt: {path}",
        $"Impossible de lire settings.json ({reason}). Les paramètres par défaut sont utilisés : {path}",
        $"settings.json could not be read ({reason}). The default settings are used: {path}");

    public static string SettingReset(string setting, string reason) => T(
        $"Instelling {setting} is teruggezet naar de standaardwaarde: {reason}.",
        $"Le paramètre {setting} a été réinitialisé à sa valeur par défaut : {reason}.",
        $"Setting {setting} was reset to its default value: {reason}.");

    // The reasons that go behind "Setting X was reset:"
    public static string ReasonEmpty => T(
        "waarde is leeg", "la valeur est vide", "value is empty");

    public static string ReasonOutOfRange(int value, int min, int max) => T(
        $"{value} is buiten {min}-{max}",
        $"{value} est en dehors de {min}-{max}",
        $"{value} is outside {min}-{max}");

    public static string ReasonUnknownEncoding(string name) => T(
        $"'{name}' is geen bekende codering",
        $"'{name}' n'est pas un encodage connu",
        $"'{name}' is not a known encoding");

    public static string ReasonEmptyFile => T(
        "leeg bestand", "fichier vide", "empty file");

    public static string ReasonUnknownLanguage(string value) => T(
        $"'{value}' is geen ondersteunde taal (nl, fr of en)",
        $"'{value}' n'est pas une langue prise en charge (nl, fr ou en)",
        $"'{value}' is not a supported language (nl, fr or en)");

    // ---- Warnings (the conversion continues) ----
    public static string CncFolderNotReachable(string folder) => T(
        $"TopSolid-exportmap niet bereikbaar, CNC-programma's niet gecontroleerd: {folder}",
        $"Dossier d'export TopSolid inaccessible, programmes CNC non vérifiés : {folder}",
        $"TopSolid export folder not reachable, CNC programs not checked: {folder}");

    public static string CncFileNotFound(string path) => T(
        $"CNC-programma niet gevonden (TopSolid schrijft het misschien nog): {path}",
        $"Programme CNC introuvable (TopSolid est peut-être encore en train de l'écrire) : {path}",
        $"CNC program not found (TopSolid may still be writing it): {path}");

    public static string DuplicatePartId(string id, IEnumerable<string> sheetNames)
    {
        string sheets = string.Join(", ", sheetNames);
        return T(
            $"Onderdeelnummer {id} komt meer dan één keer voor in de batch (platen: {sheets}).",
            $"Le numéro de pièce {id} apparaît plusieurs fois dans le lot (panneaux : {sheets}).",
            $"Part number {id} occurs more than once in the batch (sheets: {sheets}).");
    }

    public static string ThicknessDiffers(string material, double inMaterialsFile, double inTopSolid)
    {
        string csv = NumberFormat.ToDss(inMaterialsFile), topSolid = NumberFormat.ToDss(inTopSolid);
        return T(
            $"Materiaal '{material}': dikte in materials.csv ({csv} mm) verschilt van SUP_DESIGNATION in TopSolid ({topSolid} mm).",
            $"Matériau '{material}' : l'épaisseur dans materials.csv ({csv} mm) diffère de SUP_DESIGNATION dans TopSolid ({topSolid} mm).",
            $"Material '{material}': thickness in materials.csv ({csv} mm) differs from SUP_DESIGNATION in TopSolid ({topSolid} mm).");
    }
}
