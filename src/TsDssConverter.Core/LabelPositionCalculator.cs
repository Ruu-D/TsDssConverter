namespace TsDssConverter.Core;

/// <summary>Label position as Duivestein wants it: whole millimetres and a rotation of 0, 90, 180 or 270.</summary>
public class LabelPosition
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Rotation { get; set; }
}

/// <summary>Turns the TopSolid label position (LABEL_X, LABEL_Y, LABEL_ANGLE) into the Duivestein position.</summary>
public static class LabelPositionCalculator
{
    /// <param name="part">Only used in the error message.</param>
    public static LabelPosition Calculate(
        double sheetLength, double sheetWidth,
        double labelX, double labelY, double labelAngle,
        bool flipX, bool flipY, string part)
    {
        double x = labelX;
        double y = labelY;

        // Zero-point flips are applied BEFORE rounding.
        if (flipX)
        {
            x = sheetLength - x;
        }

        if (flipY)
        {
            y = sheetWidth - y;
        }

        var position = new LabelPosition
        {
            X = RoundToMillimetre(x),
            Y = RoundToMillimetre(y),
            Rotation = NormaliseAngle(labelAngle, part),
        };

        // ------------------------------------------------------------------------------------------
        // ROTATION AND FLIPS - decided after the physical test sheet (open point 11).
        // For now a flip does NOT change the rotation. If it must change (for example 90 <-> 270
        // when flipping in X), this is the ONE place to do it.
        // ------------------------------------------------------------------------------------------

        return position;
    }

    /// <summary>Rounds half away from zero: 288.5 gives 289 (not the "banker's rounding" 288).</summary>
    private static int RoundToMillimetre(double value)
    {
        return (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>0, 90, 180, 270 stay; 360 becomes 0; -90 becomes 270. Anything else is an error.</summary>
    private static int NormaliseAngle(double angle, string part)
    {
        double rounded = Math.Round(angle);

        // Allow a little floating-point noise, but not 45 degrees.
        bool isWholeNumber = Math.Abs(angle - rounded) < 0.001;
        if (!isWholeNumber || (long)rounded % 90 != 0)
        {
            throw new ConversionException(Messages.AngleNotMultipleOf90(angle, part));
        }

        int degrees = (int)(((long)rounded % 360 + 360) % 360);
        return degrees;
    }
}
