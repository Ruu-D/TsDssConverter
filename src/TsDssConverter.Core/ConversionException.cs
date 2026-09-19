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

    /// <summary>
    /// True when the problem is not the fault of the input files but of the situation (a network folder that is
    /// not reachable right now). The files then stay where they are and the conversion is tried again later,
    /// instead of moving the files to the error folder.
    /// </summary>
    public bool IsTemporary { get; init; }

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
