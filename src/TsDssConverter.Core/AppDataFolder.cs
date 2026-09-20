using System.Text;

namespace TsDssConverter.Core;

/// <summary>
/// The one folder of the program: C:\TsDssConverter\ with the exe, settings.json, materials.csv and logs\.
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
    /// Creates the folders, an empty materials.csv (header row only) and the three column files (columns-li.txt,
    /// columns-lp.txt and columns-label.txt, with the default names) if they are not there yet, so there is always a file to open.
    /// An existing file is never touched: the customer may have changed it.
    /// Throws IOException or UnauthorizedAccessException if the folder cannot be created (for example
    /// when the IT department locked down C:\); the caller shows a message.
    /// </summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogFolder);

        if (!File.Exists(MaterialsFile))
        {
            string header = "TopSolidMaterial;DssMaterial;Thickness;Grain" + "\r\n";
            File.WriteAllText(MaterialsFile, header, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        if (!File.Exists(InfoColumnsFile))
        {
            ColumnMap.DefaultLabelInfo().WriteTemplate(InfoColumnsFile);
        }

        if (!File.Exists(PositionColumnsFile))
        {
            ColumnMap.DefaultLabelPosition().WriteTemplate(PositionColumnsFile);
        }

        if (!File.Exists(LabelColumnsFile))
        {
            new LabelColumnNames().WriteTemplate(LabelColumnsFile);
        }
    }
}
