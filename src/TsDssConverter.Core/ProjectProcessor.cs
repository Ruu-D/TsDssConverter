using System.Globalization;
using System.Text;

namespace TsDssConverter.Core;

/// <summary>What happened to one project. The tray shows this to the user (list, icon, balloon).</summary>
public class ProcessOutcome
{
    public string Project { get; set; } = "";
    public bool Success { get; set; }

    /// <summary>Success: a short summary. Failure: the reason(s), one per line.</summary>
    public string Message { get; set; } = "";

    /// <summary>Things that did not stop the conversion (for example a CNC program that is not there yet).</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// The problem is the situation, not the files (for example the Duivestein folder is not reachable).
    /// The files stay in the export folder and the conversion is tried again at the next scan.
    /// </summary>
    public bool IsTemporary { get; set; }

    /// <summary>Set by the watcher: this is the same temporary problem as last time, so the user is not told again.</summary>
    public bool IsRepeat { get; set; }

    /// <summary>The conversion succeeded but the files could not be moved yet. The watcher tries the move again.</summary>
    public bool MovePending { get; set; }

    /// <summary>The folder where the files were moved to (_Verwerkt or _Fout). Null if they were not moved.</summary>
    public string? MovedTo { get; set; }
}

/// <summary>
/// Converts ONE project and puts its files away:
///   success  -> {export}\_Verwerkt\{yyyyMMdd-HHmmss} {project}\
///   an error -> {export}\_Fout\{yyyyMMdd-HHmmss} {project}\ together with fout.txt (in the selected language)
/// The CNC programs stay in the export folder: the batch XML points to them there.
/// This class never throws: every problem ends up in the outcome.
/// </summary>
public class ProjectProcessor
{
    public const string DoneFolderName = "_Verwerkt";
    public const string ErrorFolderName = "_Fout";
    public const string ErrorFileName = "fout.txt";

    private readonly string _materialsFile;
    private readonly string? _infoColumnsFile;
    private readonly string? _positionColumnsFile;
    private readonly string? _labelColumnsFile;

    /// <param name="infoColumnsFile">columns-li.txt: the header names in the LI file. Null = the built-in names.</param>
    /// <param name="positionColumnsFile">columns-lp.txt: the header names in the LP file. Null = the built-in names.</param>
    /// <param name="labelColumnsFile">columns-label.txt: the names of the DESC columns in the label CSV. Null = DESC1 .. DESC10.</param>
    public ProjectProcessor(
        string materialsFile, string? infoColumnsFile = null, string? positionColumnsFile = null, string? labelColumnsFile = null)
    {
        _materialsFile = materialsFile;
        _infoColumnsFile = infoColumnsFile;
        _positionColumnsFile = positionColumnsFile;
        _labelColumnsFile = labelColumnsFile;
    }

    public ProcessOutcome Process(ProjectFiles files, AppSettings settings, DateTime now)
    {
        var outcome = new ProcessOutcome { Project = files.Project };

        try
        {
            if (files.InfoPath == null || files.PositionPath == null)
            {
                throw new ConversionException(Messages.FileMissingKind(files.InfoPath == null ? "LI" : "LP"));
            }

            // Without materials.csv nothing can be converted, but the files are fine: wait until it is back.
            if (!File.Exists(_materialsFile))
            {
                throw new ConversionException(Messages.FileNotFound(_materialsFile)) { IsTemporary = true };
            }

            // The column files are read again for every project, so a change is used at once, without a restart.
            // A file with a mistake throws a temporary error (handled below): the TopSolid files are fine.
            ColumnMap infoColumns = ColumnMap.LoadLabelInfo(_infoColumnsFile);
            ColumnMap positionColumns = ColumnMap.LoadLabelPosition(_positionColumnsFile);
            LabelColumnNames labelColumns = LabelColumnNames.Load(_labelColumnsFile);

            ConversionResult result = new Converter().Convert(
                files.InfoPath, files.PositionPath, _materialsFile,
                settings.BatchFolder, settings.LabelFolder, settings.ToConverterSettings(), now.Date,
                infoColumns, positionColumns, labelColumns);

            outcome.Success = true;
            outcome.Message = Messages.ConversionDone(result.LabelPaths.Count, result.PartCount, result.Warnings.Count);
            outcome.Warnings.AddRange(result.Warnings);
        }
        catch (ConversionException error) when (error.IsTemporary)
        {
            MakeTemporary(outcome, error.Message);
            return outcome;
        }
        catch (ConversionException error)
        {
            return Reject(files, settings, error.Problems, now);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // A read or write problem (network hiccup, file locked): not the fault of the files, try again later.
            MakeTemporary(outcome, error.Message);
            return outcome;
        }
        catch (Exception error)
        {
            // Something we did not expect, for example an XLSX file that is not a real XLSX file.
            // The files are the most likely cause, so they go to the error folder with the reason.
            return Reject(files, settings, new[] { Messages.UnexpectedError(error.Message) }, now);
        }

        // The batch is written. Now put the input files away, so they are not converted a second time.
        TryMove(files, settings.TopSolidExportPath, DoneFolderName, now, outcome);
        return outcome;
    }

    /// <summary>
    /// Puts the files of a project in the error folder with fout.txt. Also used for a project that waited
    /// too long for its files (see <see cref="ExportScanner"/>).
    /// </summary>
    public ProcessOutcome Reject(ProjectFiles files, AppSettings settings, IReadOnlyList<string> problems, DateTime now)
    {
        var outcome = new ProcessOutcome
        {
            Project = files.Project,
            Success = false,
            // The message of a ConversionException shows the first 25 problems and "... and N more".
            Message = new ConversionException(problems).Message,
        };

        try
        {
            string folder = MoveFiles(files, settings.TopSolidExportPath, ErrorFolderName, now);
            WriteErrorReport(folder, files.Project, problems, now);
            outcome.MovedTo = folder;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // The error folder cannot be used right now. The files stay where they are; the same problem
            // will be found again at the next scan, and the watcher only tells the user once.
            outcome.IsTemporary = true;
            outcome.Message += Environment.NewLine + Messages.MoveFailed(ErrorFolderName, error.Message);
        }

        return outcome;
    }

    private static void MakeTemporary(ProcessOutcome outcome, string reason)
    {
        outcome.Success = false;
        outcome.IsTemporary = true;
        outcome.Message = reason + " " + Messages.WillRetry;
    }

    // ------------------------------------------------------------------ moving the files

    /// <summary>
    /// Moves the files that are still in the export folder to {export}\{subFolder}\{yyyyMMdd-HHmmss} {project}\
    /// and returns that folder. Files that are already moved are skipped, so a second attempt after a failure
    /// only moves what is left. Throws IOException or UnauthorizedAccessException if it does not work.
    /// </summary>
    public static string MoveFiles(ProjectFiles files, string exportFolder, string subFolder, DateTime stamp)
    {
        string folderName = stamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + " " + files.Project;
        string target = Path.Combine(exportFolder, subFolder, folderName);
        List<string> existing = files.ExistingFiles();

        if (existing.Count == 0)
        {
            return target; // nothing left to move
        }

        Directory.CreateDirectory(target);

        foreach (string path in existing)
        {
            File.Move(path, Path.Combine(target, Path.GetFileName(path)), overwrite: true);
        }

        return target;
    }

    /// <summary>Second attempt for a conversion that worked but whose files could not be moved.</summary>
    public static string MoveProcessedFiles(ProjectFiles files, string exportFolder, DateTime stamp)
    {
        return MoveFiles(files, exportFolder, DoneFolderName, stamp);
    }

    private static void TryMove(ProjectFiles files, string exportFolder, string subFolder, DateTime now, ProcessOutcome outcome)
    {
        try
        {
            outcome.MovedTo = MoveFiles(files, exportFolder, subFolder, now);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // The batch is written, so this is still a success. Say so, and let the watcher try again later.
            outcome.MovePending = true;
            outcome.Warnings.Add(Messages.MoveFailed(subFolder, error.Message));
        }
    }

    // ------------------------------------------------------------------ fout.txt

    /// <summary>Writes fout.txt in the error folder: what is wrong (ALL problems) and what to do about it.</summary>
    private static void WriteErrorReport(string folder, string project, IReadOnlyList<string> problems, DateTime now)
    {
        var text = new StringBuilder();
        text.AppendLine(Messages.ErrorReportTitle);
        text.AppendLine(Messages.ErrorReportProject(project));
        text.AppendLine(Messages.ErrorReportTime(now));
        text.AppendLine();
        text.AppendLine(Messages.ErrorReportProblems);

        foreach (string problem in problems)
        {
            text.AppendLine("- " + problem);
        }

        text.AppendLine();
        text.AppendLine(Messages.ErrorReportHint);

        // With a byte order mark, so an old Notepad also shows the accents of French text correctly.
        // AppendLine uses the line ending of Windows (CRLF).
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ErrorFileName), text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
