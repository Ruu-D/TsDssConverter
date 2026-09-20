using ClosedXML.Excel;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

public class XlsxTableTests
{
    [Fact]
    public void Load_FindsColumnsByName_IgnoresUnnamedColumns_AndStopsAtTheFirstEmptyRow()
    {
        using var folder = new TempFolder();
        string path = folder.File("test.xlsx");

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "Name";
            sheet.Cell(1, 2).Value = "Amount";
            sheet.Cell(1, 3).Value = "";        // unnamed column
            sheet.Cell(1, 4).Value = "Text ";   // header with a trailing space

            sheet.Cell(2, 1).Value = "  one ";
            sheet.Cell(2, 2).Value = 289.24;             // a real number, not text
            sheet.Cell(2, 3).Value = "Submap1";
            sheet.Cell(2, 4).Value = "x";

            sheet.Cell(3, 1).Value = "two";
            sheet.Cell(3, 2).Value = 3050;

            // row 4 is empty: everything below it must be ignored
            sheet.Cell(5, 1).Value = "after the empty row";

            workbook.SaveAs(path);
        }

        var table = XlsxTable.Load(path);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("one", table.Rows[0]["Name"]);                 // trimmed
        Assert.Equal("289.24", table.Rows[0]["Amount"]);            // dot as decimal separator (also on a Belgian PC)
        Assert.Equal("x", table.Rows[0]["text"]);                   // header trimmed, case-insensitive
        Assert.Equal("3050", table.Rows[1]["Amount"]);
        Assert.Equal("", table.Rows[1]["Text"]);                    // empty cell = empty text
        Assert.DoesNotContain("Submap1", table.Rows[0].Values);     // unnamed column is not in the row
    }

    [Fact]
    public void FindHeader_GivesTheFirstNameThatTheFileHas_CaseInsensitive_OrNull()
    {
        using var folder = new TempFolder();
        string path = folder.File("test.xlsx");

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "Naam_Plaat";
            sheet.Cell(1, 2).Value = "Project";
            sheet.Cell(2, 1).Value = "White_18#01";
            workbook.SaveAs(path);
        }

        var table = XlsxTable.Load(path);

        Assert.Equal("Naam_Plaat", table.FindHeader(new[] { "Naam_Plaat" }));
        Assert.Equal("Project", table.FindHeader(new[] { "Test", "Project" }));        // the first name is not there: the second is
        Assert.Equal("naam_plaat", table.FindHeader(new[] { "naam_plaat" }));           // matching ignores capitals
        Assert.Null(table.FindHeader(new[] { "Afmetingen", "Size" }));
        Assert.True(table.HasColumn("naam_plaat"));
        Assert.False(table.HasColumn("Afmetingen"));
    }

    [Fact]
    public void Load_MissingFile_IsAnError()
    {
        Assert.Throws<ConversionException>(() => XlsxTable.Load(@"C:\does\not\exist.xlsx"));
    }

    [Fact]
    public void SampleFiles_HaveSixtyThreeParts()
    {
        Assert.Equal(63, TopSolidReader.ReadLabelInfo(TestPaths.InfoFile).Count);
        Assert.Equal(63, TopSolidReader.ReadLabelPositions(TestPaths.PositionFile).Count);
    }
}
