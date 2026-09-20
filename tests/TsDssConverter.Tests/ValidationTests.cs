using ClosedXML.Excel;
using System.Xml.Linq;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>Checks that the whole conversion reports problems and warnings correctly (stage 2).</summary>
public class ValidationTests
{
    private static ConversionResult ConvertSample(string outFolder, ConverterSettings settings, string? materialsFile = null)
    {
        return new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, materialsFile ?? TestPaths.MaterialsFile,
            outFolder, outFolder, settings, TestPaths.GoldenPlanDate);
    }

    /// <summary>
    /// Makes an export folder that contains a (fake) CNC program for every sheet of the sample.
    /// The file names are taken from the batch XML, so this does not repeat the naming rule.
    /// </summary>
    private static string CreateExportFolderWithAllCncFiles(TempFolder folder)
    {
        string probeFolder = folder.File("probe");
        Directory.CreateDirectory(probeFolder);
        ConvertSample(probeFolder, TestPaths.SafeSettings());

        string exportFolder = folder.File("export");
        Directory.CreateDirectory(exportFolder);

        var cncPaths = XDocument.Load(System.IO.Path.Combine(probeFolder, "DAAN_ROGIERS-P2026.09.xml"))
            .Descendants("CNCFilename").Select(e => e.Value);
        foreach (string cncPath in cncPaths)
        {
            File.WriteAllText(System.IO.Path.Combine(exportFolder, System.IO.Path.GetFileName(cncPath)), "fake cnc program");
        }

        return exportFolder;
    }

    private static string NewOutFolder(TempFolder folder)
    {
        string path = folder.File("out");
        Directory.CreateDirectory(path);
        return path;
    }

    // ---------------------------------------------------------------- CNC program warnings

    [Fact]
    public void AllCncFilesPresent_GivesNoWarnings()
    {
        using var folder = new TempFolder();
        var settings = new ConverterSettings { TopSolidExportPath = CreateExportFolderWithAllCncFiles(folder) };

        var result = ConvertSample(NewOutFolder(folder), settings);

        Assert.Empty(result.Warnings); // also: no thickness or duplicate part ID warnings for the sample
    }

    [Fact]
    public void MissingCncFiles_AreWarnings_AndTheConversionStillSucceeds()
    {
        using var folder = new TempFolder();
        string exportFolder = CreateExportFolderWithAllCncFiles(folder);
        File.Delete(System.IO.Path.Combine(exportFolder, "Melamine_18#02.xcs"));
        File.Delete(System.IO.Path.Combine(exportFolder, "Melamine_08#01.xcs"));
        string outFolder = NewOutFolder(folder);

        var result = ConvertSample(outFolder, new ConverterSettings { TopSolidExportPath = exportFolder });

        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.Contains("CNC-programma niet gevonden") && w.Contains("Melamine_18#02.xcs"));
        Assert.Contains(result.Warnings, w => w.Contains("Melamine_08#01.xcs"));
        Assert.True(File.Exists(System.IO.Path.Combine(outFolder, "DAAN_ROGIERS-P2026.09.xml"))); // still written
    }

    [Fact]
    public void ExportFolderNotReachable_IsOneWarning_NotOnePerSheet()
    {
        using var folder = new TempFolder();
        var settings = new ConverterSettings { TopSolidExportPath = folder.File("does-not-exist") };

        var result = ConvertSample(NewOutFolder(folder), settings);

        Assert.Single(result.Warnings);
        Assert.Contains("niet bereikbaar", result.Warnings[0]);
    }

    [Fact]
    public void CncCheck_UsesTheExportFolder_NotThePrefixThatIsWrittenInTheXml()
    {
        using var folder = new TempFolder();
        var settings = new ConverterSettings
        {
            TopSolidExportPath = CreateExportFolderWithAllCncFiles(folder),
            CncPathPrefixInXml = @"\\server\topsolid\Export", // the machine's view; this PC cannot check it
        };

        var result = ConvertSample(NewOutFolder(folder), settings);

        Assert.Empty(result.Warnings);
    }

    // ---------------------------------------------------------------- thickness warning

    [Fact]
    public void ThicknessInMaterialsFileThatDiffersFromTopSolid_IsAWarning_AndTheFileValueIsUsed()
    {
        using var folder = new TempFolder();
        var settings = new ConverterSettings { TopSolidExportPath = CreateExportFolderWithAllCncFiles(folder) };

        // The check compares materials.csv with the NUMBER AT THE START of SUP_DESIGNATION ("8.0_panel 8mm"). The sample
        // export has none ("Melamine___2850x2100x8"), so give the Melamine_08 sheet one: TopSolid says 8 mm ...
        string positionFile = folder.File("DAAN_ROGIERS-P2026.09-LP.xlsx");
        using (var workbook = new ClosedXML.Excel.XLWorkbook(TestPaths.PositionFile))
        {
            var sheet = workbook.Worksheets.First();
            int lastRow = sheet.LastRowUsed()!.RowNumber();
            for (int row = 2; row <= lastRow; row++)
            {
                if (sheet.Cell(row, 2).GetString().StartsWith("Melamine_08"))   // column B = SP, column A = SUP_DESIGNATION
                {
                    sheet.Cell(row, 1).Value = "8.0_Melamine_08 2850x2100x8";
                }
            }

            workbook.SaveAs(positionFile);
        }

        // ... and here materials.csv says 9.
        string materialsFile = folder.File("materials.csv");
        File.WriteAllText(materialsFile, File.ReadAllText(TestPaths.MaterialsFile).Replace("Melamine_08;Melamine_08;8;0", "Melamine_08;Melamine_08;9;0"));
        string outFolder = NewOutFolder(folder);

        var result = new Converter().Convert(
            TestPaths.InfoFile, positionFile, materialsFile, outFolder, outFolder, settings, TestPaths.GoldenPlanDate);

        Assert.Single(result.Warnings);
        Assert.Contains("'Melamine_08'", result.Warnings[0]);

        // materials.csv is the truth: the label file of Melamine_08 (the third sheet) says 9.
        string firstDataRow = File.ReadAllLines(System.IO.Path.Combine(outFolder, "DAAN_ROGIERS-P2026.09_003.csv"))[1];
        Assert.Equal("9", firstDataRow.Split('\t')[3]); // SHEETTHICKNESS

        // The 18 mm sheets have no number in front: nothing to compare, so no warning for them.
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Melamine_18"));
    }

    // ---------------------------------------------------------------- errors: all inputs at once

    [Fact]
    public void ProblemsInAllThreeInputFiles_AreReportedTogether()
    {
        using var folder = new TempFolder();

        // LI with only one column, LP with only one column, materials.csv without the Grain column.
        string infoPath = folder.File("Test-1-LI.xlsx");
        string positionPath = folder.File("Test-1-LP.xlsx");
        WriteWorkbookWithOneColumn(infoPath, "Naam_Plaat");
        WriteWorkbookWithOneColumn(positionPath, "SP");
        string materialsPath = folder.File("materials.csv");
        File.WriteAllText(materialsPath, "TopSolidMaterial;DssMaterial;Thickness\nWhite_18;White_18;18\n");
        string outFolder = NewOutFolder(folder);

        var error = Assert.Throws<ConversionException>(() => new Converter().Convert(
            infoPath, positionPath, materialsPath, outFolder, outFolder, TestPaths.SafeSettings(), TestPaths.GoldenPlanDate));

        Assert.Contains(error.Problems, p => p.Contains("'Omschrijving'") && p.Contains("LI"));
        Assert.Contains(error.Problems, p => p.Contains("'Afmetingen'") && p.Contains("LI"));
        Assert.Contains(error.Problems, p => p.Contains("'ID'") && p.Contains("LP"));
        Assert.Contains(error.Problems, p => p.Contains("'SUP_L'") && p.Contains("LP"));
        Assert.Contains(error.Problems, p => p.Contains("'Grain'") && p.Contains("materials.csv"));
        Assert.Empty(Directory.GetFiles(outFolder)); // nothing written
    }

    [Fact]
    public void LpFileWithUnreadableNumbers_ReportsEveryOne()
    {
        using var folder = new TempFolder();
        string path = folder.File("Test-1-LP.xlsx");

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            string[] headers = { "SP", "ID", "SUP_L", "SUP_B", "LABEL_X", "LABEL_Y", "LABEL_ANGLE" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
            }

            // Two rows, each with a text where a number should be ("2055,97" has a comma: not TopSolid format).
            string[] row1 = { "White_18#01", "A - 1", "3050", "1300", "2055,97", "289.24", "0" };
            string[] row2 = { "White_18#01", "B - 2", "3050", "abc", "100", "100", "0" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cell(2, i + 1).Value = row1[i];
                sheet.Cell(3, i + 1).Value = row2[i];
            }

            workbook.SaveAs(path);
        }

        var error = Assert.Throws<ConversionException>(() => TopSolidReader.ReadLabelPositions(path));

        Assert.Equal(2, error.Problems.Count);
        Assert.Contains(error.Problems, p => p.Contains("LABEL_X") && p.Contains("2055,97"));
        Assert.Contains(error.Problems, p => p.Contains("SUP_B") && p.Contains("abc"));
    }

    private static void WriteWorkbookWithOneColumn(string path, string header)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        sheet.Cell(1, 1).Value = header;
        sheet.Cell(2, 1).Value = "x";
        workbook.SaveAs(path);
    }
}
