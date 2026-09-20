using TsDssConverter.Core;
using TsDssConverter.Tray;

namespace TsDssConverter.Tests;

/// <summary>
/// Tests that the settings window is scaled and sized correctly, so the list of conversions is visible
/// when the window opens (it was not on a 150% screen: the user had to enlarge the window).
/// Note: the real check at 150% is done by running the exe (see the README); here we guard the rules.
/// </summary>
public class WindowSizeTests
{
    private static readonly Rectangle BigScreen = new(0, 0, 4000, 3000);

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
        public string RegistryPath { get; } = @"Software\TsDssConverterTests\Size-" + Guid.NewGuid().ToString("N");
        public SettingsForm Form { get; }

        public Window()
        {
            var startup = new StartWithWindows(RegistryPath, @"C:\TsDssConverter\TsDssConverter.exe");
            Form = new SettingsForm(
                () => new AppSettings(), saved => { }, () => { }, new ConversionHistory(), startup,
                new AppDataFolder(@"C:\TsDssTestData"), path => { }, path => { });
        }

        public void Dispose()
        {
            Form.AllowClose = true;
            Form.Dispose();
            TrayTests.TestRegistry.Remove(RegistryPath);
        }
    }

    // ---------------------------------------------------------------- scaling

    [Fact]
    public void Window_ScalesWithTheScreenScaling_ByDpi()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();

            // Both settings are needed: sizes are written for 96 dpi (100%) and multiplied by the screen scaling.
            Assert.Equal(AutoScaleMode.Dpi, window.Form.AutoScaleMode);
            Assert.Equal(new SizeF(96F, 96F), window.Form.AutoScaleDimensions);
        });
    }

    [Fact]
    public void ListColumns_AreScaledByTheCode_BecauseAListViewDoesNotDoThatItself()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            var columns = window.Form.HistoryList.Columns;

            Assert.Equal(window.Form.LogicalToDeviceUnits(100), columns[0].Width); // Time
            Assert.Equal(window.Form.LogicalToDeviceUnits(150), columns[1].Width); // Project
            Assert.Equal(window.Form.LogicalToDeviceUnits(95), columns[2].Width);  // Result
        });
    }

    // ---------------------------------------------------------------- the window is as big as its content

    [Fact]
    public void OnABigScreen_EverythingIsVisible_WithoutScrolling_AndWithoutEnlargingTheWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;

            // The list of conversions is fully visible, and so is the footer below it.
            Assert.True(form.HistoryList.Height >= form.LogicalToDeviceUnits(120));
            Control footer = form.CreditLabel.Parent!;
            Assert.True(form.Scroller.PointToClient(footer.PointToScreen(new Point(0, footer.Height))).Y <= form.Scroller.ClientSize.Height);

            // Nothing to scroll: all content fits in the window.
            Assert.True(form.Scroller.AutoScrollMinSize.Height <= form.Scroller.ClientSize.Height);
            Assert.False(form.Scroller.VerticalScroll.Visible);
        });
    }

    [Fact]
    public void TheWindowHasTheHeightOfItsContent_NotMore()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            var form = window.Form;

            // The footer is the last thing in the window: the room below it is only the margin.
            Control footer = form.CreditLabel.Parent!;
            int spaceBelowFooter = form.Scroller.ClientSize.Height - form.Scroller.PointToClient(footer.PointToScreen(new Point(0, footer.Height))).Y;
            Assert.InRange(spaceBelowFooter, 0, form.LogicalToDeviceUnits(60));
        });
    }

    [Fact]
    public void WhenTheFolderWarningsAppearLater_TheyDoNotPushTheFooterOutOfView()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            var form = window.Form;

            // "Folder not reachable" is checked in the background and can appear a moment after the window opens.
            foreach (Control warning in form.Scroller.Controls[0].Controls.Cast<Control>()
                         .Where(c => c is System.Windows.Forms.Label l && l.ForeColor == Theme.Error))
            {
                warning.Visible = true;
            }

            form.PerformLayout();

            Control footer = form.CreditLabel.Parent!;
            Assert.True(form.Scroller.PointToClient(footer.PointToScreen(new Point(0, footer.Height))).Y <= form.Scroller.ClientSize.Height);
            Assert.False(form.Scroller.VerticalScroll.Visible);
        });
    }

    // ---------------------------------------------------------------- the buttons

    [Fact]
    public void SaveAndCancel_AreOneSize_SitInOneLine_AndEndAtTheRightEdgeOfTheOtherRows()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;

            var save = (Button)form.AcceptButton!;
            var cancel = (Button)form.CancelButton!;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            Assert.Equal(save.Size, cancel.Size);                 // Save (bold text) used to be smaller than Cancel
            Assert.Equal(Place(save).Y, Place(cancel).Y);          // one line
            Assert.True(Place(save).X + save.Width < Place(cancel).X); // Save left of Cancel, not on top of it

            // At the bottom of the window: below the list of conversions, on the same line as the footer.
            Assert.True(Place(save).Y >= Place(form.HistoryList).Y + form.HistoryList.Height);
            Assert.True(cancel.Bottom == cancel.Parent!.Height); // the buttons end where the footer ends
            Assert.True(Place(cancel).Y + cancel.Height >= Place(form.CompanyLink).Y);

            // The right edge of the buttons is the right edge of the list and of the divider lines.
            int buttonsRight = Place(cancel).X + cancel.Width;
            Assert.All(form.Dividers, line => Assert.Equal(buttonsRight, Place(line).X + line.Width));
            int listRight = Place(form.HistoryList).X + form.HistoryList.Width;
            Assert.InRange(listRight - buttonsRight, -1, 1); // the table rounds the width of a cell: 1 pixel at most
        });
    }

    [Fact]
    public void AllButtons_HaveTheSameNormalSize_AtEveryScaling()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            var form = window.Form;

            // 100 x 28 at 100%: big enough for "Enregistrer" in bold, and no bigger than needed.
            Size expected = new(form.LogicalToDeviceUnits(100), form.LogicalToDeviceUnits(28));
            var buttons = AllControls(form).OfType<Button>().ToList();

            Assert.Equal(10, buttons.Count); // 3 x Browse, 3 x Config, Open materials table, Open log, Save, Cancel
            Assert.All(buttons.Where(button => button != form.OpenMaterialsButton), button => Assert.Equal(expected, button.Size));

            // "Open materiaaltabel" is too long for 100 pixels: that one button may be wider, never smaller.
            Assert.Equal(expected.Height, form.OpenMaterialsButton.Height);
            Assert.True(form.OpenMaterialsButton.Width >= expected.Width);
        });
    }

    [Fact]
    public void BothConfigButtons_SitUnderTheExportFolder_OneUnderTheOther_AndOpenLogSitsAboveTheList_InLineWithTheBrowseButtons()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;

            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));
            int Bottom(Control control) => Place(control).Y + control.Height;

            // First Config button (LI): just below the export folder. The second one (LP) comes right under it,
            // and both are above the title of the batch folder.
            Assert.True(Place(form.ConfigInfoButton).Y >= Bottom(form.ExportFolderBox));
            Assert.True(Place(form.ConfigPositionButton).Y >= Bottom(form.ConfigInfoButton));
            Assert.True(Place(form.ConfigPositionButton).Y - Bottom(form.ConfigInfoButton) < form.LogicalToDeviceUnits(12)); // close under it
            Assert.True(Place(form.ConfigPositionHint).Y >= Bottom(form.ConfigInfoHint));
            Assert.True(Bottom(form.ConfigPositionButton) <= Place(form.BatchCaption).Y);

            // Third Config button (label CSV): just below the Duivestein label folder, above the line and the zero-point title.
            Assert.True(Place(form.ConfigLabelButton).Y >= Bottom(form.LabelFolderBox));
            Assert.True(Bottom(form.ConfigLabelButton) <= Place(form.ZeroPointTitle).Y);
            Assert.True(Place(form.ConfigLabelButton).Y >= Bottom(form.BatchFolderBox));

            // "Open log": above the list of conversions (on the line of its title).
            Assert.True(Bottom(form.OpenLogButton) <= Place(form.HistoryList).Y);
            Assert.True(Place(form.OpenLogButton).Y >= Bottom(form.FlipYBox));

            // The three buttons at the right sit exactly in the column of the Browse buttons.
            var browse = AllControls(form).OfType<Button>().Where(b => b.Text == Strings.Browse).ToList();
            Assert.Equal(3, browse.Count);
            Assert.All(new[] { form.ConfigInfoButton, form.ConfigPositionButton, form.ConfigLabelButton, form.OpenLogButton },
                button => Assert.Equal(Place(browse[0]).X, Place(button).X));
        });
    }

    [Fact]
    public void OpenMaterialsTable_SitsLeftOfOpenLog_OnTheSameLine_WithARoomBetween()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            Point materials = Place(form.OpenMaterialsButton), log = Place(form.OpenLogButton);
            int gap = log.X - (materials.X + form.OpenMaterialsButton.Width);

            Assert.Equal(log.Y, materials.Y);                                        // one line
            Assert.InRange(gap, 1, form.LogicalToDeviceUnits(20));                    // left of it, not touching, not far away
            Assert.True(Place(form.OpenMaterialsButton).Y + form.OpenMaterialsButton.Height <= Place(form.HistoryList).Y);
        });
    }

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void TheNewButtonTexts_FitInTheButtons_AndTheHintsDoNotRunIntoTheirButton(AppLanguage language)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;

            foreach (Button button in new[] { form.ConfigInfoButton, form.ConfigPositionButton, form.ConfigLabelButton, form.OpenMaterialsButton, form.OpenLogButton })
            {
                int textWidth = TextRenderer.MeasureText(button.Text, button.Font).Width;
                Assert.True(textWidth + form.LogicalToDeviceUnits(16) <= button.Width, $"'{button.Text}' does not fit in its button in {language}");
            }

            Assert.True(form.ConfigInfoHint.Right <= form.ConfigInfoButton.Left, $"The LI hint runs into its button in {language}");
            Assert.True(form.ConfigPositionHint.Right <= form.ConfigPositionButton.Left, $"The LP hint runs into its button in {language}");
            Assert.True(form.ConfigLabelHint.Right <= form.ConfigLabelButton.Left, $"The label hint runs into its button in {language}");
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

    // ---------------------------------------------------------------- a small screen

    [Fact]
    public void OnASmallScreen_TheWindowFitsTheScreen_TheSettingsScroll_AndTheListKeepsItsSize()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            var small = new Rectangle(0, 0, 1400, 600);   // for example 1080p at 150%: 720 high, minus the taskbar

            window.Form.SizeToContent(small);
            window.Form.PerformLayout();
            var form = window.Form;

            Assert.True(form.Height <= small.Height);                                          // fits the screen
            Assert.True(form.Scroller.AutoScrollMinSize.Height > form.Scroller.ClientSize.Height); // so it scrolls
            Assert.True(form.HistoryList.Height >= form.LogicalToDeviceUnits(120));            // the table is never squeezed out
        });
    }

    [Fact]
    public void FitToArea_KeepsTheWindowInsideTheFreeScreenArea_AndCentresIt()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            var area = new Rectangle(100, 50, 1300, 700);

            window.Form.FitToArea(area);
            var form = window.Form;

            Assert.True(form.Left >= area.Left && form.Right <= area.Right);
            Assert.True(form.Top >= area.Top && form.Bottom <= area.Bottom);
            Assert.InRange(form.Left - area.Left, 0, 1 + (area.Width - form.Width) / 2);
        });
    }

    [Fact]
    public void FitToArea_NeverMakesTheWindowBiggerThanItWas()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            Size before = window.Form.Size;

            window.Form.FitToArea(BigScreen);

            Assert.Equal(before, window.Form.Size);
        });
    }
}
