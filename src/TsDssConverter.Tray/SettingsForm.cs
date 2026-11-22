using TsDssConverter.Core;

// Core also has a class Label (a part on a sheet). In this file "Label" always means the text control.
using Label = System.Windows.Forms.Label;

namespace TsDssConverter.Tray;

/// <summary>
/// The settings window (the only window of the program), since 2.0.0 in the "Maestro" skin of DssLabelTemplate (Daan,
/// 2026-11-21, from his mockup): a top bar with the title tab, white panels on a grey frame (the folders, the label zero
/// point with two tiles, the list of the last conversions) and a blue bar at the bottom with round buttons and Save /
/// Cancel. It keeps the normal Windows title bar (minimise, maximise, close; Daan, 2026-11-22).
/// Closing the window (the X) only hides it: the program keeps running in the tray.
/// The layout is built in code, so it can be read and reviewed like any other code.
/// </summary>
internal class SettingsForm : Form
{
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<AppSettings> _save;
    private readonly Action _windowOpened;
    private readonly ConversionHistory _history;
    private readonly StartWithWindows _startup;
    private readonly AppDataFolder _dataFolder;
    private readonly Action<string> _openFile;
    private readonly Action<string> _openFolder;
    private readonly Action _retryFailed;
    private readonly Action _scanNow;
    private readonly Action _togglePause;
    private readonly Func<bool> _isPaused;
    private readonly Func<TrayState> _getState;
    private readonly Action<bool> _saveFolded;
    private readonly Action<string> _changeLanguage;
    private readonly Func<(DateTime At, TimeSpan Interval)?> _nextScan;

    // Moves the countdown ring of the Status tile while the window is visible (10 times a second: a smooth ring).
    private readonly System.Windows.Forms.Timer _countdownTimer = new() { Interval = 100 };

    // The controls we need to read or fill (internal: the tests check them)
    internal readonly CheckBox StartWithWindowsBox = new FluentCheckBox();
    internal readonly TextBox ExportFolderBox = new();
    internal readonly TextBox BatchFolderBox = new();
    internal readonly TextBox LabelFolderBox = new();
    internal readonly ComboBox ReferenceCornerBox = new();       // "Oriëntatie" in DSSClient: sets FlipX / FlipY together
    internal readonly CheckBox PlanRotatedBox = new FluentCheckBox(); // "Gedraaid" in DSSClient: the batch XML's Plan Rotation
    internal readonly ReferenceCornerPreview CornerPreview = new(); // small schematic that follows ReferenceCornerBox
    internal readonly HistoryListView HistoryList = new();
    private readonly Label _exportWarning = new();
    private readonly Label _batchWarning = new();
    private readonly Label _labelWarning = new();
    private readonly Label _historyEmpty = new();
    // The file names and DESC1 – DESC10 in the hints are bold (Daan, 2026-11-22); they are the same in every language.
    internal readonly BoldPartsLabel ConfigInfoHint = new() { BoldParts = new[] { "-LI.xlsx" } };
    internal readonly BoldPartsLabel ConfigPositionHint = new() { BoldParts = new[] { "-LP.xlsx" } };
    internal readonly BoldPartsLabel ConfigLabelHint = new() { BoldParts = new[] { "(DESC1 – DESC10)" } };

    // The window at 100%: wide enough for the two halves of folders and the two tiles. A screen at 150% shows it 1848
    // pixels wide. Narrower is allowed down to the minimum (the hints then wrap, the title tab shortens its subtitle).
    private const int WindowWidth = 1232; // 1120 until 2.0.0-alpha.5; 10% wider for the release (Daan)
    private const int MinimumWidth = 1040; // the bottom bar with the material list needs it (French, alpha.4)

    // Parts of the layout that the tests look at
    internal readonly RoundedButton ConfigInfoButton = new();      // opens columns-li.txt (below the export folder)
    internal readonly RoundedButton ConfigPositionButton = new();  // opens columns-lp.txt (below the LI one)
    internal readonly RoundedButton ConfigLabelButton = new();     // opens columns-label.txt (below the label folder)
    internal readonly BarButton MaterialsButton = new() { Glyph = BarGlyph.Materials }; // opens materials.csv (right end of the blue bar)
    internal readonly RoundedButton ClearHistoryButton = new();    // empties the list of conversions, only in the window
    internal readonly BarButton ScanNowButton = new() { Glyph = BarGlyph.Scan };          // the round buttons of the bottom bar,
    internal readonly BarButton RetryFailedButton = new() { Glyph = BarGlyph.Retry };     // left to right
    internal readonly BarButton PauseButton = new() { Glyph = BarGlyph.Pause };
    internal readonly BarButton OpenLogButton = new() { Glyph = BarGlyph.LogFolder };
    internal readonly PillButton SavePill = new() { ShowCheck = true };
    internal readonly PillButton CancelPill = new();
    internal readonly Panel TopBar = new();
    internal readonly TitleTab TitleTab = new();
    internal BottomBar BottomBar = null!;             // made in BuildWindow (it needs its buttons)
    internal readonly Label FoldersTitle = new();
    internal readonly Label ExportCaption = new();
    internal readonly Label BatchCaption = new();
    internal readonly Label LabelCaption = new();
    internal readonly Label ZeroPointTitle = new();
    internal readonly Label HistoryTitle = new();
    internal readonly List<Control> Sections = new(); // the white panels, top to bottom
    internal readonly Label LanguageCaption = new();
    internal readonly ComboBox LanguageBox = new();
    internal readonly Label CreditLabel = new();     // "Dev.: Daan Verhoost", in the top bar
    internal readonly Label VersionLabel = new();    // "App-versie: 2.0.0", below the credit
    internal readonly PictureBox LogoPicture = new(); // the small ROGIERS logo in the top bar (a link to the website)
    internal readonly HelpButton UserGuideButton = new(); // the "?" at the top right (opens the GitHub page)
    internal readonly Panel FolderDivider = new();   // the vertical line between the TopSolid and the Duivestein folders
    internal readonly InfoTile StatusTile = new();
    internal readonly InfoTile LastConversionTile = new();
    internal readonly FoldButton FoldFoldersButton = new();   // the "v" / ">" before "Mappen"
    internal readonly FolderSummary FoldedFolders = new();    // the three folders on one line while folded
    private readonly HashSet<TextBox> _unreachable = new();   // the folders the background check could not find
    private readonly ToolTip _foldTip = new();

    /// <summary>True while the folders panel is folded up to one line.</summary>
    internal bool FoldersFolded { get; private set; }
    private TableLayoutPanel? _folders;              // the row with the two halves of folders (see FitHints)
    private TableLayoutPanel? _middleRow;            // the zero point and the two tiles (see FitMiddleRow)
    private TableLayoutPanel? _zeroPointSection;
    private TableLayoutPanel? _foldersSection;
    private TableLayoutPanel? _historyHeader;
    private TableLayoutPanel? _body;
    private readonly List<(Label Hint, Control[] Buttons)> _wrappedHints = new(); // the hints beside their buttons
    internal readonly Panel Scroller = new();        // holds the panels; scrolls if the window is too small

    // The list of conversions is never smaller than this (in pixels at 100% scaling): it must always be visible.
    private const int MinimumHistoryHeight = 150;

    // When the window opens, the list gets this much more than its minimum (at 100%).
    private const int ExtraHistoryHeight = 10;

    // Sizes at 100%.
    private const int TopBarHeight = 76;
    private const int FieldHeight = 32;          // the folder boxes and the Browse buttons: one line
    private const int BrowseMinimumWidth = 120;
    private const int GreyButtonHeight = 30;     // Config, Clear list
    private const int GreyButtonMinimumWidth = 110;
    private const int TileWidth = 230;
    private const int Gap = 14;                  // the grey between the panels
    private const int RowHeight = 28;            // a row of the list of conversions


    // Widths of the columns Time, Project and Result at 100%. The Message column takes the rest.
    // (A ListView does not scale its columns by itself, so they are scaled in ScaleHistoryColumns.)
    private static readonly int[] HistoryColumnWidths = { 110, 210, 100 };

    /// <summary>One line of the language box: the language and the text that is shown for it.</summary>
    internal class LanguageItem
    {
        public AppLanguage Language { get; }
        public string Text { get; }

        public LanguageItem(AppLanguage language, string text)
        {
            Language = language;
            Text = text;
        }

        public override string ToString() => Text; // what the ComboBox shows
    }

    /// <summary>One item of the reference-corner box: the corner and the text shown for it.</summary>
    internal class ReferenceCornerItem
    {
        public ReferenceCorner Corner { get; }
        public string Text { get; }

        public ReferenceCornerItem(ReferenceCorner corner, string text)
        {
            Corner = corner;
            Text = text;
        }

        public override string ToString() => Text; // what the ComboBox shows
    }

    /// <summary>Set to true by the tray app when the program really exits; until then closing only hides.</summary>
    [System.ComponentModel.Browsable(false)] // these two attributes tell the Visual Studio designer to ignore it
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AllowClose { get; set; }

    /// <param name="getSettings">Gives the settings that are in use now (asked every time the window is shown).</param>
    /// <param name="save">Saves new settings. May throw IOException / UnauthorizedAccessException.</param>
    /// <param name="windowOpened">Called every time the window is shown (the tray clears its error state).</param>
    /// <param name="dataFolder">Where the column files (columns-li.txt, columns-lp.txt) and the logs are.</param>
    /// <param name="openFile">Opens a file in the program Windows uses for it (Notepad for a .txt file).</param>
    /// <param name="openFolder">Opens a folder in Explorer.</param>
    /// <param name="retryFailed">The "Retry failed" button: puts the failed projects back in the export folder (the tray does the work).</param>
    /// <param name="scanNow">The "Scan now" button (the tray's menu item of the same name).</param>
    /// <param name="togglePause">The "Pause" / "Resume" button (the tray's menu item of the same name).</param>
    /// <param name="isPaused">True while the user has paused the program.</param>
    /// <param name="getState">What the tray icon shows now (OK, busy, error, paused): the Status tile shows the same.</param>
    /// <param name="saveFolded">Saves at once that the folders panel was folded (true) or opened (false).</param>
    /// <param name="changeLanguage">The user chose another language ("nl", "fr", "en"): the tray saves it and switches at once.</param>
    public SettingsForm(
        Func<AppSettings> getSettings, Action<AppSettings> save, Action windowOpened,
        ConversionHistory history, StartWithWindows startup,
        AppDataFolder dataFolder, Action<string> openFile, Action<string> openFolder, Action retryFailed,
        Action? scanNow = null, Action? togglePause = null, Func<bool>? isPaused = null, Func<TrayState>? getState = null,
        Action<bool>? saveFolded = null, Action<string>? changeLanguage = null,
        Func<(DateTime At, TimeSpan Interval)?>? nextScan = null)
    {
        _nextScan = nextScan ?? (() => null); // no watcher (tests): no countdown ring
        _countdownTimer.Tick += (sender, e) => UpdateCountdown(DateTime.Now);
        _saveFolded = saveFolded ?? (folded => { });
        _changeLanguage = changeLanguage ?? (code => { });
        _getSettings = getSettings;
        _save = save;
        _windowOpened = windowOpened;
        _history = history;
        _startup = startup;
        _dataFolder = dataFolder;
        _openFile = openFile;
        _openFolder = openFolder;
        _retryFailed = retryFailed;
        _scanNow = scanNow ?? (() => { });
        _togglePause = togglePause ?? (() => { });
        _isPaused = isPaused ?? (() => false);
        _getState = getState ?? (() => _isPaused() ? TrayState.Paused : TrayState.Ok);

        // Like a form made with the Visual Studio designer: build everything between SuspendLayout and
        // ResumeLayout. WinForms applies the automatic scaling (for 125%, 150%, ...) when the layout resumes.
        SuspendLayout();
        BuildWindow();
        ResumeLayout(performLayout: true);

        _history.Changed += OnHistoryChanged;
        LoadFromSettings();
    }

    // ------------------------------------------------------------------ building the window

    private void BuildWindow()
    {
        // Scaling: all sizes below are written for a screen at 100% (96 dpi) and are multiplied by the screen
        // scaling (125%, 150%, ...), the same in width and height. WinForms only does this if the window is built
        // between SuspendLayout and ResumeLayout (see the constructor).
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.CreateBodyFont(); // Verdana, like Maestro; all controls below inherit it
        Text = Strings.WindowTitle;
        Icon = AppIcons.ForWindow();
        BackColor = Theme.Frame;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(WindowWidth, 820);
        MinimumSize = new Size(MinimumWidth, 520); // smaller than the content needs: then the panels scroll
        MaximizeBox = true; // the normal Windows title bar stays (Daan): minimise, maximise, close

        BuildTopBar();

        // The panels, top to bottom, on the grey frame: one column, the list takes the rest of the height.
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Gap),
            ColumnCount = 1,
            BackColor = Theme.Frame,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _body = body;

        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(BuildFoldersSection(), 0, 0);
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(BuildMiddleRow(), 0, 1);
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(BuildHistorySection(), 0, 2);
        body.RowCount = 3;

        // The panels sit in a scroll panel: if the window is smaller than the content (a small screen at a high
        // scaling), a scroll bar appears and nothing is cut off. On a normal screen there is nothing to scroll.
        Scroller.Dock = DockStyle.Fill;
        Scroller.AutoScroll = true;
        Scroller.BackColor = Theme.Frame;
        Scroller.Controls.Add(body);

        BuildBottomBar();

        // Docking: the control that is added LAST is docked first (the bars), the scroll panel gets the rest.
        Controls.Add(Scroller);
        Controls.Add(BottomBar);
        Controls.Add(TopBar);
    }

    /// <summary>
    /// The bar at the top: the white title tab at the left (logo, name, one line), and at the right the language, the
    /// credit and the version, the company logo and the "?" of the user guide.
    /// </summary>
    private void BuildTopBar()
    {
        TopBar.Dock = DockStyle.Top;
        TopBar.Height = TopBarHeight;
        TopBar.BackColor = Theme.TopBar;

        TitleTab.Text = Strings.AppName;
        TitleTab.Subtitle = Strings.BannerSubtitle; // English in every language, like DssLabelTemplate's
        TitleTab.Dock = DockStyle.Fill;

        LanguageCaption.Text = Strings.LanguageTitle;
        LanguageCaption.AutoSize = true;
        LanguageCaption.ForeColor = Theme.Text;
        LanguageCaption.Margin = new Padding(0, 0, 0, 3);

        LanguageBox.DropDownStyle = ComboBoxStyle.DropDownList; // choose from the list, no typing
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.Dutch, Strings.LanguageDutch));
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.French, Strings.LanguageFrench));
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.English, Strings.LanguageEnglish));
        StyleDropDown(LanguageBox, 190);
        LanguageBox.Margin = new Padding(0);
        // Another language is used AT ONCE (Daan, 2026-11-22): saved and the whole window switches, without Opslaan.
        // SelectionChangeCommitted only comes from the user, not when the code sets the box (LoadFromSettings).
        LanguageBox.SelectionChangeCommitted += (sender, e) => LanguageChosen();

        var language = NewStack(LanguageCaption, LanguageBox);
        language.Anchor = AnchorStyles.Left;

        CreditLabel.Text = Strings.DeveloperCredit;
        CreditLabel.AutoSize = true;
        CreditLabel.ForeColor = Theme.Text;
        CreditLabel.Anchor = AnchorStyles.Right;
        CreditLabel.Margin = new Padding(0, 0, 0, 3);

        VersionLabel.Text = Strings.AppVersion(AppInfo.Version);
        VersionLabel.AutoSize = true;
        VersionLabel.ForeColor = Theme.Text;
        VersionLabel.Anchor = AnchorStyles.Right;
        VersionLabel.Margin = new Padding(0);

        var credits = NewStack(CreditLabel, VersionLabel);
        credits.Anchor = AnchorStyles.Left;

        LogoPicture.Image = AppIcons.CompanyLogo();
        LogoPicture.SizeMode = PictureBoxSizeMode.Zoom; // keeps the proportions of the logo
        // As high as the disc of the "?" beside it (Daan, 2026-11-22), as wide as the logo's own proportions then need, so
        // the logo fills the box and is exactly that high.
        Image logo = LogoPicture.Image;
        LogoPicture.Size = new Size((int)Math.Round((double)TsDssConverter.Tray.HelpButton.DiscSize * logo.Width / logo.Height), TsDssConverter.Tray.HelpButton.DiscSize);
        LogoPicture.BackColor = Theme.TopBar;
        LogoPicture.Anchor = AnchorStyles.Left;
        LogoPicture.Margin = new Padding(16, 0, 8, 0);
        LogoPicture.Cursor = Cursors.Hand;
        new ToolTip().SetToolTip(LogoPicture, Strings.CompanyName);
        LogoPicture.Click += (sender, e) => LogoClicked(shiftDown: (ModifierKeys & Keys.Shift) == Keys.Shift);

        UserGuideButton.Size = new Size(30, 30);
        UserGuideButton.Anchor = AnchorStyles.Left;
        UserGuideButton.Margin = new Padding(0);
        UserGuideButton.AccessibleName = Strings.UserGuideTooltip;
        new ToolTip().SetToolTip(UserGuideButton, Strings.UserGuideTooltip);
        UserGuideButton.Click += (sender, e) => OpenWebsite(Strings.UserGuideUrl);

        // The right part: one row, every item in the middle of the bar's height, a short grey line between the language
        // and the credit (like the mockup).
        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = 1,
            ColumnCount = 5,
            Padding = new Padding(16, 0, 18, 0),
            Margin = new Padding(0),
            BackColor = Theme.TopBar,
        };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
        {
            right.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }

        var separator = new Panel { Size = new Size(1, 40), BackColor = Theme.Separator, Anchor = AnchorStyles.Left, Margin = new Padding(16, 0, 16, 0) };
        right.Controls.Add(language, 0, 0);
        right.Controls.Add(separator, 1, 0);
        right.Controls.Add(credits, 2, 0);
        right.Controls.Add(LogoPicture, 3, 0);
        right.Controls.Add(UserGuideButton, 4, 0);

        TopBar.Controls.Add(TitleTab); // Fill: the rest of the width (added first, docked last)
        TopBar.Controls.Add(right);
    }

    /// <summary>Two controls one above the other, in a small table that is only as big as they are.</summary>
    private static TableLayoutPanel NewStack(Control top, Control bottom)
    {
        var stack = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0), BackColor = Color.Transparent };
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(top, 0, 0);
        stack.Controls.Add(bottom, 0, 1);
        return stack;
    }

    /// <summary>A drop-down in the Maestro look: flat, light grey, no typing.</summary>
    private static void StyleDropDown(ComboBox box, int width)
    {
        box.FlatStyle = FlatStyle.Flat;
        box.BackColor = Theme.Field;
        box.ForeColor = Theme.Text;
        box.Width = width;
        box.Cursor = Cursors.Hand;
    }

    /// <summary>A white panel on the grey frame: a table with white background and room inside.</summary>
    private TableLayoutPanel NewSection(Padding padding)
    {
        var section = new TableLayoutPanel
        {
            BackColor = Theme.White,
            Padding = padding,
            Margin = new Padding(0, 0, 0, Gap),
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Sections.Add(section);
        return section;
    }

    /// <summary>A title of a panel ("Mappen", "Laatste conversies"): 2 pt bigger, in the blue of the text.</summary>
    private void StyleTitle(Label title, string text)
    {
        title.Text = text;
        title.AutoSize = true;
        title.Font = Theme.CreateTitleFont(Font);
        title.ForeColor = Theme.Text;
    }

    /// <summary>
    /// The folders: the title "Mappen" with "Start with Windows" at its right, then two halves side by side with a thin
    /// line between them. Left, TopSolid: the export folder, the column names of the LI file and those of the LP file (the
    /// material list is a round button of the bottom bar since alpha.4). Right, Duivestein: the batch folder, the label folder and the names of the ten extra description
    /// columns (DESC1 .. DESC10) of the label CSV.
    /// </summary>
    private Control BuildFoldersSection()
    {
        TableLayoutPanel section = NewSection(new Padding(22, 12, 22, 16));
        _foldersSection = section;

        StyleTitle(FoldersTitle, Strings.FoldersTitle);
        FoldersTitle.Anchor = AnchorStyles.Left;
        FoldersTitle.Margin = new Padding(0);

        StartWithWindowsBox.Text = Strings.StartWithWindows;
        StartWithWindowsBox.AutoSize = true;
        StartWithWindowsBox.ForeColor = Theme.Text;
        StartWithWindowsBox.Anchor = AnchorStyles.Right;
        StartWithWindowsBox.Margin = new Padding(0);

        // The "v" before the title folds the panel up to one line (Daan, 2026-11-22): then the three folders are shown,
        // read only, between the title and "Start met Windows" (FoldedFolders), and the list gets the room.
        FoldFoldersButton.Size = new Size(28, 28);
        FoldFoldersButton.Anchor = AnchorStyles.Left;
        FoldFoldersButton.Margin = new Padding(0, 0, 4, 0);
        FoldFoldersButton.Click += (sender, e) => ToggleFolders();
        _foldTip.SetToolTip(FoldFoldersButton, Strings.FoldFolders);
        FoldFoldersButton.AccessibleName = Strings.FoldFolders;

        // Not AutoSize, in the Percent column: it takes the rest of the line. Anchored left and right, NOT top and bottom
        // (a control stretched to the height of an AutoSize row keeps that row as high as it ever was).
        FoldedFolders.Height = 22; // one line of the text, at 100%
        FoldedFolders.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        FoldedFolders.Margin = new Padding(24, 0, 16, 0);
        FoldedFolders.Visible = false;

        var header = new TableLayoutPanel { ColumnCount = 4, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.Controls.Add(FoldFoldersButton, 0, 0);
        header.Controls.Add(FoldersTitle, 1, 0);
        header.Controls.Add(FoldedFolders, 2, 0);
        header.Controls.Add(StartWithWindowsBox, 3, 0);

        TableLayoutPanel topSolidSide = NewHalf();
        int left = 0;
        AddFolderRow(topSolidSide, ref left, ExportCaption, Strings.ExportFolder, ExportFolderBox, _exportWarning);
        AddButtonsAndHints(topSolidSide, ref left,
            (ConfigInfoButton, Strings.ConfigButton, ConfigInfoHint, Strings.ColumnsInfoHint, () => _openFile(_dataFolder.InfoColumnsFile)),
            (ConfigPositionButton, Strings.ConfigButton, ConfigPositionHint, Strings.ColumnsPositionHint, () => _openFile(_dataFolder.PositionColumnsFile)));

        TableLayoutPanel duivesteinSide = NewHalf();
        int right = 0;
        AddFolderRow(duivesteinSide, ref right, BatchCaption, Strings.BatchFolder, BatchFolderBox, _batchWarning);
        AddFolderRow(duivesteinSide, ref right, LabelCaption, Strings.LabelFolder, LabelFolderBox, _labelWarning);
        AddButtonsAndHints(duivesteinSide, ref right,
            (ConfigLabelButton, Strings.ConfigButton, ConfigLabelHint, Strings.ColumnsLabelHint, () => _openFile(_dataFolder.LabelColumnsFile)));

        // The line is as high as the higher half. It is NOT stretched to the row (Dock = Fill): a table sizes a row by the
        // current height of such a control, so a stretched line kept the row as high as it ever was (1.1.0 at 125% / 150%).
        // Its height is set from the two halves instead, which always have the height of their content.
        FolderDivider.Width = 1;
        FolderDivider.Height = 1;
        FolderDivider.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        FolderDivider.BackColor = Theme.GridLine;
        FolderDivider.Margin = new Padding(26, 10, 26, 0);
        void FitDivider() => FolderDivider.Height = Math.Max(1, Math.Max(topSolidSide.Height, duivesteinSide.Height) - FolderDivider.Margin.Top);
        topSolidSide.SizeChanged += (sender, e) => FitDivider();
        duivesteinSide.SizeChanged += (sender, e) => FitDivider();

        // Two halves of equal width; the line only takes its own width and margins.
        var folders = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        folders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        folders.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        folders.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        folders.Controls.Add(topSolidSide, 0, 0);
        folders.Controls.Add(FolderDivider, 1, 0);
        folders.Controls.Add(duivesteinSide, 2, 0);
        _folders = folders;
        folders.SizeChanged += (sender, e) => FitHints();

        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.Controls.Add(header, 0, 0);
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.Controls.Add(folders, 0, 1);
        section.RowCount = 2;
        return section;
    }

    /// <summary>
    /// One half of the folders row (TopSolid or Duivestein): a wide column for the text boxes and a column for the
    /// Browse buttons.
    /// </summary>
    private static TableLayoutPanel NewHalf()
    {
        // Anchored at the top, not Dock = Fill: the lower half keeps its own height (stretched to the row, its last row got
        // all the extra room).
        var half = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0) };
        half.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        half.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return half;
    }

    /// <summary>A caption, a text box with a navy Browse button, and a warning that is only visible when needed.</summary>
    private void AddFolderRow(TableLayoutPanel table, ref int row, Label captionLabel, string caption, TextBox box, Label warning)
    {
        captionLabel.Text = caption;
        captionLabel.AutoSize = true;
        captionLabel.ForeColor = Theme.Text;
        captionLabel.Font = Theme.CreateFont(Font, Font.SizeInPoints, FontStyle.Underline); // underlined (Daan, 2026-11-22)
        captionLabel.Margin = new Padding(0, 10, 0, 5);
        AddFullRow(table, ref row, captionLabel);

        // The text box sits in a flat grey field that is as high as the Browse button, so both line up in one row.
        box.ForeColor = Theme.Text;
        var frame = new TextBoxFrame(box)
        {
            Height = FieldHeight,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, // wide, and in the middle of the row's height
            Margin = new Padding(0, 0, 10, 0),
        };

        var browse = new RoundedButton { Text = Strings.Browse, ShowFolderIcon = true, Margin = new Padding(0) };
        Theme.StyleNavyButton(browse);
        browse.Size = new Size(Math.Max(BrowseMinimumWidth, TextWidthAt100(browse.Text, Font) + 18 + 8 + 28), FieldHeight);

        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(frame, 0, row);
        table.Controls.Add(browse, 1, row);
        row++;

        warning.Text = Strings.FolderNotReachable;
        warning.ForeColor = Theme.Error;
        warning.AutoSize = true;
        warning.Visible = false; // a row with an invisible control takes no space
        warning.Margin = new Padding(0, 3, 0, 0);
        AddFullRow(table, ref row, warning);

        browse.Click += (sender, e) => BrowseForFolder(box, warning);
        box.Leave += (sender, e) => CheckFolder(box, warning);
    }

    /// <summary>
    /// The grey buttons under a folder, one under the other, each with a short explanation at its RIGHT (like the
    /// mockup): Config (opens a file with column names). They sit in one small table, so the
    /// hints start at the same place; a hint wraps onto more lines when it does not fit (see FitHints).
    /// </summary>
    private void AddButtonsAndHints(TableLayoutPanel half, ref int row, params (RoundedButton Button, string Text, Label Hint, string HintText, Action Open)[] lines)
    {
        // Both columns AutoSize: a fixed-size control in a Percent column of an AutoSize table is shrunk (seen in 1.0.4
        // and 1.1.0). The hints get their size from the window (FitHints), the table follows.
        var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 12, 0, 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var buttons = new List<Control>();
        for (int i = 0; i < lines.Length; i++)
        {
            var (button, text, hint, hintText, open) = lines[i];
            button.Text = text;
            Theme.StyleGreyButton(button);
            button.Size = new Size(Math.Max(GreyButtonMinimumWidth, TextWidthAt100(text, Font) + 28), GreyButtonHeight);
            button.Anchor = AnchorStyles.Left;
            button.Margin = new Padding(0, i == 0 ? 0 : 6, 14, 0);
            button.Click += (sender, e) => open();
            buttons.Add(button);

            hint.Text = hintText;
            hint.ForeColor = Theme.TextSoft;
            hint.AutoSize = false;
            hint.Anchor = AnchorStyles.Left; // in the middle of the row's height
            hint.Margin = new Padding(0, i == 0 ? 0 : 6, 0, 0);
            hint.FontChanged += (sender, e) => FitHints(); // the window moved to a screen with another scaling
            button.SizeChanged += (sender, e) => FitHints();

            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(button, 0, i);
            table.Controls.Add(hint, 1, i);
        }

        table.RowCount = lines.Length;
        foreach (var line in lines)
        {
            _wrappedHints.Add((line.Hint, buttons.ToArray()));
        }

        AddFullRow(half, ref row, table);
    }

    /// <summary>
    /// Gives every hint its size. The width of a half is taken from the whole folders row (which gets its width from the
    /// window), never from the half itself: a half is as wide as its widest hint, so measuring the half would keep a hint
    /// that was wide once wide forever. Why not let the table wrap it (an AutoSize label anchored Left | Right)? While
    /// Windows scales the window to 125% or 150% it is laid out for a moment at a much narrower width; the table measured
    /// the hints there and kept rows of ten lines high (Daan's screenshots of 1.1.0).
    /// </summary>
    private void FitHints()
    {
        if (_folders == null)
        {
            return; // still building the window: fitted when the folders row gets its width
        }

        int half = (_folders.ClientSize.Width - FolderDivider.Width - FolderDivider.Margin.Horizontal) / 2;
        foreach (var (hint, buttons) in _wrappedHints)
        {
            int buttonColumn = buttons.Max(button => button.Width + button.Margin.Horizontal);
            int width = half - buttonColumn - hint.Margin.Horizontal;
            if (width <= 0)
            {
                continue; // no width yet: fitted again when the row gets one
            }

            var size = new Size(width, hint.GetPreferredSize(new Size(width, 0)).Height);
            if (hint.Size != size) // also stops the loop: a new height resizes the row, which calls this again
            {
                hint.Size = size;
            }
        }
    }

    /// <summary>
    /// The row below the folders: the Status tile, the Last conversion tile and then the label zero point (DSSClient's
    /// "Oriëntatie" / "Gedraaid", with the little drawing of the sheet), which takes the rest of the width (this order
    /// since 2.0.0-alpha.5, Daan; the zero point was at the left before).
    /// </summary>
    private Control BuildMiddleRow()
    {
        TableLayoutPanel section = NewSection(new Padding(22, 12, 22, 14));
        section.Margin = new Padding(0);
        section.ColumnCount = 2;
        section.ColumnStyles.Clear();
        section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _zeroPointSection = section;

        StyleTitle(ZeroPointTitle, Strings.LabelZeroPoint);
        ZeroPointTitle.Margin = new Padding(0, 0, 0, 10);

        ReferenceCornerBox.DropDownStyle = ComboBoxStyle.DropDownList; // choose from the list, no typing
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.BottomLeft, Strings.ReferenceCornerBottomLeft));
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.TopLeft, Strings.ReferenceCornerTopLeft));
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.BottomRight, Strings.ReferenceCornerBottomRight));
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.TopRight, Strings.ReferenceCornerTopRight));
        StyleDropDown(ReferenceCornerBox, 220);
        ReferenceCornerBox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        ReferenceCornerBox.Margin = new Padding(0, 0, 0, 12);
        // The little schematic follows the drop-down at once, even before Save (LoadFromSettings sets the first value).
        ReferenceCornerBox.SelectedIndexChanged += (sender, e) =>
            CornerPreview.SelectedCorner = ((ReferenceCornerItem)ReferenceCornerBox.SelectedItem!).Corner;

        PlanRotatedBox.Text = Strings.PlanRotated;
        PlanRotatedBox.AutoSize = true;
        PlanRotatedBox.ForeColor = Theme.Text;
        PlanRotatedBox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        PlanRotatedBox.Margin = new Padding(0);

        CornerPreview.Size = new Size(100, 76);
        CornerPreview.Margin = new Padding(36, 0, 0, 0);
        CornerPreview.Anchor = AnchorStyles.Left | AnchorStyles.Top; // in line with the drop-down's row

        // Title over both columns; then the drop-down above the checkbox at the left, the schematic at the right of them.
        // All AutoSize (no Percent column: that shrank the drop-down to ~75 px in 1.0.4).
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.Controls.Add(ZeroPointTitle, 0, 0);
        section.SetColumnSpan(ZeroPointTitle, 2);
        section.Controls.Add(ReferenceCornerBox, 0, 1);
        section.Controls.Add(PlanRotatedBox, 0, 2);
        section.Controls.Add(CornerPreview, 1, 1);
        section.SetRowSpan(CornerPreview, 2);
        section.RowCount = 3;

        StatusTile.Caption = Strings.StatusTitle;
        LastConversionTile.Caption = Strings.LastConversionTitle;
        LastConversionTile.Line2Lines = 3; // the message wraps instead of ending in "…" after a few words (Daan)
        foreach (InfoTile tile in new[] { StatusTile, LastConversionTile })
        {
            tile.Size = new Size(TileWidth, 120);
            tile.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            tile.Margin = new Padding(0, 0, Gap, 0);
        }

        // The tiles keep their width; the zero point takes the rest (it is AutoSize itself, so it may sit in the Percent
        // column).
        var row = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Gap), BackColor = Theme.Frame };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.Controls.Add(StatusTile, 0, 0);
        row.Controls.Add(LastConversionTile, 1, 0);
        row.Controls.Add(section, 2, 0);
        _middleRow = row;
        row.SizeChanged += (sender, e) => FitMiddleRow();
        ZeroPointTitle.FontChanged += (sender, e) => FitMiddleRow();
        return row;
    }

    /// <summary>
    /// The widths of the middle row: Status and Last conversion each a quarter, the zero point the half (Daan, 2.0.0).
    /// The tiles get their width here, in pixels, from the row's width: not from Percent columns, because a fixed-size
    /// control in a Percent column of an AutoSize table is shrunk (§10); the zero point takes the rest. A tile is never
    /// narrower than TileWidth (230 at 100%). Then the zero-point title wraps onto a second line when the panel is too
    /// narrow (the French one is long), with a width computed from the whole row (see FitHints for why not from the
    /// panel); the tiles are as high as the panel.
    /// </summary>
    private void FitMiddleRow()
    {
        if (_middleRow == null || _zeroPointSection == null)
        {
            return;
        }

        int gaps = StatusTile.Margin.Horizontal + LastConversionTile.Margin.Horizontal;
        int quarter = Math.Max(LogicalToDeviceUnits(TileWidth), (_middleRow.ClientSize.Width - gaps) / 4);
        foreach (InfoTile tile in new[] { StatusTile, LastConversionTile })
        {
            if (tile.Width != quarter)
            {
                tile.Width = quarter;
            }
        }

        int tiles = StatusTile.Width + StatusTile.Margin.Horizontal + LastConversionTile.Width + LastConversionTile.Margin.Horizontal;
        int width = _middleRow.ClientSize.Width - tiles - _zeroPointSection.Padding.Horizontal;
        if (width > 0 && ZeroPointTitle.MaximumSize.Width != width)
        {
            ZeroPointTitle.MaximumSize = new Size(width, 0);
        }

        int panel = _zeroPointSection.GetPreferredSize(new Size(Math.Max(1, _middleRow.ClientSize.Width - tiles), 0)).Height;
        foreach (InfoTile tile in new[] { StatusTile, LastConversionTile })
        {
            int height = Math.Max(tile.HeightNeeded(), panel);
            if (tile.Height != height)
            {
                tile.Height = height;
            }
        }
    }

    /// <summary>The list of the last conversions: the title with "Clear conversion list" at the right, then the list.</summary>
    private Control BuildHistorySection()
    {
        TableLayoutPanel section = NewSection(new Padding(0));
        section.AutoSize = false;     // it takes the rest of the window's height
        section.Margin = new Padding(0);

        StyleTitle(HistoryTitle, Strings.HistoryTitle);
        HistoryTitle.Anchor = AnchorStyles.Left;
        HistoryTitle.Margin = new Padding(0);

        // Only empties the list in this window: the log files are written by the tray and are not touched.
        ClearHistoryButton.Text = Strings.ClearHistoryButton;
        Theme.StyleGreyButton(ClearHistoryButton);
        ClearHistoryButton.Size = new Size(Math.Max(GreyButtonMinimumWidth, TextWidthAt100(ClearHistoryButton.Text, Font) + 28), GreyButtonHeight);
        ClearHistoryButton.Anchor = AnchorStyles.Right;
        ClearHistoryButton.Margin = new Padding(0);
        ClearHistoryButton.Click += (sender, e) => _history.Clear();

        var header = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(22, 10, 14, 10) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.Controls.Add(HistoryTitle, 0, 0);
        header.Controls.Add(ClearHistoryButton, 1, 0);
        _historyHeader = header;

        BuildHistoryList();

        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.Controls.Add(header, 0, 0);
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        section.Controls.Add(HistoryList, 0, 1);
        section.RowCount = 2;
        return section;
    }

    private void BuildHistoryList()
    {
        HistoryList.View = View.Details;
        HistoryList.FullRowSelect = true;
        HistoryList.HideSelection = false;
        HistoryList.BorderStyle = BorderStyle.None;
        HistoryList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        HistoryList.Dock = DockStyle.Fill;
        HistoryList.MinimumSize = new Size(0, MinimumHistoryHeight); // never squeezed out of view
        HistoryList.Margin = new Padding(0);
        HistoryList.BackColor = Theme.White;
        HistoryList.ForeColor = Theme.TableText;
        HistoryList.Columns.Add(Strings.HistoryTime, HistoryColumnWidths[0]);
        HistoryList.Columns.Add(Strings.HistoryProject, HistoryColumnWidths[1]);
        HistoryList.Columns.Add(Strings.HistoryResult, HistoryColumnWidths[2]);
        HistoryList.Columns.Add(Strings.HistoryMessage, 300);
        HistoryList.SizeChanged += (sender, e) => HistoryList.Columns[3].Width = -2; // last column takes the rest

        _historyEmpty.Text = Strings.HistoryEmpty;
        _historyEmpty.AutoSize = true;
        _historyEmpty.Font = new Font(Font, FontStyle.Italic);
        _historyEmpty.ForeColor = Theme.TextSoft;
        _historyEmpty.BackColor = Theme.White;
        _historyEmpty.Location = new Point(12, 40);
        HistoryList.Controls.Add(_historyEmpty);
    }

    /// <summary>
    /// The blue bar at the bottom: scan now, retry the failed projects, pause / resume and open the log folder as round
    /// buttons with their name below them; Cancel and Save at the right, on the grey.
    /// </summary>
    private void BuildBottomBar()
    {
        ScanNowButton.Text = Strings.MenuScanNow;
        ScanNowButton.Click += (sender, e) => _scanNow();

        RetryFailedButton.Text = Strings.RetryFailedButton;
        RetryFailedButton.Click += (sender, e) => _retryFailed();

        PauseButton.Text = Strings.MenuPause;
        PauseButton.Click += (sender, e) =>
        {
            _togglePause();
            RefreshStatus();
        };

        OpenLogButton.Text = Strings.OpenLogButton;
        OpenLogButton.Click += (sender, e) => _openFolder(_dataFolder.LogFolder);

        // At the right end of the band (Daan, 2026-11-22: it replaces the grey "Open materiaaltabel" button).
        MaterialsButton.Text = Strings.MaterialsButton;
        MaterialsButton.Click += (sender, e) => _openFile(_dataFolder.MaterialsFile);

        SavePill.Text = Strings.Save;
        CancelPill.Text = Strings.Cancel;
        SavePill.Click += (sender, e) => SaveAndHide();
        CancelPill.Click += (sender, e) => Hide();
        AcceptButton = SavePill;
        CancelButton = CancelPill;

        BottomBar = new BottomBar(new[] { ScanNowButton, RetryFailedButton, PauseButton, OpenLogButton }, MaterialsButton, CancelPill, SavePill)
        {
            Dock = DockStyle.Bottom,
            Height = BottomBar.BarHeight,
        };
    }

    /// <summary>
    /// A click on the logo opens the website. Shift+click opens settings.json in Notepad instead (Daan, 2026-11-08): a
    /// hidden way in for whoever maintains the program, because the advanced settings are only in that file. It is not
    /// shown anywhere on purpose. The tray uses a change in the file at once and keeps it when Save is pressed.
    /// </summary>
    internal void LogoClicked(bool shiftDown)
    {
        if (shiftDown)
        {
            _openFile(_dataFolder.SettingsFile); // made again first if someone deleted it
        }
        else
        {
            OpenWebsite(Strings.CompanyUrl);
        }
    }

    private void OpenWebsite(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception || error is IOException)
        {
            MessageBox.Show(this, Strings.CannotOpenLink(error.Message), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// The width of a text at 100%, for a size written at 100% (the window scaling makes it bigger later, like every other
    /// size). MeasureText measures on the screen, so on a 150% screen the text comes back 1.5 times as wide; it is brought
    /// back to 100% here, otherwise the window scaling made these buttons 1.5 times too wide (seen up to 1.1.0).
    /// </summary>
    private static int TextWidthAt100(string text, Font font)
    {
        using Graphics screen = Graphics.FromHwnd(IntPtr.Zero);
        return (int)Math.Ceiling(TextRenderer.MeasureText(screen, text, font).Width * 96f / screen.DpiX);
    }

    private static void AddFullRow(TableLayoutPanel table, ref int row, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
        row++;
    }

    // ------------------------------------------------------------------ filling and reading

    /// <summary>Fills the window with the settings in use and the REAL state of "start with Windows".</summary>
    internal void LoadFromSettings()
    {
        ShowValues(_getSettings(), SafeIsStartWithWindows());
        RefreshHistory();
        RefreshStatus();
    }

    /// <summary>
    /// Shows values that were typed in an earlier copy of this window but not saved yet (the window is built again in
    /// another language: what the user typed stays). Call after the window is shown.
    /// </summary>
    internal void ShowEdits(AppSettings typed, bool startWithWindows)
    {
        ShowValues(typed, startWithWindows);
    }

    private void ShowValues(AppSettings settings, bool startWithWindows)
    {
        StartWithWindowsBox.Checked = startWithWindows;
        LanguageBox.SelectedIndex = (int)Localizer.FromCode(settings.Language); // the list is in the order of the enum
        ExportFolderBox.Text = settings.TopSolidExportPath;
        BatchFolderBox.Text = settings.BatchFolder;
        LabelFolderBox.Text = settings.LabelFolder;
        ReferenceCorner corner = ReferenceCornerMapping.FromFlips(settings.FlipX, settings.FlipY);
        ReferenceCornerBox.SelectedIndex = (int)corner; // items added in enum order
        CornerPreview.SelectedCorner = corner; // set directly: SelectedIndexChanged only fires when the index actually moves
        PlanRotatedBox.Checked = settings.PlanRotated;
        SetFolded(_getSettings().FoldersFolded);
        CheckAllFolders();
    }

    /// <summary>The user chose a language in the box: when it is another one, the tray saves it and switches at once.</summary>
    internal void LanguageChosen()
    {
        string code = Localizer.ToCode(((LanguageItem)LanguageBox.SelectedItem!).Language);
        if (code != Localizer.ToCode(Localizer.Current))
        {
            _changeLanguage(code);
        }
    }

    // ------------------------------------------------------------------ folding the folders panel

    /// <summary>The "v" / ">" was clicked: fold or open the folders panel, and save that at once.</summary>
    internal void ToggleFolders()
    {
        SetFolded(!FoldersFolded);
        _saveFolded(FoldersFolded);
    }

    /// <summary>
    /// Folded: only the title line of the folders panel is left, with the three folders on it (read only); the list of
    /// conversions gets the room (the window keeps its size). Open: the two halves as usual.
    /// </summary>
    private void SetFolded(bool folded)
    {
        FoldersFolded = folded;
        FoldFoldersButton.Folded = folded;
        string tip = folded ? Strings.UnfoldFolders : Strings.FoldFolders;
        _foldTip.SetToolTip(FoldFoldersButton, tip);
        FoldFoldersButton.AccessibleName = tip;
        UpdateFolderSummary();
        FoldedFolders.Visible = folded;
        if (_folders != null)
        {
            _folders.Visible = !folded;
        }

        if (_body != null && IsHandleCreated)
        {
            UpdateScrollSize();
        }
    }

    /// <summary>The three folders of the folded line, a folder that is not reachable in red.</summary>
    private void UpdateFolderSummary()
    {
        FoldedFolders.Show(new[]
        {
            (Strings.FoldedExport, ExportFolderBox.Text.Trim(), !_unreachable.Contains(ExportFolderBox)),
            (Strings.FoldedBatch, BatchFolderBox.Text.Trim(), !_unreachable.Contains(BatchFolderBox)),
            (Strings.FoldedLabels, LabelFolderBox.Text.Trim(), !_unreachable.Contains(LabelFolderBox)),
        });
    }

    /// <summary>
    /// The settings as typed in the window. It starts from a copy of the settings in use,
    /// so the advanced settings (only in settings.json) are kept.
    /// </summary>
    internal AppSettings ReadSettings()
    {
        AppSettings settings = _getSettings().Clone();

        settings.TopSolidExportPath = ExportFolderBox.Text.Trim();
        settings.BatchFolder = BatchFolderBox.Text.Trim();
        settings.LabelFolder = LabelFolderBox.Text.Trim();
        ReferenceCorner corner = ((ReferenceCornerItem)ReferenceCornerBox.SelectedItem!).Corner;
        (settings.FlipX, settings.FlipY) = ReferenceCornerMapping.ToFlips(corner);
        settings.PlanRotated = PlanRotatedBox.Checked;
        settings.FoldersFolded = FoldersFolded;
        settings.Language = Localizer.ToCode(((LanguageItem)LanguageBox.SelectedItem!).Language);
        return settings;
    }

    private bool SafeIsStartWithWindows()
    {
        try
        {
            return _startup.IsEnabled();
        }
        catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is IOException)
        {
            return false; // the registry cannot be read: show "off" instead of crashing
        }
    }

    // ------------------------------------------------------------------ the Status tile and the pause button

    /// <summary>
    /// Shows what the program is doing in the Status tile (the same as the tray icon: error, busy, paused or OK) and the
    /// right name on the pause button. The tray calls this whenever its icon changes; it must be called on the window's
    /// thread.
    /// </summary>
    internal void RefreshStatus()
    {
        bool paused = _isPaused();
        PauseButton.Text = paused ? Strings.MenuResume : Strings.MenuPause;
        PauseButton.Glyph = paused ? BarGlyph.Resume : BarGlyph.Pause;
        PauseButton.Invalidate();

        AppSettings settings = _getSettings();
        switch (_getState())
        {
            case TrayState.Error:
                ShowStatus(Strings.StatusError, Theme.Error, Strings.StatusErrorLine1, Strings.StatusErrorLine2);
                break;
            case TrayState.Busy:
                ShowStatus(Strings.StatusBusy, Theme.TileValue, Strings.StatusBusyLine, Strings.StatusWatching(settings.TopSolidExportPath));
                break;
            case TrayState.Paused:
                ShowStatus(Strings.StatusPaused, Theme.TextSoft, Strings.StatusPausedLine1, Strings.StatusPausedLine2);
                break;
            default:
                ShowStatus(Strings.StatusActive, Theme.TileValue, Strings.StatusWatching(settings.TopSolidExportPath), Strings.StatusRescan(settings.RescanSeconds));
                break;
        }
    }

    /// <summary>
    /// The countdown ring of the Status tile (Daan, 2.0.0): the seconds until the watcher scans by itself again, out of
    /// its whole wait (RescanSeconds, or 2 s while a project waits for its files). No ring while paused (nothing is
    /// scanned then) or while a scan is running. A file that appears or "Nu scannen" scans earlier: the ring starts again.
    /// </summary>
    internal void UpdateCountdown(DateTime now)
    {
        var next = _isPaused() ? null : _nextScan();
        StatusTile.Countdown = next is { } scan
            ? (Math.Max(0, (scan.At - now).TotalSeconds), scan.Interval.TotalSeconds)
            : null;
    }

    private void ShowStatus(string value, Color colour, string line1, string line2)
    {
        StatusTile.Value = value;
        StatusTile.ValueColor = colour;
        StatusTile.Line1 = line1;
        StatusTile.Line2 = line2;
    }

    // ------------------------------------------------------------------ save

    private void SaveAndHide()
    {
        AppSettings settings = ReadSettings();

        if (settings.TopSolidExportPath == "" || settings.BatchFolder == "" || settings.LabelFolder == "")
        {
            MessageBox.Show(this, Strings.FillAllFolders, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // A folder that is not reachable is only warned about (next to the box), never blocked.
        try
        {
            _save(settings);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            MessageBox.Show(this, Strings.SaveFailed(error.Message), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            _startup.SetEnabled(StartWithWindowsBox.Checked);
        }
        catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is IOException)
        {
            // The settings are saved; only this checkbox failed. Tell the user and keep the window open.
            MessageBox.Show(this, Strings.StartWithWindowsFailed(error.Message), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Hide();
    }

    // ------------------------------------------------------------------ folder check

    private void BrowseForFolder(TextBox box, Label warning)
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = box.Text,
            UseDescriptionForTitle = true,
            Description = Strings.Browse.TrimEnd('…'),
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            box.Text = dialog.SelectedPath;
            CheckFolder(box, warning);
        }
    }

    /// <summary>Remembers the answer of the folder check, for the folded line (a folder that is not reachable is red there).</summary>
    internal void MarkReachable(TextBox box, bool reachable)
    {
        if (reachable)
        {
            _unreachable.Remove(box);
        }
        else
        {
            _unreachable.Add(box);
        }

        UpdateFolderSummary();
    }

    private void CheckAllFolders()
    {
        CheckFolder(ExportFolderBox, _exportWarning);
        CheckFolder(BatchFolderBox, _batchWarning);
        CheckFolder(LabelFolderBox, _labelWarning);
    }

    /// <summary>
    /// Checks in the background whether the folder exists. A network drive that is not connected can make
    /// Directory.Exists wait for seconds, and the window must not freeze meanwhile.
    /// </summary>
    private void CheckFolder(TextBox box, Label warning)
    {
        string path = box.Text.Trim();

        Task.Run(() => Directory.Exists(path)).ContinueWith(task =>
        {
            if (IsDisposed || !IsHandleCreated || !task.IsCompletedSuccessfully)
            {
                return;
            }

            bool exists = task.Result;
            BeginInvoke(() =>
            {
                // The user may have typed something else in the meantime: then this answer is outdated.
                if (box.Text.Trim() == path)
                {
                    warning.Visible = !exists;
                    MarkReachable(box, exists);
                }
            });
        });
    }

    // ------------------------------------------------------------------ history

    private void OnHistoryChanged()
    {
        // Comes from the thread that did the conversion.
        if (!IsDisposed && IsHandleCreated)
        {
            BeginInvoke(RefreshHistory);
        }
    }

    internal void RefreshHistory()
    {
        HistoryList.BeginUpdate();
        HistoryList.Items.Clear();
        List<HistoryEntry> entries = _history.GetEntries().ToList();

        foreach (HistoryEntry entry in entries)
        {
            var item = new ListViewItem(entry.Time.ToString("dd/MM HH:mm"));
            item.SubItems.Add(entry.Project);
            item.SubItems.Add(entry.Success ? Strings.ResultOk : Strings.ResultError);
            item.SubItems.Add(OneLine(entry.Message));

            if (!entry.Success)
            {
                item.ForeColor = Theme.Error;
            }

            HistoryList.Items.Add(item);
        }

        _historyEmpty.Visible = HistoryList.Items.Count == 0;
        HistoryList.EndUpdate();

        // The "Last conversion" tile: the newest line of the list (empty again after "Clear conversion list").
        HistoryEntry? newest = entries.FirstOrDefault();
        LastConversionTile.Value = newest == null ? Strings.NothingYetValue : Strings.LastConversionValue(newest.Time, newest.Success);
        LastConversionTile.ValueColor = newest == null || newest.Success ? Theme.TileValue : Theme.Error;
        // An error without a project (e.g. a settings.json with a mistake) has "-" as its project: the tile shows no
        // line for it and the message moves up (Daan, 2026-11-22).
        LastConversionTile.Line1 = newest == null ? Strings.HistoryEmpty : newest.Project == Strings.NoProject ? "" : newest.Project;
        LastConversionTile.Line2 = newest == null ? "" : OneLine(newest.Message);
    }

    /// <summary>An error report has several lines; the list shows them on one line.</summary>
    private static string OneLine(string text)
    {
        return text.Replace("\r\n", " | ").Replace("\n", " | ");
    }

    // ------------------------------------------------------------------ size and scaling

    /// <summary>
    /// Makes sure the window is not bigger than the part of the screen that is free (not under the taskbar)
    /// and puts it in the middle of it. On a small screen at a high scaling the window is then shorter than the
    /// content, and the panels scroll (the list of conversions keeps its minimum size).
    /// </summary>
    /// <param name="area">The free part of the screen, in pixels.</param>
    internal void FitToArea(Rectangle area)
    {
        int width = Math.Min(Width, area.Width);
        int height = Math.Min(Height, area.Height);
        Size = new Size(width, height);
        Location = new Point(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2);
    }

    /// <summary>
    /// The height that all the panels need at the current width, with the list at its minimum height. Added up by hand
    /// (not the body's own preferred size): a list that fills the rest of the height would be measured at whatever height
    /// it has now, so the window would keep any height it once had.
    /// </summary>
    /// <param name="asIfOpen">True: as if the folders panel were open, also when it is folded. The window opens at that
    /// height, so folding gives the list more room instead of making the window smaller (Daan, 2026-11-22).</param>
    private int ContentHeight(bool asIfOpen = false)
    {
        int width = Scroller.ClientSize.Width - _body!.Padding.Horizontal;
        int Needed(Control control) => control.GetPreferredSize(new Size(width, 0)).Height + control.Margin.Vertical;
        int height = _body.Padding.Vertical + Needed(_foldersSection!) + Needed(_middleRow!) + Needed(_historyHeader!) + HistoryList.MinimumSize.Height;
        if (asIfOpen && FoldersFolded && _folders != null)
        {
            height += Needed(_folders); // the halves are hidden while folded: their height is added by hand
        }

        return height;
    }

    /// <summary>The height that all the panels need, so the scroll panel knows when to show a scroll bar.</summary>
    internal void UpdateScrollSize()
    {
        Scroller.AutoScrollMinSize = new Size(0, ContentHeight());
    }

    /// <summary>
    /// Gives the window the height that its content needs, so the list of conversions is visible right away
    /// without having to enlarge the window. This works at any screen scaling and in any language, because it
    /// measures the real content. If the screen is too small the window is as big as the screen allows
    /// and the panels scroll (see <see cref="FitToArea"/>).
    /// </summary>
    /// <param name="area">The free part of the screen, in pixels.</param>
    internal void SizeToContent(Rectangle area)
    {
        FitHints();
        FitMiddleRow();
        int contentHeight = ContentHeight(asIfOpen: true);

        // The warnings "folder not reachable" are checked in the background and appear later.
        // Their room is reserved now, otherwise the window would need a scroll bar a moment after it opened.
        foreach (Control warning in new Control[] { _exportWarning, _batchWarning, _labelWarning })
        {
            if (!warning.Visible)
            {
                contentHeight += warning.GetPreferredSize(Size.Empty).Height + warning.Margin.Vertical;
            }
        }

        ClientSize = new Size(ClientSize.Width, TopBar.Height + BottomBar.Height + contentHeight + LogicalToDeviceUnits(ExtraHistoryHeight));
        FitToArea(area);
        UpdateScrollSize();
    }

    /// <summary>
    /// Scales the widths of the list columns and the height of its rows to the screen scaling (a ListView does not do
    /// that itself). The row height comes from a small picture list: a ListView makes its rows as high as its pictures.
    /// </summary>
    private void ScaleHistoryColumns()
    {
        for (int i = 0; i < HistoryColumnWidths.Length; i++)
        {
            HistoryList.Columns[i].Width = LogicalToDeviceUnits(HistoryColumnWidths[i]);
        }

        HistoryList.Columns[3].Width = -2; // the last column takes the rest

        ImageList? old = HistoryList.SmallImageList;
        HistoryList.SmallImageList = new ImageList { ImageSize = new Size(1, LogicalToDeviceUnits(RowHeight)) };
        old?.Dispose();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e); // the scaling of the whole window has been applied by now
        ScaleHistoryColumns();
        SizeToContent(Screen.FromControl(this).WorkingArea);
    }

    /// <summary>The window was moved to a screen with another scaling: WinForms scales the window, we the columns.</summary>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ScaleHistoryColumns();
        UpdateScrollSize();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_body != null && IsHandleCreated)
        {
            UpdateScrollSize(); // a narrower window can need more height (the hints wrap)
        }
    }

    // ------------------------------------------------------------------ hide instead of close

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            LoadFromSettings(); // always show the current situation, also after a Cancel
            _windowOpened();
            RefreshStatus();    // opening the window clears the red state of the tray
            UpdateCountdown(DateTime.Now);
            _countdownTimer.Start();
        }
        else
        {
            _countdownTimer.Stop(); // a hidden window does not need the ring
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The X only hides the window. Windows shutdown and the tray's Exit menu can still really close it.
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _history.Changed -= OnHistoryChanged;
            HistoryList.SmallImageList?.Dispose();
            _countdownTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
