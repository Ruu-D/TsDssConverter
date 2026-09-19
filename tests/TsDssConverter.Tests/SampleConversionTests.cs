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
            outFolder, outFolder, settings ?? new ConverterSettings(), TestPaths.GoldenPlanDate);
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
            batchFolder, labelFolder, new ConverterSettings(), TestPaths.GoldenPlanDate);

        var xml = XDocument.Load(System.IO.Path.Combine(batchFolder, "Verschuren-P-20.xml"));
        string[] labelNames = xml.Descendants("LabelFilename").Select(e => e.Value).ToArray();

        Assert.Equal(11, labelNames.Length);
        Assert.Equal(System.IO.Path.Combine(labelFolder, "Verschuren-P-20_001.csv"), labelNames[0]);
        Assert.Equal(System.IO.Path.Combine(labelFolder, "Verschuren-P-20_011.csv"), labelNames[10]);

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

        string[] goldenFiles = Directory.GetFiles(TestPaths.GoldenFolder, "Verschuren-P-20*");
        Assert.Equal(12, goldenFiles.Length); // 1 XML + 11 CSVs

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
                string text = new UTF8Encoding(false).GetString(actual);
                actual = new UTF8Encoding(false).GetBytes(text.Replace(folder.Path + @"\", GoldenLabelFolder + @"\"));
            }

            Assert.True(
                File.ReadAllBytes(goldenFile).SequenceEqual(actual),
                "Output differs from the golden file " + name);
        }

        // Nothing else was written, and no ".tmp" files are left behind.
        Assert.Equal(12, Directory.GetFiles(folder.Path).Length);
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

            byte[] golden = File.ReadAllBytes(System.IO.Path.Combine(TestPaths.GoldenFolder, "Verschuren-P-20_002.csv"));
            byte[] actual = File.ReadAllBytes(folder.File("Verschuren-P-20_002.csv"));
            Assert.True(golden.SequenceEqual(actual));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void BatchXml_HasFivePlansInLpOrder()
    {
        using var folder = new TempFolder();
        var result = ConvertSample(folder.Path);

        Assert.Equal("Verschuren-P-20", result.BatchName);
        Assert.Equal(11, result.LabelPaths.Count);
        Assert.Equal(63, result.PartCount);

        var plans = XDocument.Load(folder.File("Verschuren-P-20.xml")).Root!.Element("Plans")!.Elements("Plan").ToList();

        // Order of first appearance in the LP file, with the number of sheets per material.
        string chene = "Ch\u00eane";
        var expected = new (string Material, int Sheets)[]
        {
            ("Paars_18", 1),
            ("White_18", 5),
            ("H1145_-_ST10_-_" + chene + "_Bardolino_naturel_Zijdewit_19", 2),
            ("H1145_-_ST10_-_" + chene + "_Bardolino_naturel_Zijdewit_40", 1),
            ("White_9", 2),
        };

        Assert.Equal(expected.Length, plans.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Material, plans[i].Element("Material")!.Value);
            Assert.Equal(expected[i].Sheets.ToString(), plans[i].Element("Quantity")!.Value);
            Assert.Equal(expected[i].Sheets, plans[i].Element("LabelFilenames")!.Elements().Count());
            Assert.Equal(expected[i].Sheets, plans[i].Element("CNCFilenames")!.Elements().Count());
        }

        Assert.Equal("2026-09-19", XDocument.Load(folder.File("Verschuren-P-20.xml")).Root!.Element("PlanDate")!.Value);
    }

    [Fact]
    public void LabelCsvs_HaveTheExpectedNumberOfRowsPerSheet()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);

        // Parts per sheet, in file order 001 ... 011.
        int[] expectedRows = { 6, 10, 9, 7, 10, 8, 4, 2, 1, 5, 1 };

        for (int i = 0; i < expectedRows.Length; i++)
        {
            var rows = ReadCsv(folder.File($"Verschuren-P-20_{i + 1:000}.csv"));
            Assert.Equal(expectedRows[i], rows.Count);

            // N runs 1..n on every sheet.
            Assert.Equal(Enumerable.Range(1, rows.Count).Select(n => n.ToString()), rows.Select(r => r["N"]));
        }

        Assert.Equal(63, expectedRows.Sum());
    }

    [Fact]
    public void Part13099_HasTheExpectedValues()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);

        // White_18#01 is the first sheet of the second plan = label file 002.
        var row = ReadCsv(folder.File("Verschuren-P-20_002.csv")).Single(r => r["ID"] == "13099");

        Assert.Equal("White_18", row["MATERIAL"]);
        Assert.Equal("3050", row["SHEETLENGTH"]);
        Assert.Equal("1300", row["SHEETWIDTH"]);
        Assert.Equal("18", row["SHEETTHICKNESS"]);
        Assert.Equal("0", row["GRAIN"]);
        Assert.Equal("Geen", row["GRAINSTR"]);
        Assert.Equal("2056", row["X"]);
        Assert.Equal("289", row["Y"]);
        Assert.Equal("180", row["ROTATION"]);
        Assert.Equal("734", row["PANELLENGTH"]);
        Assert.Equal("568,5", row["PANELWIDTH"]);
        Assert.Equal("Verschuren - K2 - Zijkant links - 13099", row["DESCRIPTION"]);
        Assert.Equal("White", row["MATERIALNAME"]);  // trailing space of the LI text is trimmed
        Assert.Equal("P-20", row["PROJECT"]);
        Assert.Equal("White_18#01", row["SHEET"]);
    }

    [Fact]
    public void Part13099_WithFlipX_HasX994()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path, new ConverterSettings { FlipX = true });

        var row = ReadCsv(folder.File("Verschuren-P-20_002.csv")).Single(r => r["ID"] == "13099");

        Assert.Equal("994", row["X"]);
        Assert.Equal("289", row["Y"]);
    }

    [Fact]
    public void Part13099_WithFlipY_HasY1011()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path, new ConverterSettings { FlipY = true });

        var row = ReadCsv(folder.File("Verschuren-P-20_002.csv")).Single(r => r["ID"] == "13099");

        Assert.Equal("2056", row["X"]);
        Assert.Equal("1011", row["Y"]);
    }

    [Fact]
    public void Part17075_Angle360BecomesRotation0()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);

        var row = ReadCsv(folder.File("Verschuren-P-20_002.csv")).Single(r => r["ID"] == "17075");

        Assert.Equal("0", row["ROTATION"]);
    }

    [Fact]
    public void CncPrefix_ReplacesTheExportFolderInTheXml()
    {
        using var folder = new TempFolder();
        var settings = new ConverterSettings { CncPathPrefixInXml = @"\\server\topsolid\Export" };

        ConvertSample(folder.Path, settings);

        string xml = File.ReadAllText(folder.File("Verschuren-P-20.xml"));
        Assert.Contains(@"<CNCFilename>\\server\topsolid\Export\Verschuren-P-20_White_18_01.xcs</CNCFilename>", xml);
        Assert.DoesNotContain("Z:", xml);
    }

    [Fact]
    public void ExistingBatchXml_IsNeverOverwritten()
    {
        using var folder = new TempFolder();
        ConvertSample(folder.Path);
        byte[] xmlBefore = File.ReadAllBytes(folder.File("Verschuren-P-20.xml"));

        var error = Assert.Throws<ConversionException>(() => ConvertSample(folder.Path));

        Assert.Contains("Batch bestaat al", error.Message);
        Assert.True(xmlBefore.SequenceEqual(File.ReadAllBytes(folder.File("Verschuren-P-20.xml"))));
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

        Assert.Contains("'White_18'", error.Message);
        Assert.Contains("'White_9'", error.Message);
        Assert.Contains("Zijdewit_19'", error.Message);
        Assert.Contains("Zijdewit_40'", error.Message);
        Assert.DoesNotContain("'Paars_18'", error.Message);
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
    [InlineData(@"C:\export\Verschuren-P-20-LI.xlsx", "Verschuren-P-20")]
    [InlineData(@"C:\export\Verschuren-P-20-li.xlsx", "Verschuren-P-20")]
    [InlineData(@"C:\export\Huppy-002-LI.xlsx", "Huppy-002")]
    public void BatchName_IsTheFileNameWithoutTheLiSuffix(string path, string expected)
    {
        Assert.Equal(expected, Converter.GetBatchName(path));
    }

    [Theory]
    [InlineData(@"C:\export\Verschuren-P-20.xlsx")]
    [InlineData(@"C:\export\Verschuren-P-20-LP.xlsx")]
    [InlineData(@"C:\export\-LI.xlsx")]
    public void BatchName_WithoutLiSuffix_IsAnError(string path)
    {
        Assert.Throws<ConversionException>(() => Converter.GetBatchName(path));
    }
}
