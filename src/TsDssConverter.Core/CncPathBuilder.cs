namespace TsDssConverter.Core;

/// <summary>
/// Builds the paths of the CNC program of one sheet.
///
/// TopSolid names the CNC program of a sheet after the sheet ONLY ({sheet name}{extension}, e.g. MELA_BLANC_18#01.xcs);
/// the name is not in the XLSX files, so it is built from the sheet name. Since 1.1.2 (agreed with TopSolid 2026-11-04)
/// TopSolid writes the programs of a project in a folder named after the project, inside the export folder:
/// {ExportPath}\{project}\{sheet name}{ext}, e.g. Z:\TopSolid\Export\P2026-Cuisine-ROGIERS\MELA_BLANC_18#01.xcs.
/// Older exports wrote them directly in the export folder ({ExportPath}\{sheet name}{ext}); that place is still looked at
/// (<see cref="CncMover"/>), so both kinds of export work.
///
/// After the conversion the programs of a job are MOVED to their own folder: {ExportPath}\CNC\{project}\{sheet name}{ext}
/// and the batch XML points there. The export folder itself then only holds what is not converted yet.
/// This is the ONLY place that knows these rules, so it is easy to change.
/// </summary>
public static class CncPathBuilder
{
    /// <summary>The folder inside the export folder that holds one subfolder per converted project.</summary>
    public const string FolderName = "CNC";

    /// <summary>The path as written in the batch XML (as the Duivestein machine sees it): in the folder of the project.</summary>
    public static string Build(ConverterSettings settings, string project, string sheetName)
    {
        // The machine side may not know the Z: drive: then a prefix (e.g. a UNC path) replaces the export folder.
        string exportFolder = string.IsNullOrWhiteSpace(settings.CncPathPrefixInXml)
            ? settings.TopSolidExportPath
            : settings.CncPathPrefixInXml;

        return Combine(ProjectFolder(exportFolder, project), sheetName, settings);
    }

    /// <summary>The path on THIS PC where the program of a converted sheet is (never the prefix of the XML).</summary>
    public static string BuildLocalPath(ConverterSettings settings, string project, string sheetName)
    {
        return Combine(ProjectFolder(settings.TopSolidExportPath, project), sheetName, settings);
    }

    /// <summary>
    /// The folder where TopSolid writes the programs of one project: {export}\{project}\ (since 1.1.2).
    /// The project is the name of the XLSX files (everything before "-LI.xlsx").
    /// </summary>
    public static string BuildSourceFolder(ConverterSettings settings, string project)
    {
        return settings.TopSolidExportPath.TrimEnd('\\', '/') + "\\" + project;
    }

    /// <summary>Where TopSolid writes the program, before it is converted: in the folder of the project (since 1.1.2).</summary>
    public static string BuildSourcePath(ConverterSettings settings, string project, string sheetName)
    {
        return Combine(BuildSourceFolder(settings, project), sheetName, settings);
    }

    /// <summary>Where older exports wrote the program: directly in the export folder. Still looked at when the first place is empty.</summary>
    public static string BuildOldSourcePath(ConverterSettings settings, string sheetName)
    {
        return Combine(settings.TopSolidExportPath, sheetName, settings);
    }

    private static string ProjectFolder(string exportFolder, string project)
    {
        return exportFolder.TrimEnd('\\', '/') + "\\" + FolderName + "\\" + project;
    }

    private static string Combine(string folder, string sheetName, ConverterSettings settings)
    {
        return folder.TrimEnd('\\', '/') + "\\" + sheetName + settings.CncExtension;
    }
}
