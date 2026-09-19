using TsDssConverter.Core;

namespace TsDssConverter.Tests;

public class ConversionExceptionTests
{
    [Fact]
    public void SingleMessage_IsTheOnlyProblem()
    {
        var error = new ConversionException("Iets is fout.");

        Assert.Equal("Iets is fout.", error.Message);
        Assert.Equal(new[] { "Iets is fout." }, error.Problems);
    }

    [Fact]
    public void SeveralProblems_AreOneLineEachInTheMessage()
    {
        var error = new ConversionException(new List<string> { "Eerste.", "Tweede." });

        Assert.Equal("Eerste." + Environment.NewLine + "Tweede.", error.Message);
        Assert.Equal(2, error.Problems.Count);
    }

    [Fact]
    public void AVeryLongList_IsCutOffInTheMessage_ButKeptCompleteInProblems()
    {
        // A wrong pair of files can give hundreds of unmatched parts. The message must stay readable.
        var problems = Enumerable.Range(1, 40).Select(i => "Probleem " + i).ToList();

        var error = new ConversionException(problems);

        string[] lines = error.Message.Split(Environment.NewLine);
        Assert.Equal(26, lines.Length);                    // 25 problems + 1 summary line
        Assert.Equal("Probleem 25", lines[24]);
        Assert.Contains("15 andere problemen", lines[25]);
        Assert.Equal(40, error.Problems.Count);            // nothing is lost
    }
}
