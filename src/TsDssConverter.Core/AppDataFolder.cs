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
    /// Creates the folders, and an empty materials.csv (header row only) if there is none yet,
    /// so "Open materiaaltabel" always has a file to open. An existing file is never touched.
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
    }
}
