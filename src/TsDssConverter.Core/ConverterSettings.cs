using System.Text;

namespace TsDssConverter.Core;

/// <summary>
/// The settings the conversion needs. The tray app will fill this from settings.json (stage 3);
/// the CLI uses the defaults plus a few command line switches.
/// </summary>
public class ConverterSettings
{
    /// <summary>Label zero point: X = sheet length - LABEL_X.</summary>
    public bool FlipX { get; set; } = false;

    /// <summary>Label zero point: Y = sheet width - LABEL_Y.</summary>
    public bool FlipY { get; set; } = false;

    /// <summary>TopSolid export folder, as seen from the Duivestein machine (drive letter, same Z: as the tray settings).</summary>
    public string TopSolidExportPath { get; set; } = @"Z:\TopSolid\Export\";

    /// <summary>Extension of the CNC program per sheet.</summary>
    public string CncExtension { get; set; } = ".xcs";

    /// <summary>If filled, replaces the export folder in the CNC paths of the XML (e.g. a UNC path). Empty = not used.</summary>
    public string CncPathPrefixInXml { get; set; } = "";

    /// <summary>Encoding of the label CSV files. To be confirmed by Duivestein (open point 10).</summary>
    public string CsvEncoding { get; set; } = "utf-8";

    /// <summary>
    /// Gives the .NET encoding for <see cref="CsvEncoding"/>.
    /// UTF-8 is written WITHOUT a byte order mark: the Duivestein example files have none.
    /// </summary>
    public Encoding GetCsvEncoding()
    {
        if (CsvEncoding.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        // Windows-1252 and other "old" code pages are only available after registering this provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(CsvEncoding);
    }
}
