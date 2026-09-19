namespace TsDssConverter.Core;

/// <summary>
/// Watches the TopSolid export folder and converts every project that is complete.
///
/// How it works, in plain words:
///  - ONE background thread does everything, one conversion at a time (that is the "queue").
///  - The thread scans the folder at the start, then every RescanSeconds, and also right away when the
///    FileSystemWatcher says that an xlsx file appeared or changed. The watcher alone is not enough: on network
///    drives it can miss events, so the regular rescan is the safety net.
///  - While a project is still waiting for its files (TR file seen, LI/LP still locked or missing) it scans every 2 seconds.
///  - It never throws: a folder that is not reachable is only a problem that is reported once, and it recovers by itself.
///
/// The tray app listens to the events and shows them (icon, balloon, list). This class knows nothing about windows.
/// </summary>
public class ExportWatcher : IDisposable
{
    /// <summary>How often to scan while a project is waiting for its files.</summary>
    public static readonly TimeSpan WaitingInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// A folder that is not reachable is only reported to the user after this long. Just after the PC started,
    /// the network drive is often not connected yet; that should not give a red icon and a balloon.
    /// </summary>
    public static readonly TimeSpan FolderProblemGrace = TimeSpan.FromSeconds(60);

    private readonly Func<AppSettings> _getSettings;
    private readonly Func<bool> _isPaused;
    private readonly LogWriter _log;
    private readonly Func<DateTime> _now;
    private readonly ExportScanner _scanner;
    private readonly ProjectProcessor _processor;

    private readonly AutoResetEvent _wakeUp = new(initialState: false);
    private Thread? _thread;
    private volatile bool _stopping;
    private volatile bool _scanRequested;   // the user chose "Nu scannen"
    private volatile bool _watcherBroken;   // the FileSystemWatcher reported an error: build a new one

    private FileSystemWatcher? _fileWatcher;
    private string _watchedFolder = "";
    private bool _isWaiting;

    // The export folder is not reachable: since when, and did we tell the user?
    private DateTime? _folderProblemSince;
    private bool _folderProblemReported;

    // Temporary problems per project, so the user hears about the same problem only once.
    private readonly Dictionary<string, string> _lastTemporaryMessage = new(StringComparer.OrdinalIgnoreCase);

    // Conversions that worked but whose files could not be moved yet.
    private readonly Dictionary<string, PendingMove> _pendingMoves = new(StringComparer.OrdinalIgnoreCase);

    private class PendingMove
    {
        public ProjectFiles Files { get; init; } = new();
        public string ExportFolder { get; init; } = "";
        public DateTime Stamp { get; init; }
    }

    /// <summary>A conversion is about to start (the tray shows the "busy" icon). Comes from the watcher's thread.</summary>
    public event Action<string>? ConversionStarted;

    /// <summary>A project is finished, well or not. Comes from the watcher's thread.</summary>
    public event Action<ProcessOutcome>? ConversionFinished;

    /// <summary>The export folder has not been reachable for a while (the text says why). Raised once per problem.</summary>
    public event Action<string>? FolderProblemFound;

    /// <summary>The export folder is reachable again after <see cref="FolderProblemFound"/>.</summary>
    public event Action? FolderProblemSolved;

    /// <param name="getSettings">Gives the settings in use (asked for every scan, so a change is used at once).</param>
    /// <param name="isPaused">True while the user has paused the program: then nothing is scanned.</param>
    /// <param name="materialsFile">The materials.csv in the data folder.</param>
    /// <param name="now">Only for tests: gives the "current" time.</param>
    public ExportWatcher(Func<AppSettings> getSettings, Func<bool> isPaused, LogWriter log, string materialsFile, Func<DateTime>? now = null)
    {
        _getSettings = getSettings;
        _isPaused = isPaused;
        _log = log;
        _now = now ?? (() => DateTime.Now);
        _scanner = new ExportScanner(_now);
        _processor = new ProjectProcessor(materialsFile);
    }

    // ------------------------------------------------------------------ start, stop, wake up

    /// <summary>Starts the background thread. The first scan happens right away (the "rescan at startup").</summary>
    public void Start()
    {
        if (_thread != null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "TsDssConverter export watcher" };
        _thread.Start();
    }

    /// <summary>"Nu scannen": scan right now, also when the program is paused.</summary>
    public void ScanNow()
    {
        _scanRequested = true;
        _wakeUp.Set();
    }

    /// <summary>Scan soon, without forcing (after Resume and after the settings changed).</summary>
    public void WakeUp()
    {
        _wakeUp.Set();
    }

    public void Dispose()
    {
        _stopping = true;
        _wakeUp.Set();

        // Give a conversion that is busy a few seconds to finish. The thread is a background thread, so
        // the program can end anyway. (Files are written as .tmp first, so a stop halfway does no harm.)
        _thread?.Join(TimeSpan.FromSeconds(5));
        StopWatching();

        // _wakeUp is not disposed on purpose: a file event that is still on its way would call Set() on it,
        // and that must not throw. The event cleans itself up when the program ends.
    }

    // ------------------------------------------------------------------ the thread

    private void Run()
    {
        while (!_stopping)
        {
            bool force = _scanRequested;
            _scanRequested = false;

            try
            {
                RunOnce(force);
            }
            catch (Exception error)
            {
                // This thread must never die: without it nothing would be converted anymore.
                _log.Error(Messages.LogWatcherFailure(error.ToString()));
            }

            TimeSpan wait = _isWaiting ? WaitingInterval : TimeSpan.FromSeconds(_getSettings().RescanSeconds);
            _wakeUp.WaitOne(wait); // ends early when a file appears, or on "scan now", or when stopping
        }
    }

    /// <summary>
    /// One round: check the folder, convert what is ready. The thread calls this in a loop; the tests call it
    /// directly. <paramref name="force"/> scans also while paused.
    /// </summary>
    public void RunOnce(bool force = false)
    {
        AppSettings settings = _getSettings();

        if (_isPaused() && !force)
        {
            _isWaiting = false;
            return;
        }

        EnsureWatching(settings.TopSolidExportPath);
        RetryPendingMoves();

        ScanResult scan = _scanner.Scan(settings.TopSolidExportPath, settings.RequireTriggerFile);
        _isWaiting = scan.IsWaiting;

        HandleFolderProblem(scan.FolderProblem, settings.TopSolidExportPath);
        if (scan.FolderProblem != null)
        {
            return; // try again at the next scan
        }

        foreach (GaveUpProject gaveUp in scan.GaveUp)
        {
            ProcessOutcome outcome = _processor.Reject(gaveUp.Files, settings, new[] { gaveUp.Reason }, _now());
            Finish(outcome, gaveUp.Files, settings);
        }

        foreach (ProjectFiles files in scan.Ready)
        {
            if (_stopping)
            {
                return;
            }

            if (_pendingMoves.ContainsKey(files.Project))
            {
                continue; // already converted; only moving the files is left (see RetryPendingMoves)
            }

            _log.Info(Messages.LogConversionStarted(files.Project));
            ConversionStarted?.Invoke(files.Project);

            ProcessOutcome result = _processor.Process(files, settings, _now());
            Finish(result, files, settings);
        }
    }

    // ------------------------------------------------------------------ results

    private void Finish(ProcessOutcome outcome, ProjectFiles files, AppSettings settings)
    {
        foreach (string warning in outcome.Warnings)
        {
            _log.Warning(Messages.LogProjectWarning(outcome.Project, warning));
        }

        if (outcome.MovedTo != null)
        {
            _log.Info(Messages.LogFilesMoved(outcome.MovedTo));
        }

        if (outcome.IsTemporary)
        {
            _lastTemporaryMessage.TryGetValue(outcome.Project, out string? last);
            outcome.IsRepeat = last == outcome.Message;
            _lastTemporaryMessage[outcome.Project] = outcome.Message;
        }
        else
        {
            _lastTemporaryMessage.Remove(outcome.Project);
        }

        if (outcome.MovePending)
        {
            _pendingMoves[outcome.Project] = new PendingMove { Files = files, ExportFolder = settings.TopSolidExportPath, Stamp = _now() };
        }

        ConversionFinished?.Invoke(outcome);
    }

    /// <summary>The batch was written but the files could not be moved: try again, and never convert twice.</summary>
    private void RetryPendingMoves()
    {
        foreach (var (project, pending) in _pendingMoves.ToList())
        {
            try
            {
                string folder = ProjectProcessor.MoveProcessedFiles(pending.Files, pending.ExportFolder, pending.Stamp);
                _pendingMoves.Remove(project);
                _log.Info(Messages.LogFilesMoved(folder));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // Still locked: the next scan tries again.
            }
        }
    }

    // ------------------------------------------------------------------ the folder is (not) reachable

    private void HandleFolderProblem(string? problem, string folder)
    {
        if (problem == null)
        {
            if (_folderProblemSince != null)
            {
                _log.Info(Messages.LogExportFolderBack(folder));
                _watcherBroken = true; // build the FileSystemWatcher again, the old one may have lost the folder
            }

            if (_folderProblemReported)
            {
                FolderProblemSolved?.Invoke();
            }

            _folderProblemSince = null;
            _folderProblemReported = false;
            return;
        }

        if (_folderProblemSince == null)
        {
            _folderProblemSince = _now();
            _log.Warning(problem);
        }

        if (!_folderProblemReported && _now() - _folderProblemSince >= FolderProblemGrace)
        {
            _folderProblemReported = true;
            FolderProblemFound?.Invoke(problem);
        }
    }

    // ------------------------------------------------------------------ the FileSystemWatcher

    /// <summary>
    /// Makes sure a FileSystemWatcher is watching the export folder: it is created at the start, again when
    /// the folder was changed in the settings, and again after an error. If the folder is not reachable
    /// nothing is created; this is tried again at the next scan.
    /// </summary>
    private void EnsureWatching(string folder)
    {
        if (_fileWatcher != null && !_watcherBroken && _watchedFolder == folder)
        {
            return;
        }

        StopWatching();
        _watcherBroken = false;

        try
        {
            if (!Directory.Exists(folder))
            {
                return; // the scan reports the problem
            }

            var watcher = new FileSystemWatcher(folder, "*.xlsx")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024, // the biggest allowed: fewer lost events when TopSolid writes many files
            };

            // All these events do the same: wake the thread. The thread then looks at the folder itself.
            watcher.Created += (sender, e) => _wakeUp.Set();
            watcher.Changed += (sender, e) => _wakeUp.Set();
            watcher.Renamed += (sender, e) => _wakeUp.Set();
            watcher.Error += OnWatcherError;
            watcher.EnableRaisingEvents = true;

            _fileWatcher = watcher;
            _watchedFolder = folder;
            _log.Info(Messages.LogWatching(folder));
        }
        catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
        {
            _log.Warning(Messages.LogWatcherCannotStart(error.Message));
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // Buffer overflow or the folder went away (network drive). Build a new watcher and scan everything again.
        _log.Warning(Messages.LogWatcherError(e.GetException().Message));
        _watcherBroken = true;
        _wakeUp.Set();
    }

    private void StopWatching()
    {
        _fileWatcher?.Dispose();
        _fileWatcher = null;
    }
}
