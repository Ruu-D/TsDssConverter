using System.Diagnostics;
using TsDssConverter.Core;

namespace TsDssConverter.Tray;

/// <summary>
/// The tray application: the icon with its menu, the tooltip, the balloon on errors and the settings window.
/// It runs for as long as the program runs (there is no main window).
///
/// Stage 3: the shell only. "Nu scannen" is disabled and Pause only switches a flag; the folder watching
/// and the conversion come in stage 4, which will call <see cref="ReportResult"/> and <see cref="SetState"/>
/// from its own thread (that is safe).
/// </summary>
internal class TrayApp : ApplicationContext
{
    private const int MaxTooltipLength = 127; // the limit of Windows

    private readonly AppDataFolder _dataFolder;
    private readonly LogWriter _log;
    private readonly ConversionHistory _history = new();
    private readonly StartWithWindows _startWithWindows = new();
    private readonly NotifyIcon _notifyIcon = new();
    private readonly Dictionary<TrayState, Icon> _icons = new();
    private readonly bool _demo;

    // Lets other threads (stage 4) change the icon safely: the work is handed over to the tray's own thread.
    private readonly Control _uiThread = new();

    private ToolStripMenuItem _pauseItem = new();
    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private bool _settingsFormOutdated; // true after a language change: the window is built again the next time
    private TrayState _state = TrayState.Ok;
    private bool _paused;

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

        _log.DeleteOldLogs();
        _log.Info(Strings.LogStarted(AppInfo.Version, dataFolder.Root));

        CreateTrayIcon();

        if (!loaded.FileExisted)
        {
            TrySaveSettings(_settings); // first start: write settings.json so it can be found and edited
        }

        if (loaded.Problems.Count > 0)
        {
            ShowProblem(Strings.BalloonSettingsTitle, string.Join(Environment.NewLine, loaded.Problems));
        }

        if (options.ShowSettings)
        {
            _uiThread.BeginInvoke(new Action(ShowSettings)); // runs as soon as the message loop has started
        }
    }

    /// <summary>The settings in use. Stage 4 reads them for every conversion.</summary>
    public AppSettings Settings => _settings;

    /// <summary>True while the user has paused the program. Stage 4 skips scanning while this is true.</summary>
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
            _history.Add(new HistoryEntry { Time = DateTime.Now, Project = project, Success = success, Message = message });

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

        var scanItem = new ToolStripMenuItem(Strings.MenuScanNow) { Enabled = false }; // stage 4
        menu.Items.Add(scanItem);

        _pauseItem = new ToolStripMenuItem(_paused ? Strings.MenuResume : Strings.MenuPause);
        _pauseItem.Click += (sender, e) => TogglePause();
        menu.Items.Add(_pauseItem);

        menu.Items.Add(Strings.MenuOpenLogFolder, null, (sender, e) => OpenFolder(_dataFolder.LogFolder));
        menu.Items.Add(Strings.MenuOpenMaterials, null, (sender, e) => OpenFile(_dataFolder.MaterialsFile));

        if (_demo)
        {
            // To try out the looks of the icon and the list before the real conversion exists (stage 4).
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

        HistoryEntry? last = _history.Latest;
        string text = last == null
            ? Strings.TooltipNothingYet
            : Strings.TooltipLast(last.Project, last.Success, last.Time);

        if (_paused)
        {
            text += Strings.TooltipPausedSuffix;
        }

        _notifyIcon.Text = text.Length <= MaxTooltipLength ? text : text.Substring(0, MaxTooltipLength);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Text = _paused ? Strings.MenuResume : Strings.MenuPause;
        _log.Info(_paused ? Strings.LogPaused : Strings.LogResumed);
        UpdateIconAndTooltip();
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
            _settingsForm = new SettingsForm(() => _settings, SaveSettings, AcknowledgeError, _history, _startWithWindows);
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

    /// <summary>The red icon stays until the next success OR until the window is opened.</summary>
    private void AcknowledgeError()
    {
        if (_state == TrayState.Error)
        {
            _state = TrayState.Ok;
            UpdateIconAndTooltip();
        }
    }

    /// <summary>Called by the window's Save button. Throws IOException if settings.json cannot be written.</summary>
    private void SaveSettings(AppSettings newSettings)
    {
        SettingsStore.Save(_dataFolder.SettingsFile, newSettings);

        bool languageChanged = newSettings.Language != _settings.Language;
        _settings = newSettings;
        _log.Info(Strings.LogSettingsSaved);

        if (languageChanged)
        {
            ApplyLanguage(newSettings.Language);
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

    /// <summary>Opens a folder in Explorer, or a file in the program Windows uses for it (Excel for .csv).</summary>
    private void OpenWithWindows(string path, Func<string, string> cannotOpenMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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
