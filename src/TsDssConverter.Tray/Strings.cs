using TsDssConverter.Core;

namespace TsDssConverter.Tray;

/// <summary>
/// ALL texts of the user interface, in Dutch, French and English, in one place so they are easy to change.
/// The three versions of a text always sit next to each other: nl, fr, en. The language is chosen by
/// <see cref="Localizer"/>. (Error messages of the conversion itself are in Core\Messages.cs.)
/// </summary>
internal static class Strings
{
    private static string T(string dutch, string french, string english) => Localizer.T(dutch, french, english);

    // ---- Names that are the same in every language ----
    public const string AppName = "TsDssConverter";
    public const string DeveloperCredit = "Dev.: Daan Verhoost";
    public const string CompanyName = "ROGIERS NV/SA";
    public const string CompanyUrl = "https://www.rogiers.be/";

    /// <summary>The line at the bottom left of the settings window, e.g. "App version: 1.0.0".</summary>
    public static string AppVersion(string version) => T(
        $"App-versie: {version}",
        $"Version de l'application : {version}",
        $"App version: {version}");

    // ---- Language box: its title has all three languages, so it can be found in any language ----
    public const string LanguageTitle = "Taal / Langue / Language";
    public const string LanguageDutch = "Dutch - Nederlands";
    public const string LanguageFrench = "French - Français";
    public const string LanguageEnglish = "English - Engels";

    // ---- Tray menu ----
    public static string MenuSettings => T("Instellingen…", "Paramètres…", "Settings…");
    public static string MenuScanNow => T("Nu scannen", "Analyser maintenant", "Scan now");
    public static string MenuPause => T("Pauzeren", "Suspendre", "Pause");
    public static string MenuResume => T("Hervatten", "Reprendre", "Resume");
    public static string MenuOpenLogFolder => T("Open logmap", "Ouvrir le dossier des journaux", "Open log folder");
    public static string MenuOpenMaterials => T("Open materiaaltabel", "Ouvrir la table des matériaux", "Open materials table");
    public static string MenuExit => T("Afsluiten", "Quitter", "Exit");

    // ---- Demo menu (only with the --demo switch) ----
    public static string MenuDemoOk => T("Demo: conversie OK", "Démo : conversion OK", "Demo: conversion OK");
    public static string MenuDemoBusy => T("Demo: bezig", "Démo : en cours", "Demo: busy");
    public static string MenuDemoError => T("Demo: conversie mislukt", "Démo : conversion échouée", "Demo: conversion failed");
    public static string DemoOkMessage => T("3 platen, 12 labels", "3 panneaux, 12 étiquettes", "3 sheets, 12 labels");
    public static string DemoErrorMessage => T(
        "Materiaal 'White_18' staat niet in materials.csv.\r\nMateriaal 'White_9' staat niet in materials.csv.",
        "Le matériau 'White_18' ne figure pas dans materials.csv.\r\nLe matériau 'White_9' ne figure pas dans materials.csv.",
        "Material 'White_18' is not in materials.csv.\r\nMaterial 'White_9' is not in materials.csv.");

    // ---- Tray tooltip ----
    public static string TooltipNothingYet => T(
        "TsDssConverter - nog geen conversies",
        "TsDssConverter - aucune conversion pour l'instant",
        "TsDssConverter - no conversions yet");

    public static string TooltipLast(string project, bool success, DateTime time)
    {
        string result = success ? "OK" : T("FOUT", "ERREUR", "ERROR");
        return T(
            $"Laatste: {project} {result} {time:HH:mm}",
            $"Dernière : {project} {result} {time:HH:mm}",
            $"Last: {project} {result} {time:HH:mm}");
    }

    public static string TooltipPausedSuffix => T(" (gepauzeerd)", " (suspendu)", " (paused)");

    // ---- Balloon (only on errors) ----
    public static string BalloonErrorTitle(string project) => T(
        $"Conversie mislukt: {project}",
        $"Conversion échouée : {project}",
        $"Conversion failed: {project}");

    public static string BalloonSettingsTitle => T("Instellingen", "Paramètres", "Settings");

    // ---- Settings window ----
    public static string WindowTitle => T(
        "TsDssConverter - Instellingen", "TsDssConverter - Paramètres", "TsDssConverter - Settings");

    public static string StartWithWindows => T("Start met Windows", "Démarrer avec Windows", "Start with Windows");

    public static string ExportFolder => T(
        "TopSolid-exportmap (XLSX en CNC-programma's .xcs)",
        "Dossier d'export TopSolid (XLSX et programmes CNC .xcs)",
        "TopSolid export folder (XLSX and CNC programs .xcs)");

    public static string BatchFolder => T(
        "Duivestein-batchmap (XML, opdrachten)",
        "Dossier des lots Duivestein (XML, commandes)",
        "Duivestein batch folder (XML, jobs)");

    public static string LabelFolder => T(
        "Duivestein-labelmap (CSV)",
        "Dossier des étiquettes Duivestein (CSV)",
        "Duivestein label folder (CSV)");

    public static string Browse => T("Bladeren…", "Parcourir…", "Browse…");

    public static string FolderNotReachable => T(
        "Map niet bereikbaar. De instelling wordt toch bewaard.",
        "Dossier inaccessible. Le paramètre est quand même enregistré.",
        "Folder not reachable. The setting is saved anyway.");

    public static string LabelZeroPoint => T("Nulpunt van het label", "Point zéro de l'étiquette", "Label zero point");

    public static string FlipX => T(
        "Spiegelen in X (X = plaatlengte − X)",
        "Inverser en X (X = longueur du panneau − X)",
        "Flip in X (X = sheet length − X)");

    public static string FlipY => T(
        "Spiegelen in Y (Y = plaatbreedte − Y)",
        "Inverser en Y (Y = largeur du panneau − Y)",
        "Flip in Y (Y = sheet width − Y)");

    public static string Save => T("Opslaan", "Enregistrer", "Save");
    public static string Cancel => T("Annuleren", "Annuler", "Cancel");

    // ---- History list ----
    public static string HistoryTitle => T("Laatste conversies", "Dernières conversions", "Last conversions");
    public static string HistoryTime => T("Tijd", "Heure", "Time");
    public static string HistoryProject => T("Project", "Projet", "Project");
    public static string HistoryResult => T("Resultaat", "Résultat", "Result");
    public static string HistoryMessage => T("Melding", "Message", "Message");
    public static string HistoryEmpty => T("Nog geen conversies.", "Aucune conversion pour l'instant.", "No conversions yet.");
    public static string ResultOk => "OK";
    public static string ResultError => T("Fout", "Erreur", "Error");

    // ---- Messages in dialogs ----
    public static string FillAllFolders => T(
        "Vul alle drie de mappen in.",
        "Remplissez les trois dossiers.",
        "Fill in all three folders.");

    public static string SaveFailed(string reason) => T(
        $"De instellingen konden niet worden opgeslagen: {reason}",
        $"Les paramètres n'ont pas pu être enregistrés : {reason}",
        $"The settings could not be saved: {reason}");

    public static string StartWithWindowsFailed(string reason) => T(
        $"'Start met Windows' kon niet worden aangepast: {reason}",
        $"« Démarrer avec Windows » n'a pas pu être modifié : {reason}",
        $"'Start with Windows' could not be changed: {reason}");

    public static string DataFolderFailed(string folder, string reason) => T(
        $"De map {folder} kan niet worden aangemaakt of gebruikt ({reason}).\n\n" +
        "Maak de map zelf aan en geef alle gebruikers schrijfrechten, of vraag het aan de IT-afdeling.",
        $"Le dossier {folder} ne peut pas être créé ou utilisé ({reason}).\n\n" +
        "Créez le dossier vous-même et donnez à tous les utilisateurs les droits d'écriture, ou demandez-le au service informatique.",
        $"The folder {folder} cannot be created or used ({reason}).\n\n" +
        "Create the folder yourself and give all users write permission, or ask the IT department.");

    public static string CannotOpenFolder(string reason) => T(
        $"De map kon niet worden geopend: {reason}",
        $"Le dossier n'a pas pu être ouvert : {reason}",
        $"The folder could not be opened: {reason}");

    public static string CannotOpenFile(string reason) => T(
        $"Het bestand kon niet worden geopend: {reason}",
        $"Le fichier n'a pas pu être ouvert : {reason}",
        $"The file could not be opened: {reason}");

    public static string CannotOpenLink(string reason) => T(
        $"De website kon niet worden geopend: {reason}",
        $"Le site web n'a pas pu être ouvert : {reason}",
        $"The website could not be opened: {reason}");

    // ---- Log lines ----
    public static string LogStarted(string version, string folder) => T(
        $"{AppName} {version} gestart. Gegevensmap: {folder}",
        $"{AppName} {version} démarré. Dossier de données : {folder}",
        $"{AppName} {version} started. Data folder: {folder}");

    public static string LogExiting => T("Afgesloten door de gebruiker.", "Fermé par l'utilisateur.", "Closed by the user.");
    public static string LogSettingsSaved => T("Instellingen opgeslagen.", "Paramètres enregistrés.", "Settings saved.");
    public static string LogPaused => T("Gepauzeerd.", "Suspendu.", "Paused.");
    public static string LogResumed => T("Hervat.", "Repris.", "Resumed.");

    public static string LogLanguageChanged(string code) => T(
        $"Taal gewijzigd naar: {code}",
        $"Langue changée en : {code}",
        $"Language changed to: {code}");

    public static string LogResult(string project, string message) => T(
        $"Conversie {project}: {message}",
        $"Conversion {project} : {message}",
        $"Conversion {project}: {message}");
}
