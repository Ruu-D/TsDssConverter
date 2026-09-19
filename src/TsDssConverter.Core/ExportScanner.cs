namespace TsDssConverter.Core;

/// <summary>The XLSX files of ONE TopSolid project in the export folder. A path is null if that file is not there.</summary>
public class ProjectFiles
{
    public string Project { get; set; } = "";
    public string? InfoPath { get; set; }      // {project}-LI.xlsx
    public string? PositionPath { get; set; }  // {project}-LP.xlsx
    public string? TriggerPath { get; set; }   // {project}-TR.xlsx

    /// <summary>The files that exist right now (what is moved to _Verwerkt or _Fout).</summary>
    public List<string> ExistingFiles()
    {
        var files = new List<string>();

        foreach (string? path in new[] { InfoPath, PositionPath, TriggerPath })
        {
            if (path != null && File.Exists(path))
            {
                files.Add(path);
            }
        }

        return files;
    }
}

/// <summary>A project that waited too long for its files: the reason in plain language.</summary>
public class GaveUpProject
{
    public ProjectFiles Files { get; set; } = new();
    public string Reason { get; set; } = "";
}

/// <summary>What one scan of the export folder found.</summary>
public class ScanResult
{
    /// <summary>Projects whose files are complete and can be converted now.</summary>
    public List<ProjectFiles> Ready { get; } = new();

    /// <summary>Projects that gave up waiting for a missing or locked file (they go to the error folder).</summary>
    public List<GaveUpProject> GaveUp { get; } = new();

    /// <summary>True if some project is still waiting: the caller should scan again soon (every 2 seconds).</summary>
    public bool IsWaiting { get; set; }

    /// <summary>Not null if the export folder itself cannot be read (for example the Z: drive is not there).</summary>
    public string? FolderProblem { get; set; }
}

/// <summary>
/// Looks in the export folder and decides which projects are ready to be converted. It has no threads and
/// no events: the caller asks "what is ready now?" and this class answers. That keeps it easy to test.
///
/// Normal mode (RequireTriggerFile = true): a project is ready when {project}-TR.xlsx exists (TopSolid writes it
/// as the very last file) and LI, LP and TR can all be opened. If that is not the case after 2 minutes: give up.
///
/// Without a trigger file: a project is ready when LI and LP exist, can be opened and have not changed for 10 seconds.
/// </summary>
public class ExportScanner
{
    /// <summary>How long to wait for a TR file's companions before giving up.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(2);

    /// <summary>Without trigger file: how long LI and LP must stay unchanged.</summary>
    public static readonly TimeSpan QuietTime = TimeSpan.FromSeconds(10);

    private readonly Func<DateTime> _now;

    // Trigger mode: since when is this project waiting? (key = project name)
    private readonly Dictionary<string, DateTime> _waitingSince = new(StringComparer.OrdinalIgnoreCase);

    // Mode without trigger: what did each file look like the last time, and since when is it like that?
    private Dictionary<string, FileState> _fileStates = new(StringComparer.OrdinalIgnoreCase);

    private readonly record struct FileState(DateTime LastWriteUtc, long Length, DateTime Since);

    /// <param name="now">Only for tests: gives the "current" time, so waiting does not take real minutes.</param>
    public ExportScanner(Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.Now);
    }

    public ScanResult Scan(string exportFolder, bool requireTriggerFile)
    {
        var result = new ScanResult();
        Dictionary<string, ProjectFiles> projects;

        try
        {
            projects = FindProjects(exportFolder);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // The folder is not there or not readable (network drive gone). Not a crash, just a message.
            result.FolderProblem = Messages.ExportFolderNotReachable(exportFolder);
            _waitingSince.Clear();
            _fileStates.Clear();
            return result;
        }

        if (requireTriggerFile)
        {
            ScanWithTrigger(projects, result);
        }
        else
        {
            ScanWithoutTrigger(projects, result);
        }

        return result;
    }

    // ------------------------------------------------------------------ finding the files

    /// <summary>Groups the LI, LP and TR files of the top level of the folder by project name.</summary>
    private static Dictionary<string, ProjectFiles> FindProjects(string exportFolder)
    {
        var projects = new Dictionary<string, ProjectFiles>(StringComparer.OrdinalIgnoreCase);

        // Only the top level: the _Verwerkt and _Fout folders below it must not be scanned again.
        foreach (string path in Directory.EnumerateFiles(exportFolder, "*.xlsx", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(path);

            // Excel makes "~$name.xlsx" while a file is open in Excel. That is not a real file.
            if (fileName.StartsWith("~$") || !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(fileName);
            string? suffix = FindSuffix(name);

            if (suffix == null)
            {
                continue; // some other xlsx file: not ours
            }

            string project = name.Substring(0, name.Length - suffix.Length - 1);
            if (!projects.TryGetValue(project, out ProjectFiles? files))
            {
                files = new ProjectFiles { Project = project };
                projects[project] = files;
            }

            if (suffix == "LI") files.InfoPath = path;
            else if (suffix == "LP") files.PositionPath = path;
            else files.TriggerPath = path;
        }

        return projects;
    }

    /// <summary>"Verschuren-P-20-LI" gives "LI". Anything that does not end in -LI, -LP or -TR gives null.</summary>
    private static string? FindSuffix(string nameWithoutExtension)
    {
        foreach (string suffix in new[] { "LI", "LP", "TR" })
        {
            bool endsWith = nameWithoutExtension.EndsWith("-" + suffix, StringComparison.OrdinalIgnoreCase);

            if (endsWith && nameWithoutExtension.Length > suffix.Length + 1) // there must be a project name in front
            {
                return suffix;
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ with the trigger file

    private void ScanWithTrigger(Dictionary<string, ProjectFiles> projects, ScanResult result)
    {
        DateTime now = _now();
        var stillWaiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ProjectFiles files in projects.Values)
        {
            if (files.TriggerPath == null)
            {
                continue; // TopSolid is not finished (or these are old LI/LP files): wait for the TR file
            }

            List<string> problems = FindProblems(files);

            if (problems.Count == 0)
            {
                result.Ready.Add(files);
                continue;
            }

            // Not complete yet. Wait, and give up after 2 minutes (counted from the first time we saw the TR file).
            if (!_waitingSince.TryGetValue(files.Project, out DateTime since))
            {
                since = now;
                _waitingSince[files.Project] = since;
            }

            if (now - since >= GiveUpAfter)
            {
                result.GaveUp.Add(new GaveUpProject
                {
                    Files = files,
                    Reason = Messages.GaveUpWaiting((int)GiveUpAfter.TotalMinutes, string.Join("; ", problems)),
                });
            }
            else
            {
                stillWaiting.Add(files.Project);
                result.IsWaiting = true;
            }
        }

        // Forget projects that are ready, given up or gone.
        foreach (string project in _waitingSince.Keys.Where(p => !stillWaiting.Contains(p)).ToList())
        {
            _waitingSince.Remove(project);
        }

        _fileStates.Clear();
    }

    /// <summary>What is still wrong with the files of a project: a missing LI or LP, or a file that is locked.</summary>
    private static List<string> FindProblems(ProjectFiles files)
    {
        var problems = new List<string>();

        if (files.InfoPath == null) problems.Add(Messages.FileMissingKind("LI"));
        if (files.PositionPath == null) problems.Add(Messages.FileMissingKind("LP"));

        foreach (string? path in new[] { files.InfoPath, files.PositionPath, files.TriggerPath })
        {
            if (path != null && !CanOpen(path))
            {
                problems.Add(Messages.FileInUse(Path.GetFileName(path)));
            }
        }

        return problems;
    }

    /// <summary>
    /// True if nobody else has the file open. TopSolid keeps a file open while it writes it, and moving it
    /// (or converting it while it is still growing) would go wrong. FileShare.None asks for exclusive access.
    /// </summary>
    private static bool CanOpen(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            return false; // locked, or removed in the meantime: try again later
        }
    }

    // ------------------------------------------------------------------ without the trigger file

    private void ScanWithoutTrigger(Dictionary<string, ProjectFiles> projects, ScanResult result)
    {
        DateTime now = _now();
        var newStates = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
        _waitingSince.Clear();

        foreach (ProjectFiles files in projects.Values)
        {
            if (files.InfoPath == null || files.PositionPath == null)
            {
                continue; // the other file is not there (yet): the folder watcher wakes us when it appears
            }

            bool quiet = true;
            foreach (string path in new[] { files.InfoPath, files.PositionPath })
            {
                quiet &= IsQuiet(path, now, newStates);
            }

            if (quiet && FindProblems(files).Count == 0)
            {
                result.Ready.Add(files);
            }
            else
            {
                result.IsWaiting = true;
            }
        }

        _fileStates = newStates;
    }

    /// <summary>True if the file has looked the same (date and size) for at least <see cref="QuietTime"/>.</summary>
    private bool IsQuiet(string path, DateTime now, Dictionary<string, FileState> newStates)
    {
        try
        {
            var info = new FileInfo(path);
            var state = new FileState(info.LastWriteTimeUtc, info.Length, now);

            if (_fileStates.TryGetValue(path, out FileState before) && before.LastWriteUtc == state.LastWriteUtc && before.Length == state.Length)
            {
                state = before; // unchanged: keep the moment we first saw it like this
            }

            newStates[path] = state;
            return now - state.Since >= QuietTime;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            return false;
        }
    }
}
