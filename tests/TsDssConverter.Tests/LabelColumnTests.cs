using ClosedXML.Excel;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>
/// Tests for the ten extra description columns (DESC1 .. DESC10): they are optional columns in the LI and LP file,
/// they are merged (LI first, LP fills the gaps) and they are the last columns of the label CSV, with names that can
/// be changed in columns-label.txt.
/// </summary>
public class LabelColumnTests
{
    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Copies a sample file and adds the description columns after the last column.
    /// <paramref name="value"/> gives the text of each cell (number of the field 1..10, data row 2..n); null = empty cell.
    /// <paramref name="header"/> gives the header of each column.
    /// </summary>
    private static void AddDescriptionColumns(
        string source, string target, Func<int, string> header, Func<int, int, string?> value, int count = ColumnKeys.DescriptionCount)
    {
        using var workbook = new XLWorkbook(source);
        var sheet = workbook.Worksheets.First();
        int firstNew = sheet.LastColumnUsed()!.ColumnNumber() + 1;
        int lastRow = sheet.LastRowUsed()!.RowNumber();

        for (int number = 1; number <= count; number++)
        {
            sheet.Cell(1, firstNew + number - 1).Value = header(number);
            for (int row = 2; row <= lastRow; row++)
            {
                string? text = value(number, row);
                if (text != null)
                {
                    sheet.Cell(row, firstNew + number - 1).Value = text;
                }
            }
        }

        workbook.SaveAs(target);
    }

    private static ConversionResult Convert(
        string infoPath, string positionPath, string outFolder, LabelColumnNames? labelColumns = null,
        ColumnMap? infoColumns = null)
    {
        return new Converter().Convert(
            infoPath, positionPath, TestPaths.MaterialsFile, outFolder, outFolder,
            new ConverterSettings(), TestPaths.GoldenPlanDate, infoColumns, null, labelColumns);
    }

    /// <summary>The label CSV of the first sheet as rows of cells (tab separated), header included.</summary>
    private static List<string[]> ReadFirstCsv(string folder)
    {
        string path = Path.Combine(folder, "Verschuren-P-20_001.csv");
        return File.ReadAllLines(path).Select(line => line.Split('\t')).ToList();
    }

    private const int FixedColumns = 23;

    // ---------------------------------------------------------------- the columns in the LI and LP files

    [Fact]
    public void TheTwoColumnFiles_HaveTenOptionalDescriptionFields_WithDefaultHeadersDESC1ToDESC10()
    {
        var info = ColumnMap.DefaultLabelInfo();
        var position = ColumnMap.DefaultLabelPosition();

        for (int number = 1; number <= ColumnKeys.DescriptionCount; number++)
        {
            Assert.Equal(new[] { "DESC" + number }, info.HeadersFor(ColumnKeys.Desc(number)));
            Assert.Equal(new[] { "DESC" + number }, position.HeadersFor(ColumnKeys.Desc(number)));
        }
    }

    [Fact]
    public void TheNewColumnFiles_ListTheDescriptionFields_UnderTheirOwnNote_InEveryLanguage()
    {
        foreach (AppLanguage language in new[] { AppLanguage.Dutch, AppLanguage.French, AppLanguage.English })
        {
            using var scope = new LanguageScope(language);

            foreach (string text in new[] { ColumnMap.DefaultLabelInfo().ToText(), ColumnMap.DefaultLabelPosition().ToText() })
            {
                // (the '=' signs line up per file, so the number of spaces before them differs)
                var first = System.Text.RegularExpressions.Regex.Match(text, @"Desc1\s+= DESC1\r\n");
                Assert.True(first.Success);
                Assert.Matches(@"Desc10\s+= DESC10\r\n", text);
                Assert.Contains("columns-label.txt", text);                        // the note points to the label file
                Assert.True(text.IndexOf("columns-label.txt") < first.Index);       // and is above the fields
            }
        }
    }

    [Fact]
    public void Reader_FillsTheDescriptions_WhenTheColumnsAreThere_AndLeavesThemEmptyWhenNot()
    {
        using var folder = new TempFolder();
        string withColumns = folder.File("a-LI.xlsx");
        AddDescriptionColumns(TestPaths.InfoFile, withColumns, n => "DESC" + n, (n, row) => $"info{n}-{row}");

        var rows = TopSolidReader.ReadLabelInfo(withColumns);
        Assert.Equal("info1-2", rows[0].Descriptions[0]);
        Assert.Equal("info10-2", rows[0].Descriptions[9]);
        Assert.Equal("info3-64", rows[62].Descriptions[2]);

        Assert.All(TopSolidReader.ReadLabelInfo(TestPaths.InfoFile), row => Assert.All(row.Descriptions, d => Assert.Equal("", d)));
        Assert.All(TopSolidReader.ReadLabelPositions(TestPaths.PositionFile), row => Assert.Equal(10, row.Descriptions.Length));
    }

    [Fact]
    public void ADescriptionColumn_CanHaveAnotherHeader_ByEditingTheColumnFile()
    {
        using var folder = new TempFolder();
        string renamed = folder.File("b-LI.xlsx");
        AddDescriptionColumns(TestPaths.InfoFile, renamed, n => "Extra " + n, (n, row) => $"v{n}", count: 2);
        var map = ColumnMap.DefaultLabelInfo();
        map.ApplyText("Desc1 = Extra 1\r\nDesc2 = Extra 2 | DESC2");

        var rows = TopSolidReader.ReadLabelInfo(renamed, map);

        Assert.Equal("v1", rows[0].Descriptions[0]);
        Assert.Equal("v2", rows[0].Descriptions[1]);
        Assert.Equal("", rows[0].Descriptions[2]);    // no such column: empty
    }

    [Fact]
    public void ADescriptionColumn_IsNeverRequired_SoOldExportsKeepWorking()
    {
        // The sample files have no DESC columns at all, and still convert.
        using var folder = new TempFolder();

        ConversionResult result = Convert(TestPaths.InfoFile, TestPaths.PositionFile, folder.Path);

        Assert.Equal(63, result.PartCount);
    }

    // ---------------------------------------------------------------- the label CSV

    [Fact]
    public void LabelCsv_HasTheFixedColumnsFirst_AndTenDescriptionColumnsAtTheEnd()
    {
        using var folder = new TempFolder();
        string info = folder.File("Verschuren-P-20-LI.xlsx");
        AddDescriptionColumns(TestPaths.InfoFile, info, n => "DESC" + n, (n, row) => $"D{n}");

        Convert(info, TestPaths.PositionFile, folder.Path);
        List<string[]> csv = ReadFirstCsv(folder.Path);

        Assert.Equal(23, LabelCsvWriter.FixedColumnNames.Count);
        Assert.Equal(FixedColumns + 10, csv[0].Length);
        Assert.Equal("SHEET", csv[0][FixedColumns - 1]);
        Assert.Equal(Enumerable.Range(1, 10).Select(n => "DESC" + n), csv[0].Skip(FixedColumns));
        Assert.All(csv, row => Assert.Equal(FixedColumns + 10, row.Length));             // every row has all the columns
        Assert.Equal(Enumerable.Range(1, 10).Select(n => "D" + n), csv[1].Skip(FixedColumns));
    }

    [Fact]
    public void LabelCsv_WithoutDescriptionColumnsInTheExport_StillHasTheTenColumns_Empty()
    {
        using var folder = new TempFolder();

        Convert(TestPaths.InfoFile, TestPaths.PositionFile, folder.Path);
        List<string[]> csv = ReadFirstCsv(folder.Path);

        Assert.Equal(Enumerable.Range(1, 10).Select(n => "DESC" + n), csv[0].Skip(FixedColumns));
        Assert.All(csv.Skip(1), row => Assert.All(row.Skip(FixedColumns), value => Assert.Equal("", value)));
    }

    [Fact]
    public void Descriptions_ComeFromLi_AndLpFillsTheGaps()
    {
        using var folder = new TempFolder();
        string info = folder.File("Verschuren-P-20-LI.xlsx");
        string position = folder.File("Verschuren-P-20-LP.xlsx");
        // LI: DESC1 filled in the even rows only (32 of the 63 parts). LP: DESC1 in every row, DESC2 only in LP.
        AddDescriptionColumns(TestPaths.InfoFile, info, n => "DESC" + n, (n, row) => row % 2 == 0 ? "from-LI" : null, count: 1);
        AddDescriptionColumns(TestPaths.PositionFile, position, n => "DESC" + n, (n, row) => n == 1 ? "from-LP-1" : "from-LP-2", count: 2);

        Convert(info, position, folder.Path);

        // (the order of the rows follows the LP file, so count over all the label files instead of picking one row)
        List<string[]> rows = Directory.GetFiles(folder.Path, "*.csv")
            .SelectMany(file => File.ReadAllLines(file).Skip(1))
            .Select(line => line.Split('\t'))
            .ToList();

        Assert.Equal(63, rows.Count);
        Assert.Equal(32, rows.Count(row => row[FixedColumns] == "from-LI"));       // the LI value wins over the LP value
        Assert.Equal(31, rows.Count(row => row[FixedColumns] == "from-LP-1"));     // LI was empty: the LP value fills the gap
        Assert.All(rows, row => Assert.Equal("from-LP-2", row[FixedColumns + 1])); // only LP has DESC2
        Assert.All(rows, row => Assert.Equal("", row[FixedColumns + 2]));          // nobody has DESC3
    }

    [Fact]
    public void TabsAndLineBreaksInADescription_AreReplacedByASpace_SoTheCsvStaysIntact()
    {
        using var folder = new TempFolder();
        string info = folder.File("Verschuren-P-20-LI.xlsx");
        AddDescriptionColumns(TestPaths.InfoFile, info, n => "DESC" + n, (n, row) => "eerste\tregel\ntweede", count: 1);

        Convert(info, TestPaths.PositionFile, folder.Path);
        List<string[]> csv = ReadFirstCsv(folder.Path);

        Assert.All(csv, row => Assert.Equal(FixedColumns + 10, row.Length));
        Assert.Equal("eerste regel tweede", csv[1][FixedColumns]);
    }

    [Fact]
    public void LabelCsv_UsesTheNamesOfColumnsLabelTxt_ForTheDescriptionColumns()
    {
        using var folder = new TempFolder();
        var names = new LabelColumnNames();
        names.ApplyText("Desc1 = KLANT\r\nDesc2 = OPMERKING");

        Convert(TestPaths.InfoFile, TestPaths.PositionFile, folder.Path, names);
        List<string[]> csv = ReadFirstCsv(folder.Path);

        Assert.Equal("KLANT", csv[0][FixedColumns]);
        Assert.Equal("OPMERKING", csv[0][FixedColumns + 1]);
        Assert.Equal("DESC3", csv[0][FixedColumns + 2]);   // not in the file: keeps its name
    }

    // ---------------------------------------------------------------- columns-label.txt

    [Fact]
    public void TheDefaultNames_AreDESC1ToDESC10()
    {
        Assert.Equal(Enumerable.Range(1, 10).Select(n => "DESC" + n), new LabelColumnNames().Names);
    }

    [Fact]
    public void ApplyText_ChangesNames_IgnoresCommentsAndBlankLines_AndKeysIgnoreCapitals()
    {
        var names = new LabelColumnNames();

        names.ApplyText("# comment\r\n\r\n desc4 =  Klant_naam \r\nDesc10=Note-10\r\n");

        Assert.Equal("Klant_naam", names.Names[3]);
        Assert.Equal("Note-10", names.Names[9]);
        Assert.Equal("DESC1", names.Names[0]);
    }

    [Fact]
    public void ApplyText_ReportsEveryProblem_WithTheFileAndLineNumber()
    {
        string text =
            "Desc1 = KLANT\r\n" +            // 1: fine
            "Colour = Red\r\n" +             // 2: unknown field
            "no equals sign\r\n" +           // 3: not a line
            "Desc2 =\r\n" +                  // 4: no name
            "Desc3 = Naam met spatie\r\n" +  // 5: not allowed in a column name
            "Desc4 = Één\r\n" +              // 6: accent
            "Desc1 = ANDERS\r\n" +           // 7: field twice
            "Desc5 = MATERIAL\r\n";          // 8: same as a fixed column

        var error = Assert.Throws<ConversionException>(() => new LabelColumnNames().ApplyText(text));

        Assert.Equal(7, error.Problems.Count);
        Assert.All(error.Problems, problem => Assert.StartsWith("columns-label.txt, regel ", problem));
        Assert.Contains("regel 2:", error.Problems[0]);
        Assert.Contains("Desc1, Desc2", error.Problems[0]);          // the possible fields are listed
        Assert.Contains("regel 3:", error.Problems[1]);
        Assert.Contains("regel 4:", error.Problems[2]);
        Assert.Contains("regel 5:", error.Problems[3]);
        Assert.Contains("regel 6:", error.Problems[4]);
        Assert.Contains("regel 7:", error.Problems[5]);
        Assert.Contains("regel 8:", error.Problems[6]);
        Assert.Contains("'MATERIAL'", error.Problems[6]);
    }

    [Theory]
    [InlineData("Desc1 = ID")]              // a mandatory column of Duivestein
    [InlineData("Desc1 = description")]     // a fixed column, in other capitals
    [InlineData("Desc1 = DESC2")]           // the name of another description column (which keeps its default name)
    public void ANameThatIsAlreadyAColumn_IsAnError_BecauseDuivesteinWouldNotKnowWhichOneIsMeant(string line)
    {
        var error = Assert.Throws<ConversionException>(() => new LabelColumnNames().ApplyText(line));

        Assert.Single(error.Problems);
        Assert.StartsWith("columns-label.txt, regel 1:", error.Problems[0]);   // blamed on the line the user wrote
    }

    [Fact]
    public void TwoDescriptionColumnsSwappingNames_IsFine()
    {
        var names = new LabelColumnNames();

        names.ApplyText("Desc1 = DESC2\r\nDesc2 = DESC1");

        Assert.Equal("DESC2", names.Names[0]);
        Assert.Equal("DESC1", names.Names[1]);
    }

    [Fact]
    public void Load_NoFile_GivesTheDefaults_AMistakeIsTemporary_AndTheTemplateReadsBack()
    {
        using var folder = new TempFolder();
        Assert.Equal("DESC1", LabelColumnNames.Load(folder.File("weg.txt")).Names[0]);
        Assert.Equal("DESC1", LabelColumnNames.Load(null).Names[0]);

        string bad = folder.File("bad.txt");
        File.WriteAllText(bad, "Colour = Red\r\n");
        var error = Assert.Throws<ConversionException>(() => LabelColumnNames.Load(bad));
        Assert.True(error.IsTemporary);   // the TopSolid files are fine: they wait until the file is fixed

        foreach (AppLanguage language in new[] { AppLanguage.Dutch, AppLanguage.French, AppLanguage.English })
        {
            using var scope = new LanguageScope(language);
            string path = folder.File("template-" + language + ".txt");
            new LabelColumnNames().WriteTemplate(path);

            Assert.Equal(Enumerable.Range(1, 10).Select(n => "DESC" + n), LabelColumnNames.Load(path).Names);
        }
    }

    [Fact]
    public void TheNewFile_ExplainsItself_AndHasOneLinePerDescriptionColumn()
    {
        using var folder = new TempFolder();
        string path = folder.File("columns-label.txt");
        new LabelColumnNames().WriteTemplate(path);
        string text = File.ReadAllText(path);

        Assert.StartsWith("# TsDssConverter - de 10 extra beschrijvingskolommen", text);
        Assert.Contains("Desc1  = DESC1\r\n", text);
        Assert.Contains("Desc10 = DESC10\r\n", text);
        Assert.Equal(10, text.Split("\r\n").Count(line => line.Contains(" = ") && !line.StartsWith('#')));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(path).Take(3));   // with a byte order mark
    }

    // ---------------------------------------------------------------- in the running program

    [Fact]
    public void DataFolder_MakesColumnsLabelTxt_AndNeverOverwritesAChangedOne()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.File("TsDssConverter"));
        Assert.EndsWith("columns-label.txt", folder.LabelColumnsFile);

        folder.EnsureCreated();
        Assert.Equal("DESC1", LabelColumnNames.Load(folder.LabelColumnsFile).Names[0]);

        File.WriteAllText(folder.LabelColumnsFile, "Desc1 = KLANT\r\n");
        folder.EnsureCreated();
        Assert.Equal("KLANT", LabelColumnNames.Load(folder.LabelColumnsFile).Names[0]);

        File.Delete(folder.LabelColumnsFile);
        folder.EnsureCreated();
        Assert.Equal("DESC1", LabelColumnNames.Load(folder.LabelColumnsFile).Names[0]);
    }

    [Fact]
    public void Processor_UsesColumnsLabelTxt_AndABrokenFileIsTemporary_UntilItIsFixed()
    {
        using var folder = new TempFolder();
        using var world = new ExportFixture();
        world.AddProject();
        string labelFile = folder.File("columns-label.txt");
        var processor = new ProjectProcessor(world.MaterialsFile, null, null, labelFile);

        File.WriteAllText(labelFile, "Desc1 = ID\r\n");   // a mistake: ID is a column already
        ProcessOutcome first = processor.Process(world.Files(), world.Settings, world.Clock.Now);
        Assert.False(first.Success);
        Assert.True(first.IsTemporary);
        Assert.Contains("columns-label.txt, regel 1", first.Message);
        Assert.True(File.Exists(world.InfoPath()));       // the TopSolid files stay

        File.WriteAllText(labelFile, "Desc1 = KLANT\r\n");
        ProcessOutcome second = processor.Process(world.Files(), world.Settings, world.Clock.Now);
        Assert.True(second.Success, second.Message);

        string header = File.ReadLines(Path.Combine(world.Label, "Verschuren-P-20_001.csv")).First();
        Assert.Equal("KLANT", header.Split('\t')[FixedColumns]);
    }
}
