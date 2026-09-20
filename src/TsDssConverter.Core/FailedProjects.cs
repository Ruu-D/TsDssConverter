using System.Text.RegularExpressions;

namespace TsDssConverter.Core;

/// <summary>What "Retry failed" did. The tray turns this into a log line and, if needed, a message for the user.</summary>
public class RetryResult
{
    /// <summary>Projects whose LI/LP/TR files are back in the export folder. The next scan converts them again.</summary>
    public List<string> MovedBack { get; } = new();

    /// <summary>
    /// Projects that were left alone: the export folder already has a file with the same name (for example a new export
    /// of the same project). Nothing is overwritten; the user has to decide which export is the right one.
    /// </summary>
    public List<string> Blocked { get; } = new();

    /// <summary>Not null if the export folder (or a file in it) could not be used: the reason.</summary>
    public string? Problem { get; set; }
}

/// <summary>
/// "Retry failed": puts the files of the failed projects back in the export folder, so they are converted again.
///
/// A failed project sits in {export}\_Fout\{yyyyMMdd-HHmmss} {project}\ with LI, LP, TR and fout.txt (_Erreur / _Error in
/// French / English; all three names are searched). The user fixes the cause (for example adds a material to
/// materials.csv) and presses the button. Moving the files back is the same thing the user could do by hand, and it starts
/// a new attempt at the next scan.
///
/// Rules: the TR file goes back LAST (the scanner starts on the TR file and must find LI and LP already there);
/// nothing in the export folder is ever overwritten; fout.txt (our own report, the reason is in the log too) and the
/// then empty job folder are removed, so the same job is not offered twice.
/// The CNC programs of a failed job never left the export folder, so they need no moving.
/// </summary>
public static class FailedProjects
{
    // "20260920-143200 DAAN_ROGIERS-P2026.09": the time stamp, one space, the project name.
    private static readonly Regex JobFolderName = new(@"^\d{8}-\d{6} (?<project>.+)$");

    /// <summary>Never throws: a problem ends up in <see cref="RetryResult.Problem"/>.</summary>
    public static RetryResult RetryAll(string exportFolder)
    {
        var result = new RetryResult();

        try
        {
            if (!Directory.Exists(exportFolder))
            {
                result.Problem = Messages.ExportFolderNotReachable(exportFolder);
                return result;
            }

            foreach (string errorFolderName in Messages.ErrorFolderNamesInAllLanguages)
            {
                string errorFolder = Path.Combine(exportFolder, errorFolderName);
                if (!Directory.Exists(errorFolder))
                {
                    continue;
                }

                // The folder name starts with the time stamp, so this is oldest first.
                foreach (string jobFolder in Directory.GetDirectories(errorFolder).OrderBy(path => path, StringComparer.Ordinal))
                {
                    RetryJob(jobFolder, exportFolder, result);
                }
            }
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            result.Problem = error.Message;
        }

        return result;
    }

    private static void RetryJob(string jobFolder, string exportFolder, RetryResult result)
    {
        Match match = JobFolderName.Match(Path.GetFileName(jobFolder));
        if (!match.Success)
        {
            return; // not a folder that we made
        }

        string project = match.Groups["project"].Value;

        // The TR file last: the scanner starts on TR, and LI and LP must be there by then.
        List<string> files = Directory.GetFiles(jobFolder, "*.xlsx")
            .OrderBy(path => path.EndsWith("-TR.xlsx", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToList();

        if (files.Count == 0)
        {
            return; // nothing to retry (an earlier retry already took the files)
        }

        // Check first, move afterwards: never overwrite what TopSolid (or the user) put in the export folder.
        if (files.Any(path => File.Exists(Path.Combine(exportFolder, Path.GetFileName(path)))))
        {
            result.Blocked.Add(project);
            return;
        }

        MoveAllOrNone(files, exportFolder);

        string report = Path.Combine(jobFolder, ProjectProcessor.ErrorFileName);
        if (File.Exists(report))
        {
            File.Delete(report);
        }

        if (Directory.GetFileSystemEntries(jobFolder).Length == 0)
        {
            Directory.Delete(jobFolder); // only when empty: anything the user put there stays
        }

        result.MovedBack.Add(project);
    }

    /// <summary>
    /// Moves the files to the export folder. If one of them fails, the ones that were already moved go back to where they
    /// were, so a half-moved project (LI back, LP not) can never happen. The original problem is thrown again.
    /// </summary>
    private static void MoveAllOrNone(List<string> files, string exportFolder)
    {
        var moved = new List<(string From, string To)>();

        try
        {
            foreach (string source in files)
            {
                string target = Path.Combine(exportFolder, Path.GetFileName(source));
                File.Move(source, target);
                moved.Add((source, target));
            }
        }
        catch
        {
            foreach (var (from, to) in moved)
            {
                try
                {
                    File.Move(to, from);
                }
                catch (Exception undoError) when (undoError is IOException || undoError is UnauthorizedAccessException)
                {
                    // Best effort: the original problem below is the one to report.
                }
            }

            throw;
        }
    }
}
