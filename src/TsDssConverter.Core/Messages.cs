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

    /// <summary>Added once after the missing columns of a file: TopSolid may simply have renamed a column.</summary>
    public static string MissingColumnTip(string fileKind, string configFile) => T(
        $"Tip voor het {fileKind}-bestand: heeft TopSolid een kolom anders genoemd? Pas de naam aan in {configFile} (knop Config in het instellingenvenster).",
        $"Conseil pour le fichier {fileKind} : TopSolid a-t-il nommé une colonne autrement ? Modifiez le nom dans {configFile} (bouton Config dans la fenêtre des paramètres).",
        $"Tip for the {fileKind} file: did TopSolid name a column differently? Change the name in {configFile} (Config button in the settings window).");

    // ---- The column files (columns-li.txt and columns-lp.txt) ----
    public static string ColumnsBadLine(string fileName, int lineNumber, string reason) => T(
        $"{fileName}, regel {lineNumber}: {reason}",
        $"{fileName}, ligne {lineNumber} : {reason}",
        $"{fileName}, line {lineNumber}: {reason}");

    // The reasons that go behind "columns-li.txt, line N:"
    public static string ColumnsNotALine(string text) => T(
        $"'{text}' is geen geldige regel. Verwacht: Veld = Kolomnaam.",
        $"'{text}' n'est pas une ligne valide. Attendu : Champ = Nom de colonne.",
        $"'{text}' is not a valid line. Expected: Field = Column name.");

    public static string ColumnsUnknownField(string field, string knownFields) => T(
        $"onbekend veld '{field}'. Mogelijke velden: {knownFields}.",
        $"champ inconnu '{field}'. Champs possibles : {knownFields}.",
        $"unknown field '{field}'. Possible fields: {knownFields}.");

    public static string ColumnsDuplicateField(string field) => T(
        $"'{field}' staat er twee keer in.",
        $"'{field}' apparaît deux fois.",
        $"'{field}' appears twice.");

    public static string ColumnsNoName(string field) => T(
        $"'{field}' heeft geen kolomnaam achter het =-teken.",
        $"'{field}' n'a pas de nom de colonne après le signe =.",
        $"'{field}' has no column name after the = sign.");

    /// <summary>The explanation at the top of a new column file. One text per line; each line starts with "# " in the file.</summary>
    public static string ColumnsFileIntro(string fileKind, string example) => T(
        $"TsDssConverter - kolomnamen in het {fileKind}-bestand van TopSolid (-{fileKind}.xlsx)\n" +
        "Links van het = staat wat het programma nodig heeft (niet wijzigen).\n" +
        "Rechts staat de kolomnaam in het Excel-bestand. Pas die aan als TopSolid een kolom anders noemt.\n" +
        "Meerdere namen mogen, gescheiden door |. De eerste naam die in het bestand voorkomt, wordt gebruikt.\n" +
        $"Bijvoorbeeld:  {example}\n" +
        "De volgorde van de kolommen in Excel maakt niet uit, en hoofdletters ook niet.\n" +
        "Regels die met # beginnen worden genegeerd. Na het opslaan is herstarten niet nodig.",
        $"TsDssConverter - noms des colonnes du fichier {fileKind} de TopSolid (-{fileKind}.xlsx)\n" +
        "À gauche du signe = : ce dont le programme a besoin (à ne pas modifier).\n" +
        "À droite : le nom de la colonne dans le fichier Excel. Modifiez-le si TopSolid nomme une colonne autrement.\n" +
        "Plusieurs noms sont possibles, séparés par |. Le premier nom présent dans le fichier est utilisé.\n" +
        $"Exemple :  {example}\n" +
        "L'ordre des colonnes dans Excel n'a pas d'importance, ni les majuscules.\n" +
        "Les lignes qui commencent par # sont ignorées. Après l'enregistrement, aucun redémarrage n'est nécessaire.",
        $"TsDssConverter - column names in the TopSolid {fileKind} file (-{fileKind}.xlsx)\n" +
        "Left of the = is what the program needs (do not change it).\n" +
        "Right of the = is the column name in the Excel file. Change it if TopSolid names a column differently.\n" +
        "Several names are allowed, separated by |. The first name that is in the file is used.\n" +
        $"Example:  {example}\n" +
        "The order of the columns in Excel does not matter, and neither do capital letters.\n" +
        "Lines that start with # are ignored. After saving, no restart is needed.");

    /// <summary>The line above a field that the Excel file may do without.</summary>
    public static string ColumnsFileOptional => T(
        "optioneel: mag ontbreken in het Excel-bestand",
        "facultatif : peut être absent du fichier Excel",
        "optional: may be missing in the Excel file");

    /// <summary>The note above the ten description fields in columns-li.txt and columns-lp.txt (one text per line).</summary>
    public static string ColumnsFileDescriptionNote(string labelFile) => T(
        $"De 10 extra beschrijvingsvelden voor het label: kolom DESC1 t/m DESC10 in het Excel-bestand (mogen ontbreken).\n" +
        $"Ze komen achteraan in het label-CSV; de namen daar staan in {labelFile}.\n" +
        "Staan ze in beide bestanden (LI en LP), dan gaat de waarde uit LI voor; is die leeg, dan wordt die uit LP gebruikt.",
        $"Les 10 champs de description supplémentaires pour l'étiquette : colonnes DESC1 à DESC10 du fichier Excel (facultatives).\n" +
        $"Ils se retrouvent à la fin du CSV des étiquettes ; leurs noms y sont dans {labelFile}.\n" +
        "S'ils figurent dans les deux fichiers (LI et LP), la valeur de LI est utilisée ; si elle est vide, celle de LP.",
        $"The 10 extra description fields for the label: columns DESC1 to DESC10 in the Excel file (they may be missing).\n" +
        $"They end up at the end of the label CSV; their names there are in {labelFile}.\n" +
        "If they are in both files (LI and LP), the LI value is used; if that is empty, the LP value.");

    /// <summary>The explanation at the top of columns-label.txt. One text per line; each line starts with "# " in the file.</summary>
    public static string LabelColumnsFileIntro => T(
        "TsDssConverter - de 10 extra beschrijvingskolommen achteraan in het label-CSV voor Duivestein\n" +
        "Links van het = staat het veld (niet wijzigen): Desc1 t/m Desc10.\n" +
        "Rechts staat de naam van de kolom in het label-CSV. Naar die naam verwijst de labeltemplate in Duivestein.\n" +
        "De waarde komt uit de kolom DESC1 t/m DESC10 van het LI-bestand (of, als die leeg is, van het LP-bestand);\n" +
        "de kolomnamen in die Excel-bestanden staan in columns-li.txt en columns-lp.txt.\n" +
        "Toegestaan: enkel letters (zonder accenten), cijfers, underscore en streepje. De naam mag niet gelijk zijn aan een\n" +
        "andere kolom van het label-CSV (bv. MATERIAL, ID, X, DESCRIPTION).\n" +
        "Regels die met # beginnen worden genegeerd. Na het opslaan is herstarten niet nodig.",
        "TsDssConverter - les 10 colonnes de description supplémentaires à la fin du CSV des étiquettes pour Duivestein\n" +
        "À gauche du signe = : le champ (à ne pas modifier) : Desc1 à Desc10.\n" +
        "À droite : le nom de la colonne dans le CSV des étiquettes. Le modèle d'étiquette de Duivestein renvoie à ce nom.\n" +
        "La valeur vient de la colonne DESC1 à DESC10 du fichier LI (ou, si elle est vide, du fichier LP) ;\n" +
        "les noms de colonnes de ces fichiers Excel sont dans columns-li.txt et columns-lp.txt.\n" +
        "Autorisé : uniquement des lettres (sans accents), chiffres, tiret bas et tiret. Le nom ne peut pas être identique à\n" +
        "une autre colonne du CSV des étiquettes (p. ex. MATERIAL, ID, X, DESCRIPTION).\n" +
        "Les lignes qui commencent par # sont ignorées. Après l'enregistrement, aucun redémarrage n'est nécessaire.",
        "TsDssConverter - the 10 extra description columns at the end of the label CSV for Duivestein\n" +
        "Left of the = is the field (do not change it): Desc1 to Desc10.\n" +
        "Right of the = is the name of the column in the label CSV. The label template in Duivestein refers to that name.\n" +
        "The value comes from the column DESC1 to DESC10 of the LI file (or, if that is empty, of the LP file);\n" +
        "the column names in those Excel files are in columns-li.txt and columns-lp.txt.\n" +
        "Allowed: only letters (no accents), digits, underscore and dash. The name must not be the same as another\n" +
        "column of the label CSV (e.g. MATERIAL, ID, X, DESCRIPTION).\n" +
        "Lines that start with # are ignored. After saving, no restart is needed.");

    // The reasons that go behind "columns-label.txt, line N:"
    public static string LabelColumnBadName(string name) => T(
        $"kolomnaam '{name}' mag enkel letters (zonder accenten), cijfers, underscore en streepje bevatten.",
        $"le nom de colonne '{name}' ne peut contenir que des lettres (sans accents), chiffres, tirets bas et tirets.",
        $"column name '{name}' may only contain letters (no accents), digits, underscore and dash.");

    public static string LabelColumnClash(string name) => T(
        $"'{name}' is al de naam van een andere kolom van het label-CSV.",
        $"'{name}' est déjà le nom d'une autre colonne du CSV des étiquettes.",
        $"'{name}' is already the name of another column of the label CSV.");

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

    // Not a warning but an error: the program on disk belongs to another job, so it must not be used.
    public static string CncOverwrittenByLaterExport(string path) => T(
        $"CNC-programma is overschreven door een latere export (nieuwer dan het TR-bestand): {path}. Exporteer deze job opnieuw uit TopSolid.",
        $"Le programme CNC a été écrasé par un export plus récent (plus récent que le fichier TR) : {path}. Exportez ce job à nouveau depuis TopSolid.",
        $"CNC program was overwritten by a later export (newer than the TR file): {path}. Export this job again from TopSolid.");

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

    // ---- Watching the export folder and processing (stage 4) ----

    // The two folders inside the export folder where the TopSolid files are put away. Named in the selected language.
    // The scanner only looks at the top level, so whatever the name, these folders are never scanned again.
    public static string DoneFolderName => T("_Verwerkt", "_Effectuee", "_Converted");

    public static string ErrorFolderName => T("_Fout", "_Erreur", "_Error");

    /// <summary>
    /// The error folder in EVERY language, so "Retry failed" also finds the jobs that failed before the user switched the
    /// language. Keep in sync with <see cref="ErrorFolderName"/> (a test checks that).
    /// </summary>
    public static readonly string[] ErrorFolderNamesInAllLanguages = { "_Fout", "_Erreur", "_Error" };

    public static string ConversionDone(int sheets, int parts, int warnings)
    {
        string text = T(
            $"{sheets} platen, {parts} labels",
            $"{sheets} panneaux, {parts} étiquettes",
            $"{sheets} sheets, {parts} labels");

        if (warnings == 0)
        {
            return text;
        }

        return text + T(
            $" - waarschuwingen: {warnings} (zie logboek)",
            $" - avertissements : {warnings} (voir le journal)",
            $" - warnings: {warnings} (see log)");
    }

    /// <summary>Added to a problem that is tried again by itself later.</summary>
    public static string WillRetry => T(
        "Er wordt automatisch opnieuw geprobeerd.",
        "Une nouvelle tentative aura lieu automatiquement.",
        "It will be tried again automatically.");

    public static string FileMissingKind(string kind) => T(
        $"{kind}-bestand ontbreekt",
        $"fichier {kind} manquant",
        $"{kind} file is missing");

    public static string FileInUse(string fileName) => T(
        $"{fileName} is in gebruik door een ander programma",
        $"{fileName} est utilisé par un autre programme",
        $"{fileName} is in use by another program");

    public static string GaveUpWaiting(int minutes, string reasons) => T(
        $"Na {minutes} minuten wachten zijn de exportbestanden nog niet compleet: {reasons}.",
        $"Après {minutes} minutes d'attente, les fichiers d'export ne sont toujours pas complets : {reasons}.",
        $"After waiting {minutes} minutes the export files are still not complete: {reasons}.");

    public static string UnexpectedError(string message) => T(
        $"Onverwachte fout bij het lezen of omzetten van de bestanden: {message}",
        $"Erreur inattendue lors de la lecture ou de la conversion des fichiers : {message}",
        $"Unexpected error while reading or converting the files: {message}");

    public static string MoveFailed(string folder, string reason) => T(
        $"De bestanden konden niet naar {folder} worden verplaatst: {reason}",
        $"Les fichiers n'ont pas pu être déplacés vers {folder} : {reason}",
        $"The files could not be moved to {folder}: {reason}");

    // ---- fout.txt (written next to the files in the error folder) ----
    public static string ErrorReportTitle => T(
        "TsDssConverter - conversie mislukt",
        "TsDssConverter - conversion échouée",
        "TsDssConverter - conversion failed");

    public static string ErrorReportProject(string project) => T(
        $"Projectnaam: {project}",
        $"Projet : {project}",
        $"Project name: {project}");

    public static string ErrorReportTime(DateTime time)
    {
        // Fixed format: never depend on the regional settings of the PC.
        string text = time.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        return T($"Tijdstip: {text}", $"Heure : {text}", $"Time: {text}");
    }

    public static string ErrorReportProblems => T(
        "Wat er mis is:", "Ce qui ne va pas :", "What is wrong:");

    public static string ErrorReportHint => T(
        "Corrigeer het probleem en zet de bestanden terug in de TopSolid-exportmap (of exporteer opnieuw). De conversie start dan vanzelf opnieuw.",
        "Corrigez le problème puis remettez les fichiers dans le dossier d'export TopSolid (ou exportez à nouveau). La conversion redémarre alors automatiquement.",
        "Fix the problem and put the files back in the TopSolid export folder (or export again). The conversion then starts again by itself.");

    // ---- Log lines of the watcher ----
    public static string LogWatching(string folder) => T(
        $"Exportmap wordt bewaakt: {folder}",
        $"Surveillance du dossier d'export : {folder}",
        $"Watching the export folder: {folder}");

    public static string LogWatcherError(string reason) => T(
        $"De bewaking van de exportmap viel weg ({reason}). Ze wordt opnieuw gestart.",
        $"La surveillance du dossier d'export s'est arrêtée ({reason}). Elle est relancée.",
        $"Watching the export folder stopped ({reason}). It is being restarted.");

    public static string LogWatcherCannotStart(string reason) => T(
        $"De exportmap kan nog niet bewaakt worden: {reason}",
        $"Le dossier d'export ne peut pas encore être surveillé : {reason}",
        $"The export folder cannot be watched yet: {reason}");

    public static string ExportFolderNotReachable(string folder) => T(
        $"TopSolid-exportmap niet bereikbaar: {folder}. Er wordt automatisch opnieuw geprobeerd.",
        $"Dossier d'export TopSolid inaccessible : {folder}. Une nouvelle tentative aura lieu automatiquement.",
        $"TopSolid export folder not reachable: {folder}. It will be tried again automatically.");

    public static string LogExportFolderBack(string folder) => T(
        $"TopSolid-exportmap is weer bereikbaar: {folder}",
        $"Le dossier d'export TopSolid est de nouveau accessible : {folder}",
        $"TopSolid export folder is reachable again: {folder}");

    public static string LogConversionStarted(string project) => T(
        $"Conversie gestart: {project}",
        $"Conversion démarrée : {project}",
        $"Conversion started: {project}");

    public static string LogProjectWarning(string project, string warning) => T(
        $"Waarschuwing bij {project}: {warning}",
        $"Avertissement pour {project} : {warning}",
        $"Warning for {project}: {warning}");

    public static string LogFilesMoved(string folder) => T(
        $"Bestanden verplaatst naar: {folder}",
        $"Fichiers déplacés vers : {folder}",
        $"Files moved to: {folder}");

    public static string LogWatcherFailure(string reason) => T(
        $"Onverwachte fout in de bewaking: {reason}",
        $"Erreur inattendue dans la surveillance : {reason}",
        $"Unexpected error in the watcher: {reason}");
}
