using TsDssConverter.Core;

namespace TsDssConverter.Tests;

public class TopSolidTextTests
{
    [Theory]
    [InlineData("L: 660.0mm X B: 590.0mm", 660, 590)]
    [InlineData("L: 734.0mm X B: 568.5mm", 734, 568.5)]
    [InlineData("L: 1708.0mm X B: 590.9mm", 1708, 590.9)]
    [InlineData("L: 100mm X B: 200mm", 100, 200)]           // decimals are optional
    [InlineData("  L: 543.0mm X B: 118.5mm ", 543, 118.5)]  // spaces around
    public void ParseDimensions_ReadsLengthAndWidth(string text, double length, double width)
    {
        TopSolidText.ParseDimensions(text, "test part", out double actualLength, out double actualWidth);

        Assert.Equal(length, actualLength);
        Assert.Equal(width, actualWidth);
    }

    [Fact]
    public void ParseDimensions_NeverSwapsLengthAndWidth()
    {
        // L is smaller than B here and must stay that way.
        TopSolidText.ParseDimensions("L: 100.0mm X B: 900.0mm", "test part", out double length, out double width);

        Assert.Equal(100, length);
        Assert.Equal(900, width);
    }

    [Theory]
    [InlineData("")]
    [InlineData("660 x 590")]
    [InlineData("L: abcmm X B: 590.0mm")]
    [InlineData("L: 660,0mm X B: 590,0mm")] // comma decimals are not TopSolid format: better an error than a wrong size
    public void ParseDimensions_UnreadableText_IsAnErrorThatNamesThePart(string text)
    {
        var error = Assert.Throws<ConversionException>(
            () => TopSolidText.ParseDimensions(text, "White_18#01 / Front - 1", out _, out _));

        Assert.Contains("Afmetingen niet leesbaar", error.Message);
        Assert.Contains("White_18#01 / Front - 1", error.Message);
    }

    [Theory]
    [InlineData("Verschuren - K2 - Front - 19587", "19587")]
    [InlineData("Verschuren - K2 - Zijkant links - 13099", "13099")]
    [InlineData("Verschuren - K2 - Front - 19587 ", "19587")]
    public void GetPartId_IsTheTrailingNumber(string description, string expected)
    {
        Assert.Equal(expected, TopSolidText.GetPartId(description));
    }

    [Fact]
    public void GetPartId_WithoutTrailingNumber_IsAnError()
    {
        Assert.Throws<ConversionException>(() => TopSolidText.GetPartId("Verschuren - K2 - Front"));
    }

    [Theory]
    [InlineData("White_18#01", "White_18", 1)]
    [InlineData("White_18#12", "White_18", 12)]
    [InlineData("H1145_-_ST10_-_Chêne_Bardolino_naturel_Zijdewit_19#02", "H1145_-_ST10_-_Chêne_Bardolino_naturel_Zijdewit_19", 2)]
    public void TryParseSheetName_SplitsMaterialAndNumber(string sheetName, string material, int number)
    {
        Assert.True(TopSolidText.TryParseSheetName(sheetName, out string actualMaterial, out int actualNumber));
        Assert.Equal(material, actualMaterial);
        Assert.Equal(number, actualNumber);
    }

    [Theory]
    [InlineData("White_18")]     // no sheet number
    [InlineData("#01")]          // no material
    [InlineData("White_18#x")]   // number is not a number
    [InlineData("")]
    public void TryParseSheetName_WrongForm_ReturnsFalse(string sheetName)
    {
        Assert.False(TopSolidText.TryParseSheetName(sheetName, out _, out _));
    }
}
