using System.Text.Json;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>
/// The default settings.json, materials.csv and column files are the files in the folder "defaults" of the project. They are
/// built into the exe and written to C:\TsDssConverter on the first start (so the delivery stays one file).
/// </summary>
public class PackedDefaultsTests
{
    private static string DefaultsFolder => Path.Combine(TestPaths.SamplesFolder, "..", "defaults");

    [Fact]
    public void EveryDefaultFile_IsInsideTheProgram_AndIdenticalToTheFileInTheDefaultsFolder()
    {
        Assert.Equal(5, PackedDefaults.FileNames.Length);

        foreach (string name in PackedDefaults.FileNames)
        {
            byte[]? packed = PackedDefaults.Read(name);

            Assert.True(packed != null && packed.Length > 0, name + " is not built into the program");
            Assert.True(File.ReadAllBytes(Path.Combine(DefaultsFolder, name)).SequenceEqual(packed!), name + " differs from defaults\\" + name);
        }
    }

    [Fact]
    public void AFileThatIsNotPacked_GivesNull()
    {
        Assert.Null(PackedDefaults.Read("nothing.txt"));
    }

    [Fact]
    public void ANewInstallation_GetsAllFiveDefaultFiles_ByteForByte()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.File("TsDssConverter"));

        folder.EnsureCreated();

        foreach (string name in PackedDefaults.FileNames)
        {
            Assert.True(File.ReadAllBytes(Path.Combine(folder.Root, name)).SequenceEqual(PackedDefaults.Read(name)!), name);
        }
    }

    [Fact]
    public void AnExistingFile_IsNeverReplaced_AndADeletedOneComesBack()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.Path);
        folder.EnsureCreated();

        File.WriteAllText(folder.MaterialsFile, "TopSolidMaterial;DssMaterial;Thickness;Grain\r\nMy_18;My_18;18;0\r\n");   // the customer's own list
        File.WriteAllText(folder.SettingsFile, "{ \"Language\": \"fr\" }");
        File.Delete(folder.InfoColumnsFile);

        folder.EnsureCreated();

        Assert.Contains("My_18", File.ReadAllText(folder.MaterialsFile));
        Assert.Contains("\"fr\"", File.ReadAllText(folder.SettingsFile));
        Assert.True(File.ReadAllBytes(folder.InfoColumnsFile).SequenceEqual(PackedDefaults.Read(ColumnMap.InfoFileName)!));
    }

    [Fact]
    public void ThePackedSettings_AreExactlyTheDefaultsOfTheProgram_AndLoadWithoutProblems()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.Path);
        folder.EnsureCreated();

        SettingsLoadResult loaded = SettingsStore.Load(folder.SettingsFile);

        Assert.True(loaded.FileExisted);
        Assert.Empty(loaded.Problems);
        Assert.Equal("nl", loaded.Settings.Language);   // a new installation starts in Dutch
        Assert.Equal(JsonSerializer.Serialize(new AppSettings()), JsonSerializer.Serialize(loaded.Settings));
    }

    [Fact]
    public void ThePackedFiles_ConvertTheSampleExport_WithoutAnyChange()
    {
        // The real test of the defaults: a new installation converts the golden input with nothing but the packed files.
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.File("TsDssConverter"));
        folder.EnsureCreated();
        string outFolder = Directory.CreateDirectory(temp.File("out")).FullName;

        var result = new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, folder.MaterialsFile, outFolder, outFolder, TestPaths.SafeSettings(), TestPaths.GoldenPlanDate,
            ColumnMap.LoadLabelInfo(folder.InfoColumnsFile), ColumnMap.LoadLabelPosition(folder.PositionColumnsFile),
            LabelColumnNames.Load(folder.LabelColumnsFile));

        Assert.Equal(3, result.LabelPaths.Count);
        Assert.Equal(22, result.PartCount);

        string xml = File.ReadAllText(Path.Combine(outFolder, "DAAN_ROGIERS-P2026.09.xml"));
        Assert.Contains("<Material>standaard_plaat_18mm</Material>", xml);   // the warehouse names of defaults\materials.csv
        Assert.Contains("<Material>rugpanelen</Material>", xml);

        string[] header = File.ReadAllLines(Path.Combine(outFolder, "DAAN_ROGIERS-P2026.09_001.csv"))[0].Split('\t');
        Assert.Equal(33, header.Length);
        Assert.Equal("DESC1", header[23]);
        Assert.Equal("DESC10", header[32]);
    }

    [Fact]
    public void ThePackedMaterialsFile_IsAValidFile_WithTheKnownMaterials()
    {
        // The packed materials.csv must be a valid file (a mistake in it would stop every conversion on a new installation).
        MaterialTable table = MaterialTable.Load(Path.Combine(DefaultsFolder, "materials.csv"));

        Assert.Equal("SP_standaard_18mm", table.Find("SP_M_030_18").DssName);
        Assert.Equal(8, table.Find("Melamine_08").Thickness);
    }
}
