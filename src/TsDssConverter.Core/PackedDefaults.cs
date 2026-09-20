using System.Reflection;

namespace TsDssConverter.Core;

/// <summary>
/// The default files that come with the program: settings.json, materials.csv, columns-li.txt, columns-lp.txt and
/// columns-label.txt. They are the files in the folder "defaults" of the project, built INTO the exe, so the delivery
/// is still one single file. <see cref="AppDataFolder.EnsureCreated"/> writes them to C:\TsDssConverter on the first start.
/// To change what a new installation starts with: edit the files in "defaults" and publish again.
/// </summary>
public static class PackedDefaults
{
    public static readonly string[] FileNames =
    {
        "settings.json", "materials.csv", ColumnMap.InfoFileName, ColumnMap.PositionFileName, LabelColumnNames.FileName,
    };

    /// <summary>The exact bytes of a default file (with the byte order mark and line endings it has). Null if there is none.</summary>
    public static byte[]? Read(string fileName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("defaults/" + fileName);
        if (stream == null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
