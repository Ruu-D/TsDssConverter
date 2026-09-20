using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>
/// TopSolid names the CNC program of a sheet after the sheet only (SP_M_030_18#01.xcs, Melamine_18#01.xcs) and writes it in
/// the shared export folder. After the conversion the programs of a job are MOVED to {export}\CNC\{project}\, so two jobs with
/// the same material never overwrite each other's files, and the export folder only holds what is not converted yet.
/// </summary>
public class CncPathBuilderTests
{
    // ---------------------------------------------------------------- the paths

    [Theory]
    [InlineData(@"Z:\TopSolid\Export\", "DAAN_ROGIERS-P2026.09", "Melamine_18#01", @"Z:\TopSolid\Export\CNC\DAAN_ROGIERS-P2026.09\Melamine_18#01.xcs")]
    [InlineData(@"Z:\TopSolid\Export", "P-20", "SP_M_030_8#02", @"Z:\TopSolid\Export\CNC\P-20\SP_M_030_8#02.xcs")]   // with or without the last backslash
    [InlineData(@"D:\x\", "Job", "White_18#01", @"D:\x\CNC\Job\White_18#01.xcs")]
    public void AConvertedProgram_IsInTheProjectFolder_UnderCnc(string exportFolder, string project, string sheetName, string expected)
    {
        var settings = new ConverterSettings { TopSolidExportPath = exportFolder };

        Assert.Equal(expected, CncPathBuilder.Build(settings, project, sheetName));
        Assert.Equal(expected, CncPathBuilder.BuildLocalPath(settings, project, sheetName));
    }

    [Fact]
    public void TopSolidWritesTheProgram_DirectlyInTheExportFolder_NamedAfterTheSheet()
    {
        var settings = new ConverterSettings { TopSolidExportPath = @"Z:\TopSolid\Export\" };

        Assert.Equal(@"Z:\TopSolid\Export\Melamine_18#01.xcs", CncPathBuilder.BuildSourcePath(settings, "Melamine_18#01"));
    }

    [Fact]
    public void ThePrefixInTheXml_ReplacesTheExportFolder_ButNeverThePathOnThisPc()
    {
        var settings = new ConverterSettings
        {
            TopSolidExportPath = @"Z:\TopSolid\Export\",
            CncPathPrefixInXml = @"\\server\topsolid\Export\",
        };

        Assert.Equal(@"\\server\topsolid\Export\CNC\P-20\Melamine_18#01.xcs", CncPathBuilder.Build(settings, "P-20", "Melamine_18#01"));
        Assert.Equal(@"Z:\TopSolid\Export\CNC\P-20\Melamine_18#01.xcs", CncPathBuilder.BuildLocalPath(settings, "P-20", "Melamine_18#01"));
        Assert.Equal(@"Z:\TopSolid\Export\Melamine_18#01.xcs", CncPathBuilder.BuildSourcePath(settings, "Melamine_18#01"));
    }

    [Fact]
    public void TheExtension_IsASetting()
    {
        var settings = new ConverterSettings { TopSolidExportPath = @"Z:\Export", CncExtension = ".cnc" };

        Assert.Equal(@"Z:\Export\CNC\Job\White_9#01.cnc", CncPathBuilder.Build(settings, "Job", "White_9#01"));
        Assert.Equal(@"Z:\Export\White_9#01.cnc", CncPathBuilder.BuildSourcePath(settings, "White_9#01"));
    }

    // ---------------------------------------------------------------- moving the programs

    private const string Project = "DAAN_ROGIERS-P2026.09";
    private static readonly string[] Sheets = { "Melamine_18#01", "Melamine_18#02", "Melamine_08#01" };

    private static Batch BatchWithTheThreeSheets()
    {
        var batch = new Batch { Name = Project };
        var plan18 = new Plan { PlanName = "001" };
        plan18.Sheets.Add(new Sheet { Name = Sheets[0] });
        plan18.Sheets.Add(new Sheet { Name = Sheets[1] });
        var plan08 = new Plan { PlanName = "002" };
        plan08.Sheets.Add(new Sheet { Name = Sheets[2] });
        batch.Plans.Add(plan18);
        batch.Plans.Add(plan08);
        return batch;
    }

    private static string ExportWithAllPrograms(TempFolder folder)
    {
        string export = Directory.CreateDirectory(folder.File("export")).FullName;
        foreach (string sheet in Sheets)
        {
            File.WriteAllText(Path.Combine(export, sheet + ".xcs"), "cnc of " + sheet);
        }

        return export;
    }

    private static string ProjectFolder(string export, string project = Project) => Path.Combine(export, "CNC", project);

    [Fact]
    public void TheProgramsOfTheJob_AreMovedToTheProjectFolder_SoTheExportFolderOnlyHoldsWhatIsNotConverted()
    {
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        var settings = new ConverterSettings { TopSolidExportPath = export };

        var warnings = CncMover.MoveToProjectFolder(BatchWithTheThreeSheets(), settings, exportFinishedAt: null);

        Assert.Empty(warnings);
        Assert.Empty(Directory.GetFiles(export));                                   // nothing left in the export folder itself
        Assert.Equal(3, Directory.GetFiles(ProjectFolder(export), "*.xcs").Length);
        Assert.Equal("cnc of Melamine_18#02", File.ReadAllText(Path.Combine(ProjectFolder(export), "Melamine_18#02.xcs")));
    }

    [Fact]
    public void TwoJobsWithTheSameSheetNames_EndUpInTheirOwnFolders_WithTheirOwnContent()
    {
        using var folder = new TempFolder();
        string export = Directory.CreateDirectory(folder.File("export")).FullName;
        var settings = new ConverterSettings { TopSolidExportPath = export };
        var batchOne = new Batch { Name = "Customer-One" };
        var batchTwo = new Batch { Name = "Customer-Two" };
        foreach (var batch in new[] { batchOne, batchTwo })
        {
            var plan = new Plan { PlanName = "001" };
            plan.Sheets.Add(new Sheet { Name = "Melamine_18#01" });
            batch.Plans.Add(plan);
        }

        // Job one is exported and converted, THEN job two is exported: the same file name is used again.
        File.WriteAllText(Path.Combine(export, "Melamine_18#01.xcs"), "program of job one");
        CncMover.MoveToProjectFolder(batchOne, settings, null);
        File.WriteAllText(Path.Combine(export, "Melamine_18#01.xcs"), "program of job two");
        CncMover.MoveToProjectFolder(batchTwo, settings, null);

        Assert.Equal("program of job one", File.ReadAllText(Path.Combine(ProjectFolder(export, "Customer-One"), "Melamine_18#01.xcs")));
        Assert.Equal("program of job two", File.ReadAllText(Path.Combine(ProjectFolder(export, "Customer-Two"), "Melamine_18#01.xcs")));
        Assert.Empty(Directory.GetFiles(export));
    }

    [Fact]
    public void AProgramThatIsNotThere_IsAWarning_AndTheOthersAreStillMoved()
    {
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        File.Delete(Path.Combine(export, "Melamine_18#02.xcs"));
        var settings = new ConverterSettings { TopSolidExportPath = export };

        var warnings = CncMover.MoveToProjectFolder(BatchWithTheThreeSheets(), settings, null);

        string warning = Assert.Single(warnings);
        Assert.Contains("CNC-programma niet gevonden", warning);
        Assert.Contains(Path.Combine(export, "Melamine_18#02.xcs"), warning);   // the place where TopSolid should have written it
        Assert.Equal(2, Directory.GetFiles(ProjectFolder(export), "*.xcs").Length);
    }

    [Fact]
    public void ASecondAttempt_AfterAnEarlierOneStoppedHalfway_MovesWhatIsLeft_WithoutWarnings()
    {
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        var settings = new ConverterSettings { TopSolidExportPath = export };

        // Attempt one moved only the first sheet (the rest failed, for example a network problem).
        Directory.CreateDirectory(ProjectFolder(export));
        File.Move(Path.Combine(export, "Melamine_18#01.xcs"), Path.Combine(ProjectFolder(export), "Melamine_18#01.xcs"));

        var warnings = CncMover.MoveToProjectFolder(BatchWithTheThreeSheets(), settings, null);

        Assert.Empty(warnings);
        Assert.Empty(Directory.GetFiles(export));
        Assert.Equal(3, Directory.GetFiles(ProjectFolder(export), "*.xcs").Length);
    }

    [Fact]
    public void AnExportFolderThatIsNotReachable_IsOneWarning_AndNothingIsCreated()
    {
        using var folder = new TempFolder();
        var settings = new ConverterSettings { TopSolidExportPath = folder.File("not-there") };

        var warnings = CncMover.MoveToProjectFolder(BatchWithTheThreeSheets(), settings, null);

        Assert.Single(warnings);
        Assert.Contains("niet bereikbaar", warnings[0]);
        Assert.False(Directory.Exists(folder.File("not-there")));
    }

    // ---------------------------------------------------------------- overwritten by a later export

    [Fact]
    public void AProgramThatIsNewerThanTheTriggerFile_WasOverwrittenByALaterExport_ItIsRefused_AndNothingIsMoved()
    {
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        var settings = new ConverterSettings { TopSolidExportPath = export };
        DateTime triggerTime = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(export, "Melamine_18#01.xcs"), triggerTime.AddMinutes(-1));   // this job's own program
        File.SetLastWriteTimeUtc(Path.Combine(export, "Melamine_18#02.xcs"), triggerTime.AddMinutes(-1));
        File.SetLastWriteTimeUtc(Path.Combine(export, "Melamine_08#01.xcs"), triggerTime.AddMinutes(30));   // a later job wrote this name again

        var error = Assert.Throws<ConversionException>(() => CncMover.MoveToProjectFolder(BatchWithTheThreeSheets(), settings, triggerTime));

        Assert.Contains("Melamine_08#01.xcs", error.Message);
        Assert.Contains("overschreven", error.Message);
        Assert.Equal(3, Directory.GetFiles(export).Length);            // everything is still where it was
        Assert.False(Directory.Exists(ProjectFolder(export)));
    }

    [Fact]
    public void AProgramWrittenJustBeforeOrJustAfterTheTriggerFile_IsFine()
    {
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        var settings = new ConverterSettings { TopSolidExportPath = export };
        DateTime triggerTime = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(export, "Melamine_18#01.xcs"), triggerTime.AddMinutes(-5));
        File.SetLastWriteTimeUtc(Path.Combine(export, "Melamine_18#02.xcs"), triggerTime.AddSeconds(2));   // within the tolerance
        File.SetLastWriteTimeUtc(Path.Combine(export, "Melamine_08#01.xcs"), triggerTime);

        var warnings = CncMover.MoveToProjectFolder(BatchWithTheThreeSheets(), settings, triggerTime);

        Assert.Empty(warnings);
        Assert.Equal(3, Directory.GetFiles(ProjectFolder(export), "*.xcs").Length);
    }

    [Fact]
    public void WithoutATriggerTime_TheAgeOfTheProgramsIsNotChecked()
    {
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        var settings = new ConverterSettings { TopSolidExportPath = export };
        File.SetLastWriteTimeUtc(Path.Combine(export, "Melamine_18#01.xcs"), DateTime.UtcNow.AddDays(1));

        CncMover.MoveToProjectFolder(BatchWithTheThreeSheets(), settings, exportFinishedAt: null);

        Assert.Equal(3, Directory.GetFiles(ProjectFolder(export), "*.xcs").Length);
    }

    // ---------------------------------------------------------------- the whole conversion

    [Fact]
    public void TheConversion_MovesThePrograms_AndTheXmlPointsToThem()
    {
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        string outFolder = Directory.CreateDirectory(folder.File("out")).FullName;
        var settings = new ConverterSettings { TopSolidExportPath = export };

        var result = new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, TestPaths.MaterialsFile, outFolder, outFolder, settings, TestPaths.GoldenPlanDate);

        Assert.Empty(result.Warnings);
        Assert.Empty(Directory.GetFiles(export));

        var xml = System.Xml.Linq.XDocument.Load(Path.Combine(outFolder, Project + ".xml"));
        string[] cncPaths = xml.Descendants("CNCFilename").Select(e => e.Value).ToArray();
        Assert.Equal(3, cncPaths.Length);
        Assert.All(cncPaths, path => Assert.True(File.Exists(path), "the XML points to a file that is not there: " + path));
        Assert.All(cncPaths, path => Assert.Equal(ProjectFolder(export), Path.GetDirectoryName(path)));
    }

    [Fact]
    public void ARefusedBatch_LeavesThePrograms_WhereTheyAre()
    {
        // The batch XML exists already: the job is refused, so the programs must not move (they are not converted).
        using var folder = new TempFolder();
        string export = ExportWithAllPrograms(folder);
        string outFolder = Directory.CreateDirectory(folder.File("out")).FullName;
        File.WriteAllText(Path.Combine(outFolder, Project + ".xml"), "an earlier job");
        var settings = new ConverterSettings { TopSolidExportPath = export };

        Assert.Throws<ConversionException>(() => new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, TestPaths.MaterialsFile, outFolder, outFolder, settings, TestPaths.GoldenPlanDate));

        Assert.Equal(3, Directory.GetFiles(export).Length);
        Assert.False(Directory.Exists(Path.Combine(export, "CNC")));
    }
}
