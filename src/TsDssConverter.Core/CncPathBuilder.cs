namespace TsDssConverter.Core;

/// <summary>
/// Builds the path of the CNC program of one sheet, as written in the batch XML.
///
/// PROVISIONAL: the CNC file name is not in the TopSolid export. Until TopSolid confirms their
/// naming convention this is {ExportPath}\{BatchName}_{sheet name with # replaced by _}{extension},
/// e.g. Z:\TopSolid\Export\Verschuren-P-20_White_18_01.xcs
/// This is the ONLY place that knows the rule, so it is easy to change.
/// </summary>
public static class CncPathBuilder
{
    public static string Build(ConverterSettings settings, string batchName, string sheetName)
    {
        string fileName = batchName + "_" + sheetName.Replace('#', '_') + settings.CncExtension;

        // The machine side may not know the Z: drive: then a prefix (e.g. a UNC path) replaces the export folder.
        string folder = string.IsNullOrWhiteSpace(settings.CncPathPrefixInXml)
            ? settings.TopSolidExportPath
            : settings.CncPathPrefixInXml;

        return folder.TrimEnd('\\', '/') + "\\" + fileName;
    }
}
