using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>
/// Tests the checks (errors) and the warnings of the BatchBuilder with small hand-made input,
/// so no XLSX files are needed.
/// </summary>
public class BatchBuilderTests
{
    private static LabelInfoRow Info(string sheet, string description, string dimensions = "L: 500.0mm X B: 300.0mm")
    {
        return new LabelInfoRow { SheetName = sheet, Description = description, Dimensions = dimensions, MaterialText = "White", Project = "P-1" };
    }

    private static LabelPositionRow Pos(
        string sheet, string description,
        double x = 100, double y = 100, double angle = 0,
        double sheetLength = 3050, double sheetWidth = 1300, string designation = "18.0_panel 18mm")
    {
        return new LabelPositionRow
        {
            SheetName = sheet, Description = description,
            LabelX = x, LabelY = y, LabelAngle = angle,
            SheetLength = sheetLength, SheetWidth = sheetWidth, Designation = designation,
        };
    }

    private static MaterialTable Materials()
    {
        return MaterialTable.FromText("TopSolidMaterial;DssMaterial;Thickness;Grain\nWhite_18;White_18;18;0\nWhite_9;White_9;9;0\n");
    }

    private static Batch Build(List<LabelInfoRow> info, List<LabelPositionRow> positions, MaterialTable? materials = null)
    {
        return BatchBuilder.Build("Test-1", info, positions, materials ?? Materials(), new ConverterSettings(), new DateTime(2026, 1, 1));
    }

    private static IReadOnlyList<string> BuildAndGetProblems(List<LabelInfoRow> info, List<LabelPositionRow> positions, MaterialTable? materials = null)
    {
        return Assert.Throws<ConversionException>(() => Build(info, positions, materials)).Problems;
    }

    // ---------------------------------------------------------------- valid input

    [Fact]
    public void ValidInput_BuildsTheBatch_WithoutWarnings()
    {
        var batch = Build(
            new() { Info("White_18#01", "Part A - 100"), Info("White_18#01", "Part B - 101") },
            new() { Pos("White_18#01", "Part A - 100"), Pos("White_18#01", "Part B - 101") });

        Assert.Single(batch.Plans);
        Assert.Equal(2, batch.Plans[0].Sheets[0].Labels.Count);
        Assert.Empty(batch.Warnings);
    }

    // ---------------------------------------------------------------- errors

    [Fact]
    public void PartInOnlyOneFile_IsListedForBothDirections_InOneReport()
    {
        var problems = BuildAndGetProblems(
            new() { Info("White_18#01", "Part A - 100"), Info("White_18#01", "Only in LI - 102") },
            new() { Pos("White_18#01", "Part A - 100"), Pos("White_18#01", "Only in LP - 101") });

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("Only in LP - 101") && p.Contains("LP maar niet in LI"));
        Assert.Contains(problems, p => p.Contains("Only in LI - 102") && p.Contains("LI maar niet in LP"));
    }

    [Fact]
    public void DuplicateJoinKey_IsAnError_InLiAndInLp()
    {
        var problems = BuildAndGetProblems(
            new() { Info("White_18#01", "Part A - 100"), Info("White_18#01", "Part A - 100") },
            new() { Pos("White_18#01", "Part A - 100"), Pos("White_18#01", "Part A - 100") });

        Assert.Contains(problems, p => p.StartsWith("LI: dubbele"));
        Assert.Contains(problems, p => p.StartsWith("LP: dubbele"));
    }

    [Fact]
    public void SheetNameWithoutNumber_IsAnError()
    {
        var problems = BuildAndGetProblems(
            new() { Info("White_18", "Part A - 100") },
            new() { Pos("White_18", "Part A - 100") });

        Assert.Single(problems);
        Assert.Contains("materiaal#nummer", problems[0]);
    }

    [Fact]
    public void UnknownMaterial_NamesTheMaterial()
    {
        var problems = BuildAndGetProblems(
            new() { Info("Oak_18#01", "Part A - 100") },
            new() { Pos("Oak_18#01", "Part A - 100") });

        Assert.Single(problems);
        Assert.Contains("'Oak_18'", problems[0]);
    }

    [Fact]
    public void DifferentSheetSizesWithinOneMaterial_IsAnError()
    {
        var problems = BuildAndGetProblems(
            new() { Info("White_18#01", "Part A - 100"), Info("White_18#02", "Part B - 101") },
            new() { Pos("White_18#01", "Part A - 100", sheetLength: 3050), Pos("White_18#02", "Part B - 101", sheetLength: 2800) });

        Assert.Single(problems);
        Assert.Contains("'White_18'", problems[0]);
    }

    [Fact]
    public void SameSizeForDifferentMaterials_IsFine()
    {
        var batch = Build(
            new() { Info("White_18#01", "Part A - 100"), Info("White_9#01", "Part B - 101") },
            new() { Pos("White_18#01", "Part A - 100", sheetLength: 3050, designation: "18.0_x"), Pos("White_9#01", "Part B - 101", sheetLength: 2800, designation: "9.0_x") });

        Assert.Equal(2, batch.Plans.Count);
    }

    [Fact]
    public void EveryKindOfProblemIsReportedAtOnce()
    {
        // One good part and five bad ones. The user must see all five problems in one report.
        var problems = BuildAndGetProblems(
            new()
            {
                Info("White_18#01", "Good - 100"),
                Info("White_18#01", "Bad size - 101", dimensions: "660 x 590"),
                Info("White_18#01", "Bad angle - 102"),
                Info("White_18#01", "Outside - 103"),
                Info("White_18#01", "No number - abc"),
                Info("Oak_18#01", "Unknown material - 104"),
            },
            new()
            {
                Pos("White_18#01", "Good - 100"),
                Pos("White_18#01", "Bad size - 101"),
                Pos("White_18#01", "Bad angle - 102", angle: 45),
                Pos("White_18#01", "Outside - 103", x: 5000),
                Pos("White_18#01", "No number - abc"),
                Pos("Oak_18#01", "Unknown material - 104"),
            });

        Assert.Equal(5, problems.Count);
        Assert.Contains(problems, p => p.Contains("'Oak_18'"));
        Assert.Contains(problems, p => p.Contains("Afmetingen niet leesbaar") && p.Contains("Bad size - 101"));
        Assert.Contains(problems, p => p.Contains("veelvoud van 90") && p.Contains("Bad angle - 102"));
        Assert.Contains(problems, p => p.Contains("buiten de plaat") && p.Contains("Outside - 103"));
        Assert.Contains(problems, p => p.Contains("Geen onderdeelnummer") && p.Contains("No number - abc"));
    }

    [Fact]
    public void LabelOutsideTheSheetAfterTheFlip_IsAnError()
    {
        // X = 3200 is beyond the sheet length (3050). With the flip it becomes 3050 - 3200 = -150: still outside.
        var settings = new ConverterSettings { FlipX = true };
        var info = new List<LabelInfoRow> { Info("White_18#01", "Part A - 100") };
        var positions = new List<LabelPositionRow> { Pos("White_18#01", "Part A - 100", x: 3200) };

        var error = Assert.Throws<ConversionException>(
            () => BatchBuilder.Build("Test-1", info, positions, Materials(), settings, new DateTime(2026, 1, 1)));

        Assert.Contains("buiten de plaat", error.Message);
    }

    // ---------------------------------------------------------------- warnings

    [Fact]
    public void DuplicatePartId_IsAWarning_NotAnError()
    {
        // The trailing number 100 is used on two different sheets.
        var batch = Build(
            new() { Info("White_18#01", "Part A - 100"), Info("White_18#02", "Part B - 100"), Info("White_18#02", "Part C - 101") },
            new() { Pos("White_18#01", "Part A - 100"), Pos("White_18#02", "Part B - 100"), Pos("White_18#02", "Part C - 101") });

        Assert.Single(batch.Warnings);
        Assert.Contains("100", batch.Warnings[0]);
        Assert.Contains("White_18#01", batch.Warnings[0]);
        Assert.Contains("White_18#02", batch.Warnings[0]);
    }

    [Fact]
    public void ThicknessThatDiffersFromSupDesignation_IsAWarning_OncePerMaterial()
    {
        // materials.csv says White_9 is 9 mm; TopSolid says 8 mm. Three parts, but one warning.
        var batch = Build(
            new() { Info("White_9#01", "A - 1"), Info("White_9#01", "B - 2"), Info("White_9#02", "C - 3") },
            new() { Pos("White_9#01", "A - 1", designation: "8.0_panel"), Pos("White_9#01", "B - 2", designation: "8.0_panel"), Pos("White_9#02", "C - 3", designation: "8.0_panel") });

        Assert.Single(batch.Warnings);
        Assert.Contains("'White_9'", batch.Warnings[0]);
        Assert.Contains("9 mm", batch.Warnings[0]);
        Assert.Contains("8 mm", batch.Warnings[0]);
    }

    [Theory]
    [InlineData("18.0_panel 18mm")]   // same thickness
    [InlineData("18_panel")]          // no decimals
    [InlineData("")]                  // column missing in the export: nothing to compare
    [InlineData("panel 18mm")]        // does not start with a number: nothing to compare
    public void ThicknessThatMatchesOrCannotBeChecked_GivesNoWarning(string designation)
    {
        var batch = Build(
            new() { Info("White_18#01", "A - 1") },
            new() { Pos("White_18#01", "A - 1", designation: designation) });

        Assert.Empty(batch.Warnings);
    }

    [Fact]
    public void ErrorsAreThrownBeforeWarningsMatter()
    {
        // A batch with an error never gets warnings: nothing is built.
        var problems = BuildAndGetProblems(
            new() { Info("White_9#01", "A - 1"), Info("Oak#01", "B - 2") },
            new() { Pos("White_9#01", "A - 1", designation: "8.0"), Pos("Oak#01", "B - 2") });

        Assert.Single(problems);
        Assert.Contains("'Oak'", problems[0]);
    }
}
