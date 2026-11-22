using System.Diagnostics;
using TsDssConverter.Core;

namespace TsDssConverter.Tray;

/// <summary>
/// The tray application: the icon with its menu, the tooltip, the balloon on errors and the settings window.
/// It runs for as long as the program runs (there is no main window).
///
/// It owns the <see cref="ExportWatcher"/> (stage 4), which converts the TopSolid exports on its own thread and
/// reports back with events. The events end in <see cref="ReportResult"/> and <see cref="SetState"/>, which can be
/// called from any thread (the work is handed over to the tray's own thread).
/// </summary>
internal class TrayApp : ApplicationContext
{
    private const int MaxTooltipLength = 127; // the limit of Windows

    private readonly AppDataFolder _dataFolder;
    private readonly LogWriter _log;
    private readonly ConversionHistory _history = new();
    private HistoryEntry? _lastResult; // for the tooltip: stays when the user clears the list in the window
    private readonly StartWithWindows _startWithWindows = new();
    private readonly NotifyIcon _notifyIcon = new();
    private readonly Dictionary<TrayState, Icon> _icons = new();
    private readonly bool _demo;

    // Lets other threads (the watcher) change the icon safely: the work is handed over to the tray's own thread.
    private readonly Control _uiThread = new();

    private ToolStripMenuItem _pauseItem = new();
    private readonly ExportWatcher _watcher;

    // Read by the watcher's thread as well: volatile makes sure it always sees the latest value.
    private volatile AppSettings _settings;
    private SettingsForm? _settingsForm;
    private bool _settingsFormOutdated; // true after a language change: the window is built again the next time
    private TrayState _state = TrayState.Ok;
    private volatile bool _paused;

    // settings.json may still be edited by hand while the program runs (in Notepad; the window's Magic parameters are the
    // normal way since 2.0.1). Every second the tray looks whether the file changed (only its date: cheap), and then uses
    // the new values at once, also in an open window (2.0.0-alpha.3; it was every 5 s).
    private readonly SettingsFileChecker _settingsFileChecker;
    private readonly System.Windows.Forms.Timer _settingsFileTimer = new() { Interval = 1000 };

    public TrayApp(AppDataFolder dataFolder, StartupOptions options)
    {
        _dataFolder = dataFolder;
        _demo = options.Demo;
        _log = new LogWriter(dataFolder.LogFolder);
        _ = _uiThread.Handle; // creates the window handle on this (the tray's) thread

        // The language comes from settings.json, so the settings are read before the first text is written.
        SettingsLoadResult loaded = SettingsStore.Load(dataFolder.SettingsFile);
        AppLanguage language = Localizer.FromCode(loaded.Settings.Language);
        if (language != Localizer.Current)
        {
            Localizer.Current = language;
            loaded = SettingsStore.Load(dataFolder.SettingsFile); // again, so the notes are in that language
        }

        _settings = loaded.Settings;

        // The watcher is created here (the menu needs it) and started at the very end of this constructor.
        _watcher = new ExportWatcher(
            () => _settings, () => _paused, _log, dataFolder.MaterialsFile,
            infoColumnsFile: dataFolder.InfoColumnsFile, positionColumnsFile: dataFolder.PositionColumnsFile,
            labelColumnsFile: dataFolder.LabelColumnsFile);
        _watcher.ConversionStarted += project => SetState(TrayState.Busy);
        _watcher.ConversionFinished += OnConversionFinished;
        _watcher.FolderProblemFound += ReportFolderProblem;
        _watcher.FolderProblemSolved += () => SetState(TrayState.Ok);

        _log.DeleteOldLogs();
        _log.Info(Strings.LogStarted(AppInfo.Version, dataFolder.Root));

        CreateTrayIcon();

        if (!loaded.FileExisted)
        {
            TrySaveSettings(_settings); // first start: write settings.json so it can be found and edited
        }

        _settingsFileChecker = new SettingsFileChecker(dataFolder.SettingsFile); // what is in the file now is known
        _settingsFileTimer.Tick += (sender, e) => ReloadSettingsIfChanged();
        _settingsFileTimer.Start();

        if (loaded.Problems.Count > 0)
        {
            ShowProblem(Strings.BalloonSettingsTitle, string.Join(Environment.NewLine, loaded.Problems));
        }

        if (options.ShowSettings)
        {
            _uiThread.BeginInvoke(new Action(ShowSettings)); // runs as soon as the message loop has started
        }

        _watcher.Start(); // the first scan of the export folder happens right away
    }

    /// <summary>The settings in use. The watcher reads them for every scan.</summary>
    public AppSettings Settings => _settings;

    /// <summary>True while the user has paused the program. The watcher does not scan while this is true.</summary>
    public bool IsPaused => _paused;

    // ------------------------------------------------------------------ what other parts of the program call

    /// <summary>
    /// Says what the program is doing: Ok, Busy or Error. Can be called from any thread.
    /// The "paused" look is not set here: the tray shows it by itself while the user has paused.
    /// </summary>
    public void SetState(TrayState state)
    {
        OnTrayThread(() =>
        {
            _state = state == TrayState.Paused ? TrayState.Ok : state;
            UpdateIconAndTooltip();
        });
    }

    /// <summary>
    /// Reports the end of one conversion: it goes to the log and to the list in the window.
    /// Success: the icon is OK again. Error: the icon turns red and ONE balloon appears.
    /// Can be called from any thread.
    /// </summary>
    public void ReportResult(string project, bool success, string message)
    {
        OnTrayThread(() =>
        {
            AddToHistory(new HistoryEntry { Time = DateTime.Now, Project = project, Success = success, Message = message });

            if (success)
            {
                _log.Info(Strings.LogResult(project, message));
                _state = TrayState.Ok;
            }
            else
            {
                _log.Error(Strings.LogResult(project, message));
                _state = TrayState.Error;
                ShowBalloon(Strings.BalloonErrorTitle(project), message);
            }

            UpdateIconAndTooltip();
        });
    }

    // ------------------------------------------------------------------ what the watcher reports

    /// <summary>A project is finished (comes from the watcher's thread; the calls below hand the work to the tray thread).</summary>
    private void OnConversionFinished(ProcessOutcome outcome)
    {
        if (outcome.IsRepeat)
        {
            // The same temporary problem as before (for example the Duivestein folder is still not reachable):
            // the user knows already, so no second balloon and no second line in the list. Only the icon goes
            // back to red (it was set to "busy" for this attempt).
            SetState(TrayState.Error);
            return;
        }

        ReportResult(outcome.Project, outcome.Success, outcome.Message);
    }

    /// <summary>The export folder has not been reachable for a while: red icon, one balloon, one line in the list.</summary>
    private void ReportFolderProblem(string message)
    {
        OnTrayThread(() =>
        {
            AddToHistory(new HistoryEntry { Time = DateTime.Now, Project = Strings.NoProject, Success = false, Message = message });
            _log.Error(message);
            _state = TrayState.Error;
            ShowBalloon(Strings.BalloonFolderTitle, message);
            UpdateIconAndTooltip();
        });
    }

    /// <summary>A line for the list in the window. The tooltip keeps its own copy, because the user can clear the list.</summary>
    private void AddToHistory(HistoryEntry entry)
    {
        _lastResult = entry;
        _history.Add(entry);
    }

    private void ScanNow()
    {
        _log.Info(Strings.LogScanNow);
        _watcher.ScanNow();
    }

    // ------------------------------------------------------------------ retry failed

    /// <summary>
    /// The "Retry failed" button: puts the files of all failed projects back in the export folder and scans at once.
    /// The moving happens on another thread (a dead network drive must not freeze the window). A success is silent,
    /// like every other success: the projects show up in the list of conversions. The user only gets a message when
    /// there was nothing to retry, when a project was left alone, or when something went wrong.
    /// </summary>
    private void RetryFailed()
    {
        string exportFolder = _settings.TopSolidExportPath;

        Task.Run(() => FailedProjects.RetryAll(exportFolder)).ContinueWith(task =>
        {
            RetryResult result = task.IsCompletedSuccessfully
                ? task.Result
                : new RetryResult { Problem = task.Exception?.GetBaseException().Message ?? "?" };

            OnTrayThread(() => ReportRetry(result));
        });
    }

    private void ReportRetry(RetryResult result)
    {
        foreach (string project in result.MovedBack)
        {
            _log.Info(Strings.LogRetryMovedBack(project));
        }

        foreach (string project in result.Blocked)
        {
            _log.Warning(Strings.LogRetryBlocked(project));
        }

        if (result.Problem != null)
        {
            _log.Error(Strings.RetryProblem(result.Problem));
        }

        if (result.MovedBack.Count > 0)
        {
            _watcher.ScanNow(); // also while paused: the user asked for it just now
        }
        else if (result.Blocked.Count == 0 && result.Problem == null)
        {
            _log.Info(Strings.LogRetryNothing);
        }

        // Only tell the user what needs telling.
        string? message = result.Problem != null ? Strings.RetryProblem(result.Problem)
            : result.Blocked.Count > 0 ? Strings.RetryBlocked(result.Blocked)
            : result.MovedBack.Count == 0 ? Strings.RetryNothingFound
            : null;

        if (message != null)
        {
            bool nothingToRetry = result.MovedBack.Count == 0 && result.Blocked.Count == 0 && result.Problem == null;
            MessageBox.Show(
                _settingsForm, message, Strings.AppName, MessageBoxButtons.OK,
                nothingToRetry ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }

    // ------------------------------------------------------------------ tray icon and menu

    private void CreateTrayIcon()
    {
        foreach (TrayState state in Enum.GetValues<TrayState>())
        {
            _icons[state] = AppIcons.ForState(state);
        }

        _notifyIcon.ContextMenuStrip = BuildMenu();
        _notifyIcon.DoubleClick += (sender, e) => ShowSettings();
        _notifyIcon.BalloonTipClicked += (sender, e) => ShowSettings();
        UpdateIconAndTooltip();
        _notifyIcon.Visible = true;
    }

    /// <summary>Builds the right-click menu in the selected language. Called again after a language change.</summary>
    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.MenuSettings, null, (sender, e) => ShowSettings());

        menu.Items.Add(Strings.MenuScanNow, null, (sender, e) => ScanNow());

        _pauseItem = new ToolStripMenuItem(_paused ? Strings.MenuResume : Strings.MenuPause);
        _pauseItem.Click += (sender, e) => TogglePause();
        menu.Items.Add(_pauseItem);

        menu.Items.Add(Strings.MenuOpenLogFolder, null, (sender, e) => OpenFolder(_dataFolder.LogFolder));
        menu.Items.Add(Strings.MenuOpenMaterials, null, (sender, e) => OpenFile(_dataFolder.MaterialsFile));

        if (_demo)
        {
            // To try out the looks of the icon and the list without a real conversion.
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Strings.MenuDemoOk, null, (sender, e) => ReportResult("Demo", true, Strings.DemoOkMessage));
            menu.Items.Add(Strings.MenuDemoBusy, null, (sender, e) => SetState(TrayState.Busy));
            menu.Items.Add(Strings.MenuDemoError, null, (sender, e) => ReportResult("Demo", false, Strings.DemoErrorMessage));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuExit, null, (sender, e) => ExitProgram());
        return menu;
    }

    private void UpdateIconAndTooltip()
    {
        // _state is what the program is doing; the icon also depends on whether the user paused it.
        _notifyIcon.Icon = _icons[TrayStateRules.Displayed(_state, _paused)];

        HistoryEntry? last = _lastResult;
        string text = last == null
            ? Strings.TooltipNothingYet
            : Strings.TooltipLast(last.Project, last.Success, last.Time);

        if (_paused)
        {
            text += Strings.TooltipPausedSuffix;
        }

        _notifyIcon.Text = text.Length <= MaxTooltipLength ? text : text.Substring(0, MaxTooltipLength);

        // The Status tile of the window shows the same as the icon (2.0.0).
        if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.IsHandleCreated)
        {
            _settingsForm.RefreshStatus();
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Text = _paused ? Strings.MenuResume : Strings.MenuPause;
        _log.Info(_paused ? Strings.LogPaused : Strings.LogResumed);
        UpdateIconAndTooltip();

        if (!_paused)
        {
            _watcher.WakeUp(); // scan at once, what arrived during the pause is waiting
        }
    }

    // ------------------------------------------------------------------ language

    /// <summary>
    /// Switches the whole interface to another language: the menu and the tooltip now, the settings window
    /// the next time it is opened (it is built again then).
    /// </summary>
    private void ApplyLanguage(string code)
    {
        Localizer.Current = Localizer.FromCode(code);
        _log.Info(Strings.LogLanguageChanged(code));

        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildMenu();
        oldMenu?.Dispose();

        UpdateIconAndTooltip();
        _settingsFormOutdated = true;
    }

    // ------------------------------------------------------------------ balloon

    private void ShowBalloon(string title, string message)
    {
        // A balloon is small: only the first lines of a long error report.
        string shortMessage = message.Length <= 200 ? message : message.Substring(0, 200) + "…";
        _notifyIcon.ShowBalloonTip(10000, title, shortMessage, ToolTipIcon.Error);
    }

    /// <summary>A problem at start-up (for example an unreadable settings.json): log, red icon and one balloon.</summary>
    private void ShowProblem(string title, string message)
    {
        _log.Warning(message);
        _state = TrayState.Error;
        UpdateIconAndTooltip();
        ShowBalloon(title, message);
    }

    // ------------------------------------------------------------------ settings window

    private void ShowSettings()
    {
        // After a language change the old window still has the old texts: build a new one.
        // (This is not done inside the Save click itself: a window cannot be disposed from its own event.)
        if (_settingsFormOutdated && _settingsForm != null)
        {
            _settingsForm.AllowClose = true;
            _settingsForm.Dispose();
            _settingsForm = null;
        }

        _settingsFormOutdated = false;

        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = CreateSettingsForm();
        }

        if (!_settingsForm.Visible)
        {
            _settingsForm.Show();
        }

        if (_settingsForm.WindowState == FormWindowState.Minimized)
        {
            _settingsForm.WindowState = FormWindowState.Normal;
        }

        _settingsForm.Activate();
    }

    private SettingsForm CreateSettingsForm()
    {
        return new SettingsForm(
            () => _settings, SaveSettings, AcknowledgeError, _history, _startWithWindows,
            _dataFolder, OpenDataFile, OpenFolder, RetryFailed,
            scanNow: ScanNow, togglePause: TogglePause, isPaused: () => _paused,
            getState: () => TrayStateRules.Displayed(_state, _paused),
            saveFolded: SaveFolded, changeLanguage: ChangeLanguage,
            nextScan: () => _watcher.NextScan, saveAdvanced: SaveMagicParameters);
    }

    /// <summary>
    /// Save in the Magic parameters popup: only its five values are changed in settings.json (the file is read again first,
    /// so everything else in it stays exactly as it is, also what was typed in the window but not saved), and they are
    /// used at once (a new scan interval, CNC extension, ...). Throws IOException if settings.json cannot be written or has
    /// a mistake (it is then not overwritten).
    /// </summary>
    private void SaveMagicParameters(AppSettings fromPopup)
    {
        SettingsLoadResult saved = SettingsStore.SaveChange(_dataFolder.SettingsFile, _settings, settings => settings.CopyAdvancedSettingsFrom(fromPopup));
        _settingsFileChecker.Remember(); // our own change: nothing to reload
        _log.Info(Strings.LogMagicParametersSaved);
        UseSettings(saved);
    }

    /// <summary>
    /// Builds the open settings window again (another language), in the same place and size. With
    /// <paramref name="keepEdits"/> what the user typed but did not save yet is put in the new window; without it the new
    /// window shows the settings in use (settings.json was changed by hand: the file wins). A hidden window is built
    /// again the next time it is opened.
    /// </summary>
    private void RebuildSettingsWindow(bool keepEdits)
    {
        SettingsForm? old = _settingsForm;
        if (old == null || old.IsDisposed || !old.Visible)
        {
            _settingsFormOutdated = true;
            return;
        }

        AppSettings typed = old.ReadSettings();
        bool startWithWindows = old.StartWithWindowsBox.Checked;
        FormWindowState state = old.WindowState;
        Rectangle bounds = state == FormWindowState.Normal ? old.Bounds : old.RestoreBounds;

        _settingsFormOutdated = false;
        _settingsForm = CreateSettingsForm();
        _settingsForm.StartPosition = FormStartPosition.Manual;
        _settingsForm.Show();
        _settingsForm.Bounds = bounds; // after Show: opening sizes the window to its content first
        _settingsForm.WindowState = state;
        if (keepEdits)
        {
            _settingsForm.ShowEdits(typed, startWithWindows);
        }

        _settingsForm.Activate();

        // The old window is closed a moment later: this can be running inside one of its own events (the language box),
        // and a window cannot be disposed from its own event.
        _uiThread.BeginInvoke(new Action(() =>
        {
            old.AllowClose = true;
            old.Dispose();
        }));
    }

    /// <summary>
    /// The user chose another language in the window: it is saved at once (only the language: settings.json is read
    /// again first and the other values in it stay) and the whole interface switches now, the window too.
    /// </summary>
    private void ChangeLanguage(string code)
    {
        try
        {
            SettingsLoadResult saved = SettingsStore.SaveChange(_dataFolder.SettingsFile, _settings, settings => settings.Language = code);
            _settingsFileChecker.Remember(); // our own change: nothing to reload
            UseSettings(saved);              // switches the menu, the tooltip and the log (ApplyLanguage)
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            _log.Warning(Strings.SaveFailed(error.Message));
            MessageBox.Show(_settingsForm, Strings.SaveFailed(error.Message), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        RebuildSettingsWindow(keepEdits: true);
    }

    /// <summary>The folders panel was folded or opened: saved at once, so the window opens the same way next time.</summary>
    private void SaveFolded(bool folded)
    {
        try
        {
            SettingsLoadResult saved = SettingsStore.SaveChange(_dataFolder.SettingsFile, _settings, settings => settings.FoldersFolded = folded);
            _settingsFileChecker.Remember();
            _settings = saved.Settings;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // Not worth a message: the panel is folded now anyway, it is only not remembered after a restart.
            _log.Warning(Strings.SaveFailed(error.Message));
            AppSettings inUse = _settings.Clone();
            inUse.FoldersFolded = folded;
            _settings = inUse;
        }
    }

    /// <summary>The red icon stays until the next success OR until the window is opened.</summary>
    private void AcknowledgeError()
    {
        if (_state == TrayState.Error)
        {
            _state = TrayState.Ok;
            UpdateIconAndTooltip();
        }
    }

    /// <summary>
    /// Called by the window's Save button. settings.json is read again first, so what was edited there by hand is kept
    /// (see <see cref="SettingsStore.SaveWindowSettings"/>). Throws IOException if settings.json cannot be written or
    /// has a mistake (it is then not overwritten).
    /// </summary>
    private void SaveSettings(AppSettings newSettings)
    {
        SettingsLoadResult saved = SettingsStore.SaveWindowSettings(_dataFolder.SettingsFile, newSettings);
        _settingsFileChecker.Remember(); // our own change: nothing to reload
        _log.Info(Strings.LogSettingsSaved);
        UseSettings(saved);
    }

    /// <summary>
    /// The timer: settings.json was changed outside the program (by hand) since the last look? Then its values are used
    /// at once (folders, advanced settings, language). A file with a mistake is reported ONCE (red icon, balloon) and the
    /// settings in use stay until the file is fixed.
    /// </summary>
    private void ReloadSettingsIfChanged()
    {
        SettingsLoadResult? loaded = _settingsFileChecker.ReadIfChanged();
        if (loaded == null)
        {
            return;
        }

        if (loaded.ReadError != "")
        {
            ShowProblem(Strings.BalloonSettingsTitle, Strings.SettingsReloadFailed(_dataFolder.SettingsFile, loaded.ReadError));
            return;
        }

        _log.Info(Strings.LogSettingsReloaded);
        bool languageChanged = loaded.Settings.Language != _settings.Language;
        UseSettings(loaded);

        // An open window shows the new values at once (Daan, 2026-11-22): in another language it is built again, otherwise
        // it is filled again. Here the file wins: what was typed in the window but not saved is replaced.
        if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Visible)
        {
            if (languageChanged)
            {
                RebuildSettingsWindow(keepEdits: false);
            }
            else
            {
                _settingsForm.LoadFromSettings();
            }
        }
    }

    /// <summary>Puts newly saved or read settings in use; repaired values are reported.</summary>
    private void UseSettings(SettingsLoadResult result)
    {
        bool languageChanged = result.Settings.Language != _settings.Language;
        _settings = result.Settings;
        _watcher.WakeUp(); // a new export folder is watched at once

        if (languageChanged)
        {
            ApplyLanguage(result.Settings.Language);
        }

        if (result.Problems.Count > 0)
        {
            ShowProblem(Strings.BalloonSettingsTitle, string.Join(Environment.NewLine, result.Problems));
        }
    }

    /// <summary>Saves settings without a window (first start). A failure is logged, not shown.</summary>
    private void TrySaveSettings(AppSettings settings)
    {
        try
        {
            SettingsStore.Save(_dataFolder.SettingsFile, settings);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            _log.Warning(Strings.SaveFailed(error.Message));
        }
    }

    // ------------------------------------------------------------------ open folder / file

    private void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        OpenWithWindows(path, Strings.CannotOpenFolder);
    }

    private void OpenFile(string path)
    {
        OpenWithWindows(path, Strings.CannotOpenFile);
    }

    /// <summary>
    /// Opens a file from the data folder (the column files). If someone deleted it, it is made again first with the
    /// default names, so the button never opens nothing.
    /// </summary>
    private void OpenDataFile(string path)
    {
        try
        {
            _dataFolder.EnsureCreated();
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // Not fatal here: opening the file below reports the problem to the user.
        }

        OpenFile(path);
    }

    /// <summary>Opens a folder in Explorer, or a file in the program Windows uses for it (Excel for .csv).</summary>
    private void OpenWithWindows(string path, Func<string, string> cannotOpenMessage)
    {
        Start(new ProcessStartInfo(path) { UseShellExecute = true }, cannotOpenMessage);
    }

    /// <summary>Starts a program; a failure is logged and shown in a balloon, never thrown.</summary>
    private void Start(ProcessStartInfo start, Func<string, string> cannotOpenMessage)
    {
        try
        {
            Process.Start(start);
        }
        catch (Exception error) when (error is IOException || error is System.ComponentModel.Win32Exception || error is UnauthorizedAccessException)
        {
            string message = cannotOpenMessage(error.Message);
            _log.Warning(message);
            ShowBalloon(Strings.AppName, message);
        }
    }

    // ------------------------------------------------------------------ threads and exit

    private void OnTrayThread(Action action)
    {
        if (_uiThread.IsDisposed)
        {
            return; // the program is exiting
        }

        if (_uiThread.InvokeRequired)
        {
            _uiThread.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void ExitProgram()
    {
        _log.Info(Strings.LogExiting);
        _settingsFileTimer.Stop();
        _watcher.Dispose(); // stops the background thread (waits a few seconds for a conversion that is busy)

        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _settingsForm.AllowClose = true;
            _settingsForm.Close();
        }

        // Without this the icon stays in the tray until the mouse moves over it.
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcher.Dispose(); // harmless if the Exit menu already did this
            _settingsFileTimer.Dispose();
            _notifyIcon.Dispose();
            foreach (Icon icon in _icons.Values)
            {
                icon.Dispose();
            }

            _uiThread.Dispose();
        }

        base.Dispose(disposing);
    }
}
