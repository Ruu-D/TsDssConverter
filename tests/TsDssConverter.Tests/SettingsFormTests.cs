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
        public SettingsForm Form { get; }

        public TestWindow()
        {
            Startup = new StartWithWindows(RegistryPath, @"C:\TsDssConverter\TsDssConverter.exe");
            Form = new SettingsForm(
                () => Current, saved => { Saved = saved; Current = saved; }, () => TimesOpened++, History, Startup,
                DataFolder, OpenedFiles.Add, OpenedFolders.Add, () => RetryClicks++);
        }

        public void Dispose()
        {
            Form.AllowClose = true;
            Form.Dispose();
            TrayTests.TestRegistry.Remove(RegistryPath);
        }
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

            Assert.Equal("Open materiaaltabel", form.OpenMaterialsButton.Text);
            form.OpenMaterialsButton.PerformClick();
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
            window.Current = new AppSettings { TopSolidExportPath = @"D:\Export\", BatchFolder = @"D:\Batch", LabelFolder = @"D:\Label", FlipX = true };
            window.Form.LoadFromSettings();

            Assert.Equal(@"D:\Export\", window.Form.ExportFolderBox.Text);
            Assert.Equal(@"D:\Batch", window.Form.BatchFolderBox.Text);
            Assert.Equal(@"D:\Label", window.Form.LabelFolderBox.Text);
            Assert.True(window.Form.FlipXBox.Checked);
            Assert.False(window.Form.FlipYBox.Checked);
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
            window.Form.FlipYBox.Checked = true;
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

    // ---------------------------------------------------------------- layout

    [Fact]
    public void Banner_KeepsTheProportionsOfThePicture_AndFitsInTheWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Form.Show();

            // The size itself is a choice (BannerWidth / BannerHeight in SettingsForm.cs) and is not repeated here.
            // What must stay true: the panel and the picture have the proportions of the 540 x 84 banner (otherwise the
            // picture is zoomed smaller and blue bars appear), and the whole banner fits in the window.
            double pictureRatio = (double)window.Form.BannerPicture.Image!.Width / window.Form.BannerPicture.Image.Height;
            double bannerRatio = (double)window.Form.BannerPicture.Width / window.Form.BannerPicture.Parent!.Height;

            Assert.InRange(bannerRatio, pictureRatio - 0.05, pictureRatio + 0.05);
            Assert.True(window.Form.ClientSize.Width >= window.Form.BannerPicture.Width);
        });
    }

    [Fact]
    public void TitlesOfTheFolders_AreBold_ButTheCheckboxesAreNot()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();

            Assert.True(window.Form.ExportCaption.Font.Bold);
            Assert.True(window.Form.BatchCaption.Font.Bold);
            Assert.True(window.Form.LabelCaption.Font.Bold);
            Assert.True(window.Form.ZeroPointTitle.Font.Bold);

            Assert.False(window.Form.StartWithWindowsBox.Font.Bold);
            Assert.False(window.Form.FlipXBox.Font.Bold);
            Assert.False(window.Form.FlipYBox.Font.Bold);
        });
    }

    [Fact]
    public void DividerLines_SitBelowStartWithWindows_AboveTheDuivesteinFolders_AndAboveTheLabelZeroPoint()
    {
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Form.Show();
            window.Form.PerformLayout();
            var form = window.Form;

            Assert.Equal(3, form.Dividers.Count);
            var first = form.Dividers[0];
            var second = form.Dividers[1];
            var third = form.Dividers[2];

            // First line: right below the "Start met Windows" checkbox and above the first folder title.
            Assert.True(first.Top >= form.StartWithWindowsBox.Bottom);
            Assert.True(first.Bottom <= form.ExportCaption.Top);

            // Second line: below the two Config rows (the TopSolid part) and above "Duivestein-batchmap".
            Assert.True(second.Top >= form.ConfigPositionButton.Bottom);
            Assert.True(second.Bottom <= form.BatchCaption.Top);

            // Third line: below the last folder box and above "Nulpunt van het label".
            Assert.True(third.Top >= form.LabelFolderBox.Parent!.Bottom); // the text box sits in its rounded frame
            Assert.True(third.Top >= form.ConfigLabelButton.Bottom);       // and so does the Config row under it
            Assert.True(third.Bottom <= form.ZeroPointTitle.Top);

            // Thin, spanning the width of the window, and all in the same colour.
            Assert.All(form.Dividers, line => Assert.InRange(line.Height, 1, 3));
            Assert.All(form.Dividers, line => Assert.True(line.Width > form.ClientSize.Width * 0.8));
            Assert.All(form.Dividers, line => Assert.Equal(Theme.PaleBlue, line.BackColor));
        });
    }

    [Fact]
    public void Window_CanBeDrawn_AndTheBannerIsOnTop()
    {
        // A picture of the window: also useful to look at. Set TSDSS_SCREENSHOT to a file name to keep it.
        RunOnStaThread(() =>
        {
            using var window = new TestWindow();
            window.Current = new AppSettings { TopSolidExportPath = @"C:\Windows\", BatchFolder = @"C:\Windows\", LabelFolder = @"C:\Windows\" };
            window.History.Add(new HistoryEntry { Time = DateTime.Now.AddMinutes(-9), Project = "DAAN_ROGIERS-P2026.09", Success = true, Message = "3 platen, 22 labels" });
            window.History.Add(new HistoryEntry { Time = DateTime.Now.AddMinutes(-2), Project = "Huppy-002", Success = false, Message = "Materiaal 'Oak_18' staat niet in materials.csv." });
            window.Form.Show();
            window.Form.LoadFromSettings();
            Thread.Sleep(600); // let the background folder check finish
            Application.DoEvents();

            using var bitmap = new Bitmap(window.Form.Width, window.Form.Height);
            window.Form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));

            string? target = Environment.GetEnvironmentVariable("TSDSS_SCREENSHOT");
            if (!string.IsNullOrEmpty(target))
            {
                bitmap.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            }

            // The banner has the brand colour on top of the client area.
            Point insideBanner = new Point(bitmap.Width - 60, 40 + (window.Form.Height - window.Form.ClientSize.Height));
            Assert.Equal(Theme.Brand.ToArgb(), bitmap.GetPixel(insideBanner.X, insideBanner.Y).ToArgb());
        });
    }
}
