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

    // The controls we need to read or fill (internal: the tests check them)
    internal readonly CheckBox StartWithWindowsBox = new();
    internal readonly TextBox ExportFolderBox = new();
    internal readonly TextBox BatchFolderBox = new();
    internal readonly TextBox LabelFolderBox = new();
    internal readonly CheckBox FlipXBox = new();
    internal readonly CheckBox FlipYBox = new();
    internal readonly ListView HistoryList = new();
    private readonly Label _exportWarning = new();
    private readonly Label _batchWarning = new();
    private readonly Label _labelWarning = new();
    private readonly Label _historyEmpty = new();

    // The banner picture is 540 x 84 pixels. It is shown 1.5 times as big.
    private const int BannerWidth = 810;
    private const int BannerHeight = 126;

    // Parts of the layout that the tests look at
    internal readonly PictureBox BannerPicture = new();
    internal readonly Label ExportCaption = new();
    internal readonly Label BatchCaption = new();
    internal readonly Label LabelCaption = new();
    internal readonly Label ZeroPointTitle = new();
    internal readonly List<Panel> Dividers = new();
    internal readonly Label LanguageCaption = new();
    internal readonly ComboBox LanguageBox = new();
    internal readonly LinkLabel CreditLink = new();  // "Dev.: Daan Verhoost  |  ROGIERS NV/SA" (the company is the link)
    internal readonly Label VersionLabel = new();    // "App version: 1.0.0"
    internal readonly PictureBox LogoPicture = new(); // the small ROGIERS logo at the left of the two footer lines
    internal readonly Panel Scroller = new();        // holds all the settings; scrolls if the window is too small

    // The list of conversions is never smaller than this (in pixels at 100% scaling): it must always be visible.
    private const int MinimumHistoryHeight = 120;

    // When the window opens, the list gets this much more than its minimum (at 100%).
    private const int ExtraHistoryHeight = 10;

    // All buttons (Browse, Save, Cancel) have exactly this size (at 100%), so they line up and look the same.
    // Not AutoSize: that made Save (bold text) and Cancel different in size, and all of them too big.
    private static readonly Size ButtonSize = new(100, 28);

    // The company logo in the footer (at 100%). The logo is a little wider than high; the picture keeps its proportions.
    private static readonly Size LogoSize = new(46, 42);

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

    /// <summary>Set to true by the tray app when the program really exits; until then closing only hides.</summary>
    [System.ComponentModel.Browsable(false)] // these two attributes tell the Visual Studio designer to ignore it
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AllowClose { get; set; }

    /// <param name="getSettings">Gives the settings that are in use now (asked every time the window is shown).</param>
    /// <param name="save">Saves new settings. May throw IOException / UnauthorizedAccessException.</param>
    /// <param name="windowOpened">Called every time the window is shown (the tray clears its error state).</param>
    public SettingsForm(
        Func<AppSettings> getSettings, Action<AppSettings> save, Action windowOpened,
        ConversionHistory history, StartWithWindows startup)
    {
        _getSettings = getSettings;
        _save = save;
        _windowOpened = windowOpened;
        _history = history;
        _startup = startup;

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
        Text = Strings.WindowTitle;
        Icon = AppIcons.ForWindow();
        BackColor = Theme.White;
        ForeColor = Theme.Navy;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(BannerWidth + 30, 780); // the window is a little wider than the banner
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
            Padding = new Padding(20, 4, 20, 8),
            ColumnCount = 2,
            BackColor = Theme.White,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;

        // 0. Language: right above "Start with Windows". The title has all three languages.
        LanguageCaption.Text = Strings.LanguageTitle;
        LanguageCaption.AutoSize = true;
        LanguageCaption.Font = new Font(Font, FontStyle.Bold);
        LanguageCaption.Margin = new Padding(0, 6, 0, 2);
        AddFullRow(body, ref row, LanguageCaption);

        LanguageBox.DropDownStyle = ComboBoxStyle.DropDownList; // choose from the list, no typing
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.Dutch, Strings.LanguageDutch));
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.French, Strings.LanguageFrench));
        LanguageBox.Items.Add(new LanguageItem(AppLanguage.English, Strings.LanguageEnglish));
        LanguageBox.Width = 260;
        LanguageBox.Anchor = AnchorStyles.Left;
        LanguageBox.ForeColor = Theme.Navy;
        LanguageBox.Margin = new Padding(0, 0, 0, 6);
        AddFullRow(body, ref row, LanguageBox);

        // 1. Start with Windows
        StartWithWindowsBox.Text = Strings.StartWithWindows;
        StartWithWindowsBox.AutoSize = true;
        StartWithWindowsBox.Margin = new Padding(0, 4, 0, 8);
        AddFullRow(body, ref row, StartWithWindowsBox);
        AddDivider(body, ref row, new Padding(0, 0, 0, 0)); // a line below "Start met Windows"

        // 2, 3, 4. The three folders (the titles are bold)
        AddFolderRow(body, ref row, ExportCaption, Strings.ExportFolder, ExportFolderBox, _exportWarning);
        AddFolderRow(body, ref row, BatchCaption, Strings.BatchFolder, BatchFolderBox, _batchWarning);
        AddFolderRow(body, ref row, LabelCaption, Strings.LabelFolder, LabelFolderBox, _labelWarning);

        // 5, 6. Label zero point, with a line above it
        AddDivider(body, ref row, new Padding(0, 10, 0, 0));
        ZeroPointTitle.Text = Strings.LabelZeroPoint;
        ZeroPointTitle.AutoSize = true;
        ZeroPointTitle.Margin = new Padding(0, 8, 0, 2);
        ZeroPointTitle.Font = new Font(Font, FontStyle.Bold);
        AddFullRow(body, ref row, ZeroPointTitle);

        FlipXBox.Text = Strings.FlipX;
        FlipXBox.AutoSize = true;
        AddFullRow(body, ref row, FlipXBox);
        FlipYBox.Text = Strings.FlipY;
        FlipYBox.AutoSize = true;
        AddFullRow(body, ref row, FlipYBox);

        // Save / Cancel
        // Save / Cancel are not here: they sit at the bottom right of the window, see BuildBottomRow.

        // History: this row takes all the remaining height.
        var historyTitle = new Label { Text = Strings.HistoryTitle, AutoSize = true, Margin = new Padding(0, 10, 0, 4) };
        historyTitle.Font = new Font(Font, FontStyle.Bold);
        AddFullRow(body, ref row, historyTitle);

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
    /// The bottom line of the window, below the list of conversions: the footer at the left (credit and version)
    /// and the buttons Save and Cancel at the right, so the buttons are the last thing the user reaches.
    /// </summary>
    private Control BuildBottomRow()
    {
        // Same size and same margins for both, so they sit in one line.
        var save = new Button { Text = Strings.Save, Size = ButtonSize, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new Button { Text = Strings.Cancel, Size = ButtonSize, Margin = new Padding(8, 0, 0, 0) };
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
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom, // bottom RIGHT, in line with the last line of the footer
            Margin = new Padding(0),
        };
        buttons.Controls.Add(cancel); // right to left: Cancel is the right-most button
        buttons.Controls.Add(save);

        // Two cells: the footer takes the free width, the buttons only what they need.
        var bottom = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 0, 0),
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.Controls.Add(BuildFooter(), 0, 0);
        bottom.Controls.Add(buttons, 1, 0);
        return bottom;
    }

    /// <summary>
    /// The footer at the bottom left of the window: the small logo of the company and, at its right, two lines:
    ///   Dev.: Daan Verhoost  |  ROGIERS NV/SA      (the company name is a link to its website)
    ///   App version: 1.0.0
    /// </summary>
    private Control BuildFooter()
    {
        string company = Strings.CompanyName;

        CreditLink.Text = Strings.DeveloperCredit + "  |  " + company;
        CreditLink.AutoSize = true;
        CreditLink.Margin = new Padding(0, 8, 0, 0);
        CreditLink.ForeColor = Theme.Navy;                 // the normal text
        CreditLink.LinkColor = Theme.DarkBlue;             // the link
        CreditLink.ActiveLinkColor = Theme.Navy;
        CreditLink.VisitedLinkColor = Theme.DarkBlue;
        CreditLink.Links.Add(CreditLink.Text.Length - company.Length, company.Length, Strings.CompanyUrl);
        CreditLink.LinkClicked += (sender, e) => OpenWebsite(Strings.CompanyUrl);

        VersionLabel.Text = Strings.AppVersion(AppInfo.Version);
        VersionLabel.AutoSize = true;
        VersionLabel.Margin = new Padding(0, 2, 0, 0);
        VersionLabel.ForeColor = Theme.Navy;

        // The two text lines under each other ...
        var lines = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left, // in the middle of the height of the logo
            Margin = new Padding(0),
        };
        lines.Controls.Add(CreditLink);
        lines.Controls.Add(VersionLabel);

        // ... and the small logo of the company at their left.
        LogoPicture.Image = AppIcons.CompanyLogo();
        LogoPicture.SizeMode = PictureBoxSizeMode.Zoom; // keeps the proportions of the logo
        LogoPicture.Size = LogoSize;
        LogoPicture.BackColor = Theme.White;
        LogoPicture.Anchor = AnchorStyles.Left;
        LogoPicture.Margin = new Padding(0, 6, 12, 0);

        var footer = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Anchor = AnchorStyles.Left, // bottom LEFT
            Margin = new Padding(0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.Controls.Add(LogoPicture, 0, 0);
        footer.Controls.Add(lines, 1, 0);
        return footer;
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

        box.Anchor = AnchorStyles.Left | AnchorStyles.Right; // wide, and in the middle of the row's height
        box.ForeColor = Theme.Navy;
        box.Margin = new Padding(0, 0, 8, 0);

        var browse = new Button { Text = Strings.Browse, Size = ButtonSize, Margin = new Padding(0) };
        Theme.StyleSecondaryButton(browse);

        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(box, 0, row);
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
        FlipXBox.Checked = settings.FlipX;
        FlipYBox.Checked = settings.FlipY;

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
        settings.FlipX = FlipXBox.Checked;
        settings.FlipY = FlipYBox.Checked;
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
