using TsDssConverter.Core;
using TsDssConverter.Tray;

namespace TsDssConverter.Tests;

/// <summary>Tests for the language box and the credit in the settings window, and for the window in each language.</summary>
public class LanguageWindowTests
{
    // Windows Forms needs its own STA thread (see SettingsFormTests).
    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            throw new Exception("The test failed on the STA thread: " + failure.Message, failure);
        }
    }

    private sealed class Window : IDisposable
    {
        public AppSettings Current { get; set; } = new();
        public AppSettings? Saved { get; private set; }
        public string RegistryPath { get; } = @"Software\TsDssConverterTests\Lang-" + Guid.NewGuid().ToString("N");
        public SettingsForm Form { get; }

        public Window()
        {
            var startup = new StartWithWindows(RegistryPath, @"C:\TsDssConverter\TsDssConverter.exe");
            Form = new SettingsForm(
                () => Current, saved => { Saved = saved; Current = saved; }, () => { }, new ConversionHistory(), startup,
                new AppDataFolder(@"C:\TsDssTestData"), path => { }, path => { }, () => { });
        }

        public void Dispose()
        {
            Form.AllowClose = true;
            Form.Dispose();
            TrayTests.TestRegistry.Remove(RegistryPath);
        }
    }

    // ---------------------------------------------------------------- the language box

    [Fact]
    public void LanguageBox_OffersTheThreeLanguages_AsWritten_AndOnlyByChoosing()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var box = window.Form.LanguageBox;

            Assert.Equal(ComboBoxStyle.DropDownList, box.DropDownStyle); // choose from the list, no typing
            Assert.Equal(
                new[] { "Dutch - Nederlands", "French - Français", "English - Engels" },
                box.Items.Cast<object>().Select(item => item.ToString()));
        });
    }

    [Fact]
    public void LanguageBox_SitsInTheTopBar_UnderATitleInAllThreeLanguages_RightOfTheTitleTab()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            Assert.Equal("Taal / Langue / Language", form.LanguageCaption.Text);
            Assert.True(Place(form.LanguageCaption).Y + form.LanguageCaption.Height <= Place(form.LanguageBox).Y); // the title above the box
            Assert.InRange(Place(form.LanguageBox).X - Place(form.LanguageCaption).X, -1, 1);                     // both start at one place
            Assert.True(Place(form.LanguageBox).Y + form.LanguageBox.Height <= form.TopBar.Bottom);              // both in the top bar
            Assert.True(Place(form.LanguageCaption).X >= form.TitleTab.TabBounds().Right);                     // not over the title tab
        });
    }

    [Theory]
    [InlineData("nl", 0)]
    [InlineData("fr", 1)]
    [InlineData("en", 2)]
    public void LanguageBox_ShowsTheLanguageOfTheSettings(string code, int expectedIndex)
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Current = new AppSettings { Language = code };

            window.Form.LoadFromSettings();

            Assert.Equal(expectedIndex, window.Form.LanguageBox.SelectedIndex);
        });
    }

    [Theory]
    [InlineData(0, "nl")]
    [InlineData(1, "fr")]
    [InlineData(2, "en")]
    public void Save_GivesTheChosenLanguage(int chosenIndex, string expectedCode)
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.LanguageBox.SelectedIndex = chosenIndex;

            ((Button)window.Form.AcceptButton!).PerformClick();

            Assert.Equal(expectedCode, window.Saved!.Language);
        });
    }

    [Fact]
    public void Cancel_ForgetsTheChosenLanguage()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.LanguageBox.SelectedIndex = 2;

            ((Button)window.Form.CancelButton!).PerformClick();
            window.Form.Show();

            Assert.Null(window.Saved);
            Assert.Equal(0, window.Form.LanguageBox.SelectedIndex); // Dutch again
        });
    }

    // ---------------------------------------------------------------- the window in each language

    [Theory]
    [InlineData(AppLanguage.Dutch, "TsDssConverter - Instellingen", "Start met Windows", "Nulpunt etiket nesting (vanuit welke hoek Duivestein meet)")]
    [InlineData(AppLanguage.French, "TsDssConverter - Paramètres", "Démarrer avec Windows", "Point zéro étiquette nesting (le coin à partir duquel Duivestein mesure)")]
    [InlineData(AppLanguage.English, "TsDssConverter - Settings", "Start with Windows", "Nesting label zero point (which corner Duivestein measures from)")]
    public void TheWholeWindow_IsBuiltInTheSelectedLanguage(AppLanguage language, string title, string startWithWindows, string zeroPoint)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();

            Assert.Equal(title, window.Form.Text);
            Assert.Equal(startWithWindows, window.Form.StartWithWindowsBox.Text);
            Assert.Equal(zeroPoint, window.Form.ZeroPointTitle.Text);
            Assert.Equal(Strings.ExportFolder, window.Form.ExportCaption.Text);
            Assert.Equal(Strings.HistoryEmpty, window.Form.HistoryList.Controls.OfType<System.Windows.Forms.Label>().Single().Text);
        });
    }

    [Fact]
    public void TheLanguageBox_KeepsItsOwnTexts_InEveryLanguage()
    {
        // The names of the languages do not change with the interface, so everybody can find their own.
        foreach (AppLanguage language in new[] { AppLanguage.Dutch, AppLanguage.French, AppLanguage.English })
        {
            RunOnStaThread(() =>
            {
                using var scope = new LanguageScope(language);
                using var window = new Window();

                Assert.Equal("French - Français", window.Form.LanguageBox.Items[1].ToString());
                Assert.Equal("Taal / Langue / Language", window.Form.LanguageCaption.Text);
            });
        }
    }

    // ---------------------------------------------------------------- the credit, the version, the logo and the "?" (top bar)

    [Fact]
    public void Credits_ShowTheDeveloperAboveTheVersion_RightAligned_InTheTopBar()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            Assert.Equal("Dev.: Daan Verhoost", form.CreditLabel.Text);
            Assert.Equal("App-versie: " + AppInfo.Version, form.VersionLabel.Text); // Dutch is the default language

            // "Dev.: Daan Verhoost" above "App-versie: ...", both ending at the same right edge.
            Assert.True(Place(form.CreditLabel).Y + form.CreditLabel.Height <= Place(form.VersionLabel).Y);
            Assert.InRange(Place(form.CreditLabel).X + form.CreditLabel.Width - (Place(form.VersionLabel).X + form.VersionLabel.Width), -1, 1);

            // In the top bar, right of the language box, on the right side of the window.
            Assert.True(Place(form.VersionLabel).Y + form.VersionLabel.Height <= form.TopBar.Bottom);
            Assert.True(Place(form.CreditLabel).X > Place(form.LanguageBox).X + form.LanguageBox.Width);
            Assert.True(Place(form.CreditLabel).X > form.ClientSize.Width / 2);
        });
    }

    [Fact]
    public void Version_IsTheVersionOfTheProgram_InTheSelectedLanguage()
    {
        RunOnStaThread(() =>
        {
            using (new LanguageScope(AppLanguage.English))
            using (var window = new Window())
            {
                Assert.Equal("App version: " + AppInfo.Version, window.Form.VersionLabel.Text);
            }

            using (new LanguageScope(AppLanguage.French))
            using (var window = new Window())
            {
                Assert.Equal("Version app : " + AppInfo.Version, window.Form.VersionLabel.Text);
            }

            // The number itself is the <Version> line in TsDssConverter.Tray.csproj (the only place to change it): not repeated here.
            // A test build may have a suffix ("2.0.0-alpha"); never the "+commit" the build adds.
            Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$", AppInfo.Version);
        });
    }

    [Fact]
    public void OnlyTheLogo_IsTheLinkToTheWebsite_ThereIsNoCompanyNameText()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();

            // Clicking the logo would open a browser, so only what the user sees is checked: the hand cursor.
            Assert.Equal(Cursors.Hand, window.Form.LogoPicture.Cursor);
            Assert.False(window.Form.CreditLabel is LinkLabel);

            // The text "ROGIERS NV/SA" was removed from the window (Daan, 1.1.0): no control shows it.
            Assert.DoesNotContain(AllControls(window.Form), control => control.Text == "ROGIERS NV/SA");
        });
    }

    private static IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control grandChild in AllControls(child))
            {
                yield return grandChild;
            }
        }
    }

    [Fact]
    public void TheLogoAndTheUserGuide_SitAtTheRightEndOfTheTopBar_InTheMiddleOfItsHeight()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));
            int Middle(Control control) => Place(control).Y + control.Height / 2;

            Assert.NotNull(form.LogoPicture.Image);
            Point logo = Place(form.LogoPicture), guide = Place(form.UserGuideButton);

            // Credit, then the logo, then the "?", then the cog of the Magic parameters (2.0.1), the last thing of the bar,
            // close to the window's right edge.
            Point cog = Place(form.MagicParametersButton);
            Assert.True(Place(form.VersionLabel).X + form.VersionLabel.Width <= logo.X);
            Assert.True(logo.X + form.LogoPicture.Width <= guide.X);
            Assert.True(guide.X + form.UserGuideButton.Width <= cog.X);
            Assert.InRange(form.ClientSize.Width - (cog.X + form.MagicParametersButton.Width), 0, form.LogicalToDeviceUnits(30));
            Assert.Equal(form.UserGuideButton.Size, form.MagicParametersButton.Size);

            // All three in the middle of the bar's height.
            int barMiddle = form.TopBar.Top + form.TopBar.Height / 2;
            Assert.InRange(Middle(form.LogoPicture) - barMiddle, -form.LogicalToDeviceUnits(2), form.LogicalToDeviceUnits(2));
            Assert.InRange(Middle(form.UserGuideButton) - barMiddle, -form.LogicalToDeviceUnits(2), form.LogicalToDeviceUnits(2));
            Assert.InRange(Middle(form.MagicParametersButton) - barMiddle, -form.LogicalToDeviceUnits(2), form.LogicalToDeviceUnits(2));

            // As high as the disc of the "?" (Daan, 2026-11-22), and as wide as its proportions need: the logo fills its box,
            // so the drawn logo is exactly that high. Not squeezed (it once ended up 2 pixels wide in a Percent column).
            int disc = form.UserGuideButton.LogicalToDeviceUnits(TsDssConverter.Tray.HelpButton.DiscSize);
            Assert.InRange(form.LogoPicture.Height - disc, -1, 1);
            Image picture = form.LogoPicture.Image!;
            double expectedWidth = (double)form.LogoPicture.Height * picture.Width / picture.Height;
            Assert.InRange(form.LogoPicture.Width, expectedWidth - 2, expectedWidth + 2);

            // Clicking would open a browser, so only what the user sees is checked: the hand cursor. And the address.
            Assert.Equal(Cursors.Hand, form.UserGuideButton.Cursor);
            Assert.Equal("https://github.com/Ruu-D/TsDssConverter", Strings.UserGuideUrl);
        });
    }

    [Fact]
    public void Credits_AreReadable_NotSmallerThanTheNormalText()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            float normal = window.Form.StartWithWindowsBox.Font.Size;

            Assert.True(window.Form.CreditLabel.Font.Size >= normal);
            Assert.True(window.Form.VersionLabel.Font.Size >= normal);
        });
    }

    [Fact]
    public void Logo_IsCutToTheVisiblePart_AndMadeSmallOnce()
    {
        // The picture file has a transparent margin. If it was left on, the logo would be indented and look too small.
        using var logo = (Bitmap)AppIcons.CompanyLogo();

        Assert.Equal(128, Math.Max(logo.Width, logo.Height));

        bool VisibleIn(IEnumerable<Point> pixels) => pixels.Any(p => logo.GetPixel(p.X, p.Y).A > 16);
        Assert.True(VisibleIn(Enumerable.Range(0, logo.Height).Select(y => new Point(0, y))), "left edge is empty");
        Assert.True(VisibleIn(Enumerable.Range(0, logo.Height).Select(y => new Point(logo.Width - 1, y))), "right edge is empty");
        Assert.True(VisibleIn(Enumerable.Range(0, logo.Width).Select(x => new Point(x, 0))), "top edge is empty");
        Assert.True(VisibleIn(Enumerable.Range(0, logo.Width).Select(x => new Point(x, logo.Height - 1))), "bottom edge is empty");
    }

    // ---------------------------------------------------------------- pictures of the window in each language

    [Theory]
    [InlineData(AppLanguage.Dutch, "nl")]
    [InlineData(AppLanguage.French, "fr")]
    [InlineData(AppLanguage.English, "en")]
    public void Window_CanBeDrawn_InEveryLanguage(AppLanguage language, string code)
    {
        // Set TSDSS_SCREENSHOTS to a folder to keep the pictures: window-nl.png, window-fr.png, window-en.png.
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            window.Current = new AppSettings { TopSolidExportPath = @"C:\Windows\", BatchFolder = @"C:\Windows\", LabelFolder = @"C:\Windows\", Language = code };
            window.Form.Show();
            window.Form.LoadFromSettings();
            Thread.Sleep(600);
            Application.DoEvents();

            using var bitmap = new Bitmap(window.Form.Width, window.Form.Height);
            window.Form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));

            string? folder = Environment.GetEnvironmentVariable("TSDSS_SCREENSHOTS");
            if (!string.IsNullOrEmpty(folder))
            {
                bitmap.Save(System.IO.Path.Combine(folder, $"window-{code}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }

            // Nothing may be cut off at the bottom: the buttons and the list must fit in the window.
            Assert.True(window.Form.HistoryList.Bottom <= window.Form.ClientSize.Height);
        });
    }
}
