using System.Text;
using ClosedXML.Excel;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>
/// Tests for the column files (columns-li.txt / columns-lp.txt): TopSolid may rename a header in the XLSX,
/// and the customer must be able to follow that without a new version of the program.
/// </summary>
public class ColumnMapTests
{
    // ---------------------------------------------------------------- helpers: changed copies of the sample files

    private static void RenameHeader(string source, string target, string oldName, string newName)
    {
        using var workbook = new XLWorkbook(source);
        var sheet = workbook.Worksheets.First();
        sheet.Row(1).CellsUsed().First(cell => cell.GetString().Trim() == oldName).Value = newName;
        workbook.SaveAs(target);
    }

    /// <summary>
    /// Takes a column "out" of the file by emptying its header: a column without a name does not exist for the
    /// reader. (Deleting the whole column in ClosedXML fails on a defined name in the sample files.)
    /// </summary>
    private static void DeleteColumn(string source, string target, string name)
    {
        using var workbook = new XLWorkbook(source);
        var sheet = workbook.Worksheets.First();
        sheet.Row(1).CellsUsed().First(cell => cell.GetString().Trim() == name).Value = "";
        workbook.SaveAs(target);
    }

    /// <summary>The same table with the columns in the opposite order.</summary>
    private static void ReverseColumns(string source, string target)
    {
        using var input = new XLWorkbook(source);
        var from = input.Worksheets.First();
        int lastColumn = from.LastColumnUsed()!.ColumnNumber();
        int lastRow = from.LastRowUsed()!.RowNumber();

        using var output = new XLWorkbook();
        var to = output.AddWorksheet("Sheet1");
        for (int row = 1; row <= lastRow; row++)
        {
            for (int column = 1; column <= lastColumn; column++)
            {
                to.Cell(row, lastColumn - column + 1).Value = from.Cell(row, column).Value;
            }
        }

        output.SaveAs(target);
    }

    private static ColumnMap InfoMap(string text)
    {
        var map = ColumnMap.DefaultLabelInfo();
        map.ApplyText(text);
        return map;
    }

    private static ColumnMap PositionMap(string text)
    {
        var map = ColumnMap.DefaultLabelPosition();
        map.ApplyText(text);
        return map;
    }

    // ---------------------------------------------------------------- reading the file

    [Fact]
    public void TheBuiltInNames_AreTheHeadersOfTheSampleFiles()
    {
        var info = ColumnMap.DefaultLabelInfo();
        var position = ColumnMap.DefaultLabelPosition();

        Assert.Equal(new[] { "Naam_Plaat" }, info.HeadersFor(ColumnKeys.Info.SheetName));
        Assert.Equal(new[] { "Opleg_2_?" }, info.HeadersFor(ColumnKeys.Info.Opleg2));
        Assert.Equal(new[] { "Test" }, info.HeadersFor(ColumnKeys.Info.Project));
        Assert.Equal(new[] { "SP" }, position.HeadersFor(ColumnKeys.Position.SheetName));
        Assert.Equal(new[] { "SUP_DESIGNATION" }, position.HeadersFor(ColumnKeys.Position.Designation));
    }

    [Fact]
    public void ApplyText_ChangesTheNamesInTheFile_AndKeepsTheOthers_IgnoringCommentsAndBlankLines()
    {
        var map = InfoMap(
            "# a comment\r\n" +
            "\r\n" +
            "Project = Projectnummer\r\n" +
            "   Cam2   =   CAM 2   \r\n" +   // spaces around the key and the name are ignored, spaces inside are kept
            "# EdgeL1 = Nothing\r\n");

        Assert.Equal(new[] { "Projectnummer" }, map.HeadersFor(ColumnKeys.Info.Project));
        Assert.Equal(new[] { "CAM 2" }, map.HeadersFor(ColumnKeys.Info.Cam2));
        Assert.Equal(new[] { "L1" }, map.HeadersFor(ColumnKeys.Info.EdgeL1));           // in a comment: not changed
        Assert.Equal(new[] { "Naam_Plaat" }, map.HeadersFor(ColumnKeys.Info.SheetName)); // not in the file: keeps its name
    }

    [Fact]
    public void ApplyText_SeveralNamesForOneField_AreKeptInOrder_AndTheKeyIgnoresCapitals()
    {
        var map = InfoMap("project = Test | Project |  Projectnr ");

        Assert.Equal(new[] { "Test", "Project", "Projectnr" }, map.HeadersFor(ColumnKeys.Info.Project));
    }

    [Fact]
    public void ApplyText_ReportsEveryProblem_WithTheFileAndLineNumber()
    {
        string text =
            "Project = Test\r\n" +          // line 1: fine
            "Colour = Red\r\n" +            // line 2: unknown field
            "this is not a line\r\n" +      // line 3: no '='
            "Cam2 =\r\n" +                  // line 4: no name
            "Cam2 = CAM_2\r\n" +            // line 5: fine
            "cam2 = CAM_3\r\n";             // line 6: the same field twice

        var error = Assert.Throws<ConversionException>(() => InfoMap(text));

        Assert.Equal(4, error.Problems.Count);
        Assert.All(error.Problems, problem => Assert.StartsWith("columns-li.txt, regel ", problem));
        Assert.Contains("regel 2:", error.Problems[0]);
        Assert.Contains("'Colour'", error.Problems[0]);
        Assert.Contains("SheetName", error.Problems[0]);            // the possible fields are listed
        Assert.Contains("regel 3:", error.Problems[1]);
        Assert.Contains("this is not a line", error.Problems[1]);
        Assert.Contains("regel 4:", error.Problems[2]);
        Assert.Contains("regel 6:", error.Problems[3]);
    }

    [Fact]
    public void AFieldOfTheOtherFile_IsUnknown()
    {
        // SheetLength belongs to the LP file, not to the LI file.
        Assert.Throws<ConversionException>(() => InfoMap("SheetLength = SUP_L"));
        Assert.Throws<ConversionException>(() => PositionMap("Project = Test"));
    }

    // ---------------------------------------------------------------- loading from disk

    [Fact]
    public void Load_NoFile_GivesTheBuiltInNames_ADeletedFileNeverStopsTheProgram()
    {
        using var folder = new TempFolder();

        Assert.Equal(new[] { "Test" }, ColumnMap.LoadLabelInfo(folder.File("weg.txt")).HeadersFor(ColumnKeys.Info.Project));
        Assert.Equal(new[] { "Test" }, ColumnMap.LoadLabelInfo(null).HeadersFor(ColumnKeys.Info.Project));
    }

    [Fact]
    public void Load_AFileWithAMistake_IsATemporaryProblem_BecauseTheTopSolidFilesAreFine()
    {
        using var folder = new TempFolder();
        string path = folder.File("columns-li.txt");
        File.WriteAllText(path, "Colour = Red\r\n");

        var error = Assert.Throws<ConversionException>(() => ColumnMap.LoadLabelInfo(path));

        Assert.True(error.IsTemporary);
        Assert.Contains("columns-li.txt", error.Message);
    }

    [Fact]
    public void Load_UnderstandsBothUtf8AndAnsi_SoAnAccentInAHeaderSurvivesNotepad()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var folder = new TempFolder();
        string utf8 = folder.File("utf8.txt");
        string ansi = folder.File("ansi.txt");
        File.WriteAllText(utf8, "Description = Omschrijving_é\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(ansi, "Description = Omschrijving_é\r\n", Encoding.GetEncoding(1252)); // an old Notepad saves this

        Assert.Equal(new[] { "Omschrijving_é" }, ColumnMap.LoadLabelInfo(utf8).HeadersFor(ColumnKeys.Info.Description));
        Assert.Equal(new[] { "Omschrijving_é" }, ColumnMap.LoadLabelInfo(ansi).HeadersFor(ColumnKeys.Info.Description));
    }

    // ---------------------------------------------------------------- the file that the program writes

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void TheNewFile_CanBeReadBackToTheSameNames_InEveryLanguage(AppLanguage language)
    {
        using var scope = new LanguageScope(language);
        using var folder = new TempFolder();
        string infoPath = folder.File("columns-li.txt");
        string positionPath = folder.File("columns-lp.txt");

        ColumnMap.DefaultLabelInfo().WriteTemplate(infoPath);
        ColumnMap.DefaultLabelPosition().WriteTemplate(positionPath);

        var info = ColumnMap.LoadLabelInfo(infoPath);
        var position = ColumnMap.LoadLabelPosition(positionPath);
        foreach (string key in new[] { ColumnKeys.Info.SheetName, ColumnKeys.Info.Opleg2, ColumnKeys.Info.Project })
        {
            Assert.Equal(ColumnMap.DefaultLabelInfo().HeadersFor(key), info.HeadersFor(key));
        }

        foreach (string key in new[] { ColumnKeys.Position.SheetName, ColumnKeys.Position.LabelAngle, ColumnKeys.Position.Designation })
        {
            Assert.Equal(ColumnMap.DefaultLabelPosition().HeadersFor(key), position.HeadersFor(key));
        }
    }

    [Fact]
    public void TheNewFile_ExplainsItself_HasOneLinePerField_AndMarksTheOptionalOne()
    {
        string info = ColumnMap.DefaultLabelInfo().ToText();
        string position = ColumnMap.DefaultLabelPosition().ToText();

        Assert.StartsWith("# TsDssConverter - kolomnamen in het LI-bestand", info);
        Assert.Contains("SheetName    = Naam_Plaat", info);            // the '=' signs line up
        Assert.Contains("Opleg2       = Opleg_2_?", info);
        Assert.Contains("Project      = Test", info);
        Assert.Equal(21, info.Split("\r\n").Count(line => line.Contains(" = ") && !line.StartsWith('#')));   // 11 + the 10 DESC fields
        Assert.DoesNotContain("optioneel", info);                      // every LI column is needed (the DESC fields have their own note)

        Assert.Equal(18, position.Split("\r\n").Count(line => line.Contains(" = ") && !line.StartsWith('#')));  // 8 + the 10 DESC fields
        Assert.Contains("# optioneel", position);
        Assert.True(position.IndexOf("# optioneel") < position.IndexOf("Designation"));   // the note is above its field

        // The example in the explanation is about a field of THIS file (Project does not exist in the LP file).
        Assert.Contains("# Bijvoorbeeld:  SheetName = Naam_Plaat | Naam_Plaat_2", info);
        Assert.Contains("# Bijvoorbeeld:  SheetName = SP | SP_2", position);
        Assert.DoesNotContain("Project", position);

        // The fields follow each other without blank lines; only the optional one has a blank line and a note above it.
        Assert.Contains("SheetName   = SP\r\nDescription = ID\r\n", position);
        Assert.Contains("LabelAngle  = LABEL_ANGLE\r\n\r\n# optioneel", position);
    }

    [Fact]
    public void WriteTemplate_UsesAByteOrderMark_AndWindowsLineEndings()
    {
        using var folder = new TempFolder();
        string path = folder.File("columns-li.txt");

        ColumnMap.DefaultLabelInfo().WriteTemplate(path);

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        string text = File.ReadAllText(path);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));       // no line ends with only \n
    }

    // ---------------------------------------------------------------- finding the columns in a real XLSX

    [Fact]
    public void ReadLabelInfo_ARenamedHeader_IsAnErrorWithoutTheFile_AndWorksWithIt()
    {
        using var folder = new TempFolder();
        string renamed = folder.File("P-1-LI.xlsx");
        RenameHeader(TestPaths.InfoFile, renamed, "Test", "Project");

        // Without a line in columns-li.txt the program does not know the new name: a clear error with a tip.
        var error = Assert.Throws<ConversionException>(() => TopSolidReader.ReadLabelInfo(renamed));
        Assert.Contains("'Test'", error.Problems[0]);
        Assert.Contains("columns-li.txt", error.Problems[1]);
        Assert.Contains("Config", error.Problems[1]);

        // With the new name in the file everything works, and the values are the same as before the rename.
        var original = TopSolidReader.ReadLabelInfo(TestPaths.InfoFile);
        var rows = TopSolidReader.ReadLabelInfo(renamed, InfoMap("Project = Project"));
        Assert.Equal(original.Select(row => row.Project), rows.Select(row => row.Project));
        Assert.Equal(63, rows.Count);
    }

    [Fact]
    public void ReadLabelInfo_TheOldAndTheNewName_BothWork_WhenBothAreInTheFile()
    {
        using var folder = new TempFolder();
        string renamed = folder.File("P-1-LI.xlsx");
        RenameHeader(TestPaths.InfoFile, renamed, "Test", "Project");
        var map = InfoMap("Project = Test | Project");

        // The old export (Test) and the new one (Project) are both understood.
        Assert.Equal(63, TopSolidReader.ReadLabelInfo(TestPaths.InfoFile, map).Count);
        Assert.Equal(63, TopSolidReader.ReadLabelInfo(renamed, map).Count);
    }

    [Fact]
    public void ReadLabelPositions_ARenamedHeader_WorksWithTheFile()
    {
        using var folder = new TempFolder();
        string renamed = folder.File("P-1-LP.xlsx");
        RenameHeader(TestPaths.PositionFile, renamed, "LABEL_X", "Label X (mm)");

        var error = Assert.Throws<ConversionException>(() => TopSolidReader.ReadLabelPositions(renamed));
        Assert.Contains("'LABEL_X'", error.Problems[0]);
        Assert.Contains("columns-lp.txt", error.Problems[1]);

        var original = TopSolidReader.ReadLabelPositions(TestPaths.PositionFile);
        var rows = TopSolidReader.ReadLabelPositions(renamed, PositionMap("LabelX = Label X (mm)"));
        Assert.Equal(original.Select(row => row.LabelX), rows.Select(row => row.LabelX));
    }

    [Fact]
    public void TheOrderOfTheColumnsInTheXlsx_NeverMatters()
    {
        using var folder = new TempFolder();
        string reversedInfo = folder.File("P-1-LI.xlsx");
        string reversedPosition = folder.File("P-1-LP.xlsx");
        ReverseColumns(TestPaths.InfoFile, reversedInfo);
        ReverseColumns(TestPaths.PositionFile, reversedPosition);

        var infoBefore = TopSolidReader.ReadLabelInfo(TestPaths.InfoFile);
        var infoAfter = TopSolidReader.ReadLabelInfo(reversedInfo);
        var positionBefore = TopSolidReader.ReadLabelPositions(TestPaths.PositionFile);
        var positionAfter = TopSolidReader.ReadLabelPositions(reversedPosition);

        Assert.Equal(
            infoBefore.Select(r => (r.SheetName, r.Description, r.Dimensions, r.MaterialText, r.EdgeL1, r.EdgeB2, r.Cam2, r.Opleg2, r.Project)),
            infoAfter.Select(r => (r.SheetName, r.Description, r.Dimensions, r.MaterialText, r.EdgeL1, r.EdgeB2, r.Cam2, r.Opleg2, r.Project)));
        Assert.Equal(
            positionBefore.Select(r => (r.SheetName, r.Description, r.SheetLength, r.SheetWidth, r.LabelX, r.LabelY, r.LabelAngle, r.Designation)),
            positionAfter.Select(r => (r.SheetName, r.Description, r.SheetLength, r.SheetWidth, r.LabelX, r.LabelY, r.LabelAngle, r.Designation)));
    }

    [Fact]
    public void AMissingOptionalColumn_IsFine_ButAMissingRequiredOne_IsAnError_ThatNamesAllOfThem_AndGivesOneTip()
    {
        using var folder = new TempFolder();

        // SUP_DESIGNATION is optional: without it the designation stays empty.
        string noDesignation = folder.File("a-LP.xlsx");
        DeleteColumn(TestPaths.PositionFile, noDesignation, "SUP_DESIGNATION");
        var rows = TopSolidReader.ReadLabelPositions(noDesignation);
        Assert.All(rows, row => Assert.Equal("", row.Designation));

        // Two required columns gone: both are named, and the tip comes once at the end.
        string noAngle = folder.File("b-LP.xlsx");
        string noAngleNoY = folder.File("c-LP.xlsx");
        DeleteColumn(noDesignation, noAngle, "LABEL_ANGLE");
        DeleteColumn(noAngle, noAngleNoY, "LABEL_Y");
        var error = Assert.Throws<ConversionException>(() => TopSolidReader.ReadLabelPositions(noAngleNoY));

        Assert.Equal(3, error.Problems.Count);
        Assert.Contains("'LABEL_Y'", error.Problems[0]);
        Assert.Contains("'LABEL_ANGLE'", error.Problems[1]);
        Assert.Contains("columns-lp.txt", error.Problems[2]);
    }

    [Fact]
    public void AMissingColumnWithSeveralNames_NamesAllOfThem()
    {
        using var folder = new TempFolder();
        string noProject = folder.File("c-LI.xlsx");
        DeleteColumn(TestPaths.InfoFile, noProject, "Test");

        var error = Assert.Throws<ConversionException>(() => TopSolidReader.ReadLabelInfo(noProject, InfoMap("Project = Test | Project")));

        Assert.Contains("'Test' / 'Project'", error.Problems[0]);
    }

    // ---------------------------------------------------------------- in the running program: the processor

    /// <summary>A world with the sample project whose LI header "Test" was renamed to "Project".</summary>
    private static ExportFixture WorldWithARenamedHeader()
    {
        var world = new ExportFixture();
        world.AddProject();
        RenameHeader(TestPaths.InfoFile, world.InfoPath(), "Test", "Project");
        return world;
    }

    [Fact]
    public void Processor_WithoutTheNewName_MovesTheFilesToFout_WithTheTipInFoutTxt()
    {
        using var folder = new TempFolder();
        using var world = WorldWithARenamedHeader();

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile, folder.File("columns-li.txt"), folder.File("columns-lp.txt"))
            .Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.False(outcome.Success);
        Assert.False(outcome.IsTemporary);   // the files really are different: they go to _Fout
        string report = File.ReadAllText(Path.Combine(outcome.MovedTo!, "fout.txt"));
        Assert.Contains("Kolom 'Test' ontbreekt in het LI-bestand", report);
        Assert.Contains("columns-li.txt", report);
    }

    [Fact]
    public void Processor_WithTheNewNameInTheColumnFile_ConvertsTheSameFiles()
    {
        using var folder = new TempFolder();
        using var world = WorldWithARenamedHeader();
        File.WriteAllText(folder.File("columns-li.txt"), "Project = Project\r\n");

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile, folder.File("columns-li.txt"), folder.File("columns-lp.txt"))
            .Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.True(outcome.Success, outcome.Message);
        Assert.StartsWith("11 platen, 63 labels", outcome.Message);
        Assert.True(File.Exists(Path.Combine(world.Batch, "Verschuren-P-20.xml")));
    }

    [Fact]
    public void Processor_ABrokenColumnFile_IsTemporary_TheFilesStay_AndTheNextTryUsesTheFixedFile()
    {
        using var folder = new TempFolder();
        using var world = WorldWithARenamedHeader();
        string columnsLi = folder.File("columns-li.txt");
        var processor = new ProjectProcessor(world.MaterialsFile, columnsLi, folder.File("columns-lp.txt"));

        // The customer made a typing mistake in the file: the TopSolid files are fine, so they are not moved.
        File.WriteAllText(columnsLi, "Projekt = Project\r\n");
        ProcessOutcome first = processor.Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.False(first.Success);
        Assert.True(first.IsTemporary);
        Assert.Contains("columns-li.txt, regel 1", first.Message);
        Assert.Contains("automatisch opnieuw", first.Message);
        Assert.True(File.Exists(world.InfoPath()));
        Assert.False(Directory.Exists(Path.Combine(world.Export, "_Fout")));

        // Fixed: the very next try converts, without restarting anything.
        File.WriteAllText(columnsLi, "Project = Project\r\n");
        ProcessOutcome second = processor.Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.True(second.Success, second.Message);
        Assert.False(File.Exists(world.InfoPath()));
    }

    [Theory]
    [InlineData(AppLanguage.French, "La colonne 'Test' est absente du fichier LI.", "Conseil pour le fichier LI")]
    [InlineData(AppLanguage.English, "Column 'Test' is missing in the LI file.", "Tip for the LI file")]
    public void TheMissingColumnMessageAndTheTip_AreInTheSelectedLanguage(AppLanguage language, string missing, string tip)
    {
        using var scope = new LanguageScope(language);
        using var folder = new TempFolder();
        string renamed = folder.File("P-1-LI.xlsx");
        RenameHeader(TestPaths.InfoFile, renamed, "Test", "Project");

        var error = Assert.Throws<ConversionException>(() => TopSolidReader.ReadLabelInfo(renamed));

        Assert.Contains(missing, error.Message);
        Assert.Contains(tip, error.Message);
    }
}
