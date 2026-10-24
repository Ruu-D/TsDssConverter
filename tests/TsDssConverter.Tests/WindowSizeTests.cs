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
                new AppDataFolder(@"C:\TsDssTestData"), path => { }, path => { }, () => { });
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

            // The list of conversions is fully visible, and so are Save / Cancel below it.
            Assert.True(form.HistoryList.Height >= form.LogicalToDeviceUnits(120));
            Control footer = ((Button)form.AcceptButton!).Parent!; // the last row: Save / Cancel
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

            // Save / Cancel are the last thing in the window: the room below them is only the margin.
            Control footer = ((Button)form.AcceptButton!).Parent!; // the last row: Save / Cancel
            int spaceBelowFooter = form.Scroller.ClientSize.Height - form.Scroller.PointToClient(footer.PointToScreen(new Point(0, footer.Height))).Y;
            Assert.InRange(spaceBelowFooter, 0, form.LogicalToDeviceUnits(60));
        });
    }

    [Fact]
    public void TheWhiteRoomBelowSaveAndCancel_IsAsWideAsTheRoomAtTheLeftAndRight()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;
            var cancel = (Button)form.CancelButton!;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            int left = Place(form.Dividers[0]).X;                                        // the content starts here (a line over the full width)
            int right = form.ClientSize.Width - (Place(cancel).X + cancel.Width);        // Cancel ends at the right edge of the content
            int below = form.ClientSize.Height - (Place(cancel).Y + cancel.Height);      // Save / Cancel are the last thing in the window

            Assert.True(left > 0);
            Assert.InRange(right, left - 1, left + 1);
            Assert.InRange(below, left - 1, left + 1);
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

            Control footer = ((Button)form.AcceptButton!).Parent!; // the last row: Save / Cancel
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

            // At the bottom of the window: below the list of conversions.
            Assert.True(Place(save).Y >= Place(form.HistoryList).Y + form.HistoryList.Height);

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

            Assert.Equal(11, buttons.Count); // 3 x Browse, 3 x Config, Open materials table, Retry failed, Open log, Save, Cancel

            var wideButtons = new Button[] { form.OpenMaterialsButton, form.RetryFailedButton };
            Assert.All(buttons.Where(button => !wideButtons.Contains(button)), button => Assert.Equal(expected, button.Size));

            // "Open materiaaltabel" and "Herstart mislukte" are too long for 100 pixels: those buttons may be wider, never smaller.
            Assert.All(wideButtons, button => Assert.Equal(expected.Height, button.Height));
            Assert.All(wideButtons, button => Assert.True(button.Width >= expected.Width));
        });
    }

    [Fact]
    public void TheTopSolidAndDuivesteinFolders_SitSideBySide_WithAVerticalLineBetween()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));
            int Right(Control control) => Place(control).X + control.Width;

            // The two halves start on the same line.
            Assert.Equal(Place(form.ExportCaption).Y, Place(form.BatchCaption).Y);

            // TopSolid left of the line, Duivestein right of it.
            Point line = Place(form.FolderDivider);
            Assert.True(Right(form.OpenMaterialsButton) < line.X);
            Assert.True(Right(form.ConfigPositionButton) < line.X);
            Assert.True(Place(form.BatchCaption).X > line.X);
            Assert.True(Place(form.ConfigLabelButton).X > line.X);

            // Thin, pale blue, and from the folder titles to the end of the taller half.
            Assert.InRange(form.FolderDivider.Width, 1, 3);
            Assert.Equal(Theme.PaleBlue, form.FolderDivider.BackColor);
            Assert.True(line.Y <= Place(form.ExportCaption).Y);
            Assert.True(line.Y + form.FolderDivider.Height >= Place(form.OpenMaterialsButton).Y + form.OpenMaterialsButton.Height);

            // The two halves are about equally wide (the folder boxes sit in rounded frames).
            int left = Right(form.ExportFolderBox.Parent!) - Place(form.ExportFolderBox.Parent!).X;
            int right = Right(form.BatchFolderBox.Parent!) - Place(form.BatchFolderBox.Parent!).X;
            Assert.InRange(left - right, -2, 2);
        });
    }

    [Fact]
    public void TheConfigButtons_SitUnderTheirFolder_OneUnderTheOther_AndOpenLogSitsAboveTheList_InLineWithTheBrowseButtons()
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

            // First Config button (LI): just below the export folder. The second one (LP) comes right under it.
            Assert.True(Place(form.ConfigInfoButton).Y >= Bottom(form.ExportFolderBox));
            Assert.True(Place(form.ConfigPositionButton).Y >= Bottom(form.ConfigInfoButton));
            // Close under it: a row is as high as its hint when that wraps onto two lines.
            Assert.True(Place(form.ConfigPositionButton).Y - Bottom(form.ConfigInfoButton)
                < Math.Max(0, form.ConfigInfoHint.Height - form.ConfigInfoButton.Height) + form.LogicalToDeviceUnits(12));
            Assert.True(Place(form.ConfigPositionHint).Y >= Bottom(form.ConfigInfoHint));
            Assert.True(Bottom(form.ConfigPositionButton) <= Place(form.ZeroPointTitle).Y);

            // Third Config button (label CSV): just below the Duivestein label folder, above the line and the zero-point title.
            Assert.True(Place(form.ConfigLabelButton).Y >= Bottom(form.LabelFolderBox));
            Assert.True(Bottom(form.ConfigLabelButton) <= Place(form.ZeroPointTitle).Y);
            Assert.True(Place(form.ConfigLabelButton).Y >= Bottom(form.BatchFolderBox));

            // "Open log": above the list of conversions (on the line of its title).
            Assert.True(Bottom(form.OpenLogButton) <= Place(form.HistoryList).Y);
            Assert.True(Place(form.OpenLogButton).Y >= Bottom(form.PlanRotatedBox));

            // The buttons sit exactly in the column of the Browse buttons of their half: the export one (left half),
            // and the batch one (right half, the right edge of the window, like Open log).
            var browse = AllControls(form).OfType<Button>().Where(b => b.Text == Strings.Browse).OrderBy(b => Place(b).X).ToList();
            Assert.Equal(3, browse.Count);
            Assert.All(new[] { form.ConfigInfoButton, form.ConfigPositionButton },
                button => Assert.Equal(Place(browse[0]).X, Place(button).X));
            Assert.All(new[] { form.ConfigLabelButton, form.OpenLogButton },
                button => Assert.Equal(Place(browse[2]).X, Place(button).X));
        });
    }

    [Fact]
    public void ReferenceCornerSection_IsTwoColumns_DropDownAndCheckboxLeft_SchematicRight()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            // The drop-down keeps its real width: a regression guard for a real bug (a Percent column in an AutoSize
            // TableLayoutPanel silently shrank it to ~75 px, cutting every corner name off to two or three letters).
            Assert.True(form.ReferenceCornerBox.Width >= form.LogicalToDeviceUnits(200), $"ReferenceCornerBox is only {form.ReferenceCornerBox.Width} px wide");

            // The schematic sits to the right of the drop-down and the checkbox (two columns).
            Assert.True(Place(form.CornerPreview).X > Place(form.ReferenceCornerBox).X + form.ReferenceCornerBox.Width);
            Assert.True(Place(form.CornerPreview).X > Place(form.PlanRotatedBox).X + form.PlanRotatedBox.Width);

            // It lines up with the drop-down specifically (top-aligned to the same row), not centred across the
            // drop-down + checkbox stack.
            Assert.InRange(Place(form.CornerPreview).Y - Place(form.ReferenceCornerBox).Y, -2, 2);

            // The checkbox sits right under the drop-down (a vertical stack in the left column), below the schematic's row.
            Assert.True(Place(form.PlanRotatedBox).Y >= Place(form.ReferenceCornerBox).Y + form.ReferenceCornerBox.Height);
        });
    }

    [Fact]
    public void RetryFailed_SitsLeftOfOpenLog_OnTheSameLine_WithARoomBetween()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            window.Form.SizeToContent(BigScreen);
            window.Form.PerformLayout();
            var form = window.Form;
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            Point retry = Place(form.RetryFailedButton), log = Place(form.OpenLogButton);
            int gap = log.X - (retry.X + form.RetryFailedButton.Width);

            Assert.Equal(log.Y, retry.Y);                                            // one line
            Assert.InRange(gap, 1, form.LogicalToDeviceUnits(20));                    // left of it, not touching, not far away
            Assert.True(retry.Y + form.RetryFailedButton.Height <= Place(form.HistoryList).Y);
            Assert.True(retry.Y >= Place(form.PlanRotatedBox).Y + form.PlanRotatedBox.Height); // on the line of the title of the list
        });
    }

    [Fact]
    public void OpenMaterialsTable_SitsUnderTheTwoConfigButtons_InTheExportSection_AndEndsAtTheRightEdgeOfTheBrowseButtons()
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

            // Right under the LP Config button, in the TopSolid half (left of the line).
            Assert.True(Place(form.OpenMaterialsButton).Y >= Bottom(form.ConfigPositionButton));
            Assert.True(Place(form.OpenMaterialsButton).Y - Bottom(form.ConfigPositionButton)
                < Math.Max(0, form.ConfigPositionHint.Height - form.ConfigPositionButton.Height) + form.LogicalToDeviceUnits(12));
            Assert.True(Place(form.MaterialsHint).Y >= Bottom(form.ConfigPositionHint));
            Assert.True(Place(form.OpenMaterialsButton).X + form.OpenMaterialsButton.Width < Place(form.FolderDivider).X);

            // It is wider than a Browse button, but ends at the same right edge (and does not make the Browse column wider).
            var browse = AllControls(form).OfType<Button>().First(b => b.Text == Strings.Browse);
            int materialsRight = Place(form.OpenMaterialsButton).X + form.OpenMaterialsButton.Width;
            int browseRight = Place(browse).X + browse.Width;
            Assert.InRange(materialsRight - browseRight, -1, 1);
            Assert.Equal(form.LogicalToDeviceUnits(100), browse.Width);
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

            foreach (Button button in new[] { form.ConfigInfoButton, form.ConfigPositionButton, form.ConfigLabelButton, form.OpenMaterialsButton, form.RetryFailedButton, form.OpenLogButton })
            {
                int textWidth = TextRenderer.MeasureText(button.Text, button.Font).Width;
                Assert.True(textWidth + form.LogicalToDeviceUnits(16) <= button.Width, $"'{button.Text}' does not fit in its button in {language}");
            }

            Assert.True(form.ConfigInfoHint.Right <= form.ConfigInfoButton.Left, $"The LI hint runs into its button in {language}");
            Assert.True(form.ConfigPositionHint.Right <= form.ConfigPositionButton.Left, $"The LP hint runs into its button in {language}");
            Assert.True(form.ConfigLabelHint.Right <= form.ConfigLabelButton.Left, $"The label hint runs into its button in {language}");
            Assert.True(form.MaterialsHint.Right <= form.OpenMaterialsButton.Left, $"The materials hint runs into its button in {language}");
        });
    }

    /// <summary>
    /// Makes the window as it would be at 150%: every size AND every font 1.5 times as big (Form.Scale alone does not
    /// change the fonts). Only fonts set on a control itself are changed; the others follow their parent.
    /// </summary>
    private static void SimulateScaling(Form form, float factor)
    {
        form.Scale(new SizeF(factor, factor));
        var ownFont = AllControls(form).Prepend(form).Where(c => c.Parent == null || !ReferenceEquals(c.Font, c.Parent.Font)).ToList();
        foreach (Control control in ownFont)
        {
            control.Font = new Font(control.Font.FontFamily, control.Font.Size * factor, control.Font.Style);
        }
    }

    [Theory]
    [InlineData(AppLanguage.Dutch, 1.0f)]
    [InlineData(AppLanguage.French, 1.0f)]
    [InlineData(AppLanguage.English, 1.0f)]
    [InlineData(AppLanguage.Dutch, 1.5f)]
    [InlineData(AppLanguage.French, 1.5f)]
    [InlineData(AppLanguage.English, 1.5f)]
    public void EveryHint_SitsBesideItsButton_AndWrapsOnAFewLinesAtMost(AppLanguage language, float scaling)
    {
        // Regression guard: in 1.1.0 the hints of "Open materiaaltabel" and of the label Config button ended up far below
        // their buttons on Daan's 150% screens (rows of ten lines high).
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Form;
            if (scaling != 1.0f)
            {
                SimulateScaling(form, scaling);
            }
            form.Show();
            form.SizeToContent(BigScreen);
            form.PerformLayout();
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            var pairs = new (System.Windows.Forms.Label Hint, Button Button)[]
            {
                (form.ConfigInfoHint, form.ConfigInfoButton),
                (form.ConfigPositionHint, form.ConfigPositionButton),
                (form.MaterialsHint, form.OpenMaterialsButton),
                (form.ConfigLabelHint, form.ConfigLabelButton),
            };

            foreach (var (hint, button) in pairs)
            {
                // Beside its button: a two-line hint is centred in a row a little higher than the button, so a few pixels
                // of room (the broken 1.1.0 was off by about 190 pixels).
                int hintMiddle = Place(hint).Y + hint.Height / 2;
                int room = form.LogicalToDeviceUnits(8);
                Assert.InRange(hintMiddle, Place(button).Y - room, Place(button).Y + button.Height + room);
                Assert.True(hint.Height <= hint.Font.Height * 3 + 4, $"'{hint.Text}' is {hint.Height} px high"); // three lines at most
                Assert.True(Place(hint).X + hint.Width <= Place(button).X);                                  // not into the button
                Assert.True(hint.GetPreferredSize(new Size(hint.Width, 0)).Height <= hint.Height, $"'{hint.Text}' is cut off"); // all visible
            }
        });
    }

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    public void AfterTheWindowWasNarrowForAMoment_TheRowsShrinkBack_AndTheHintsAreBesideTheirButtonsAgain(AppLanguage language)
    {
        // Regression guard for 1.1.0 at 125% / 150%: while Windows scales the window, it is laid out for a moment at a
        // much narrower width, so the hints wrap onto many lines and the rows grow. The folders row stayed that high
        // afterwards (the vertical line and the two halves stretched to it and then held it), and the last hint of each
        // half ended far below its button. Here the narrow moment is made on purpose.
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Form;
            form.Show();
            form.SizeToContent(BigScreen);
            Size normal = form.ClientSize;

            form.ClientSize = new Size(form.LogicalToDeviceUnits(400), normal.Height); // narrower than allowed: the minimum width wins
            form.MinimumSize = Size.Empty;
            form.ClientSize = new Size(form.LogicalToDeviceUnits(400), normal.Height);
            form.PerformLayout();
            form.ClientSize = normal;
            form.PerformLayout();
            Point Place(Control control) => form.PointToClient(control.PointToScreen(Point.Empty));

            foreach (var (hint, button) in new (Control Hint, Control Button)[]
            {
                (form.ConfigInfoHint, form.ConfigInfoButton),
                (form.ConfigPositionHint, form.ConfigPositionButton),
                (form.MaterialsHint, form.OpenMaterialsButton),
                (form.ConfigLabelHint, form.ConfigLabelButton),
            })
            {
                int hintMiddle = Place(hint).Y + hint.Height / 2;
                int room = form.LogicalToDeviceUnits(8);
                Assert.InRange(hintMiddle, Place(button).Y - room, Place(button).Y + button.Height + room);
            }

            // The vertical line is only as high as the higher half, not higher.
            int highestHalf = Math.Max(
                Place(form.OpenMaterialsButton).Y + form.OpenMaterialsButton.Height,
                Place(form.ConfigLabelButton).Y + form.ConfigLabelButton.Height);
            Assert.InRange(Place(form.FolderDivider).Y + form.FolderDivider.Height - highestHalf, -form.LogicalToDeviceUnits(30), form.LogicalToDeviceUnits(30));
        });
    }

    [Fact]
    public void TheWideButtons_AreNoWiderThanTheirTextNeeds()
    {
        // Regression guard: up to 1.1.0 the text was measured at the screen's scaling and then scaled once more, so on a
        // 150% screen "Open materiaaltabel" and "Herstart mislukte" were half as wide again as needed.
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Form;
            form.Show();

            foreach (Button button in new[] { form.OpenMaterialsButton, form.RetryFailedButton })
            {
                int needed = Math.Max(form.LogicalToDeviceUnits(100), TextRenderer.MeasureText(button.Text, button.Font).Width + form.LogicalToDeviceUnits(24));
                Assert.InRange(button.Width, needed - form.LogicalToDeviceUnits(4), needed + form.LogicalToDeviceUnits(4));
            }
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
