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
    public void LanguageBox_SitsRightAboveStartWithWindows_WithATitleInAllThreeLanguages()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.PerformLayout();
            var form = window.Form;
            int CaptionBottom() => form.PointToClient(form.LanguageCaption.PointToScreen(new Point(0, form.LanguageCaption.Height))).Y;
            int BoxTop() => form.PointToClient(form.LanguageBox.PointToScreen(Point.Empty)).Y;

            Assert.Equal("Taal / Langue / Language", form.LanguageCaption.Text);
            Assert.True(form.LanguageCaption.Font.Bold);
            Assert.True(CaptionBottom() <= BoxTop());
            Assert.True(form.LanguageBox.Bottom <= form.StartWithWindowsBox.Top);          // right above the checkbox
            Assert.True(form.StartWithWindowsBox.Top - form.LanguageBox.Bottom < 30);       // and close to it
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
    [InlineData(AppLanguage.Dutch, "TsDssConverter - Instellingen", "Start met Windows", "Nulpunt van het label")]
    [InlineData(AppLanguage.French, "TsDssConverter - Paramètres", "Démarrer avec Windows", "Point zéro de l'étiquette")]
    [InlineData(AppLanguage.English, "TsDssConverter - Settings", "Start with Windows", "Label zero point")]
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

    // ---------------------------------------------------------------- the footer (credit) and the version (top right)

    [Fact]
    public void Footer_ShowsTheDeveloperOnLineOne_TheCompanyOnLineTwo_AndTheVersionIsNotInIt()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Form;

            Assert.Equal("Dev.: Daan Verhoost", form.CreditLabel.Text);
            Assert.Equal("ROGIERS NV/SA", form.CompanyLink.Text);
            Assert.Equal("App-versie: " + AppInfo.Version, form.VersionLabel.Text); // Dutch is the default language

            // The version is not one of the footer lines any more (the footer is the parent of the logo and the lines).
            Assert.Same(form.CreditLabel.Parent, form.CompanyLink.Parent);
            Assert.NotSame(form.CreditLabel.Parent, form.VersionLabel.Parent);
        });
    }

    [Fact]
    public void Version_SitsAtTheTopRight_JustBelowTheBanner_OnTheLineOfTheLanguageTitle()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            Point version = Place(form.VersionLabel), caption = Place(form.LanguageCaption);
            int bannerBottom = form.BannerPicture.Parent!.Bottom;
            int buttonsRight = Place(form.OpenLogButton).X + form.OpenLogButton.Width;

            Assert.True(version.Y >= bannerBottom);                                   // below the banner ...
            Assert.True(version.Y - bannerBottom < form.LogicalToDeviceUnits(30));    // ... and close to it
            Assert.Equal(caption.Y, version.Y);                                        // on the line of "Taal / Langue / Language"
            Assert.True(version.X > form.ClientSize.Width / 2);                        // on the right side
            Assert.InRange(buttonsRight - (version.X + form.VersionLabel.Width), 0, form.LogicalToDeviceUnits(2)); // ends at the right edge of the buttons
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
                Assert.Equal("Version de l'application : " + AppInfo.Version, window.Form.VersionLabel.Text);
            }

            // The number itself is the <Version> line in TsDssConverter.Tray.csproj (the only place to change it): not repeated here.
            Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.Version);
        });
    }

    [Fact]
    public void OnlyTheCompanyName_IsALinkToTheWebsiteOfTheCompany()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var link = window.Form.CompanyLink;
            string company = "ROGIERS NV/SA";

            Assert.Single(link.Links);
            Assert.Equal("https://www.rogiers.be/", link.Links[0].LinkData);
            Assert.Equal(0, link.Links[0].Start);
            Assert.Equal(company.Length, link.Links[0].Length);   // the whole name is clickable
            Assert.False(window.Form.CreditLabel is LinkLabel);    // the developer's name is plain text, not a link
        });
    }

    [Fact]
    public void Footer_IsReadable_NotSmallerThanTheNormalText()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            float normal = window.Form.StartWithWindowsBox.Font.Size;

            Assert.True(window.Form.CreditLabel.Font.Size >= normal);
            Assert.True(window.Form.CompanyLink.Font.Size >= normal);
            Assert.True(window.Form.VersionLabel.Font.Size >= normal);
        });
    }

    [Fact]
    public void Footer_HasTheSmallCompanyLogo_AtTheLeftOfTheTwoLines()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000));
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            Assert.NotNull(form.LogoPicture.Image);
            Point logo = Place(form.LogoPicture), credit = Place(form.CreditLabel), company = Place(form.CompanyLink);

            Assert.True(logo.X + form.LogoPicture.Width <= credit.X);                     // the logo is left of the texts
            Assert.True(logo.X < form.ClientSize.Width / 8);                              // and at the far left
            Assert.True(logo.Y <= credit.Y + form.CreditLabel.Height / 2);                // it starts at about the first line ...
            Assert.True(logo.Y + form.LogoPicture.Height >= company.Y + form.CompanyLink.Height / 2); // ... and ends at the second
            Assert.True(form.LogoPicture.Height < form.LogicalToDeviceUnits(60));         // small
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

    [Fact]
    public void Footer_SitsBelowTheListOfConversions_OnTheLeft_InsideTheWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(new Rectangle(0, 0, 4000, 3000)); // a big screen: the result does not depend on this PC
            window.Form.PerformLayout();
            var form = window.Form;

            // The controls sit in different panels, so compare their places in the window itself.
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));
            Point credit = Place(form.CreditLabel), company = Place(form.CompanyLink);
            int listBottom = Place(form.HistoryList).Y + form.HistoryList.Height;

            Assert.True(credit.Y >= listBottom);                               // below the table
            Assert.True(company.Y > credit.Y);                                 // developer first, company under it
            Assert.True(credit.X < form.ClientSize.Width / 4);                 // on the left side
            Assert.True(company.X < form.ClientSize.Width / 4);
            Assert.InRange(Math.Abs(credit.X - company.X), 0, form.LogicalToDeviceUnits(4)); // both lines start at the same edge

            // Not hidden below the edge (at the default size the footer is visible without scrolling).
            var footer = form.CreditLabel.Parent!;
            Assert.True(form.Scroller.PointToClient(footer.PointToScreen(new Point(0, footer.Height))).Y <= form.Scroller.ClientSize.Height);
        });
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
