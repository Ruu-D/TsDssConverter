namespace TsDssConverter.Core;

/// <summary>
/// Builds the paths of the CNC program of one sheet.
///
/// TopSolid writes the CNC program of a sheet in the export folder and names it after the sheet ONLY:
/// {ExportPath}\{sheet name}{extension}, e.g. Z:\TopSolid\Export\White_18#01.xcs (seen in all real exports we have:
/// SP_M_030_18#01.xcs, Melamine_18#01.xcs). The name is not in the XLSX files, so it is built from the sheet name;
/// TopSolid has not confirmed the convention yet (open point 1).
///
/// Because the name has no project in it, a second job with the same material would overwrite the file of the first job.
/// So after the conversion the programs of a job are MOVED to their own folder: {ExportPath}\CNC\{project}\{sheet name}{ext}
/// (<see cref="CncMover"/>) and the batch XML points there. The export folder itself then only holds what is not
/// converted yet. This is the ONLY place that knows these rules, so it is easy to change.
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

    /// <summary>The path where TopSolid writes the program: directly in the export folder, before it is converted.</summary>
    public static string BuildSourcePath(ConverterSettings settings, string sheetName)
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
