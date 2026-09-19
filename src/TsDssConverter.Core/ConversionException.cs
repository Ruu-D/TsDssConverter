namespace TsDssConverter.Core;

/// <summary>
/// A problem with the input that the user can understand and fix (missing column, unknown material, ...).
/// The message is plain Dutch and is meant to be shown as-is (later: written to fout.txt).
/// </summary>
public class ConversionException : Exception
{
    public ConversionException(string message) : base(message)
    {
    }
}
