using System.Text;
using TsDssConverter.Core;

// Command line tool to test the conversion by hand (stage 1).
//
//   dotnet run --project src/TsDssConverter.Cli -- <LI.xlsx> <LP.xlsx> <materials.csv> <outFolder> [options]
//
// The XML and the label CSVs are both written to <outFolder>.
// Exit code: 0 = OK, 1 = the input has a problem (message in Dutch), 2 = unexpected error.

Console.OutputEncoding = Encoding.UTF8; // so "Chêne" is shown correctly

var settings = new ConverterSettings();
var paths = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--flipx":
            settings.FlipX = true;
            break;
        case "--flipy":
            settings.FlipY = true;
            break;
        case "--export-path" when i + 1 < args.Length:
            i++;
            settings.TopSolidExportPath = args[i];
            break;
        default:
            paths.Add(args[i]);
            break;
    }
}

if (paths.Count != 4)
{
    Console.WriteLine("Usage: TsDssConverter.Cli <LI.xlsx> <LP.xlsx> <materials.csv> <outFolder> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --flipx               label zero point: X = sheet length - LABEL_X");
    Console.WriteLine("  --flipy               label zero point: Y = sheet width - LABEL_Y");
    Console.WriteLine(@"  --export-path <path>  TopSolid export folder used in the CNC paths (default Z:\TopSolid\Export\)");
    return 1;
}

try
{
    string outFolder = paths[3];
    Directory.CreateDirectory(outFolder);

    var converter = new Converter();
    ConversionResult result = converter.Convert(
        paths[0], paths[1], paths[2], outFolder, outFolder, settings, DateTime.Today);

    Console.WriteLine($"OK: batch {result.BatchName}");
    Console.WriteLine($"  XML:        {result.XmlPath}");
    Console.WriteLine($"  Label CSVs: {result.LabelPaths.Count} files, {result.PartCount} labels in {outFolder}");
    foreach (string warning in result.Warnings)
    {
        Console.WriteLine($"  WAARSCHUWING: {warning}");
    }

    return 0;
}
catch (ConversionException ex)
{
    // A problem with the input: the message is meant for the user.
    Console.Error.WriteLine("FOUT:");
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Onverwachte fout:");
    Console.Error.WriteLine(ex);
    return 2;
}
