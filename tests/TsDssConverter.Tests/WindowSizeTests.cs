using TsDssConverter.Core;
using TsDssConverter.Tray;

namespace TsDssConverter.Tests;

/// <summary>
/// Tests that the settings window is scaled and sized correctly, so the list of conversions is visible when the window
/// opens, and that its parts sit where they belong (2.0.0: the Maestro layout).
/// Note: the real check at 125% / 150% is the render program of CLAUDE.md §3 (this test process is not DPI aware); here
/// we guard the rules.
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

        /// <summary>Shows the window as it opens on a big screen (the height of its content).</summary>
        public SettingsForm Opened()
        {
            Form.Show();
            Form.SizeToContent(BigScreen);
            Form.PerformLayout();
            return Form;
        }

        public void Dispose()
        {
            Form.AllowClose = true;
            Form.Dispose();
            TrayTests.TestRegistry.Remove(RegistryPath);
        }
    }

    private static Point Place(Form form, Control control) => form.PointToClient(control.PointToScreen(Point.Empty));
    private static int Bottom(Form form, Control control) => Place(form, control).Y + control.Height;
    private static int Right(Form form, Control control) => Place(form, control).X + control.Width;

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
    public void ListColumnsAndRows_AreScaledByTheCode_BecauseAListViewDoesNotDoThatItself()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            window.Form.Show();
            var list = window.Form.HistoryList;

            Assert.Equal(window.Form.LogicalToDeviceUnits(110), list.Columns[0].Width); // Time
            Assert.Equal(window.Form.LogicalToDeviceUnits(210), list.Columns[1].Width); // Project
            Assert.Equal(window.Form.LogicalToDeviceUnits(100), list.Columns[2].Width); // Result
            Assert.Equal(window.Form.LogicalToDeviceUnits(28), list.SmallImageList!.ImageSize.Height); // the row height
        });
    }

    [Fact]
    public void TheWindow_KeepsTheWindowsTitleBar_WithMinimiseMaximiseAndClose()
    {
        // Daan, 2026-11-22: unlike DssLabelTemplate 2.0.0, this window keeps the normal Windows title bar.
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Form;

            Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
            Assert.True(form.ControlBox);
            Assert.True(form.MinimizeBox);
            Assert.True(form.MaximizeBox);
            Assert.Equal(Strings.WindowTitle, form.Text);
        });
    }

    // ---------------------------------------------------------------- the window is as big as its content

    [Fact]
    public void OnABigScreen_EverythingIsVisible_WithoutScrolling_AndWithoutEnlargingTheWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();

            // The list of conversions is fully visible, the bottom bar is the last thing of the window.
            Assert.True(form.HistoryList.Height >= form.LogicalToDeviceUnits(150));
            Assert.True(form.Scroller.PointToClient(form.HistoryList.PointToScreen(new Point(0, form.HistoryList.Height))).Y <= form.Scroller.ClientSize.Height);
            Assert.Equal(form.ClientSize.Height, form.BottomBar.Bottom);

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
            var form = window.Opened();

            // The list gets only a little more than its minimum height (plus the room kept for the three "folder not
            // reachable" warnings, which are hidden here).
            Assert.InRange(form.HistoryList.Height, form.LogicalToDeviceUnits(150), form.LogicalToDeviceUnits(150 + 100));
        });
    }

    [Fact]
    public void TheGreyRoom_AroundAndBetweenThePanels_IsTheSameEverywhere()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();
            Control folders = form.Sections[0], zeroPoint = form.Sections[1], list = form.Sections[2];

            int left = Place(form, folders).X;
            int right = form.ClientSize.Width - Right(form, list);
            int top = Place(form, folders).Y - form.TopBar.Bottom;
            int between = Place(form, zeroPoint).Y - Bottom(form, folders);
            int below = form.BottomBar.Top - Bottom(form, list);

            Assert.True(left > 0);
            Assert.All(new[] { right, top, between, below }, room => Assert.InRange(room, left - 1, left + 1));
            Assert.InRange(Place(form, form.LastConversionTile).X - Right(form, form.StatusTile), left - 1, left + 1);
            Assert.InRange(Place(form, zeroPoint).X - Right(form, form.LastConversionTile), left - 1, left + 1);
        });
    }

    [Fact]
    public void WhenTheFolderWarningsAppearLater_TheyDoNotPushTheListOutOfView()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();

            // "Folder not reachable" is checked in the background and can appear a moment after the window opens.
            foreach (Control warning in AllControls(form).Where(c => c is System.Windows.Forms.Label l && l.ForeColor == Theme.Error))
            {
                warning.Visible = true;
            }

            form.PerformLayout();

            Assert.True(form.Scroller.PointToClient(form.HistoryList.PointToScreen(new Point(0, form.HistoryList.Height))).Y <= form.Scroller.ClientSize.Height);
            Assert.False(form.Scroller.VerticalScroll.Visible);
        });
    }

    // ---------------------------------------------------------------- the bottom bar

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void TheBottomBar_ScanRetryPauseLog_OnTheBlueBand_CancelAndSaveAtTheRight(AppLanguage language)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Opened();
            BottomBar bar = form.BottomBar;

            Assert.Same(form.SavePill, form.AcceptButton);   // Enter saves, Esc cancels
            Assert.Same(form.CancelPill, form.CancelButton);
            Assert.Equal(DockStyle.Bottom, bar.Dock);

            // Left to right, one after the other, the discs on one line; all on the blue band.
            BarButton[] round = { form.ScanNowButton, form.RetryFailedButton, form.PauseButton, form.OpenLogButton };
            for (int i = 0; i < round.Length; i++)
            {
                Assert.Same(bar, round[i].Parent);
                Assert.Equal(round[0].Top, round[i].Top);
                Assert.True(round[i].Right < bar.BandRight, $"'{round[i].Text}' is not on the band in {language}");
                if (i > 0)
                {
                    // The slots sit edge to edge; the room between two names is inside the slots.
                    Assert.True(round[i - 1].Right <= round[i].Left, $"'{round[i - 1].Text}' runs into '{round[i].Text}' in {language}");
                }

                // The whole name fits under the disc: on one line, or two for a long one, never wider than its slot.
                int slot = round[i].Width;
                Assert.InRange(round[i].CaptionLines(slot), 1, 2);
                Size caption = TextRenderer.MeasureText(round[i].Text, round[i].CaptionFont, new Size(round[i].CaptionWidth(slot), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                Assert.True(caption.Width <= round[i].CaptionWidth(slot), $"'{round[i].Text}' is cut off in {language}");
                Assert.True(round[i].DiscBounds().Bottom + caption.Height <= round[i].Height, $"'{round[i].Text}' is cut off at the bottom in {language}");
                Assert.True(round[i].Bottom <= bar.Height, $"'{round[i].Text}' runs out of the bar in {language}");
            }

            // The material list at the right end of the band (alpha.4): its disc on the line of the others, right of them,
            // the whole button before the band's round end (so its white name stays on the blue).
            BarButton materials = form.MaterialsButton;
            Assert.Same(bar, materials.Parent);
            Assert.Equal(BarGlyph.Materials, materials.Glyph);
            Assert.Equal(round[0].Top + round[0].DiscBounds().Top, materials.Top + materials.DiscBounds().Top);
            Assert.True(round[^1].Right < materials.Left, $"the material list runs into '{round[^1].Text}' in {language}");
            Assert.True(materials.Right <= bar.BandRight - bar.Height / 2, $"the material list sits on the round end in {language}");
            Assert.InRange(materials.CaptionLines(materials.Width), 1, 2);

            // Cancel and Save on the grey right of the band, one line, Save the right-most, their texts fit.
            Assert.True(bar.BandRight <= form.CancelPill.Left);
            Assert.True(form.CancelPill.Right < form.SavePill.Left);
            Assert.Equal(form.CancelPill.Top, form.SavePill.Top);
            Assert.Equal(form.CancelPill.Height, form.SavePill.Height);
            Assert.True(form.SavePill.Right <= bar.Width);
            Assert.True(form.SavePill.ShowCheck);
            Assert.False(form.CancelPill.ShowCheck);
        });
    }

    [Fact]
    public void TheDiscsOfTheBottomBar_AreAlwaysTheSameDistanceApart_InEveryLanguage()
    {
        // Daan, 2026-11-22: in French the long names ("Analyser maintenant") pushed the discs further apart.
        var spacings = new Dictionary<AppLanguage, List<float>>();
        foreach (AppLanguage language in new[] { AppLanguage.Dutch, AppLanguage.French, AppLanguage.English })
        {
            RunOnStaThread(() =>
            {
                using var scope = new LanguageScope(language);
                using var window = new Window();
                var form = window.Opened();
                float Middle(BarButton button) => button.Left + button.DiscBounds().Left + button.DiscBounds().Width / 2;
                BarButton[] round = { form.ScanNowButton, form.RetryFailedButton, form.PauseButton, form.OpenLogButton };
                spacings[language] = round.Skip(1).Select((button, i) => Middle(button) - Middle(round[i])).ToList();

                // Also after Pause turned into Resume (another name).
                form.PauseButton.Text = Strings.MenuResume;
                form.BottomBar.PerformLayout();
                spacings[language].AddRange(round.Skip(1).Select((button, i) => Middle(button) - Middle(round[i])));

                Assert.All(round, button => Assert.Equal(round[0].Top + round[0].DiscBounds().Top, button.Top + button.DiscBounds().Top)); // one line
            });
        }

        float first = spacings[AppLanguage.Dutch][0];
        Assert.True(first > 0);
        Assert.All(spacings.Values.SelectMany(list => list), spacing => Assert.Equal(first, spacing));
    }

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void InTheNarrowestWindow_NothingInTheBottomBarOverlaps(AppLanguage language)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Opened();
            form.Size = form.MinimumSize;
            form.PerformLayout();

            Assert.True(form.BottomBar.WidthNeeded() <= form.BottomBar.Width, $"the bottom bar needs {form.BottomBar.WidthNeeded()} px, it has {form.BottomBar.Width} in {language}");
            Assert.True(form.OpenLogButton.Right < form.MaterialsButton.Left);
            Assert.True(form.MaterialsButton.Right < form.CancelPill.Left);
        });
    }

    // ---------------------------------------------------------------- the folders

    [Fact]
    public void TheBrowseButtons_AreNavy_OneSize_AndAsHighAsTheirFolderBox()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();
            var browse = AllControls(form).OfType<RoundedButton>().Where(b => b.Text == Strings.Browse).ToList();

            Assert.Equal(3, browse.Count);
            Assert.All(browse, button =>
            {
                Assert.Equal(browse[0].Size, button.Size);
                Assert.Equal(Theme.ButtonNavy, button.NormalFill);
                Assert.True(button.ShowFolderIcon);
            });

            foreach (TextBox box in new[] { form.ExportFolderBox, form.BatchFolderBox, form.LabelFolderBox })
            {
                Control field = box.Parent!;
                RoundedButton beside = browse.Where(b => b.Parent == field.Parent).OrderBy(b => Math.Abs(Place(form, b).Y - Place(form, field).Y)).First();
                Assert.Equal(field.Height, beside.Height);
                Assert.Equal(Place(form, field).Y, Place(form, beside).Y);
                Assert.True(Right(form, field) < Place(form, beside).X); // the box left of its button
            }
        });
    }

    [Fact]
    public void TheTopSolidAndDuivesteinFolders_SitSideBySide_WithAVerticalLineBetween()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();

            // The two halves start on the same line, below the title "Mappen".
            Assert.Equal(Place(form, form.ExportCaption).Y, Place(form, form.BatchCaption).Y);
            Assert.True(Bottom(form, form.FoldersTitle) <= Place(form, form.ExportCaption).Y);

            // TopSolid left of the line, Duivestein right of it.
            Point line = Place(form, form.FolderDivider);
            Assert.True(Right(form, form.ConfigPositionButton) < line.X);
            Assert.True(Right(form, form.ConfigInfoHint) <= line.X);
            Assert.True(Right(form, form.ConfigPositionHint) <= line.X);
            Assert.True(Place(form, form.BatchCaption).X > line.X);
            Assert.True(Place(form, form.ConfigLabelButton).X > line.X);

            // Thin, light grey, and from the folder titles to the end of the taller half.
            Assert.InRange(form.FolderDivider.Width, 1, 3);
            Assert.Equal(Theme.GridLine, form.FolderDivider.BackColor);
            Assert.True(line.Y <= Place(form, form.ExportCaption).Y + form.LogicalToDeviceUnits(12));
            Assert.True(line.Y + form.FolderDivider.Height >= Bottom(form, form.ConfigPositionButton));

            // The two halves are about equally wide (the folder boxes sit in frames).
            int left = Right(form, form.ExportFolderBox.Parent!) - Place(form, form.ExportFolderBox.Parent!).X;
            int right = Right(form, form.BatchFolderBox.Parent!) - Place(form, form.BatchFolderBox.Parent!).X;
            Assert.InRange(left - right, -2, 2);

            // "Start with Windows" at the right of the title "Mappen", on its line.
            Assert.True(Place(form, form.StartWithWindowsBox).X > line.X);
            int titleMiddle = Place(form, form.FoldersTitle).Y + form.FoldersTitle.Height / 2;
            int boxMiddle = Place(form, form.StartWithWindowsBox).Y + form.StartWithWindowsBox.Height / 2;
            Assert.InRange(boxMiddle - titleMiddle, -form.LogicalToDeviceUnits(4), form.LogicalToDeviceUnits(4));
        });
    }

    [Fact]
    public void TheGreyButtons_SitUnderTheirFolder_OneUnderTheOther_InOneColumn_WithTheirHintAtTheRight()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();
            int field = Place(form, form.ExportFolderBox.Parent!).X;

            // TopSolid: Config (LI), Config (LP), Open materiaaltabel, under the export folder, starting where its box starts.
            RoundedButton[] topSolid = { form.ConfigInfoButton, form.ConfigPositionButton };
            Assert.True(Place(form, topSolid[0]).Y >= Bottom(form, form.ExportFolderBox.Parent!));
            for (int i = 0; i < topSolid.Length; i++)
            {
                Assert.Equal(field, Place(form, topSolid[i]).X);
                if (i > 0)
                {
                    Assert.True(Place(form, topSolid[i]).Y >= Bottom(form, topSolid[i - 1]));
                }
            }

            // Duivestein: Config (label CSV) under the label folder, at the left of its half.
            Assert.True(Place(form, form.ConfigLabelButton).Y >= Bottom(form, form.LabelFolderBox.Parent!));
            Assert.Equal(Place(form, form.BatchFolderBox.Parent!).X, Place(form, form.ConfigLabelButton).X);

            // The hints of a half all start at the same place, right of the widest button.
            var hints = new[] { form.ConfigInfoHint, form.ConfigPositionHint };
            Assert.All(hints, hint => Assert.Equal(Place(form, hints[0]).X, Place(form, hint).X));
            Assert.True(Place(form, hints[0]).X > Right(form, form.ConfigInfoButton));

            // All grey, square, one height; above the zero point.
            foreach (RoundedButton button in new[] { form.ConfigInfoButton, form.ConfigPositionButton, form.ConfigLabelButton, form.ClearHistoryButton })
            {
                Assert.Equal(Theme.GreyButton, button.NormalFill);
                Assert.Equal(0, button.Radius);
                Assert.Equal(form.ConfigInfoButton.Height, button.Height);
            }

            Assert.True(Bottom(form, form.ConfigPositionButton) < Place(form, form.ZeroPointTitle).Y);
        });
    }

    [Fact]
    public void ReferenceCornerSection_DropDownAndCheckboxLeft_SchematicRight()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();

            // The drop-down keeps its real width: a regression guard for a real bug (a Percent column in an AutoSize
            // TableLayoutPanel silently shrank it to ~75 px, cutting every corner name off to two or three letters).
            Assert.True(form.ReferenceCornerBox.Width >= form.LogicalToDeviceUnits(200), $"ReferenceCornerBox is only {form.ReferenceCornerBox.Width} px wide");

            // The schematic sits to the right of the drop-down and the checkbox, in line with the drop-down.
            Assert.True(Place(form, form.CornerPreview).X > Right(form, form.ReferenceCornerBox));
            Assert.True(Place(form, form.CornerPreview).X > Right(form, form.PlanRotatedBox));
            Assert.InRange(Place(form, form.CornerPreview).Y - Place(form, form.ReferenceCornerBox).Y, -2, 2);

            // The checkbox right under the drop-down, the title above both.
            Assert.True(Place(form, form.PlanRotatedBox).Y >= Bottom(form, form.ReferenceCornerBox));
            Assert.True(Bottom(form, form.ZeroPointTitle) <= Place(form, form.ReferenceCornerBox).Y);
        });
    }

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void ClearList_SitsAtTheRightOfTheTitleOfTheList_AboveTheList(AppLanguage language)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Opened();
            Control list = form.Sections[2];

            Assert.True(Right(form, form.HistoryTitle) <= Place(form, form.ClearHistoryButton).X, $"The title of the list runs into the button in {language}");
            Assert.True(Bottom(form, form.ClearHistoryButton) <= Place(form, form.HistoryList).Y);
            Assert.True(Right(form, form.ClearHistoryButton) < Right(form, list));
            Assert.True(Right(form, list) - Right(form, form.ClearHistoryButton) < form.LogicalToDeviceUnits(30)); // against the right
            int titleMiddle = Place(form, form.HistoryTitle).Y + form.HistoryTitle.Height / 2;
            int buttonMiddle = Place(form, form.ClearHistoryButton).Y + form.ClearHistoryButton.Height / 2;
            Assert.InRange(buttonMiddle - titleMiddle, -form.LogicalToDeviceUnits(3), form.LogicalToDeviceUnits(3));
        });
    }

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void TheButtonTexts_FitInTheButtons_AndTheHintsDoNotRunIntoTheirButton(AppLanguage language)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Opened();

            foreach (RoundedButton button in AllControls(form).OfType<RoundedButton>())
            {
                Assert.True(button.WidthNeeded() <= button.Width + form.LogicalToDeviceUnits(4), $"'{button.Text}' does not fit in its button in {language}");
            }

            foreach (PillButton pill in new[] { form.SavePill, form.CancelPill })
            {
                Assert.True(pill.WidthNeeded() <= pill.Width, $"'{pill.Text}' does not fit in its button in {language}");
            }

            Assert.True(Place(form, form.ConfigInfoHint).X >= Right(form, form.ConfigInfoButton), $"The LI hint runs into its button in {language}");
            Assert.True(Place(form, form.ConfigPositionHint).X >= Right(form, form.ConfigPositionButton), $"The LP hint runs into its button in {language}");
            Assert.True(Place(form, form.ConfigLabelHint).X >= Right(form, form.ConfigLabelButton), $"The label hint runs into its button in {language}");
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

    private static (System.Windows.Forms.Label Hint, Button Button)[] HintsAndButtons(SettingsForm form) => new (System.Windows.Forms.Label, Button)[]
    {
        (form.ConfigInfoHint, form.ConfigInfoButton),
        (form.ConfigPositionHint, form.ConfigPositionButton),
        (form.ConfigLabelHint, form.ConfigLabelButton),
    };

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

            foreach (var (hint, button) in HintsAndButtons(form))
            {
                // Beside its button: a hint of two or three lines is centred on a row a little higher than the button.
                int hintMiddle = Place(form, hint).Y + hint.Height / 2;
                int room = form.LogicalToDeviceUnits(8);
                Assert.InRange(hintMiddle, Place(form, button).Y - room, Bottom(form, button) + room);
                // Three lines at most: as high as a label of three lines in the same font (a label adds a little room).
                using var threeLines = new System.Windows.Forms.Label { Font = hint.Font, Text = "Ag\nAg\nAg" };
                Assert.True(hint.Height <= threeLines.GetPreferredSize(new Size(1000, 0)).Height, $"'{hint.Text}' is {hint.Height} px high");
                Assert.True(Place(form, hint).X >= Right(form, button));                                       // not into the button
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
        // afterwards. Here the narrow moment is made on purpose.
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Opened();
            Size normal = form.ClientSize;
            int foldersHeight = form.Sections[0].Height;

            form.MinimumSize = Size.Empty;
            form.ClientSize = new Size(form.LogicalToDeviceUnits(400), normal.Height);
            form.PerformLayout();
            form.ClientSize = normal;
            form.PerformLayout();

            foreach (var (hint, button) in HintsAndButtons(form))
            {
                int hintMiddle = Place(form, hint).Y + hint.Height / 2;
                int room = form.LogicalToDeviceUnits(8);
                Assert.InRange(hintMiddle, Place(form, button).Y - room, Bottom(form, button) + room);
            }

            Assert.InRange(form.Sections[0].Height - foldersHeight, -2, 2); // the folders panel shrank back

            // The vertical line is only as high as the higher half, not higher.
            int highestHalf = Math.Max(Bottom(form, form.ConfigPositionButton), Bottom(form, form.ConfigLabelButton));
            Assert.InRange(Bottom(form, form.FolderDivider) - highestHalf, -form.LogicalToDeviceUnits(30), form.LogicalToDeviceUnits(30));
        });
    }

    [Fact]
    public void TheGreyButtons_AreNoWiderThanTheirTextNeeds()
    {
        // Regression guard: up to 1.1.0 the text was measured at the screen's scaling and then scaled once more, so on a
        // 150% screen "Open materiaaltabel" and "Herstart mislukte" were half as wide again as needed.
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Form;
            form.Show();

            foreach (RoundedButton button in new[] { form.ClearHistoryButton, form.ConfigInfoButton })
            {
                int needed = Math.Max(form.LogicalToDeviceUnits(110), TextRenderer.MeasureText(button.Text, button.Font).Width + form.LogicalToDeviceUnits(28));
                Assert.InRange(button.Width, needed - form.LogicalToDeviceUnits(4), needed + form.LogicalToDeviceUnits(4));
            }
        });
    }

    [Theory]
    [InlineData(AppLanguage.Dutch)]
    [InlineData(AppLanguage.French)]
    [InlineData(AppLanguage.English)]
    public void TheZeroPointTitle_IsFullyVisible_AndTheTilesShowTheirTexts(AppLanguage language)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Opened();

            Control zeroPoint = form.Sections[1];
            Assert.True(Right(form, form.ZeroPointTitle) <= Right(form, zeroPoint), $"the zero-point title runs out of its panel in {language}");
            Assert.True(form.ZeroPointTitle.GetPreferredSize(new Size(form.ZeroPointTitle.Width, 0)).Height <= form.ZeroPointTitle.Height);

            foreach (InfoTile tile in new[] { form.StatusTile, form.LastConversionTile })
            {
                Assert.True(tile.Height >= tile.HeightNeeded());
                int caption = TextRenderer.MeasureText(tile.Caption, tile.CaptionFont).Width;
                int value = TextRenderer.MeasureText(tile.Value, tile.ValueFont).Width;
                Assert.True(caption <= tile.Width - form.LogicalToDeviceUnits(32), $"'{tile.Caption}' does not fit in {language}");
                Assert.True(value <= tile.Width - form.LogicalToDeviceUnits(32), $"'{tile.Value}' does not fit in {language}");
            }

            // The fixed texts of the Status tile fit too (the first line can hold a long folder: that one may end in "…").
            foreach (string line in new[] { Strings.StatusRescan(3600), Strings.StatusBusyLine, Strings.StatusPausedLine1, Strings.StatusPausedLine2, Strings.StatusErrorLine1, Strings.StatusErrorLine2 })
            {
                int width = TextRenderer.MeasureText(line, form.StatusTile.Font).Width;
                Assert.True(width <= form.StatusTile.Width - form.LogicalToDeviceUnits(32), $"'{line}' does not fit in the Status tile in {language}");
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
    public void OnASmallScreen_TheWindowFitsTheScreen_ThePanelsScroll_AndTheListKeepsItsSize()
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
            Assert.True(form.HistoryList.Height >= form.LogicalToDeviceUnits(150));            // the list is never squeezed out
            Assert.Equal(form.ClientSize.Height, form.BottomBar.Bottom);                       // Save / Cancel always visible
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

    // ---------------------------------------------------------------- the Magic parameters popup (2.0.1)

    [Theory]
    [InlineData(AppLanguage.Dutch, false)]
    [InlineData(AppLanguage.French, false)]
    [InlineData(AppLanguage.English, false)]
    [InlineData(AppLanguage.Dutch, true)]
    [InlineData(AppLanguage.French, true)]
    [InlineData(AppLanguage.English, true)]
    public void MagicParameters_ThePopupSitsInTheMiddle_AndNothingInItOverlapsOrIsCutOff(AppLanguage language, bool narrowestWindow)
    {
        RunOnStaThread(() =>
        {
            using var scope = new LanguageScope(language);
            using var window = new Window();
            var form = window.Opened();
            if (narrowestWindow)
            {
                form.Width = form.MinimumSize.Width; // the height it opens with (a low window: see the test below)
                form.PerformLayout();
            }

            form.OpenMagicParameters();
            MagicParametersPopup popup = form.MagicParameters!;
            Control overlay = popup.Parent!;
            overlay.PerformLayout();

            // In the middle of the window, inside it.
            Assert.True(overlay.ClientRectangle.Contains(popup.Bounds), $"the popup {popup.Bounds} is not inside {overlay.ClientRectangle}");
            Assert.InRange(popup.Left - (overlay.Width - popup.Right), -1, 1);

            // The rows: one under the other, the text left of the field, everything inside the white panel.
            RoundedWhitePanel panel = popup.WhitePanel;
            int previousBottom = 0;
            foreach (ParameterRow row in popup.Rows)
            {
                Assert.True(row.Caption.Top >= previousBottom, $"the row {row.Key} runs into the row above it");
                Assert.True(row.Hint.Top >= row.Caption.Bottom, row.Key);
                Assert.True(row.Hint.Right <= row.Field.Left, $"the explanation of {row.Key} runs into its field");
                Assert.True(row.KeyTag.Right <= row.Field.Left, $"the tag of {row.Key} runs into its field");
                Assert.True(row.Caption.GetPreferredSize(new Size(row.Caption.Width, 0)).Height <= row.Caption.Height, $"'{row.Caption.Text}' is cut off");
                Assert.True(row.Hint.GetPreferredSize(new Size(row.Hint.Width, 0)).Height <= row.Hint.Height, $"'{row.Hint.Text}' is cut off");
                Control last = row.After ?? row.Field;
                Assert.True(last.Right <= panel.ClientSize.Width, $"the field of {row.Key} sticks out of the panel");
                previousBottom = Math.Max(row.Hint.Bottom, row.Field.Bottom);
            }

            // On a normal screen all rows are visible without scrolling.
            Assert.True(panel.AutoScrollMinSize.Height <= panel.ClientSize.Height, $"the rows need {panel.AutoScrollMinSize.Height} px, the panel has {panel.ClientSize.Height}");

            // The bottom: where it is saved, then Cancel and Save, inside the popup.
            Assert.True(popup.SavedInLabel.Right < popup.CancelPill.Left);
            Assert.True(popup.CancelPill.Right < popup.SavePill.Left);
            Assert.True(popup.SavePill.Right <= popup.Width);
            Assert.True(popup.SavePill.Bottom <= popup.Height);
            Assert.True(popup.SavePill.WidthNeeded() <= popup.SavePill.Width);
            Assert.True(popup.CancelPill.WidthNeeded() <= popup.CancelPill.Width);
        });
    }

    [Fact]
    public void MagicParameters_InTheSmallestWindow_TheRowsScroll_AndCancelAndSaveStayVisible()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();
            form.Size = form.MinimumSize; // too low for all rows
            form.PerformLayout();

            form.OpenMagicParameters();
            MagicParametersPopup popup = form.MagicParameters!;
            Control overlay = popup.Parent!;
            overlay.PerformLayout();

            Assert.True(overlay.ClientRectangle.Contains(popup.Bounds));
            Assert.True(popup.WhitePanel.AutoScroll);
            Assert.True(popup.WhitePanel.AutoScrollMinSize.Height > popup.WhitePanel.ClientSize.Height); // it scrolls
            Assert.True(popup.SavePill.Bottom <= popup.Height);
            Assert.True(popup.WhitePanel.Bottom <= popup.SavePill.Top);
        });
    }

    [Fact]
    public void MagicParameters_ThePopupIsAThirdOfTheWindowsArea_InAWideWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new Window();
            var form = window.Opened();
            form.OpenMagicParameters();
            MagicParametersPopup popup = form.MagicParameters!;
            Control overlay = popup.Parent!;
            overlay.PerformLayout();

            // As wide as √(1/3) of the window (so its area is a third of it, Daan); the height is what the rows need.
            double share = Math.Sqrt(1.0 / 3);
            Assert.InRange(popup.Width, (int)(overlay.Width * share) - 2, (int)(overlay.Width * share) + 2);
            Assert.True(popup.Height < overlay.Height);
        });
    }
}
