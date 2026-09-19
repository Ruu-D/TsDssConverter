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
            Form = new SettingsForm(() => Current, saved => { Saved = saved; Current = saved; }, () => { }, new ConversionHistory(), startup);
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

            Assert.Equal("Taal / Langue / Language", form.LanguageCaption.Text);
            Assert.True(form.LanguageCaption.Font.Bold);
            Assert.True(form.LanguageCaption.Bottom <= form.LanguageBox.Top);
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

    // ---------------------------------------------------------------- the footer: credit and version

    [Fact]
    public void Footer_ShowsTheDeveloperTheCompanyAndTheVersion()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();

            Assert.Equal("Dev.: Daan Verhoost  |  ROGIERS NV/SA", window.Form.CreditLink.Text);
            Assert.Equal("App-versie: 1.0.0", window.Form.VersionLabel.Text); // Dutch is the default language
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
                Assert.Equal("App version: 1.0.0", window.Form.VersionLabel.Text);
            }

            using (new LanguageScope(AppLanguage.French))
            using (var window = new Window())
            {
                Assert.Equal("Version de l'application : 1.0.0", window.Form.VersionLabel.Text);
            }

            Assert.Equal("1.0.0", AppInfo.Version); // the <Version> line in TsDssConverter.Tray.csproj
        });
    }

    [Fact]
    public void OnlyTheCompanyName_IsALinkToTheWebsiteOfTheCompany()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var link = window.Form.CreditLink;
            string company = "ROGIERS NV/SA";

            Assert.Single(link.Links);
            Assert.Equal("https://www.rogiers.be/", link.Links[0].LinkData);
            Assert.Equal(link.Text.IndexOf(company), link.Links[0].Start);
            Assert.Equal(company.Length, link.Links[0].Length);   // the whole name is clickable, and nothing else
            Assert.True(link.Links[0].Start > 0);                  // the developer's name in front is not a link
        });
    }

    [Fact]
    public void Footer_IsReadable_NotSmallerThanTheNormalText()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            float normal = window.Form.StartWithWindowsBox.Font.Size;

            Assert.True(window.Form.CreditLink.Font.Size >= normal);
            Assert.True(window.Form.VersionLabel.Font.Size >= normal);
        });
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
            Point credit = Place(form.CreditLink), version = Place(form.VersionLabel);
            int listBottom = Place(form.HistoryList).Y + form.HistoryList.Height;

            Assert.True(credit.Y >= listBottom);                               // below the table
            Assert.True(version.Y > credit.Y);                                 // credit first, version under it
            Assert.True(credit.X < form.ClientSize.Width / 4);                 // on the left side
            Assert.True(version.X < form.ClientSize.Width / 4);
            Assert.InRange(Math.Abs(credit.X - version.X), 0, form.LogicalToDeviceUnits(4)); // both lines start at the same edge

            // Not hidden below the edge (at the default size the footer is visible without scrolling).
            var footer = form.CreditLink.Parent!;
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
