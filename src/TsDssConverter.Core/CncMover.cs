namespace TsDssConverter.Core;

/// <summary>
/// Moves the CNC programs of one job out of the export folder into their own folder,
/// {export}\CNC\{project}\ (see <see cref="CncPathBuilder"/> for why). Nothing is ever deleted or copied twice:
/// a program that is already in the project folder (an earlier attempt that stopped halfway) is simply left there.
///
/// TopSolid writes the programs in {export}\{project}\ (since 1.1.2); older exports wrote them directly in {export}\.
/// The first place wins; the second is only looked at when the program is not in the first.
/// </summary>
public static class CncMover
{
    /// <summary>
    /// TR is written after the CNC programs, so a program that is clearly NEWER than TR was overwritten by a later export
    /// of another job. A few seconds of difference are normal (clock of the file system), more is not.
    /// Only checked for programs directly in the export folder: in the folder of the project no other job writes.
    /// </summary>
    public static readonly TimeSpan NewerThanTriggerTolerance = TimeSpan.FromSeconds(5);

    /// <param name="exportFinishedAt">
    /// When TopSolid finished this export (the time of the TR file, UTC). Null = unknown, then that check is skipped.
    /// </param>
    /// <returns>Warnings: programs that are not there (yet), or one warning when the export folder is not reachable.</returns>
    /// <exception cref="ConversionException">A program was overwritten by a later export. Nothing has been moved then.</exception>
    public static List<string> MoveToProjectFolder(Batch batch, ConverterSettings settings, DateTime? exportFinishedAt)
    {
        var warnings = new List<string>();

        if (!Directory.Exists(settings.TopSolidExportPath))
        {
            // One warning is clearer than one per sheet.
            warnings.Add(Messages.CncFolderNotReachable(settings.TopSolidExportPath));
            return warnings;
        }

        var toMove = new List<(string Source, string Target)>();
        var problems = new List<string>();

        // Step 1: look at everything and change nothing, so an error leaves the export folder as it was.
        foreach (var plan in batch.Plans)
        {
            foreach (var sheet in plan.Sheets)
            {
                string source = CncPathBuilder.BuildSourcePath(settings, batch.Name, sheet.Name);
                string oldSource = CncPathBuilder.BuildOldSourcePath(settings, sheet.Name);
                string target = CncPathBuilder.BuildLocalPath(settings, batch.Name, sheet.Name);

                if (File.Exists(source))
                {
                    toMove.Add((source, target));
                }
                else if (File.Exists(oldSource))
                {
                    if (exportFinishedAt != null && File.GetLastWriteTimeUtc(oldSource) > exportFinishedAt.Value + NewerThanTriggerTolerance)
                    {
                        problems.Add(Messages.CncOverwrittenByLaterExport(oldSource));
                    }
                    else
                    {
                        toMove.Add((oldSource, target));
                    }
                }
                else if (!File.Exists(target))
                {
                    // Name the place where TopSolid writes it now (the folder of the project).
                    warnings.Add(Messages.CncFileNotFound(source));
                }
                // else: already in the folder of the project (an earlier attempt), nothing to do.
            }
        }

        if (problems.Count > 0)
        {
            throw new ConversionException(problems);
        }

        // Step 2: move. A failure here (IOException) is a temporary problem; a second attempt moves what is left.
        foreach (var (source, target) in toMove)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target, overwrite: true);
        }

        RemoveSourceFolderIfEmpty(CncPathBuilder.BuildSourceFolder(settings, batch.Name));

        return warnings;
    }

    /// <summary>
    /// The folder TopSolid made for the project is removed when all its programs are moved, so the export folder only holds
    /// what is not converted yet. Only when it is really empty: anything else in it stays. Never a reason to fail the job.
    /// </summary>
    private static void RemoveSourceFolderIfEmpty(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder, recursive: false);
            }
        }
        catch (IOException)
        {
            // Left for next time: it is only tidying up.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
