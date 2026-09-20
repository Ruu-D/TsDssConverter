using ClosedXML.Excel;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>
/// Tests for the fixed field CAM3 and the ten extra description columns (DESC1 .. DESC10). The sample export has them
/// in the LI file; the LP file may have them too (LI first, LP fills the gaps). They are the last columns of the label
/// CSV, with names that can be changed in columns-label.txt.
/// </summary>
public class LabelColumnTests
{
    // ---------------------------------------------------------------- helpers

    private static readonly string[] DescriptionHeaders = Enumerable.Range(1, ColumnKeys.DescriptionCount).Select(n => "DESC" + n).ToArray();

    private static (string Header, Func<int, string?> Value) Column(string header, Func<int, string?> value) => (header, value);

    /// <summary>
    /// Copies a sample file and changes columns. A column in <paramref name="columns"/> is found by its header and
    /// overwritten, or added after the last column if the file has none like that; the function gives the text of each
    /// cell for a data row (2..n), null = an empty cell. A header in <paramref name="withoutHeader"/> is emptied:
    /// a column without a name does not exist for the reader.
    /// </summary>
    private static void ChangeColumns(
        string source, string target, (string Header, Func<int, string?> Value)[] columns, params string[] withoutHeader)
    {
        using var workbook = new XLWorkbook(source);
        var sheet = workbook.Worksheets.First();
        int lastRow = sheet.LastRowUsed()!.RowNumber();
        int nextFree = sheet.LastColumnUsed()!.ColumnNumber() + 1;

        foreach (var (header, value) in columns)
        {
            var headerCell = sheet.Row(1).CellsUsed().FirstOrDefault(cell => cell.GetString().Trim() == header);
            int column = headerCell?.Address.ColumnNumber ?? nextFree++;
            sheet.Cell(1, column).Value = header;

            for (int row = 2; row <= lastRow; row++)
            {
                string? text = value(row);
                if (text == null)
                {
                    sheet.Cell(row, column).Clear();
                }
                else
                {
                    sheet.Cell(row, column).Value = text;
                }
            }
        }

        foreach (string header in withoutHeader)
        {
            sheet.Row(1).CellsUsed().First(cell => cell.GetString().Trim() == header).Value = "";
        }

        workbook.SaveAs(target);
    }

    /// <summary>The sample LI without its ten DESC columns, named like the real one so the batch name is right.</summary>
    private static string InfoWithoutDescriptions(TempFolder folder)
    {
        string info = folder.File("DAAN_ROGIERS-P2026.09-LI.xlsx");
        ChangeColumns(TestPaths.InfoFile, info, Array.Empty<(string, Func<int, string?>)>(), DescriptionHeaders);
        return info;
    }

    private static ConversionResult Convert(
        string infoPath, string positionPath, string outFolder, LabelColumnNames? labelColumns = null,
        ColumnMap? infoColumns = null)
    {
        return new Converter().Convert(
            infoPath, positionPath, TestPaths.MaterialsFile, outFolder, outFolder,
            TestPaths.SafeSettings(), TestPaths.GoldenPlanDate, infoColumns, null, labelColumns);
    }

    /// <summary>The label CSV of the first sheet as rows of cells (tab separated), header included.</summary>
    private static List<string[]> ReadFirstCsv(string folder)
    {
        string path = Path.Combine(folder, "DAAN_ROGIERS-P2026.09_001.csv");
        return File.ReadAllLines(path).Select(line => line.Split('\t')).ToList();
    }

    /// <summary>All the rows of all the label CSVs of a conversion (without the header rows).</summary>
    private static List<string[]> ReadAllRows(string folder)
    {
        return Directory.GetFiles(folder, "*.csv")
            .SelectMany(file => File.ReadAllLines(file).Skip(1))
            .Select(line => line.Split('\t'))
            .ToList();
    }

    private const int FixedColumns = 23;   // the fixed columns of the label CSV, before the ten DESC columns

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
    public void Reader_FillsTheDescriptionsFromTheSampleLi_AndLeavesThemEmptyWhereTheColumnsAreNotThere()
    {
        using var folder = new TempFolder();

        // The sample LI has the ten columns, filled for every part.
        Assert.All(TopSolidReader.ReadLabelInfo(TestPaths.InfoFile), row =>
        {
            Assert.Equal("Nr bon commande", row.Descriptions[0]);
            Assert.Equal("Extra texte 2", row.Descriptions[1]);
            Assert.Equal("Extra texte 10", row.Descriptions[9]);
        });

        // The sample LP has none, and the LI without the ten columns neither: empty texts, never a missing value.
        Assert.All(TopSolidReader.ReadLabelPositions(TestPaths.PositionFile), row =>
        {
            Assert.Equal(10, row.Descriptions.Length);
            Assert.All(row.Descriptions, d => Assert.Equal("", d));
        });
        Assert.All(TopSolidReader.ReadLabelInfo(InfoWithoutDescriptions(folder)), row => Assert.All(row.Descriptions, d => Assert.Equal("", d)));
    }

    [Fact]
    public void ADescriptionColumn_CanHaveAnotherHeader_ByEditingTheColumnFile()
    {
        using var folder = new TempFolder();
        string renamed = folder.File("b-LI.xlsx");
        ChangeColumns(TestPaths.InfoFile, renamed, new[] { Column("Extra 1", row => "v1") }, "DESC1");
        var map = ColumnMap.DefaultLabelInfo();

        // The built-in names do not know "Extra 1": Desc1 is empty. The other columns are read as before.
        var withoutMap = TopSolidReader.ReadLabelInfo(renamed);
        Assert.Equal("", withoutMap[0].Descriptions[0]);
        Assert.Equal("Extra texte 2", withoutMap[0].Descriptions[1]);

        map.ApplyText("Desc1 = Extra 1");
        var rows = TopSolidReader.ReadLabelInfo(renamed, map);

        Assert.Equal("v1", rows[0].Descriptions[0]);
        Assert.Equal("Extra texte 2", rows[0].Descriptions[1]);
    }

    [Fact]
    public void ADescriptionColumn_IsNeverRequired()
    {
        using var folder = new TempFolder();

        ConversionResult result = Convert(InfoWithoutDescriptions(folder), TestPaths.PositionFile, folder.Path);

        Assert.Equal(22, result.PartCount);
    }

    // ---------------------------------------------------------------- the label CSV

    [Fact]
    public void LabelCsv_HasTheFixedColumnsFirst_AndTenDescriptionColumnsAtTheEnd()
    {
        using var folder = new TempFolder();
        string info = folder.File("DAAN_ROGIERS-P2026.09-LI.xlsx");
        ChangeColumns(TestPaths.InfoFile, info, DescriptionHeaders.Select((h, i) => Column(h, row => $"D{i + 1}")).ToArray());

        Convert(info, TestPaths.PositionFile, folder.Path);
        List<string[]> csv = ReadFirstCsv(folder.Path);

        Assert.Equal(23, LabelCsvWriter.FixedColumnNames.Count);
        Assert.Equal(FixedColumns + 10, csv[0].Length);
        Assert.Equal("SHEET", csv[0][FixedColumns - 1]);
        Assert.Equal(DescriptionHeaders, csv[0].Skip(FixedColumns));
        Assert.All(csv, row => Assert.Equal(FixedColumns + 10, row.Length));             // every row has all the columns
        Assert.Equal(Enumerable.Range(1, 10).Select(n => "D" + n), csv[1].Skip(FixedColumns));
        Assert.DoesNotContain("OPLEG2", csv[0]);                                          // gone for good
    }

    [Fact]
    public void Cam2AndCam3_AreTheSpecialColumns_CAM3RightAfterCAM2_FilledFromTheCam3Column()
    {
        using var folder = new TempFolder();

        Convert(TestPaths.InfoFile, TestPaths.PositionFile, folder.Path);
        string[] header = ReadFirstCsv(folder.Path)[0];
        List<string[]> rows = ReadAllRows(folder.Path);

        int cam2 = Array.IndexOf(header, "CAM2"), cam3 = Array.IndexOf(header, "CAM3");
        Assert.Equal(cam2 + 1, cam3);                                                   // right after CAM2
        Assert.Equal(22, rows.Count);
        Assert.All(rows, row => Assert.NotEqual("", row[cam2]));                         // every part has a second program ...
        Assert.Equal(5, rows.Count(row => row[cam3] != ""));                             // ... and only 5 of the 22 have a third
        Assert.All(rows.Where(row => row[cam3] != ""), row => Assert.Equal(row[cam2].Replace("_2.cix", "_3.cix"), row[cam3]));
    }

    [Fact]
    public void Cam3_CanBeRenamedInTheColumnFile_AndItsNameIsTakenForTheLabelColumns()
    {
        var map = ColumnMap.DefaultLabelInfo();
        Assert.Equal(new[] { "CAM_3" }, map.HeadersFor(ColumnKeys.Info.Cam3));

        map.ApplyText("Cam3 = CAM 3 | CAM_3");
        Assert.Equal(new[] { "CAM 3", "CAM_3" }, map.HeadersFor(ColumnKeys.Info.Cam3));

        // "CAM3" is a fixed column of the label CSV, so a description column cannot have that name
        var error = Assert.Throws<ConversionException>(() => new LabelColumnNames().ApplyText("Desc1 = CAM3"));
        Assert.Contains("'CAM3'", error.Problems[0]);
    }

    [Fact]
    public void LabelCsv_WithoutDescriptionColumnsInTheExport_StillHasTheTenColumns_Empty()
    {
        using var folder = new TempFolder();

        Convert(InfoWithoutDescriptions(folder), TestPaths.PositionFile, folder.Path);
        List<string[]> csv = ReadFirstCsv(folder.Path);

        Assert.Equal(DescriptionHeaders, csv[0].Skip(FixedColumns));
        Assert.All(csv.Skip(1), row => Assert.All(row.Skip(FixedColumns), value => Assert.Equal("", value)));
    }

    [Fact]
    public void Descriptions_ComeFromLi_AndLpFillsTheGaps()
    {
        using var folder = new TempFolder();
        string info = folder.File("DAAN_ROGIERS-P2026.09-LI.xlsx");
        string position = folder.File("DAAN_ROGIERS-P2026.09-LP.xlsx");

        // LI: DESC1 filled in the even data rows only (11 of the 22 parts), DESC2 .. DESC10 emptied.
        var liColumns = DescriptionHeaders.Select((header, i) => Column(header, row => i == 0 && row % 2 == 0 ? "from-LI" : null)).ToArray();
        ChangeColumns(TestPaths.InfoFile, info, liColumns);

        // LP: DESC1 in every row, and DESC2 which only the LP has.
        ChangeColumns(TestPaths.PositionFile, position, new[] { Column("DESC1", row => "from-LP-1"), Column("DESC2", row => "from-LP-2") });

        Convert(info, position, folder.Path);
        List<string[]> rows = ReadAllRows(folder.Path);   // (the order of the rows follows the LP file: count instead of picking one)

        Assert.Equal(22, rows.Count);
        Assert.Equal(11, rows.Count(row => row[FixedColumns] == "from-LI"));       // the LI value wins over the LP value
        Assert.Equal(11, rows.Count(row => row[FixedColumns] == "from-LP-1"));     // LI was empty: the LP value fills the gap
        Assert.All(rows, row => Assert.Equal("from-LP-2", row[FixedColumns + 1])); // only LP has DESC2
        Assert.All(rows, row => Assert.Equal("", row[FixedColumns + 2]));          // nobody has DESC3
    }

    [Fact]
    public void TabsAndLineBreaksInADescription_AreReplacedByASpace_SoTheCsvStaysIntact()
    {
        using var folder = new TempFolder();
        string info = folder.File("DAAN_ROGIERS-P2026.09-LI.xlsx");
        ChangeColumns(TestPaths.InfoFile, info, new[] { Column("DESC1", row => "eerste\tregel\ntweede") });

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
        Assert.Equal("Nr bon commande", csv[1][FixedColumns]);   // the value still comes from the DESC1 column of the export
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

        string header = File.ReadLines(Path.Combine(world.Label, "DAAN_ROGIERS-P2026.09_001.csv")).First();
        Assert.Equal("KLANT", header.Split('\t')[FixedColumns]);
    }
}
