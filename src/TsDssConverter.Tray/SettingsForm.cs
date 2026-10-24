using TsDssConverter.Core;

// Core also has a class Label (a part on a sheet). In this file "Label" always means the text control.
using Label = System.Windows.Forms.Label;

namespace TsDssConverter.Tray;

/// <summary>
/// The settings window (the only window of the program): the six settings, Save / Cancel,
/// and below them a read-only list of the last 20 conversions.
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

    // The controls we need to read or fill (internal: the tests check them)
    internal readonly CheckBox StartWithWindowsBox = new FluentCheckBox();
    internal readonly TextBox ExportFolderBox = new();
    internal readonly TextBox BatchFolderBox = new();
    internal readonly TextBox LabelFolderBox = new();
    internal readonly ComboBox ReferenceCornerBox = new();       // "Oriëntatie" in DSSClient: sets FlipX / FlipY together
    internal readonly CheckBox PlanRotatedBox = new FluentCheckBox(); // "Gedraaid" in DSSClient: the batch XML's Plan Rotation
    internal readonly ReferenceCornerPreview CornerPreview = new(); // small schematic that follows ReferenceCornerBox
    internal readonly ListView HistoryList = new();
    private readonly Label _exportWarning = new();
    private readonly Label _batchWarning = new();
    private readonly Label _labelWarning = new();
    private readonly Label _historyEmpty = new();
    internal readonly Label ConfigInfoHint = new();
    internal readonly Label ConfigPositionHint = new();
    internal readonly Label ConfigLabelHint = new();
    internal readonly Label MaterialsHint = new();

    // The banner picture is 540 x 84 pixels. It is shown 1.2 times as big.
    private const int BannerWidth = 648;
    private const int BannerHeight = 101;

    // The window is wider than the banner: the TopSolid and Duivestein folders sit side by side (at 100%).
    private const int WindowWidth = 940;

    // Parts of the layout that the tests look at
    internal readonly RoundedButton ConfigInfoButton = new();      // opens columns-li.txt (below the export folder)
    internal readonly RoundedButton ConfigPositionButton = new();  // opens columns-lp.txt (below the LI one)
    internal readonly RoundedButton ConfigLabelButton = new();     // opens columns-label.txt (below the label folder)
    internal readonly RoundedButton OpenMaterialsButton = new();   // opens materials.csv (below the two Config buttons of the export folder)
    internal readonly RoundedButton RetryFailedButton = new();     // puts the failed projects back in the export folder (left of the Open log button)
    internal readonly RoundedButton OpenLogButton = new();         // opens the log folder (next to "Last conversions")
    internal readonly PictureBox BannerPicture = new();
    internal readonly Label ExportCaption = new();
    internal readonly Label BatchCaption = new();
    internal readonly Label LabelCaption = new();
    internal readonly Label ZeroPointTitle = new();
    internal readonly List<Panel> Dividers = new();
    internal readonly Label LanguageCaption = new();
    internal readonly ComboBox LanguageBox = new();
    internal readonly Label CreditLabel = new();     // "Dev.: Daan Verhoost", at the top right below the banner
    internal readonly Label VersionLabel = new();    // "App version: 1.0.1", right of the credit on the same line
    internal readonly PictureBox LogoPicture = new(); // the small ROGIERS logo at the bottom left (a link to the website)
    internal readonly Panel FolderDivider = new();   // the vertical line between the TopSolid and the Duivestein folders
    private TableLayoutPanel? _folders;              // the row with the two halves of folders (see FitHints)
    private readonly List<(Label Hint, Control Button)> _wrappedHints = new(); // the hints that wrap beside a button
    internal readonly Panel Scroller = new();        // holds all the settings; scrolls if the window is too small

    // The list of conversions is never smaller than this (in pixels at 100% scaling): it must always be visible.
    private const int MinimumHistoryHeight = 120;

    // When the window opens, the list gets this much more than its minimum (at 100%).
    private const int ExtraHistoryHeight = 10;

    // All buttons (Browse, Save, Cancel) have exactly this size (at 100%), so they line up and look the same.
    // Not AutoSize: that made Save (bold text) and Cancel different in size, and all of them too big.
    private static readonly Size ButtonSize = new(100, 28);

    // The company logo in the footer (at 100%). The logo is a little wider than high; the picture keeps its proportions.
    private static readonly Size LogoSize = new(35, 32);

    // Widths of the columns Time, Project and Result at 100%. The Message column takes the rest.
    // (A ListView does not scale its columns by itself, so they are scaled in ScaleHistoryColumns.)
    private static readonly int[] HistoryColumnWidths = { 100, 150, 95 };

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
    public SettingsForm(
        Func<AppSettings> getSettings, Action<AppSettings> save, Action windowOpened,
        ConversionHistory history, StartWithWindows startup,
        AppDataFolder dataFolder, Action<string> openFile, Action<string> openFolder, Action retryFailed)
    {
        _getSettings = getSettings;
        _save = save;
        _windowOpened = windowOpened;
        _history = history;
        _startup = startup;
        _dataFolder = dataFolder;
        _openFile = openFile;
        _openFolder = openFolder;
        _retryFailed = retryFailed;

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
        // between SuspendLayout and ResumeLayout (see the constructor): without that the text became bigger on a
        // 150% screen but the window and the banner stayed small, and the list was pushed out of view.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.CreateBodyFont(); // the Windows 11 font; all controls below inherit it
        Text = Strings.WindowTitle;
        Icon = AppIcons.ForWindow();
        BackColor = Theme.White;
        ForeColor = Theme.Navy;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(WindowWidth, 780);
        MinimumSize = new Size(BannerWidth + 30, 500); // smaller than the content needs: then the settings scroll
        MaximizeBox = false;

        // Top: the banner, on the brand colour (the banner picture has the same background).
        var banner = new Panel { Dock = DockStyle.Top, Height = BannerHeight, BackColor = Theme.Brand };
        BannerPicture.Image = AppIcons.Banner();
        BannerPicture.SizeMode = PictureBoxSizeMode.Zoom;
        BannerPicture.Dock = DockStyle.Left;
        BannerPicture.Width = BannerWidth;
        BannerPicture.BackColor = Theme.Brand;
        banner.Controls.Add(BannerPicture);

        // Below: one table with a row per item.
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 4, 20, 20), // left, top, right, bottom: the room below Save / Cancel is as wide as at the sides
            ColumnCount = 2,
            BackColor = Theme.White,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;

        // 0, 1. The top row: language and "Start with Windows" on the left, the credit, the app version and the
        // company logo on the right.
        var topRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        topRow.Controls.Add(BuildLanguageColumn(), 0, 0);
        topRow.Controls.Add(BuildCredits(), 1, 0);
        AddFullRow(body, ref row, topRow);
        // The divider lines have room above and below them. The neighbouring controls already have 8 pixels of
        // margin on this side, so the first line gets 12 + 8 = 20 above and below; the other line follows a row
        // without a bottom margin and is followed by a title with 8, so it gets 20 above and 12 + 8 = 20 below.
        AddDivider(body, ref row, new Padding(0, 12, 0, 12)); // a line below "Start met Windows"

        // 2, 3, 4. The three folders (the titles are bold), in two halves side by side with a vertical line between.
        // Left, TopSolid: below the export folder the column names of the LI file, those of the LP file and the
        // materials table. Right, Duivestein: the batch folder, the label folder and, below it, the names of the ten
        // extra description columns (DESC1 .. DESC10) in the label CSV.
        TableLayoutPanel topSolidSide = NewHalf();
        int left = 0;
        AddFolderRow(topSolidSide, ref left, ExportCaption, Strings.ExportFolder, ExportFolderBox, _exportWarning);
        AddConfigRow(topSolidSide, ref left, ConfigInfoHint, Strings.ColumnsInfoHint, ConfigInfoButton, () => _openFile(_dataFolder.InfoColumnsFile));
        AddConfigRow(topSolidSide, ref left, ConfigPositionHint, Strings.ColumnsPositionHint, ConfigPositionButton, () => _openFile(_dataFolder.PositionColumnsFile));
        AddMaterialsRow(topSolidSide, ref left);

        TableLayoutPanel duivesteinSide = NewHalf();
        int right = 0;
        AddFolderRow(duivesteinSide, ref right, BatchCaption, Strings.BatchFolder, BatchFolderBox, _batchWarning);
        AddFolderRow(duivesteinSide, ref right, LabelCaption, Strings.LabelFolder, LabelFolderBox, _labelWarning);
        AddConfigRow(duivesteinSide, ref right, ConfigLabelHint, Strings.ColumnsLabelHint, ConfigLabelButton, () => _openFile(_dataFolder.LabelColumnsFile));

        // The line is as high as the higher half. It is NOT stretched to the row (Dock = Fill): a table sizes a row by the
        // current height of such a control, so a stretched line kept the row as high as it ever was (at 125% / 150%:
        // ten lines high for a moment while Windows scales the window, see WrapHintBesideButton). Its height is set
        // from the two halves instead, which always have the height of their content.
        FolderDivider.Width = 1;
        FolderDivider.Height = 1;
        FolderDivider.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        FolderDivider.BackColor = Theme.PaleBlue;
        FolderDivider.Margin = new Padding(20, 8, 20, 0);
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
        AddFullRow(body, ref row, folders);
        _folders = folders;
        folders.SizeChanged += (sender, e) => FitHints();

        // 5, 6. Reference corner + rotated (DSSClient's "Oriëntatie" / "Gedraaid"), with a line above it
        AddDivider(body, ref row, new Padding(0, 20, 0, 12));
        ZeroPointTitle.Text = Strings.LabelZeroPoint;
        ZeroPointTitle.AutoSize = true;
        ZeroPointTitle.Margin = new Padding(0, 8, 0, 2);
        ZeroPointTitle.Font = new Font(Font, FontStyle.Bold);
        AddFullRow(body, ref row, ZeroPointTitle);

        ReferenceCornerBox.DropDownStyle = ComboBoxStyle.DropDownList; // choose from the list, no typing
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.BottomLeft, Strings.ReferenceCornerBottomLeft));
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.TopLeft, Strings.ReferenceCornerTopLeft));
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.BottomRight, Strings.ReferenceCornerBottomRight));
        ReferenceCornerBox.Items.Add(new ReferenceCornerItem(ReferenceCorner.TopRight, Strings.ReferenceCornerTopRight));
        ReferenceCornerBox.Width = 260;
        ReferenceCornerBox.Anchor = AnchorStyles.Left;
        ReferenceCornerBox.ForeColor = Theme.Navy;
        ReferenceCornerBox.Margin = new Padding(0, 0, 0, 6);
        // The little schematic follows the drop-down at once, even before Save (LoadFromSettings sets the first value).
        ReferenceCornerBox.SelectedIndexChanged += (sender, e) =>
            CornerPreview.SelectedCorner = ((ReferenceCornerItem)ReferenceCornerBox.SelectedItem!).Corner;

        PlanRotatedBox.Text = Strings.PlanRotated;
        PlanRotatedBox.AutoSize = true;
        PlanRotatedBox.Margin = new Padding(0);

        // Left column: the drop-down above the checkbox, in its own AutoSize panel (this is what keeps the drop-down at
        // its real 260 width: a Percent column in an AutoSize TableLayoutPanel is treated as AutoSize too and would
        // shrink it to the ComboBox's own — much narrower — computed preferred width, cutting the corner names off).
        var cornerColumn = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        cornerColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cornerColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cornerColumn.Controls.Add(ReferenceCornerBox, 0, 0);
        cornerColumn.Controls.Add(PlanRotatedBox, 0, 1);

        CornerPreview.Size = new Size(100, 76); // a bit bigger than the first version (was 84 x 64)
        CornerPreview.Margin = new Padding(20, 0, 0, 0);
        CornerPreview.Anchor = AnchorStyles.Left | AnchorStyles.Top; // top-anchored: lines up with the drop-down's row,
                                                                      // not centred across the drop-down + checkbox stack

        // Right column: the schematic, both AutoSize (no Percent column: see the note above).
        var cornerRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        cornerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cornerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cornerRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cornerRow.Controls.Add(cornerColumn, 0, 0);
        cornerRow.Controls.Add(CornerPreview, 1, 0);
        AddFullRow(body, ref row, cornerRow);

        // Save / Cancel
        // Save / Cancel are not here: they sit at the bottom right of the window, see BuildBottomRow.

        // History, with a line above it (the title below has 10 of margin, so 10 + 10 = 20 below the line, like the others).
        AddDivider(body, ref row, new Padding(0, 20, 0, 10));

        // The title on the left and, on the right, the buttons that retry the failed projects and open the
        // log folder (the last one in line with the Browse buttons). The list itself takes all the remaining height.
        var historyTitle = new Label { Text = Strings.HistoryTitle, AutoSize = true, Margin = new Padding(0, 10, 0, 4) };
        historyTitle.Font = new Font(Font, FontStyle.Bold);
        historyTitle.Anchor = AnchorStyles.Left; // in the middle of the buttons' height

        RetryFailedButton.Text = Strings.RetryFailedButton;
        RetryFailedButton.Size = new Size(WideButtonWidth(RetryFailedButton.Text), ButtonSize.Height);
        RetryFailedButton.Margin = new Padding(0, 10, 8, 4);
        Theme.StyleSecondaryButton(RetryFailedButton);
        RetryFailedButton.Click += (sender, e) => _retryFailed();

        OpenLogButton.Text = Strings.OpenLogButton;
        OpenLogButton.Size = ButtonSize;
        OpenLogButton.Margin = new Padding(0, 10, 0, 4);
        Theme.StyleSecondaryButton(OpenLogButton);
        OpenLogButton.Click += (sender, e) => _openFolder(_dataFolder.LogFolder);

        // A small table of its own over the full width, so the log button ends exactly at the right edge.
        var historyRow = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        historyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        historyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        historyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        historyRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        historyRow.Controls.Add(historyTitle, 0, 0);
        historyRow.Controls.Add(RetryFailedButton, 1, 0);
        historyRow.Controls.Add(OpenLogButton, 2, 0);
        AddFullRow(body, ref row, historyRow);

        BuildHistoryList();
        body.Controls.Add(HistoryList, 0, row);
        body.SetColumnSpan(HistoryList, 2);
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row++;

        // The last row, below the list: the footer on the left, Save / Cancel on the right.
        AddFullRow(body, ref row, BuildBottomRow());

        body.RowCount = row;

        // The table sits in a scroll panel: if the window is smaller than the content (a small screen at a high
        // scaling), a scroll bar appears and nothing is cut off. On a normal screen there is nothing to scroll.
        Scroller.Dock = DockStyle.Fill;
        Scroller.AutoScroll = true;
        Scroller.BackColor = Theme.White;
        Scroller.Controls.Add(body);

        // Docking: the control that is added LAST sits at the very top.
        Controls.Add(Scroller);
        Controls.Add(banner);
    }

    /// <summary>
    /// The left part of the top row: the language title (in all three languages), the language box and, below them,
    /// "Start with Windows".
    /// </summary>
    private Control BuildLanguageColumn()
    {
        LanguageCaption.Text = Strings.LanguageTitle;
        LanguageCaption.AutoSize = true;
        LanguageCaption.Font = new Font(Font, FontStyle.Bold);
        LanguageCaption.Margin = new Padding(0, 6, 0, 2);

        LanguageBox.DropDownStyle = ComboBoxStyle.DropDownList; // choose from the list, no typing
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.Dutch, Strings.LanguageDutch));
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.French, Strings.LanguageFrench));
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.English, Strings.LanguageEnglish));
        LanguageBox.Width = 260;
        LanguageBox.Anchor = AnchorStyles.Left;
        LanguageBox.ForeColor = Theme.Navy;
        LanguageBox.Margin = new Padding(0, 0, 0, 6);

        StartWithWindowsBox.Text = Strings.StartWithWindows;
        StartWithWindowsBox.AutoSize = true;
        StartWithWindowsBox.Margin = new Padding(0, 4, 0, 8);

        // One AutoSize column (no Percent column: that would shrink the drop-down, see the reference corner below).
        var column = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0) };
        column.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        column.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        column.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        column.Controls.Add(LanguageCaption, 0, 0);
        column.Controls.Add(LanguageBox, 0, 1);
        column.Controls.Add(StartWithWindowsBox, 0, 2);
        return column;
    }

    /// <summary>
    /// The right part of the top row, against the right edge: "Dev.: Daan Verhoost | App version: 1.0.4".
    /// (The company logo is in the footer at the bottom left, see BuildFooter.)
    /// </summary>
    private Control BuildCredits()
    {
        CreditLabel.Text = Strings.DeveloperCredit;
        CreditLabel.AutoSize = true;
        CreditLabel.Margin = new Padding(0);
        CreditLabel.ForeColor = Theme.Navy;

        var separator = new Label { Text = Strings.CreditSeparator, AutoSize = true, Margin = new Padding(0), ForeColor = Theme.PaleBlue };

        VersionLabel.Text = Strings.AppVersion(AppInfo.Version);
        VersionLabel.AutoSize = true;
        VersionLabel.Margin = new Padding(0);
        VersionLabel.ForeColor = Theme.Navy;

        // The one line of text, left to right.
        var line = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(0, 6, 0, 8), // the same top margin as the language title, so both sit on one line
        };
        line.Controls.Add(CreditLabel);
        line.Controls.Add(separator);
        line.Controls.Add(VersionLabel);
        return line;
    }

    /// <summary>
    /// The footer at the bottom left of the window: only the small logo of the company, a link to its website
    /// (the company name is in the tooltip).
    /// </summary>
    private Control BuildFooter()
    {
        LogoPicture.Image = AppIcons.CompanyLogo();
        LogoPicture.SizeMode = PictureBoxSizeMode.Zoom; // keeps the proportions of the logo
        LogoPicture.Size = LogoSize;
        LogoPicture.BackColor = Theme.White;
        LogoPicture.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; // bottom LEFT
        LogoPicture.Margin = new Padding(0);
        LogoPicture.Cursor = Cursors.Hand;
        new ToolTip().SetToolTip(LogoPicture, Strings.CompanyName);
        LogoPicture.Click += (sender, e) => OpenWebsite(Strings.CompanyUrl);
        return LogoPicture;
    }

    /// <summary>
    /// One half of the folders row (TopSolid or Duivestein): like the main table, a wide column for the text boxes and
    /// hints and a column for the buttons.
    /// </summary>
    private static TableLayoutPanel NewHalf()
    {
        // Anchored at the top, not Dock = Fill: the lower half keeps its own height. Stretched to the height of the row,
        // its last row got all the extra room and the last hint sat lower than its button.
        var half = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0) };
        half.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        half.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return half;
    }

    /// <summary>
    /// The bottom line of the window, below the list of conversions: the footer (company logo and link) at the left
    /// and the buttons Save and Cancel at the right, so the buttons are the last thing the user reaches.
    /// </summary>
    private Control BuildBottomRow()
    {
        // Same size and same margins for both, so they sit in one line.
        var save = new RoundedButton { Text = Strings.Save, Size = ButtonSize, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new RoundedButton { Text = Strings.Cancel, Size = ButtonSize, Margin = new Padding(8, 0, 0, 0) };
        Theme.StylePrimaryButton(save);
        Theme.StyleSecondaryButton(cancel);
        save.Click += (sender, e) => SaveAndHide();
        cancel.Click += (sender, e) => Hide();
        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom, // bottom RIGHT, in line with the bottom of the logo
            Margin = new Padding(0),
        };
        buttons.Controls.Add(cancel); // right to left: Cancel is the right-most button
        buttons.Controls.Add(save);

        // Two cells: the logo only what it needs, the buttons the rest (anchored at its right edge). Not the other way
        // round: a Percent column in an AutoSize table shrank the logo, which is not AutoSize, to 2 pixels wide at 150%.
        var bottom = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.Controls.Add(BuildFooter(), 0, 0);
        bottom.Controls.Add(buttons, 1, 0);
        return bottom;
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

    /// <summary>A caption, a text box with a Browse button, and a warning that is only visible when needed.</summary>
    private void AddFolderRow(TableLayoutPanel table, ref int row, Label captionLabel, string caption, TextBox box, Label warning)
    {
        captionLabel.Text = caption;
        captionLabel.AutoSize = true;
        captionLabel.Margin = new Padding(0, 8, 0, 2);
        captionLabel.Font = new Font(Font, FontStyle.Bold);
        AddFullRow(table, ref row, captionLabel);

        // The text box sits in a rounded frame that is as high as the buttons, so both line up in one row.
        box.ForeColor = Theme.Navy;
        var frame = new TextBoxFrame(box)
        {
            Height = ButtonSize.Height,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, // wide, and in the middle of the row's height
            Margin = new Padding(0, 0, 8, 0),
        };

        var browse = new RoundedButton { Text = Strings.Browse, Size = ButtonSize, Margin = new Padding(0) };
        Theme.StyleSecondaryButton(browse);

        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(frame, 0, row);
        table.Controls.Add(browse, 1, row);
        row++;

        warning.Text = Strings.FolderNotReachable;
        warning.ForeColor = Theme.Error;
        warning.AutoSize = true;
        warning.Visible = false; // a row with an invisible control takes no space
        warning.Margin = new Padding(0, 2, 0, 0);
        AddFullRow(table, ref row, warning);

        browse.Click += (sender, e) => BrowseForFolder(box, warning);
        box.Leave += (sender, e) => CheckFolder(box, warning);
    }

    /// <summary>
    /// A line under a folder: a short explanation on the left and a "Config" button on the right (in line with the
    /// Browse buttons) that opens the file with the column names.
    /// </summary>
    private void AddConfigRow(TableLayoutPanel table, ref int row, Label hint, string hintText, RoundedButton button, Action open)
    {
        hint.Text = hintText;
        hint.Margin = new Padding(0, 4, 8, 0);

        button.Text = Strings.ConfigButton;
        button.Size = ButtonSize;
        button.Margin = new Padding(0, 4, 0, 0);
        Theme.StyleSecondaryButton(button);
        button.Click += (sender, e) => open();

        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(hint, 0, row);
        table.Controls.Add(button, 1, row);
        row++;
        WrapHintBesideButton(hint, button);
    }

    /// <summary>
    /// A hint at the left of a button wraps onto more lines when it does not fit. The window computes the hint's size
    /// itself (FitHints), every time the folders row changes width: width = what is left in its half next to the button,
    /// height = what the text needs at that width.
    /// Why not let the table wrap it (AutoSize label anchored Left | Right)? While Windows scales the window to 125% or
    /// 150%, the window is laid out for a moment at a much narrower width; the table measured the hints there and kept
    /// rows of ten lines high: the hints ended far below their buttons (Daan's screenshots of 1.1.0).
    /// </summary>
    private void WrapHintBesideButton(Label hint, Control button)
    {
        hint.AutoSize = false;
        hint.Anchor = AnchorStyles.Left; // in the middle of the row's height
        _wrappedHints.Add((hint, button));

        hint.FontChanged += (sender, e) => FitHints(); // the window moved to a screen with another scaling
        button.SizeChanged += (sender, e) => FitHints();
    }

    /// <summary>
    /// Gives every hint of <see cref="WrapHintBesideButton"/> its size. The width of a half is taken from the whole
    /// folders row (which gets its width from the window), never from the half itself: a half is as wide as its widest
    /// hint, so measuring the half would keep a hint that was wide once wide forever.
    /// </summary>
    private void FitHints()
    {
        if (_folders == null)
        {
            return; // still building the window: fitted when the folders row gets its width
        }

        int half = (_folders.ClientSize.Width - FolderDivider.Width - FolderDivider.Margin.Horizontal) / 2;
        foreach (var (hint, button) in _wrappedHints)
        {
            int width = half - button.Width - button.Margin.Horizontal - hint.Margin.Horizontal;
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
    /// The line under the two column files: a short explanation on the left and the button "Open materiaaltabel" on the
    /// right. That text is too long for a button of 100 pixels, so the button is wider, and the row is a small table of
    /// its own over the full width (like the history row): the wide button ends exactly at the right edge of the Browse
    /// buttons and does not make their column wider.
    /// </summary>
    private void AddMaterialsRow(TableLayoutPanel table, ref int row)
    {
        MaterialsHint.Text = Strings.MaterialsHint;
        MaterialsHint.Margin = new Padding(0, 4, 8, 0);

        OpenMaterialsButton.Text = Strings.MenuOpenMaterials;
        OpenMaterialsButton.Size = new Size(WideButtonWidth(OpenMaterialsButton.Text), ButtonSize.Height);
        OpenMaterialsButton.Margin = new Padding(0, 4, 0, 0);
        Theme.StyleSecondaryButton(OpenMaterialsButton);
        OpenMaterialsButton.Click += (sender, e) => _openFile(_dataFolder.MaterialsFile);

        var materialsRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        materialsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        materialsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        materialsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        materialsRow.Controls.Add(MaterialsHint, 0, 0);
        materialsRow.Controls.Add(OpenMaterialsButton, 1, 0);
        AddFullRow(table, ref row, materialsRow);
        WrapHintBesideButton(MaterialsHint, OpenMaterialsButton);
    }

    /// <summary>
    /// The width of a button whose text is too long for the normal 100 pixels ("Open materiaaltabel", "Réessayez échoué"):
    /// as wide as the text needs, never narrower than a normal button. Measured here at 100%; the window scaling makes it
    /// bigger later, like every other size. The height stays the same as all buttons. (AutoSize was not used: it made the
    /// height different.)
    /// MeasureText measures on the screen, so on a 150% screen the text comes back 1.5 times as wide; it is brought back
    /// to 100% here, otherwise the window scaling made these buttons 1.5 times too wide (seen up to 1.1.0).
    /// </summary>
    private int WideButtonWidth(string text)
    {
        using Graphics screen = Graphics.FromHwnd(IntPtr.Zero);
        int textWidth = (int)Math.Ceiling(TextRenderer.MeasureText(screen, text, Font).Width * 96f / screen.DpiX);
        return Math.Max(ButtonSize.Width, textWidth + 24);
    }

    /// <summary>A thin horizontal line over the full width, in the pale blue of the theme.</summary>
    private void AddDivider(TableLayoutPanel table, ref int row, Padding margin)
    {
        var line = new Panel { Height = 1, Dock = DockStyle.Fill, BackColor = Theme.PaleBlue, Margin = margin };
        Dividers.Add(line);
        AddFullRow(table, ref row, line);
    }

    private static void AddFullRow(TableLayoutPanel table, ref int row, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
        row++;
    }

    private void BuildHistoryList()
    {
        HistoryList.View = View.Details;
        HistoryList.FullRowSelect = true;
        HistoryList.HideSelection = false;
        HistoryList.BorderStyle = BorderStyle.FixedSingle;
        HistoryList.Dock = DockStyle.Fill;
        HistoryList.MinimumSize = new Size(0, MinimumHistoryHeight); // never squeezed out of view
        HistoryList.Margin = new Padding(0); // the default margin (3) kept it a little inside the other rows
        HistoryList.BackColor = Theme.White;
        HistoryList.ForeColor = Theme.Navy;
        HistoryList.Columns.Add(Strings.HistoryTime, HistoryColumnWidths[0]);
        HistoryList.Columns.Add(Strings.HistoryProject, HistoryColumnWidths[1]);
        HistoryList.Columns.Add(Strings.HistoryResult, HistoryColumnWidths[2]);
        HistoryList.Columns.Add(Strings.HistoryMessage, 300);
        HistoryList.SizeChanged += (sender, e) => HistoryList.Columns[3].Width = -2; // last column takes the rest

        _historyEmpty.Text = Strings.HistoryEmpty;
        _historyEmpty.AutoSize = true;
        _historyEmpty.Font = new Font(Font, FontStyle.Italic);
        _historyEmpty.BackColor = Theme.White;
        _historyEmpty.Location = new Point(12, 36);
        HistoryList.Controls.Add(_historyEmpty);
    }

    // ------------------------------------------------------------------ filling and reading

    /// <summary>Fills the window with the settings in use and the REAL state of "start with Windows".</summary>
    internal void LoadFromSettings()
    {
        AppSettings settings = _getSettings();

        StartWithWindowsBox.Checked = SafeIsStartWithWindows();
        LanguageBox.SelectedIndex = (int)Localizer.FromCode(settings.Language); // the list is in the order of the enum
        ExportFolderBox.Text = settings.TopSolidExportPath;
        BatchFolderBox.Text = settings.BatchFolder;
        LabelFolderBox.Text = settings.LabelFolder;
        ReferenceCorner corner = ReferenceCornerMapping.FromFlips(settings.FlipX, settings.FlipY);
        ReferenceCornerBox.SelectedIndex = (int)corner; // items added in enum order
        CornerPreview.SelectedCorner = corner; // set directly: SelectedIndexChanged only fires when the index actually moves
        PlanRotatedBox.Checked = settings.PlanRotated;

        RefreshHistory();
        CheckAllFolders();
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

        foreach (HistoryEntry entry in _history.GetEntries())
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
    }

    /// <summary>An error report has several lines; the list shows them on one line.</summary>
    private static string OneLine(string text)
    {
        return text.Replace("\r\n", " | ").Replace("\n", " | ");
    }

    // ------------------------------------------------------------------ hide instead of close

    /// <summary>
    /// Makes sure the window is not bigger than the part of the screen that is free (not under the taskbar)
    /// and puts it in the middle of it. On a small screen at a high scaling the window is then shorter than the
    /// content, and the settings scroll (the list of conversions keeps its minimum size).
    /// </summary>
    /// <param name="area">The free part of the screen, in pixels.</param>
    internal void FitToArea(Rectangle area)
    {
        int width = Math.Min(Width, area.Width);
        int height = Math.Min(Height, area.Height);
        Size = new Size(width, height);
        Location = new Point(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2);
    }

    /// <summary>The height that all the settings need, so the scroll panel knows when to show a scroll bar.</summary>
    internal void UpdateScrollSize()
    {
        Control body = Scroller.Controls[0];
        Scroller.AutoScrollMinSize = new Size(0, body.GetPreferredSize(new Size(Scroller.ClientSize.Width, 0)).Height);
    }

    /// <summary>
    /// Gives the window the height that its content needs, so the list of conversions is visible right away
    /// without having to enlarge the window. This works at any screen scaling and in any language, because it
    /// measures the real content. If the screen is too small the window is as big as the screen allows
    /// and the settings scroll (see <see cref="FitToArea"/>).
    /// </summary>
    /// <param name="area">The free part of the screen, in pixels.</param>
    internal void SizeToContent(Rectangle area)
    {
        Control banner = BannerPicture.Parent!;
        Control body = Scroller.Controls[0];
        int bodyHeight = body.GetPreferredSize(new Size(Scroller.ClientSize.Width, 0)).Height;

        // The warnings "folder not reachable" are checked in the background and appear later.
        // Their room is reserved now, otherwise the window would need a scroll bar a moment after it opened.
        foreach (Control warning in new Control[] { _exportWarning, _batchWarning, _labelWarning })
        {
            if (!warning.Visible)
            {
                bodyHeight += warning.GetPreferredSize(Size.Empty).Height + warning.Margin.Vertical;
            }
        }

        ClientSize = new Size(ClientSize.Width, banner.Height + bodyHeight + LogicalToDeviceUnits(ExtraHistoryHeight));
        FitToArea(area);
        UpdateScrollSize();
    }

    /// <summary>Scales the widths of the list columns to the screen scaling (a ListView does not do that itself).</summary>
    private void ScaleHistoryColumns()
    {
        for (int i = 0; i < HistoryColumnWidths.Length; i++)
        {
            HistoryList.Columns[i].Width = LogicalToDeviceUnits(HistoryColumnWidths[i]);
        }

        HistoryList.Columns[3].Width = -2; // the last column takes the rest
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

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            LoadFromSettings(); // always show the current situation, also after a Cancel
            _windowOpened();
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
        }

        base.Dispose(disposing);
    }
}
