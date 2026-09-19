using TsDssConverter.Core;

namespace TsDssConverter.Tests;

public class LabelPositionCalculatorTests
{
    // Part "Verschuren - K2 - Zijkant links - 13099" on sheet White_18#01 (3050 x 1300).
    private const double SheetLength = 3050;
    private const double SheetWidth = 1300;
    private const double LabelX = 2055.9699999999998;
    private const double LabelY = 289.24;

    private static LabelPosition Calculate(double x, double y, double angle, bool flipX = false, bool flipY = false)
    {
        return LabelPositionCalculator.Calculate(SheetLength, SheetWidth, x, y, angle, flipX, flipY, "test part");
    }

    [Fact]
    public void NoFlip_RoundsToWholeMillimetres()
    {
        var position = Calculate(LabelX, LabelY, 180);

        Assert.Equal(2056, position.X);
        Assert.Equal(289, position.Y);
        Assert.Equal(180, position.Rotation);
    }

    [Fact]
    public void FlipX_UsesSheetLengthMinusX()
    {
        var position = Calculate(LabelX, LabelY, 180, flipX: true);

        Assert.Equal(994, position.X);  // 3050 - 2055.97 = 994.03
        Assert.Equal(289, position.Y);  // Y is not flipped
    }

    [Fact]
    public void FlipY_UsesSheetWidthMinusY()
    {
        var position = Calculate(LabelX, LabelY, 180, flipY: true);

        Assert.Equal(2056, position.X); // X is not flipped
        Assert.Equal(1011, position.Y); // 1300 - 289.24 = 1010.76
    }

    [Fact]
    public void FlipDoesNotChangeTheRotation_ForNow()
    {
        // Open point 11: decided after the physical test sheet. If this test fails, that decision was made.
        Assert.Equal(90, Calculate(LabelX, LabelY, 90, flipX: true, flipY: true).Rotation);
    }

    [Fact]
    public void Rounding_IsHalfAwayFromZero()
    {
        // "Banker's rounding" would give 288 and 0 here.
        Assert.Equal(289, Calculate(100, 288.5, 0).Y);
        Assert.Equal(1, Calculate(0.5, 100, 0).X);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(270, 270)]
    [InlineData(360, 0)]    // TopSolid writes 360 for 0
    [InlineData(450, 90)]
    [InlineData(-90, 270)]
    [InlineData(90.0000001, 90)] // floating-point noise
    public void Angle_IsNormalisedToZeroNinetyOneEightyTwoSeventy(double angle, int expected)
    {
        Assert.Equal(expected, Calculate(100, 100, angle).Rotation);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(91)]
    [InlineData(180.5)]
    public void Angle_NotAMultipleOf90_IsAnError(double angle)
    {
        var error = Assert.Throws<ConversionException>(() => Calculate(100, 100, angle));
        Assert.Contains("veelvoud van 90", error.Message);
        Assert.Contains("test part", error.Message);
    }
}
