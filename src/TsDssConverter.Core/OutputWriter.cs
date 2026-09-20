namespace TsDssConverter.Core;

/// <summary>A file that is ready to be written: full target path and the bytes.</summary>
public class PendingFile
{
    public string Path { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Writes the output safely:
///  - all label CSVs first, the batch XML LAST (DSSClient must never see an XML whose labels are missing);
///  - every file is first written as "name.tmp" and renamed afterwards, so nobody sees a half-written file;
///  - an existing batch XML is never overwritten (the warehouse may be running that job).
/// </summary>
public static class OutputWriter
{
    /// <summary>
    /// The checks before anything is written: both folders reachable and the batch not there yet. The converter runs
    /// this BEFORE it moves the CNC programs, so a refused job leaves the export folder untouched.
    /// </summary>
    public static void CheckTargets(string batchFolder, string labelFolder, string xmlPath, string batchName)
    {
        if (!Directory.Exists(batchFolder))
        {
            throw new ConversionException(Messages.FolderNotFound(batchFolder)) { IsTemporary = true };
        }

        if (!Directory.Exists(labelFolder))
        {
            throw new ConversionException(Messages.FolderNotFound(labelFolder)) { IsTemporary = true };
        }

        if (File.Exists(xmlPath))
        {
            throw new ConversionException(Messages.BatchAlreadyExists(batchName));
        }
    }

    public static void Write(string batchFolder, string labelFolder, List<PendingFile> labelFiles, PendingFile xmlFile, string batchName)
    {
        CheckTargets(batchFolder, labelFolder, xmlFile.Path, batchName);

        // Order matters: the XML is the last one.
        var allFiles = new List<PendingFile>(labelFiles) { xmlFile };
        var tempPaths = new List<string>();

        try
        {
            // Step 1: write everything as .tmp. If this fails, no final file exists yet.
            foreach (var file in allFiles)
            {
                string tempPath = file.Path + ".tmp";
                tempPaths.Add(tempPath);
                File.WriteAllBytes(tempPath, file.Content);
            }

            // Step 2: rename. Same order, so the XML appears last.
            foreach (var file in allFiles)
            {
                File.Move(file.Path + ".tmp", file.Path, overwrite: true);
            }
        }
        catch
        {
            // Remove leftover .tmp files (best effort), then report the original problem.
            foreach (string tempPath in tempPaths)
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
            }

            throw;
        }
    }
}
