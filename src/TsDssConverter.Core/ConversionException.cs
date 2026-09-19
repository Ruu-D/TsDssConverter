namespace TsDssConverter.Core;

/// <summary>
/// A problem with the input that the user can understand and fix (missing column, unknown material, ...).
/// It can hold several problems at once, so the user sees everything that is wrong in one go.
/// The message is plain Dutch and is meant to be shown as-is (later: written to fout.txt).
/// </summary>
public class ConversionException : Exception
{
    // A wrong file pair can give hundreds of problems. The message shows the first ones only;
    // Problems always holds all of them.
    private const int MaxLinesInMessage = 25;

    /// <summary>Every problem, one text each.</summary>
    public IReadOnlyList<string> Problems { get; }

    public ConversionException(string message) : base(message)
    {
        Problems = new[] { message };
    }

    public ConversionException(IReadOnlyList<string> problems) : base(BuildMessage(problems))
    {
        Problems = problems;
    }

    private static string BuildMessage(IReadOnlyList<string> problems)
    {
        var lines = problems.Take(MaxLinesInMessage).ToList();
        if (problems.Count > MaxLinesInMessage)
        {
            lines.Add(Messages.AndMoreProblems(problems.Count - MaxLinesInMessage));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
