using System.Globalization;
using System.Text;
using System.Xml.Linq;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>
/// Converts the real sample files (samples/topsolid) and checks the result against the expected output
/// in samples/duivestein ("golden files") and against the numbers in CLAUDE.md.
/// </summary>
public class SampleConversionTests
{
    private static ConversionResult ConvertSample(string outFolder, ConverterSettings? settings = null, string? materialsFile = null)
    {
        return new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, materialsFile ?? TestPaths.MaterialsFile,
            outFolder, outFolder, settings ?? TestPaths.SafeSettings(), TestPaths.GoldenPlanDate);
    }

    /// <summary>Reads a label CSV of the converted sample into rows keyed by column name.</summary>
    private static List<Dictionary<string, string>> ReadCsv(string path)
    {
        string[] lines = File.ReadAllText(path, new UTF8Encoding(false)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] header = lines[0].Split('\t');

        return lines.Skip(1)
            .Select(line => line.Split('\t'))
            .Select(cells => header.Zip(cells).ToDictionary(pair => pair.First, pair => pair.Second))
            .ToList();
    }

    // The label folder that is written in the golden XML (the default of the third setting).
    private const string GoldenLabelFolder = @"Z:\Duivestein\Label";
    private const string GoldenExportFolder = @"Z:\TopSolid\Export";

    [Fact]
    public void LabelFilenames_InTheXml_AreTheLabelFolderPlusTheFileName()
    {
        using var folder = new TempFolder();
        string labelFolder = System.IO.Path.Combine(folder.Path, "labels");
        string batchFolder = System.IO.Path.Combine(folder.Path, "batch");
        Directory.CreateDirectory(labelFolder);
        Directory.CreateDirectory(batchFolder);

        new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, TestPaths.MaterialsFile,
            batchFolder, labelFolder, TestPaths.SafeSettings(), TestPaths.GoldenPlanDate);

        var xml = XDocument.Load(System.IO.Path.Combine(batchFolder, "DAAN_ROGIERS-P2026.09.xml"));
        string[] labelNames = xml.Descendants("LabelFilename").Select(e => e.Value).ToArray();

        Assert.Equal(3, labelNames.Length);
        Assert.Equal(System.IO.Path.Combine(labelFolder, "DAAN_ROGIERS-P2026.09_001.csv"), labelNames[0]);
        Assert.Equal(System.IO.Path.Combine(labelFolder, "DAAN_ROGIERS-P2026.09_003.csv"), labelNames[2]);

        // The path points at the file that was really written.
        Assert.All(labelNames, path => Assert.True(File.Exists(path), path));
    }

    [Theory]
    [InlineData(@"Z:\Duivestein\Label")]
    [InlineData(@"Z:\Duivestein\Label\")]   // a backslash at the end of the setting gives no double backslash
    public void LabelFilename_HasExactlyOneBackslashBetweenTheFolderAndTheName(string folder)
    {
        var batch = new Batch { Name = "Daan", PlanDate = TestPaths.GoldenPlanDate };
        var material = new Material { TopSolidName = "M", DssName = "M", Thickness = 18, Grain = 0 };
        var plan = new Plan { PlanName = "001", Material = material, SheetLength = 100, SheetWidth = 50 };
        plan.Sheets.Add(new Sheet { Name = "M#01", LabelFileName = "Daan_001.csv", CncPath = @"Z:\TopSolid\Export\Daan_M_01.xcs" });
        batch.Plans.Add(plan);

        string xml = BatchXmlWriter.BuildText(batch, folder);

        Assert.Contains(@"<LabelFilename>Z:\Duivestein\Label\Daan_001.csv</LabelFilename>", xml);
    }

    [Fact]
    public void Output_IsIdenticalToTheGoldenFiles()
    {
        using var folder = new TempFolder();

        ConvertSample(folder.Path);

        // An explicit list: the folder also holds the "corrected file" from Duivestein (see the test below), which is no golden file.
        string[] goldenFiles = new[]
            {
                "DAAN_ROGIERS-P2026.09.xml", "DAAN_ROGIERS-P2026.09_001.csv", "DAAN_ROGIERS-P2026.09_002.csv", "DAAN_ROGIERS-P2026.09_003.csv",
            }
            .Select(name => System.IO.Path.Combine(TestPaths.GoldenFolder, name))
            .ToArray();
        Assert.All(goldenFiles, path => Assert.True(File.Exists(path), path)); // 1 XML + 3 CSVs

        foreach (string goldenFile in goldenFiles)
        {
            string name = System.IO.Path.GetFileName(goldenFile);
            string actualFile = folder.File(name);

            Assert.True(File.Exists(actualFile), "Missing output file " + name);
            byte[] actual = File.ReadAllBytes(actualFile);

            if (name.EndsWith(".xml"))
            {
                // The XML holds the full path of every label file: the label folder + the file name. The golden file
                // has the folder of the settings (Z:\Duivestein\Label); this test wrote to a temp folder. Put the
                // golden folder in the place of the temp folder, and compare everything else byte for byte.
                // The same for the export folder: the CNC paths hold the (non-existing) test export folder.
                string text = new UTF8Encoding(false).GetString(actual);
                text = text.Replace(TestPaths.NoExportFolder + @"\", GoldenExportFolder + @"\");
                actual = new UTF8Encoding(false).GetBytes(text.Replace(folder.Path + @"\", GoldenLabelFolder + @"\"));
            }

            Assert.True(
                File.ReadAllBytes(goldenFile).SequenceEqual(actual),
                "Output differs from the golden file " + name);
        }

        // Nothing else was written, and no ".tmp" files are left behind.
        Assert.Equal(4, Directory.GetFiles(folder.Path).Length);
    }

    [Fact]
    public void Output_WithTheRealMaterials_IsTheBatchXmlThatDssClientAccepted()
    {
        // Daan tested our first XML in DSSClient and corrected it by hand: the "corrected file" (MaterialName in every plan, and
        // the constants MaxStackHeight 1, EqualStackHeight False, CutCount 0, all confirmed by Daan). With Daan's real
        // materials.csv (the packed defaults) our XML must be that file, byte for byte.
        using var temp = new TempFolder();
        var appData = new AppDataFolder(temp.File("data"));
        appData.EnsureCreated();
        string outFolder = Directory.CreateDirectory(temp.File("out")).FullName;

        new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, appData.MaterialsFile, outFolder, outFolder,
            TestPaths.SafeSettings(), TestPaths.GoldenPlanDate);

        string actual = File.ReadAllText(System.IO.Path.Combine(outFolder, "DAAN_ROGIERS-P2026.09.xml"), new UTF8Encoding(false));
        actual = actual.Replace(TestPaths.NoExportFolder + @"\", GoldenExportFolder + @"\").Replace(outFolder + @"\", GoldenLabelFolder + @"\");

        string corrected = File.ReadAllText(TestPaths.AcceptedByDssClientFile, new UTF8Encoding(false));

        Assert.Equal(corrected, actual);
    }

    [Fact]
    public void Output_IsTheSameOnABelgianPc()
    {
        // A Belgian PC uses a comma as decimal separator. The result must not change.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-BE");
            using var folder = new TempFolder();

            ConvertSample(folder.Path);

            byte[] golden = File.ReadAllBytes(System.IO.Path.Combine(TestPaths.GoldenFolder, "DAAN_ROGIERS-P2026.09_002.csv"));
            byte[] actual = File.ReadAllBytes(folder.File("DAAN_ROGIERS-P2026.09_002.csv"));
            Assert.True(golden.SequenceEqual(actual));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void BatchXml_HasTwoPlansInLpOrder()
    {
        using var folder = new TempFolder();
        var result = ConvertSample(folder.Path);

        Assert.Equal("DAAN_ROGIERS-P2026.09", result.BatchName);
        Assert.Equal(3, result.LabelPaths.Count);
        Assert.Equal(22, result.PartCount);

        var plans = XDocument.Load(folder.File("DAAN_ROGIERS-P2026.09.xml")).Root!.Element("Plans")!.Elements("Plan").ToList();

        // Order of first appearance in the LP file, with the number of sheets per material.
        var expected = new (string Material, int Sheets)[]
        {
            ("Melamine_18", 2),
            ("Melamine_08", 1),
        };

        Assert.Equal(expected.Length, plans.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Material, plans[i].Element("MaterialName")!.Value);
            Assert.Equal(expected[i].Sheets.ToString(), plans[i].Element("Quantity")!.Value);
            Assert.Equal(expected[i].Sheets, plans[i].Element("LabelFilenames")!.Elements().Count());
            Assert.Equal(expected[i].Sheets, plans[i].Element("CNCFilenames")!.Elements().Count());
        }

        Assert.Equal("2026-09-20", XDocument.Load(folder.File("DAAN_ROGIERS-P2026.09.xml")).Root!.Element("Date")!.Value);
    }

    [Fact]
    public void BatchXml_HasTheSyntaxThatDssClientAccepted_InThatOrder()
    {
        // The "corrected file" of Duivestein (test in DSSClient, 2026-09-21): these nodes in this order, and the counts.
        using var folder = new TempFolder();
        ConvertSample(folder.Path);
        XElement root = XDocument.Load(folder.File("DAAN_ROGIERS-P2026.09.xml")).Root!;

        Assert.Equal(
            new[]
            {
                "BatchName", "BatchDescription", "Date", "MaxStackHeight", "EqualStackHeight",
                "PlanCount", "BoardCount", "PartCount", "CutCount", "Plans", "Routes",
            },
            root.Elements().Select(element => element.Name.LocalName));

        // The old names must be gone: DSSClient did not accept them.
        Assert.Null(root.Element("PlanDate"));
        Assert.Null(root.Element("AutoExpand"));

        Assert.Equal("1", root.Element("MaxStackHeight")!.Value);
        Assert.Equal("False", root.Element("EqualStackHeight")!.Value);
        Assert.Equal("2", root.Element("PlanCount")!.Value);   // 2 materials
        Assert.Equal("3", root.Element("BoardCount")!.Value);  // 3 sheets = 3 label files
        Assert.Equal("22", root.Element("PartCount")!.Value);  // 22 parts = 22 labels
        Assert.Equal("0", root.Element("CutCount")!.Value);

        // The material of EVERY plan is called MaterialName.
        foreach (XElement plan in root.Element("Plans")!.Elements("Plan"))
        {
            Assert.Equal(
                new[]
                {
                    "PlanName", "MaterialName", "XDimSize", "YDimSize", "Grain", "Quantity", "Rotation",
                    "LabelFilenames", "CNCFilenames",
                },
                plan.Elements().Select(element => element.Name.LocalName));
        }
    }

    [Fact]
    public void BatchXml_CountsFollowTheBatch_NotTheSample()
    {
        // PlanCount, BoardCount and PartCount are counted, never typed: check them with a batch of another size.
        var batch = new Batch { Name = "Daan", PlanDate = TestPaths.GoldenPlanDate };
        var material = new Material { TopSolidName = "M", DssName = "M", Thickness = 18, Grain = 0 };
        var plan = new Plan { PlanName = "001", Material = material, SheetLength = 100, SheetWidth = 50 };

        for (int sheetNumber = 1; sheetNumber <= 2; sheetNumber++)
        {
            var sheet = new Sheet { Name = "M#0" + sheetNumber, LabelFileName = $"Daan_00{sheetNumber}.csv", CncPath = @"Z:\x.xcs" };
            for (int part = 0; part < 3; part++)
            {
                sheet.Labels.Add(new TsDssConverter.Core.Label());
            }

            plan.Sheets.Add(sheet);
        }

        batch.Plans.Add(plan);

        XElement root = XDocument.Parse(BatchXmlWriter.BuildText(batch, @"Z:\Label")).Root!;

        Assert.Equal("1", root.Element("PlanCount")!.Value);
        Assert.Equal("2", root.Element("BoardCount")!.Value);
        Assert.Equal("6", root.Element("PartCount")!.Value);
    }

    [Fact]
    public void LabelCsvs_HaveTheExpectedNumberOfRowsPerSheet()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);

        // Parts per sheet, in file order 001 ... 003 (Melamine_18#01, Melamine_18#02, Melamine_08#01).
        int[] expectedRows = { 13, 5, 4 };

        for (int i = 0; i < expectedRows.Length; i++)
        {
            var rows = ReadCsv(folder.File($"DAAN_ROGIERS-P2026.09_{i + 1:000}.csv"));
            Assert.Equal(expectedRows[i], rows.Count);

            // N runs 1..n on every sheet.
            Assert.Equal(Enumerable.Range(1, rows.Count).Select(n => n.ToString()), rows.Select(r => r["N"]));
        }

        Assert.Equal(22, expectedRows.Sum());
    }

    [Fact]
    public void Part3575_HasTheExpectedValues()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);

        // Melamine_18#01 is the first sheet of the first plan = label file 001.
        var row = ReadCsv(folder.File("DAAN_ROGIERS-P2026.09_001.csv")).Single(r => r["ID"] == "3575");

        Assert.Equal("Melamine_18", row["MATERIAL"]);
        Assert.Equal("2850", row["SHEETLENGTH"]);
        Assert.Equal("2100", row["SHEETWIDTH"]);
        Assert.Equal("18", row["SHEETTHICKNESS"]);
        Assert.Equal("0", row["GRAIN"]);
        Assert.Equal("Geen", row["GRAINSTR"]);
        Assert.Equal("399", row["X"]);                  // LABEL_X 398.99, rounded
        Assert.Equal("540", row["Y"]);                  // LABEL_Y 540.05
        Assert.Equal("180", row["ROTATION"]);
        Assert.Equal("1", row["N"]);
        Assert.Equal("778", row["PANELLENGTH"]);
        Assert.Equal("596", row["PANELWIDTH"]);
        Assert.Equal("K1 - Front - 3575", row["DESCRIPTION"]);
        Assert.Equal("Melamine_18", row["MATERIALNAME"]);
        Assert.Equal("FR", row["EDGE_L1"]);
        Assert.Equal("FR", row["EDGE_B2"]);
        Assert.Equal("0003575_2.cix", row["CAM2"]);
        Assert.Equal("0003575_3.cix", row["CAM3"]);
        Assert.Equal("P2026.09", row["PROJECT"]);
        Assert.Equal("Melamine_18#01", row["SHEET"]);

        // The ten description columns come last, in the order of the export.
        Assert.Equal("Nr bon commande", row["DESC1"]);
        Assert.Equal("Extra texte 2", row["DESC2"]);
        Assert.Equal("Extra texte 10", row["DESC10"]);
    }

    [Fact]
    public void ThePartsWithoutEdgeBandingOrCam3_HaveEmptyValues_NotMissingColumns()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);

        var row = ReadCsv(folder.File("DAAN_ROGIERS-P2026.09_001.csv")).Single(r => r["ID"] == "1824");   // a Rugband

        Assert.Equal("", row["EDGE_L1"]);
        Assert.Equal("", row["CAM3"]);
        Assert.Equal("0001824_2.cix", row["CAM2"]);
    }

    [Fact]
    public void Part3575_WithFlipX_HasX2451()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path, new ConverterSettings { FlipX = true, TopSolidExportPath = TestPaths.NoExportFolder });

        var row = ReadCsv(folder.File("DAAN_ROGIERS-P2026.09_001.csv")).Single(r => r["ID"] == "3575");

        Assert.Equal("2451", row["X"]);    // 2850 - 398.99 = 2451.01
        Assert.Equal("540", row["Y"]);
    }

    [Fact]
    public void Part3575_WithFlipY_HasY1560()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path, new ConverterSettings { FlipY = true, TopSolidExportPath = TestPaths.NoExportFolder });

        var row = ReadCsv(folder.File("DAAN_ROGIERS-P2026.09_001.csv")).Single(r => r["ID"] == "3575");

        Assert.Equal("399", row["X"]);
        Assert.Equal("1560", row["Y"]);    // 2100 - 540.05 = 1559.95, rounded
    }

    [Fact]
    public void Part4544_Angle360BecomesRotation0()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);

        var row = ReadCsv(folder.File("DAAN_ROGIERS-P2026.09_001.csv")).Single(r => r["ID"] == "4544");

        Assert.Equal("0", row["ROTATION"]);
    }

    [Fact]
    public void CncPrefix_ReplacesTheExportFolderInTheXml()
    {
        using var folder = new TempFolder();
        var settings = new ConverterSettings { CncPathPrefixInXml = @"\\server\topsolid\Export", TopSolidExportPath = TestPaths.NoExportFolder };

        ConvertSample(folder.Path, settings);

        string xml = File.ReadAllText(folder.File("DAAN_ROGIERS-P2026.09.xml"));
        Assert.Contains(@"<CNCFilename>\\server\topsolid\Export\CNC\DAAN_ROGIERS-P2026.09\Melamine_18#01.xcs</CNCFilename>", xml);
        Assert.DoesNotContain("Z:", xml);
    }

    [Fact]
    public void ExistingBatchXml_IsNeverOverwritten()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);
        byte[] xmlBefore = File.ReadAllBytes(folder.File("DAAN_ROGIERS-P2026.09.xml"));

        var error = Assert.Throws<ConversionException>(() => ConvertSample(folder.Path));

        Assert.Contains("Batch bestaat al", error.Message);
        Assert.True(xmlBefore.SequenceEqual(File.ReadAllBytes(folder.File("DAAN_ROGIERS-P2026.09.xml"))));
    }

    [Fact]
    public void UnknownMaterials_AreListedAndNothingIsWritten()
    {
        using var folder = new TempFolder();
        string materialsFile = folder.File("materials.csv");
        File.WriteAllText(materialsFile, "TopSolidMaterial;DssMaterial;Thickness;Grain\nPaars_18;Paars_18;18;0\n");
        string outFolder = folder.File("out");
        Directory.CreateDirectory(outFolder);

        var error = Assert.Throws<ConversionException>(() => ConvertSample(outFolder, materialsFile: materialsFile));

        Assert.Contains("'Melamine_18'", error.Message);   // every unknown material is named, not just the first
        Assert.Contains("'Melamine_08'", error.Message);
        Assert.DoesNotContain("'Paars_18'", error.Message);
        Assert.Empty(Directory.GetFiles(outFolder));

        // With one of the two known, only the other one is named.
        File.WriteAllText(materialsFile, "TopSolidMaterial;DssMaterial;Thickness;Grain\nMelamine_18;Melamine_18;18;0\n");
        var second = Assert.Throws<ConversionException>(() => ConvertSample(outFolder, materialsFile: materialsFile));
        Assert.Contains("'Melamine_08'", second.Message);
        Assert.DoesNotContain("'Melamine_18'", second.Message);
        Assert.Empty(Directory.GetFiles(outFolder));
    }

    [Fact]
    public void OutputFolderThatDoesNotExist_IsAnError_AndWritesNothing()
    {
        using var folder = new TempFolder();
        string missing = folder.File("does-not-exist");

        var error = Assert.Throws<ConversionException>(() => ConvertSample(missing));

        Assert.Contains("Map niet bereikbaar", error.Message);
        Assert.False(Directory.Exists(missing));
    }

    [Theory]
    [InlineData(@"C:\export\DAAN_ROGIERS-P2026.09-LI.xlsx", "DAAN_ROGIERS-P2026.09")]
    [InlineData(@"C:\export\DAAN_ROGIERS-P2026.09-li.xlsx", "DAAN_ROGIERS-P2026.09")]
    [InlineData(@"C:\export\Huppy-002-LI.xlsx", "Huppy-002")]
    public void BatchName_IsTheFileNameWithoutTheLiSuffix(string path, string expected)
    {
        Assert.Equal(expected, Converter.GetBatchName(path));
    }

    [Theory]
    [InlineData(@"C:\export\DAAN_ROGIERS-P2026.09.xlsx")]
    [InlineData(@"C:\export\DAAN_ROGIERS-P2026.09-LP.xlsx")]
    [InlineData(@"C:\export\-LI.xlsx")]
    public void BatchName_WithoutLiSuffix_IsAnError(string path)
    {
        Assert.Throws<ConversionException>(() => Converter.GetBatchName(path));
    }
}
