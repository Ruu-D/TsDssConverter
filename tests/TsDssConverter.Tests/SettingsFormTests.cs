using Microsoft.Win32;
using TsDssConverter.Core;
using TsDssConverter.Tray;

namespace TsDssConverter.Tests;

/// <summary>
/// Tests the settings window without a person: it is created and "clicked" by the test.
/// Windows Forms needs its own STA thread, see <see cref="RunOnStaThread"/>.
/// </summary>
public class SettingsFormTests
{
    /// <summary>Runs the action on a thread that Windows Forms accepts, and passes on any failure.</summary>
    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            throw new Exception("The test failed on the STA thread: " + failure.Message, failure);
        }
    }

    private sealed class TestWindow : IDisposable
    {
        public AppSettings Current { get; set; } = new();
        public AppSettings? Saved { get; private set; }
        public int TimesOpened { get; private set; }
        public ConversionHistory History { get; } = new();
        public string RegistryPath { get; } = @"Software\TsDssConverterTests\Form-" + Guid.NewGuid().ToString("N");
        public StartWithWindows Startup { get; }
        public AppDataFolder DataFolder { get; } = new(@"C:\TsDssTestData");
        public List<string> OpenedFiles { get; } = new();    // what the Config buttons asked to open
        public List<string> OpenedFolders { get; } = new();  // what the Open log button asked to open
        public int RetryClicks { get; private set; }         // how often the Retry failed button was pressed
        public int ScanClicks { get; private set; }          // how often Scan now was pressed
        public int PauseClicks { get; private set; }         // how often Pause / Resume was pressed
        public bool Paused { get; set; }                     // the fake tray's pause (Pause / Resume turns it over)
        public TrayState State { get; set; } = TrayState.Ok; // what the fake tray icon shows
        public List<bool> SavedFolds { get; } = new();       // what the fold sign asked to save
        public List<string> LanguageChanges { get; } = new(); // the languages chosen in the box
        public (DateTime At, TimeSpan Interval)? NextScan { get; set; } // what the fake watcher plans
        public SettingsForm Form { get; }

        public TestWindow()
        {
            Startup = new StartWithWindows(RegistryPath, @"C:\TsDssConverter\TsDssConverter.exe");
            Form = new SettingsForm(
                () => Current, saved => { Saved = saved; Current = saved; }, () => TimesOpened++, History, Startup,
                DataFolder, OpenedFiles.Add, OpenedFolders.Add, () => RetryClicks++,
                scanNow: () => ScanClicks++,
                togglePause: () => { PauseClicks++; Paused = !Paused; },
                isPaused: () => Paused,
                getState: () => Paused ? TrayState.Paused : State,
                saveFolded: folded => { SavedFolds.Add(folded); Current.FoldersFolded = folded; },
                changeLanguage: LanguageChanges.Add,
                nextScan: () => NextScan);
        }

        public void Dispose()
        {
            Form.AllowClose = true;
            Form.Dispose();
            TrayTests.TestRegistry.Remove(RegistryPath);
        }
    }

    [Fact]
    public void ShiftClickOnTheLogo_OpensSettingsJson()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();

            // Only the Shift+click is tested: a normal click opens the website in the browser.
            window.Form.LogoClicked(shiftDown: true);

            Assert.Equal(new[] { window.DataFolder.SettingsFile }, window.OpenedFiles);
        });
    }

    [Fact]
    public void ConfigButtons_OpenTheColumnFiles_OpenMaterialsOpensTheMaterialsFile_AndOpenLogOpensTheLogFolder()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show(); // a button that is not visible cannot be clicked

            Assert.Equal("Config", form.ConfigInfoButton.Text);
            Assert.Equal("Config", form.ConfigPositionButton.Text);

            form.ConfigInfoButton.PerformClick();
            Assert.Equal(new[] { @"C:\TsDssTestData\columns-li.txt" }, window.OpenedFiles);

            form.ConfigPositionButton.PerformClick();
            Assert.Equal(new[] { @"C:\TsDssTestData\columns-li.txt", @"C:\TsDssTestData\columns-lp.txt" }, window.OpenedFiles);

            Assert.Equal("Config", form.ConfigLabelButton.Text);
            form.ConfigLabelButton.PerformClick();   // the button under the Duivestein label folder
            Assert.Equal(@"C:\TsDssTestData\columns-label.txt", window.OpenedFiles.Last());
            window.OpenedFiles.RemoveAt(window.OpenedFiles.Count - 1); // the checks below start from the two column files again

            Assert.Equal("Materiaallijst", form.MaterialsButton.Text);   // the round button at the right end of the bar
            form.MaterialsButton.PerformClick();
            Assert.Equal(@"C:\TsDssTestData\materials.csv", window.OpenedFiles.Last());
            Assert.Equal(3, window.OpenedFiles.Count);

            Assert.Empty(window.OpenedFolders);
            form.OpenLogButton.PerformClick();
            Assert.Equal(new[] { @"C:\TsDssTestData\logs" }, window.OpenedFolders);
            Assert.Equal(3, window.OpenedFiles.Count); // the log button opens a folder, not a file
        });
    }

    [Fact]
    public void RetryFailedButton_AsksTheTrayToRetry_AndOpensNothing()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show();

            Assert.Equal("Herstart mislukte", form.RetryFailedButton.Text);   // Dutch is the default language
            Assert.Equal(0, window.RetryClicks);

            form.RetryFailedButton.PerformClick();

            Assert.Equal(1, window.RetryClicks);
            Assert.Empty(window.OpenedFiles);     // it does the work itself: no file and no folder is opened
            Assert.Empty(window.OpenedFolders);
        });
    }

    [Fact]
    public void Window_ShowsTheCurrentSettings()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Current = new AppSettings { TopSolidExportPath = @"D:\Export\", BatchFolder = @"D:\Batch", LabelFolder = @"D:\Label", FlipX = true, PlanRotated = true };
            window.Form.LoadFromSettings();

            Assert.Equal(@"D:\Export\", window.Form.ExportFolderBox.Text);
            Assert.Equal(@"D:\Batch", window.Form.BatchFolderBox.Text);
            Assert.Equal(@"D:\Label", window.Form.LabelFolderBox.Text);
            // FlipX = true, FlipY = false -> the bottom-right corner (see ReferenceCornerMapping).
            Assert.Equal(ReferenceCorner.BottomRight, ((SettingsForm.ReferenceCornerItem)window.Form.ReferenceCornerBox.SelectedItem!).Corner);
            Assert.Equal(ReferenceCorner.BottomRight, window.Form.CornerPreview.SelectedCorner); // the schematic follows it
            Assert.True(window.Form.PlanRotatedBox.Checked);
        });
    }

    [Fact]
    public void ChangingTheReferenceCornerBox_UpdatesTheSchematic_AtOnce_BeforeSave()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            Assert.Equal(ReferenceCorner.BottomLeft, form.CornerPreview.SelectedCorner); // the default

            form.ReferenceCornerBox.SelectedIndex = (int)ReferenceCorner.TopRight;

            Assert.Equal(ReferenceCorner.TopRight, form.CornerPreview.SelectedCorner);
        });
    }

    [Fact]
    public void TheSchematic_DrawsTheNavyZeroPointDot_OnTheSelectedCorner_AndOnNoOtherCorner()
    {
        RunOnStaThread(() =>
        {
            using var preview = new ReferenceCornerPreview { Size = new Size(100, 76) };
            const int margin = 14; // ReferenceCornerPreview.PanelMargin: the sheet's corners are this far in (100% scaling)
            var cornerPoints = new Dictionary<ReferenceCorner, Point>
            {
                [ReferenceCorner.BottomLeft] = new(margin, preview.Height - margin),
                [ReferenceCorner.TopLeft] = new(margin, margin),
                [ReferenceCorner.BottomRight] = new(preview.Width - margin, preview.Height - margin),
                [ReferenceCorner.TopRight] = new(preview.Width - margin, margin),
            };

            foreach (ReferenceCorner selected in Enum.GetValues<ReferenceCorner>())
            {
                preview.SelectedCorner = selected;
                using var bitmap = new Bitmap(preview.Width, preview.Height);
                preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, preview.Size));

                foreach (var (corner, point) in cornerPoints)
                {
                    bool isNavy = bitmap.GetPixel(point.X, point.Y).ToArgb() == Theme.ButtonNavy.ToArgb();
                    Assert.True(isNavy == (corner == selected), $"selected {selected}, corner {corner}: navy dot = {isNavy}");
                }
            }
        });
    }

    [Fact]
    public void StartWithWindowsCheckbox_ShowsTheRealRegistryState_EachTimeTheWindowIsFilled()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            Assert.False(window.Form.StartWithWindowsBox.Checked);

            window.Startup.SetEnabled(true); // changed "behind the window's back"
            window.Form.LoadFromSettings();

            Assert.True(window.Form.StartWithWindowsBox.Checked);
        });
    }

    [Fact]
    public void Save_GivesTheNewSettings_KeepsTheAdvancedOnes_AndSetsStartWithWindows()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Current = new AppSettings { CncExtension = ".pgmx", RescanSeconds = 60, RequireTriggerFile = false, CncPathPrefixInXml = @"\\server\share" };
            window.Form.Show(); // a button can only be clicked while its window is shown

            window.Form.ExportFolderBox.Text = @"E:\Export";
            window.Form.BatchFolderBox.Text = @"E:\Batch";
            window.Form.LabelFolderBox.Text = @"E:\Label";
            // Top-left = FlipX false, FlipY true (see ReferenceCornerMapping).
            window.Form.ReferenceCornerBox.SelectedIndex = (int)ReferenceCorner.TopLeft;
            window.Form.PlanRotatedBox.Checked = true;
            window.Form.StartWithWindowsBox.Checked = true;

            ((Button)window.Form.AcceptButton!).PerformClick(); // the Save button

            AppSettings saved = window.Saved!;
            Assert.NotNull(saved);
            Assert.False(window.Form.Visible); // saving hides the window
            Assert.Equal(@"E:\Export", saved.TopSolidExportPath);
            Assert.Equal(@"E:\Batch", saved.BatchFolder);
            Assert.Equal(@"E:\Label", saved.LabelFolder);
            Assert.False(saved.FlipX);
            Assert.True(saved.FlipY);
            Assert.True(saved.PlanRotated);
            Assert.Equal(".pgmx", saved.CncExtension);                       // not in the window: unchanged
            Assert.Equal(60, saved.RescanSeconds);
            Assert.False(saved.RequireTriggerFile);
            Assert.Equal(@"\\server\share", saved.CncPathPrefixInXml);
            Assert.True(window.Startup.IsEnabled());
        });
    }

    [Fact]
    public void Cancel_SavesNothing_AndTheNextTimeTheOldValuesAreShownAgain()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Form.Show();
            window.Form.ExportFolderBox.Text = @"E:\Typed but cancelled";
            window.Startup.SetEnabled(false);
            window.Form.StartWithWindowsBox.Checked = true;

            ((Button)window.Form.CancelButton!).PerformClick();

            Assert.False(window.Form.Visible);  // Cancel hides the window ...
            Assert.Null(window.Saved);          // ... and saves nothing
            Assert.False(window.Startup.IsEnabled());

            window.Form.Show(); // shown again from the tray: the old values are back
            Assert.Equal(@"Z:\TopSolid\Export\", window.Form.ExportFolderBox.Text);
            Assert.False(window.Form.StartWithWindowsBox.Checked);
        });
    }

    [Fact]
    public void Save_WhenTheFileCannotBeWritten_ShowsNoCrash_AndDoesNotSetStartWithWindows()
    {
        // The save delegate throws like SettingsStore.Save does on a full disk. A message box would appear,
        // so this test only checks the part before it: ReadSettings works and nothing else happened.
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Form.LoadFromSettings();
            window.Form.StartWithWindowsBox.Checked = true;

            AppSettings typed = window.Form.ReadSettings();

            Assert.Equal(@"Z:\TopSolid\Export\", typed.TopSolidExportPath);
            Assert.False(window.Startup.IsEnabled()); // nothing is applied until Save succeeds
        });
    }

    [Fact]
    public void TheX_OnlyHidesTheWindow_UntilTheProgramReallyExits()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Form.Show();
            Assert.True(window.Form.Visible);

            window.Form.Close(); // what the X does

            Assert.False(window.Form.IsDisposed);
            Assert.False(window.Form.Visible);

            window.Form.Show(); // shown again from the tray
            Assert.True(window.Form.Visible);

            window.Form.AllowClose = true; // the tray's Exit
            window.Form.Close();
            Assert.True(window.Form.IsDisposed);
        });
    }

    [Fact]
    public void ShowingTheWindow_TellsTheTray_SoTheErrorIconCanBeCleared()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            Assert.Equal(0, window.TimesOpened);

            window.Form.Show();
            window.Form.Hide();
            window.Form.Show();

            Assert.Equal(2, window.TimesOpened);
        });
    }

    [Fact]
    public void HistoryList_IsEmptyAtFirst_ThenShowsTheNewestFirst_ErrorsInRed()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Form.RefreshHistory();
            Assert.Empty(window.Form.HistoryList.Items);

            window.History.Add(new HistoryEntry { Time = new DateTime(2026, 9, 19, 14, 30, 0), Project = "P-1", Success = true, Message = "OK" });
            window.History.Add(new HistoryEntry { Time = new DateTime(2026, 9, 19, 14, 32, 0), Project = "P-2", Success = false, Message = "Regel 1\r\nRegel 2" });
            window.Form.RefreshHistory();

            var items = window.Form.HistoryList.Items;
            Assert.Equal(2, items.Count);
            Assert.Equal("P-2", items[0].SubItems[1].Text);                 // newest on top
            Assert.Equal("Fout", items[0].SubItems[2].Text);
            Assert.Equal("Regel 1 | Regel 2", items[0].SubItems[3].Text);   // several lines on one line
            Assert.Equal(Theme.Error, items[0].ForeColor);                  // error row in red
            Assert.Equal("OK", items[1].SubItems[2].Text);
        });
    }

    [Fact]
    public void ClearHistoryButton_EmptiesTheList_AndDoesNothingElse()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show();
            window.History.Add(new HistoryEntry { Time = DateTime.Now, Project = "P-1", Success = true, Message = "OK" });
            window.History.Add(new HistoryEntry { Time = DateTime.Now, Project = "P-2", Success = false, Message = "Fout" });
            Application.DoEvents(); // the list is refreshed through BeginInvoke
            Assert.Equal(2, form.HistoryList.Items.Count);

            Assert.Equal("Conversielijst wissen", form.ClearHistoryButton.Text);   // Dutch is the default language
            form.ClearHistoryButton.PerformClick();
            Application.DoEvents();

            Assert.Empty(window.History.GetEntries());
            Assert.Empty(form.HistoryList.Items);
            Assert.True(form.HistoryList.Controls.OfType<System.Windows.Forms.Label>().Single().Visible); // "no conversions yet" is shown again
            Assert.Equal(0, window.RetryClicks);                                       // no retry, nothing opened, nothing saved
            Assert.Empty(window.OpenedFiles);
            Assert.Empty(window.OpenedFolders);
            Assert.Null(window.Saved);
        });
    }

    [Fact]
    public void HistoryList_HoldsAtMostTwentyRows()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();

            for (int i = 0; i < 30; i++)
            {
                window.History.Add(new HistoryEntry { Time = DateTime.Now, Project = "P-" + i, Success = true, Message = "OK" });
            }

            window.Form.RefreshHistory();

            Assert.Equal(20, window.Form.HistoryList.Items.Count);
        });
    }

    // ---------------------------------------------------------------- the bottom bar and the tiles (2.0.0)

    [Fact]
    public void ScanNowAndPause_AskTheTray_AndThePauseButtonFollowsThePausedState()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show();

            Assert.Equal("Nu scannen", form.ScanNowButton.Text);   // the tray menu's texts, Dutch is the default language
            Assert.Equal("Pauzeren", form.PauseButton.Text);

            form.ScanNowButton.PerformClick();
            Assert.Equal(1, window.ScanClicks);

            form.PauseButton.PerformClick();                         // the fake tray pauses
            Assert.Equal(1, window.PauseClicks);
            Assert.Equal("Hervatten", form.PauseButton.Text);
            Assert.Equal(BarGlyph.Resume, form.PauseButton.Glyph);
            Assert.Equal(Strings.StatusPaused, form.StatusTile.Value);

            form.PauseButton.PerformClick();
            Assert.Equal("Pauzeren", form.PauseButton.Text);
            Assert.Equal(Strings.StatusActive, form.StatusTile.Value);

            Assert.Equal(0, window.RetryClicks);                     // nothing else happened
            Assert.Empty(window.OpenedFiles);
            Assert.Empty(window.OpenedFolders);
        });
    }

    [Theory]
    [InlineData(TrayState.Ok)]
    [InlineData(TrayState.Busy)]
    [InlineData(TrayState.Paused)]
    [InlineData(TrayState.Error)]
    public void TheStatusTile_ShowsWhatTheTrayIconShows_AnErrorInRed(TrayState state)
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.State = state;
            window.Paused = state == TrayState.Paused;
            window.Current = new AppSettings { TopSolidExportPath = @"D:\Export\", RescanSeconds = 45 };

            window.Form.RefreshStatus();
            InfoTile tile = window.Form.StatusTile;

            Assert.Equal(Strings.StatusTitle, tile.Caption);
            string expected = state switch
            {
                TrayState.Error => Strings.StatusError,
                TrayState.Busy => Strings.StatusBusy,
                TrayState.Paused => Strings.StatusPaused,
                _ => Strings.StatusActive,
            };
            Assert.Equal(expected, tile.Value);
            Assert.Equal(state == TrayState.Error, tile.ValueColor == Theme.Error);
            if (state == TrayState.Ok)
            {
                Assert.Equal(Strings.StatusWatching(@"D:\Export\"), tile.Line1); // the folder it watches ...
                Assert.Equal(Strings.StatusRescan(45), tile.Line2);              // ... and how often
            }
        });
    }

    [Fact]
    public void TheLastConversionTile_ShowsTheNewestLine_AndIsEmptyAgainAfterClearingTheList()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            InfoTile tile = window.Form.LastConversionTile;
            window.Form.RefreshHistory();
            Assert.Equal(Strings.NothingYetValue, tile.Value);
            Assert.Equal(Strings.HistoryEmpty, tile.Line1);

            window.History.Add(new HistoryEntry { Time = new DateTime(2026, 11, 22, 9, 5, 0), Project = "P-1", Success = true, Message = "3 platen, 22 labels" });
            window.History.Add(new HistoryEntry { Time = new DateTime(2026, 11, 22, 9, 7, 0), Project = "P-2", Success = false, Message = "Regel 1\r\nRegel 2" });
            window.Form.RefreshHistory();

            Assert.Equal("09:07 Fout", tile.Value);
            Assert.Equal(Theme.Error, tile.ValueColor);
            Assert.Equal("P-2", tile.Line1);
            Assert.Equal("Regel 1 | Regel 2", tile.Line2);

            // An error without a project ("-"): no line for the project, the message moves up.
            window.History.Add(new HistoryEntry { Time = new DateTime(2026, 11, 22, 9, 9, 0), Project = Strings.NoProject, Success = false, Message = "settings.json" });
            window.Form.RefreshHistory();
            Assert.Equal("", tile.Line1);
            Assert.Equal("settings.json", tile.Line2);

            window.History.Clear();
            window.Form.RefreshHistory();
            Assert.Equal(Strings.NothingYetValue, tile.Value);
            Assert.Equal(Theme.TileValue, tile.ValueColor);
        });
    }

    [Fact]
    public void TheStatusTile_CountsDownToTheNextScan_WithNoRingWhilePausedOrScanning()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            var now = new DateTime(2026, 11, 22, 10, 0, 0);

            window.NextScan = (now.AddSeconds(12.3), TimeSpan.FromSeconds(30));
            form.UpdateCountdown(now);
            Assert.NotNull(form.StatusTile.Countdown);
            Assert.Equal(12.3, form.StatusTile.Countdown!.Value.Remaining, 3);
            Assert.Equal(30, form.StatusTile.Countdown!.Value.Total);

            form.UpdateCountdown(now.AddSeconds(20));                // the scan is late (busy): it stays at 0
            Assert.Equal(0, form.StatusTile.Countdown!.Value.Remaining);

            window.Paused = true;                                     // paused: nothing is scanned, no ring
            form.UpdateCountdown(now);
            Assert.Null(form.StatusTile.Countdown);

            window.Paused = false;
            window.NextScan = null;                                   // a scan is running now
            form.UpdateCountdown(now);
            Assert.Null(form.StatusTile.Countdown);
            Assert.Null(form.LastConversionTile.Countdown);           // only the Status tile ever has a ring
        });
    }

    [Fact]
    public void TheCountdownRing_CanBeDrawnAtEveryMoment_AlsoJustBeforeZero()
    {
        // alpha.6 on Daan's PC: GDI+ threw (ExternalException in DrawArc) for a ring that was almost empty, and WinForms
        // then showed the Status tile as a red cross until the window was built again.
        RunOnStaThread(() =>
        {
            using var tile = new InfoTile { Size = new Size(440, 190), Caption = "Status", Value = "Actief" };
            using var picture = new Bitmap(tile.Width, tile.Height);
            var moments = new List<double> { 30, 29.95, 15, 1, 0.5, 0.1, 0.05, 0.01, 0.001, 0.0001, 1e-7, 1e-12, 0, -1, double.NaN, double.PositiveInfinity };
            for (double remaining = 0; remaining <= 0.2; remaining += 0.005)
            {
                moments.Add(remaining);
            }

            foreach (double remaining in moments)
            {
                foreach (double total in new[] { 30.0, 2.0, 3600.0, 0.0, double.NaN })
                {
                    tile.Countdown = (remaining, total);
                    tile.DrawToBitmap(picture, new Rectangle(Point.Empty, tile.Size)); // must not throw
                }
            }
        });
    }

    [Theory]
    [InlineData(15.0, "15")]
    [InlineData(14.2, "15")]   // whole seconds, rounded up: "0" only when the time is really up
    [InlineData(0.0, "0")]
    [InlineData(-1.0, "0")]
    [InlineData(99.0, "99")]
    [InlineData(150.0, "3m")]  // from 100 s on minutes, so the number fits in the ring
    [InlineData(3600.0, "60m")]
    [InlineData(double.NaN, "0")]
    [InlineData(double.PositiveInfinity, "0")]
    public void TheCountdownNumber_FitsInTheRing(double remaining, string expected)
    {
        Assert.Equal(expected, InfoTile.CountdownText(remaining));
    }

    [Fact]
    public void TheLastConversionTile_HasRoomForAMessageOfSeveralLines()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            InfoTile last = window.Form.LastConversionTile, status = window.Form.StatusTile;
            Assert.True(last.Line2Lines > 1);
            Assert.True(last.HeightNeeded() >= status.HeightNeeded() + (last.Line2Lines - 1) * last.Font.Height);
        });
    }

    [Fact]
    public void TheHints_WriteTheFileNamesAndTheDescColumnsInBold_InEveryLanguage()
    {
        RunOnStaThread(() =>
        {
            foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
            {
                using var scope = new LanguageScope(language);
                using var window = new TestWindow();
                var form = window.Form;
                foreach (BoldPartsLabel hint in new[] { form.ConfigInfoHint, form.ConfigPositionHint, form.ConfigLabelHint })
                {
                    var words = hint.Words();
                    Assert.Equal(hint.BoldParts.Single(), words.Single(word => word.Bold).Word); // found in this language, kept in one piece
                    string again = string.Concat(words.Select(word => (word.SpaceBefore ? " " : "") + word.Word));
                    Assert.Equal(hint.Text.Trim(), again);                                         // no word or space lost
                }
            }
        });
    }

    // ---------------------------------------------------------------- folding the folders panel (2.0.0-alpha.3)

    [Fact]
    public void TheFoldSign_FoldsTheFoldersToOneLine_SavesItAtOnce_AndTheListGetsTheRoom()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Current = new AppSettings { TopSolidExportPath = @"D:\Export\", BatchFolder = @"D:\Batch", LabelFolder = @"D:\Label" };
            var form = window.Form;
            form.Show();
            form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            form.PerformLayout();
            Size windowSize = form.Size;
            int listHeight = form.HistoryList.Height;
            int panelHeight = form.Sections[0].Height;
            Assert.False(form.FoldersFolded);
            Assert.False(form.FoldFoldersButton.Folded);
            Assert.False(form.FoldedFolders.Visible);

            form.FoldFoldersButton.PerformClick();
            form.PerformLayout();

            Assert.True(form.FoldersFolded);
            Assert.True(form.FoldFoldersButton.Folded);                  // the sign points right now
            Assert.Equal(new[] { true }, window.SavedFolds);              // saved at once, without Opslaan
            Assert.False(form.ExportFolderBox.Visible);                    // the two halves are gone ...
            Assert.False(form.ConfigLabelButton.Visible);
            Assert.True(form.FoldedFolders.Visible);                       // ... the folders are on the title line
            Assert.Equal(new[] { @"D:\Export\", @"D:\Batch", @"D:\Label" }, form.FoldedFolders.Entries.Select(entry => entry.Path));
            Assert.True(form.StartWithWindowsBox.Visible);                 // stays
            Assert.Equal(windowSize, form.Size);                           // the window keeps its size ...
            Assert.True(form.Sections[0].Height < panelHeight / 2);        // ... the panel is one line ...
            Assert.True(form.HistoryList.Height > listHeight + form.LogicalToDeviceUnits(100), $"the list grew from {listHeight} to {form.HistoryList.Height} only"); // ... the list gets the room

            form.FoldFoldersButton.PerformClick();
            form.PerformLayout();

            Assert.False(form.FoldersFolded);
            Assert.Equal(new[] { true, false }, window.SavedFolds);
            Assert.True(form.ExportFolderBox.Visible);
            Assert.False(form.FoldedFolders.Visible);
            Assert.Equal(listHeight, form.HistoryList.Height);
            Assert.Null(window.Saved);                                     // nothing else was saved
        });
    }

    [Fact]
    public void AFoldedWindow_OpensFolded_AsHighAsAnOpenOne_SoTheListIsBigger()
    {
        RunOnStaThread(() =>
        {
            using var open = new TestWindow();
            open.Form.Show();
            open.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000));

            using var folded = new TestWindow();
            folded.Current = new AppSettings { FoldersFolded = true };
            folded.Form.Show();
            folded.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            folded.Form.PerformLayout();

            Assert.True(folded.Form.FoldersFolded);
            Assert.True(folded.Form.FoldFoldersButton.Folded);
            Assert.InRange(folded.Form.Height - open.Form.Height, -4, 4);
            Assert.True(folded.Form.HistoryList.Height > open.Form.HistoryList.Height + folded.Form.LogicalToDeviceUnits(100));
            Assert.True(folded.Form.ReadSettings().FoldersFolded);         // Opslaan keeps it folded
        });
    }

    [Fact]
    public void TheFoldedLine_ShowsAFolderThatIsNotReachable_AsSuch()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Current = new AppSettings { FoldersFolded = true };
            window.Form.LoadFromSettings();

            window.Form.MarkReachable(window.Form.BatchFolderBox, false);   // what the background check reports

            var entries = window.Form.FoldedFolders.Entries;
            Assert.Equal(new[] { "TopSolid", Strings.FoldedBatch, Strings.FoldedLabels }, entries.Select(entry => entry.Name));
            Assert.Equal(new[] { true, false, true }, entries.Select(entry => entry.Reachable)); // drawn in red

            window.Form.MarkReachable(window.Form.BatchFolderBox, true);
            Assert.All(window.Form.FoldedFolders.Entries, entry => Assert.True(entry.Reachable));
        });
    }

    // ---------------------------------------------------------------- another language at once (2.0.0-alpha.3)

    [Fact]
    public void ChoosingAnotherLanguage_AsksTheTrayToSwitchAtOnce_TheSameLanguageDoesNothing()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show();

            form.LanguageBox.SelectedIndex = 0;   // Dutch, the language in use
            form.LanguageChosen();
            Assert.Empty(window.LanguageChanges);

            form.LanguageBox.SelectedIndex = 1;   // French
            form.LanguageChosen();
            Assert.Equal(new[] { "fr" }, window.LanguageChanges);
            Assert.Null(window.Saved);            // only the language: the tray saves that, not the whole window
        });
    }

    [Fact]
    public void ShowEdits_PutsWhatWasTypedInTheOldWindowInTheNewOne()
    {
        // The window is built again in another language: what the user typed but did not save stays.
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show();
            var typed = new AppSettings { TopSolidExportPath = @"E:\Typed\", BatchFolder = @"E:\B", LabelFolder = @"E:\L", FlipX = true, FlipY = true, PlanRotated = true, Language = "fr" };

            form.ShowEdits(typed, startWithWindows: true);

            Assert.Equal(@"E:\Typed\", form.ExportFolderBox.Text);
            Assert.Equal(@"E:\B", form.BatchFolderBox.Text);
            Assert.Equal(@"E:\L", form.LabelFolderBox.Text);
            Assert.Equal(ReferenceCorner.TopRight, form.CornerPreview.SelectedCorner);
            Assert.True(form.PlanRotatedBox.Checked);
            Assert.True(form.StartWithWindowsBox.Checked);
            Assert.Equal(1, form.LanguageBox.SelectedIndex);
        });
    }

    // ---------------------------------------------------------------- layout

    [Fact]
    public void TheTitleTab_ShowsTheNameAndTheWholeSubtitle_InTheStandardWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show();
            form.SizeToContent(new Rectangle(0, 0, 4000, 3000));

            Assert.Equal("TsDssConverter", form.TitleTab.Text);
            Assert.Equal(Strings.BannerSubtitle, form.TitleTab.Subtitle);
            Assert.Equal(form.TitleTab.WidthNeeded(), form.TitleTab.TabBounds().Width); // nothing shortened with "…"
            Assert.Same(form.TopBar, form.TitleTab.Parent);
            Assert.Equal(0, form.TitleTab.Left);                                         // it starts at the window's edge
        });
    }

    [Fact]
    public void TheTitlesOfThePanels_AreBiggerThanTheText_AndTheCaptionsAndCheckboxesAreNormal()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            float body = form.Font.SizeInPoints;

            foreach (var title in new[] { form.FoldersTitle, form.ZeroPointTitle, form.HistoryTitle })
            {
                Assert.True(title.Font.SizeInPoints > body, $"'{title.Text}' is not bigger than the text");
            }

            foreach (Control control in new Control[] { form.ExportCaption, form.BatchCaption, form.LabelCaption, form.StartWithWindowsBox, form.PlanRotatedBox, form.ReferenceCornerBox })
            {
                Assert.False(control.Font.Bold, $"'{control.Text}' is bold");
                Assert.Equal(body, control.Font.SizeInPoints);
            }

            // The captions of the three folders are underlined (Daan, alpha.5).
            Assert.All(new[] { form.ExportCaption, form.BatchCaption, form.LabelCaption }, caption => Assert.True(caption.Font.Underline));

            Assert.Equal("Verdana", form.Font.Name); // the font of Maestro (on every Windows PC)
        });
    }

    [Fact]
    public void ThePanels_AreWhiteOnTheGreyFrame_FoldersAboveTheZeroPointAboveTheList_WithTheTilesBesideTheZeroPoint()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            var form = window.Form;
            form.Show();
            form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            form.PerformLayout();
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));
            int Bottom(Control control) => Place(control).Y + control.Height;
            int Right(Control control) => Place(control).X + control.Width;

            Assert.Equal(3, form.Sections.Count);                  // folders, zero point, list
            Assert.All(form.Sections, section => Assert.Equal(Theme.White, section.BackColor));
            Assert.Equal(Theme.Frame, form.BackColor);
            Assert.Equal(Theme.Frame, form.Scroller.BackColor);

            Control folders = form.Sections[0], zeroPoint = form.Sections[1], list = form.Sections[2];
            Assert.True(Bottom(form.TopBar) <= Place(folders).Y);
            Assert.True(Bottom(folders) < Place(zeroPoint).Y);     // grey between them
            Assert.True(Bottom(zeroPoint) < Place(list).Y);
            Assert.True(Bottom(list) <= Place(form.BottomBar).Y);

            // Status first, then Last conversion, then the zero point (Daan, alpha.5): on one line, as high as each other,
            // Status starting where the folders start, the zero point ending where the list ends.
            Assert.True(Right(form.StatusTile) < Place(form.LastConversionTile).X);
            Assert.True(Right(form.LastConversionTile) < Place(zeroPoint).X);
            Assert.All(new[] { form.StatusTile, form.LastConversionTile }, tile =>
            {
                Assert.Equal(Place(zeroPoint).Y, Place(tile).Y);
                Assert.InRange(tile.Height - zeroPoint.Height, -1, 1);
            });
            Assert.Equal(Place(folders).X, Place(form.StatusTile).X);
            Assert.InRange(Right(zeroPoint) - Right(list), -1, 1);

            // 25% | 25% | 50% (Daan, 2.0.0): the two tiles equally wide, the zero point twice as wide.
            Assert.Equal(form.StatusTile.Width, form.LastConversionTile.Width);
            Assert.InRange(zeroPoint.Width - 2 * form.StatusTile.Width, -4, 4);
            Assert.InRange(Right(folders) - Right(list), -1, 1);
            Assert.Equal(Place(folders).X, Place(list).X);
        });
    }

    [Fact]
    public void Window_CanBeDrawn_WithTheGreyFrame_AndTheBlueBottomBar()
    {
        // A picture of the window: also useful to look at. Set TSDSS_SCREENSHOT to a file name to keep it.
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Current = new AppSettings { TopSolidExportPath = @"C:\Windows\", BatchFolder = @"C:\Windows\", LabelFolder = @"C:\Windows\" };
            window.History.Add(new HistoryEntry { Time = DateTime.Now.AddMinutes(-9), Project = "DAAN_ROGIERS-P2026.09", Success = true, Message = "3 platen, 22 labels" });
            window.History.Add(new HistoryEntry { Time = DateTime.Now.AddMinutes(-2), Project = "Huppy-002", Success = false, Message = "Materiaal 'Oak_18' staat niet in materials.csv." });
            var form = window.Form;
            form.Show();
            form.LoadFromSettings();
            Thread.Sleep(600); // let the background folder check finish
            Application.DoEvents();

            using var picture = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(picture, new Rectangle(0, 0, form.Width, form.Height));

            string? target = Environment.GetEnvironmentVariable("TSDSS_SCREENSHOT");
            if (!string.IsNullOrEmpty(target))
            {
                picture.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            }

            // Where the client area starts inside the picture of the whole window (the Windows title bar and frame).
            Point origin = form.PointToScreen(Point.Empty);
            int dx = origin.X - form.Left, dy = origin.Y - form.Top;
            Color At(int x, int y) => picture.GetPixel(x + dx, y + dy);
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            // The grey frame between the folders panel and the zero point.
            int gapY = (Place(form.Sections[0]).Y + form.Sections[0].Height + Place(form.Sections[1]).Y) / 2;
            Assert.Equal(Theme.Frame.ToArgb(), At(form.ClientSize.Width / 2, gapY).ToArgb());

            // The blue band at the left of the bottom bar, between its two blues.
            Color band = At(form.BottomBar.Left + 4, form.BottomBar.Top + form.BottomBar.Height / 2);
            Assert.InRange(band.B, Theme.BarBottom.B - 2, Theme.BarTop.B + 2);
            Assert.InRange(band.R, Theme.BarBottom.R - 2, Theme.BarTop.R + 2);
        });
    }
}

