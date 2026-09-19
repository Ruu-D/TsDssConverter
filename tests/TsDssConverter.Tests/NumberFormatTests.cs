using System.Globalization;
using TsDssConverter.Core;

namespace TsDssConverter.Tests;

public class NumberFormatTests
{
    [Theory]
    [InlineData(660.0, "660")]        // trailing ",0" is dropped
    [InlineData(3050.0, "3050")]
    [InlineData(568.5, "568,5")]      // comma as decimal separator
    [InlineData(1258.1, "1258,1")]
    [InlineData(674.5, "674,5")]
    [InlineData(1234567.0, "1234567")] // no thousands separator
    [InlineData(0.0, "0")]
    public void ToDss_WritesCommaDecimalsWithoutTrailingZero(double value, string expected)
    {
        Assert.Equal(expected, NumberFormat.ToDss(value));
    }

    [Fact]
    public void ToDss_NegativeZeroIsWrittenAsZero()
    {
        // A tiny negative number that rounds to zero: .NET would write "-0".
        Assert.Equal("0", NumberFormat.ToDss(-0.0));
    }

    [Fact]
    public void TryParseInvariant_ReadsDotDecimals()
    {
        Assert.True(NumberFormat.TryParseInvariant("2055.9699999999998", out double value));
        Assert.Equal(2055.9699999999998, value);

        Assert.True(NumberFormat.TryParseInvariant(" 660.0 ", out value));
        Assert.Equal(660.0, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12,5,3")]
    public void TryParseInvariant_RejectsNonsense(string text)
    {
        Assert.False(NumberFormat.TryParseInvariant(text, out _));
    }

    [Theory]
    [InlineData("12,5", 12.5)]   // typed in Belgian Excel
    [InlineData("12.5", 12.5)]
    [InlineData("18", 18.0)]
    public void TryParseLenient_AcceptsCommaAndDot(string text, double expected)
    {
        Assert.True(NumberFormat.TryParseLenient(text, out double value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("nl-BE")] // Belgian PC: comma decimals
    [InlineData("en-US")] // dot decimals
    [InlineData("de-DE")]
    public void Results_DoNotDependOnThePcCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            Assert.True(NumberFormat.TryParseInvariant("568.5", out double value));
            Assert.Equal(568.5, value);
            Assert.Equal("568,5", NumberFormat.ToDss(568.5));
            Assert.Equal("568.5", NumberFormat.ToInvariantText(568.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
