using System.Text;

namespace TsDssConverter.Core;

/// <summary>
/// The one folder of the program: C:\TsDssConverter\ with the exe, settings.json, materials.csv, the column files and logs\.
/// The tool is so small that everything lives together; "uninstall" is deleting this folder.
/// </summary>
public class AppDataFolder
{
    /// <summary>Where the program is installed and keeps its files. Changed here, and only here.</summary>
    public const string DefaultRoot = @"C:\TsDssConverter";

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string MaterialsFile => Path.Combine(Root, "materials.csv");

    /// <summary>The header names in the LI file. The customer can edit it when TopSolid renames a column.</summary>
    public string InfoColumnsFile => Path.Combine(Root, ColumnMap.InfoFileName);

    /// <summary>The header names in the LP file. The customer can edit it when TopSolid renames a column.</summary>
    public string PositionColumnsFile => Path.Combine(Root, ColumnMap.PositionFileName);

    /// <summary>The names of the ten DESC columns in the label CSV. The customer can edit it (Config button).</summary>
    public string LabelColumnsFile => Path.Combine(Root, LabelColumnNames.FileName);

    public string LogFolder => Path.Combine(Root, "logs");

    /// <param name="root">The folder to use. Tests and development runs pass another folder.</param>
    public AppDataFolder(string root)
    {
        Root = root;
    }

    public static AppDataFolder Default()
    {
        return new AppDataFolder(DefaultRoot);
    }

    /// <summary>
    /// Creates the folders and, for every file that is not there yet, the default file that comes with the program
    /// (<see cref="PackedDefaults"/>): settings.json, materials.csv, columns-li.txt, columns-lp.txt and columns-label.txt.
    /// So there is always a file to open. An existing file is never touched: the customer may have changed it.
    /// Throws IOException or UnauthorizedAccessException if the folder cannot be created (for example
    /// when the IT department locked down C:\); the caller shows a message.
    /// </summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogFolder);

        // If a default file were ever missing from the exe (a test checks that none is), the older way is the fallback:
        // an empty materials.csv (header row only) and the column files written from the built-in names.
        CreateFromDefault(SettingsFile, () => { });   // the program writes settings.json itself
        CreateFromDefault(MaterialsFile, () =>
        {
            string header = "TopSolidMaterial;DssMaterial;Thickness;Grain" + "\r\n";
            File.WriteAllText(MaterialsFile, header, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        });
        CreateFromDefault(InfoColumnsFile, () => ColumnMap.DefaultLabelInfo().WriteTemplate(InfoColumnsFile));
        CreateFromDefault(PositionColumnsFile, () => ColumnMap.DefaultLabelPosition().WriteTemplate(PositionColumnsFile));
        CreateFromDefault(LabelColumnsFile, () => new LabelColumnNames().WriteTemplate(LabelColumnsFile));
    }

    private static void CreateFromDefault(string path, Action writeFallback)
    {
        if (File.Exists(path))
        {
            return;
        }

        byte[]? packed = PackedDefaults.Read(Path.GetFileName(path));
        if (packed != null)
        {
            File.WriteAllBytes(path, packed);
        }
        else
        {
            writeFallback();
        }
    }
}
