using TsDssConverter.Tray;

namespace TsDssConverter.Tests;

/// <summary>
/// The mapping between the reference-corner drop-down (DSSClient's "Oriëntatie") and the FlipX / FlipY booleans
/// that AppSettings actually stores. Pure logic, no window needed.
/// </summary>
public class ReferenceCornerTests
{
    [Theory]
    [InlineData(ReferenceCorner.BottomLeft, false, false)]  // TopSolid's own default: no flip needed
    [InlineData(ReferenceCorner.TopLeft, false, true)]
    [InlineData(ReferenceCorner.BottomRight, true, false)]
    [InlineData(ReferenceCorner.TopRight, true, true)]
    public void ToFlips_GivesTheExpectedFlipXAndFlipY(ReferenceCorner corner, bool expectedFlipX, bool expectedFlipY)
    {
        var (flipX, flipY) = ReferenceCornerMapping.ToFlips(corner);

        Assert.Equal(expectedFlipX, flipX);
        Assert.Equal(expectedFlipY, flipY);
    }

    [Theory]
    [InlineData(false, false, ReferenceCorner.BottomLeft)]
    [InlineData(false, true, ReferenceCorner.TopLeft)]
    [InlineData(true, false, ReferenceCorner.BottomRight)]
    [InlineData(true, true, ReferenceCorner.TopRight)]
    public void FromFlips_IsTheInverseOfToFlips(bool flipX, bool flipY, ReferenceCorner expected)
    {
        Assert.Equal(expected, ReferenceCornerMapping.FromFlips(flipX, flipY));
    }

    [Fact]
    public void EveryCorner_RoundTripsThroughFlipsAndBack()
    {
        foreach (ReferenceCorner corner in Enum.GetValues<ReferenceCorner>())
        {
            var (flipX, flipY) = ReferenceCornerMapping.ToFlips(corner);
            Assert.Equal(corner, ReferenceCornerMapping.FromFlips(flipX, flipY));
        }
    }
}
