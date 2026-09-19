namespace TsDssConverter.Core;

/// <summary>
/// Builds the path of the CNC program of one sheet.
///
/// PROVISIONAL: the CNC file name is not in the TopSolid export. Until TopSolid confirms their
/// naming convention this is {ExportPath}\{BatchName}_{sheet name with # replaced by _}{extension},
/// e.g. Z:\TopSolid\Export\Verschuren-P-20_White_18_01.xcs
/// This is the ONLY place that knows the rule, so it is easy to change.
/// </summary>
public static class CncPathBuilder
{
    /// <summary>The path as written in the batch XML (as the Duivestein machine sees it).</summary>
    public static string Build(ConverterSettings settings, string batchName, string sheetName)
    {
        // The machine side may not know the Z: drive: then a prefix (e.g. a UNC path) replaces the export folder.
        string folder = string.IsNullOrWhiteSpace(settings.CncPathPrefixInXml)
            ? settings.TopSolidExportPath
            : settings.CncPathPrefixInXml;

        return CombineFolderAndFile(folder, batchName, sheetName, settings);
    }

    /// <summary>The path on THIS PC, in the export folder. Used to check that the file exists (never the prefix).</summary>
    public static string BuildLocalPath(ConverterSettings settings, string batchName, string sheetName)
    {
        return CombineFolderAndFile(settings.TopSolidExportPath, batchName, sheetName, settings);
    }

    private static string CombineFolderAndFile(string folder, string batchName, string sheetName, ConverterSettings settings)
    {
        string fileName = batchName + "_" + sheetName.Replace('#', '_') + settings.CncExtension;
        return folder.TrimEnd('\\', '/') + "\\" + fileName;
    }
}
