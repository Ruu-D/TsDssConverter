using System.Text;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

public class MaterialTableTests
{
    private const string Header = "TopSolidMaterial;DssMaterial;Thickness;Grain";

    // "Chêne", written with an escape so this source file does not depend on its own encoding.
    private const string Chene = "Ch\u00eane";

    [Fact]
    public void SampleFile_HasTheFiveMaterialsOfTheSampleExport()
    {
        var table = MaterialTable.Load(TestPaths.MaterialsFile);

        var white = table.Find("White_18");
        Assert.Equal("White_18", white.DssName);
        Assert.Equal(18, white.Thickness);
        Assert.Equal(0, white.Grain);
        Assert.Equal("Geen", white.GrainText);

        var oak = table.Find("H1145_-_ST10_-_" + Chene + "_Bardolino_naturel_Zijdewit_19");
        Assert.Equal(19, oak.Thickness);
        Assert.Equal(1, oak.Grain);
        Assert.Equal("Langs", oak.GrainText);

        Assert.Equal(9, table.Find("White_9").Thickness);
        Assert.Equal(40, table.Find("H1145_-_ST10_-_" + Chene + "_Bardolino_naturel_Zijdewit_40").Thickness);
        Assert.Equal(18, table.Find("Paars_18").Thickness);
    }

    [Fact]
    public void Find_IsTrimmedAndCaseInsensitive()
    {
        var table = MaterialTable.FromText(Header + "\nWhite_18;WIT18;18;0\n");

        Assert.Equal("WIT18", table.Find("  white_18 ").DssName);
    }

    [Fact]
    public void Find_UnknownMaterial_IsAnErrorThatNamesIt()
    {
        var table = MaterialTable.FromText(Header + "\nWhite_18;WIT18;18;0\n");

        var error = Assert.Throws<ConversionException>(() => table.Find("Paars_18"));

        Assert.Contains("Paars_18", error.Message);
        Assert.Contains("materials.csv", error.Message);
    }

    [Fact]
    public void WorksWithWindowsAndUnixLineEndings_AndBlankLines()
    {
        var table = MaterialTable.FromText(Header + "\r\nWhite_18;WIT18;18;0\r\n\r\nWhite_9;WIT9;9;0");

        Assert.Equal("WIT18", table.Find("White_18").DssName);
        Assert.Equal("WIT9", table.Find("White_9").DssName);
    }

    [Fact]
    public void Thickness_AcceptsACommaDecimal()
    {
        var table = MaterialTable.FromText(Header + "\nBirch_12;BIR12;12,5;0\n");

        Assert.Equal(12.5, table.Find("Birch_12").Thickness);
    }

    [Fact]
    public void ColumnsAreFoundByName_NotByPosition()
    {
        var table = MaterialTable.FromText("Grain;Thickness;DssMaterial;TopSolidMaterial\n1;19;WIT19;White_19\n");

        var material = table.Find("White_19");
        Assert.Equal("WIT19", material.DssName);
        Assert.Equal(19, material.Thickness);
        Assert.Equal(1, material.Grain);
    }

    [Theory]
    [InlineData("White_18#01;WIT18;18;0", "#")]               // sheet number does not belong in the key
    [InlineData("White_18;WIT18;18;0\nwhite_18;WIT18B;18;0", "twee keer")] // duplicate (case-insensitive)
    [InlineData("White_18;;18;0", "DssMaterial is leeg")]
    [InlineData(";WIT18;18;0", "TopSolidMaterial is leeg")]
    [InlineData("White_18;WIT18;abc;0", "Thickness")]
    [InlineData("White_18;WIT18;0;0", "Thickness")]
    [InlineData("White_18;WIT18;18;3", "Grain")]
    [InlineData("White_18;WIT18;18", "te weinig kolommen")]
    public void BadLines_AreErrorsWithTheLineNumber(string lines, string expectedInMessage)
    {
        var error = Assert.Throws<ConversionException>(() => MaterialTable.FromText(Header + "\n" + lines + "\n"));

        Assert.Contains(expectedInMessage, error.Message);
        Assert.Contains("regel", error.Message);
    }

    [Fact]
    public void MissingColumn_IsAnErrorThatNamesIt()
    {
        var error = Assert.Throws<ConversionException>(
            () => MaterialTable.FromText("TopSolidMaterial;DssMaterial;Thickness\nWhite_18;WIT18;18\n"));

        Assert.Contains("Grain", error.Message);
    }

    // ---- Encoding: "Chêne" must survive UTF-8 (with and without BOM) and Excel's ANSI "CSV" ----

    public static TheoryData<string, byte[]> EncodedFiles()
    {
        string text = Header + "\nH1145_" + Chene + ";H1145;19;1\n";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var data = new TheoryData<string, byte[]>();
        data.Add("UTF-8 without BOM", new UTF8Encoding(false).GetBytes(text));
        data.Add("UTF-8 with BOM", new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(text)).ToArray());
        data.Add("Windows-1252 (Excel CSV)", Encoding.GetEncoding(1252).GetBytes(text));
        return data;
    }

    [Theory]
    [MemberData(nameof(EncodedFiles))]
    public void Load_ReadsAccentsCorrectly_InEveryEncoding(string description, byte[] fileBytes)
    {
        using var folder = new TempFolder();
        string path = folder.File("materials.csv");
        File.WriteAllBytes(path, fileBytes);

        var table = MaterialTable.Load(path);

        Assert.True(table.TryFind("H1145_" + Chene, out var material), description);
        Assert.Equal(19, material!.Thickness);
    }

    [Fact]
    public void Load_MissingFile_IsAnError()
    {
        Assert.Throws<ConversionException>(() => MaterialTable.Load(@"C:\does\not\exist\materials.csv"));
    }
}
