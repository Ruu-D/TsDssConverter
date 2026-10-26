# Changelog

The history of TsDssConverter, newest first. `CLAUDE.md` describes the current state; this file says how it got there.
The version number is `<Version>` in `src/TsDssConverter.Tray/TsDssConverter.Tray.csproj`.

## Not released yet (for 1.1.1)

- **Zero-point dot in the corner schematic (2026-10-26, Daan):** a round navy dot with a thin white ring sits exactly on the
  selected corner of the sheet (on top of the bigger brand-blue square), so the user sees where the X / Y of the label CSV
  are measured from. It follows the drop-down live, like the square (`ReferenceCornerPreview`, `OriginDot` 9 px +
  `OriginRing` 2 px at 100%). Rendered at a real 150%. New test: the navy dot is on the selected corner and on no other
  corner, for all four corners. 432 tests (was 431).

## 1.1.0 — 2026-10-24 — first customer release

- Delivery file: `dist\v1.1.0\TsDssConverter.exe`, 54,678,390 bytes, SHA-256
  `1170f116d8c54c41e1ee2500bd6a6818a2bd3af0a3471394199f5b9947e4e2f6`.
  (Built several times under this number before delivery: `9d69104d...` (first layout, 1276 px wide, broken at 125%/150%),
  an unpublished preview with the first scaling fix, `337cb439...` (final layout, without the user-guide icon), then this
  one. Only the last one counts.)
- **User-guide icon (Daan):** a small navy book with a question mark (`media/user-guide.png`, built into the exe,
  `AppIcons.UserGuide`) right after the app version at the top right, 16 x 16 at 100%. Clicking it opens
  https://github.com/Ruu-D/TsDssConverter (`Strings.UserGuideUrl`); hand cursor, tooltip "Gebruikershandleiding (opent de
  website)" in three languages. `AppIcons.CompanyLogo` and `UserGuide` share one helper (`TrimmedAndScaled`).
- **First customer release** (Daan). From here on we wait for real-world tests at the customer; **1.1.1 will be the bug fixes**
  found once real customer data flows through. Contains everything of 1.0.4 (reference-corner drop-down, "Gedraaid" /
  `PlanRotated`, corner schematic), which had not been delivered on its own.
- **Settings window layout (Daan):**
  - The window is wider: 940 px at 100% = 1410 px at 150% (was 678). The minimum width is unchanged. On the way there it was
    1160, then 1276 px; Daan measured 1913 px at 150% and asked for 1410.
  - The TopSolid export folder (with its two Config rows and Open materiaaltabel) and the two Duivestein folders (with the
    label Config row) sit side by side, two halves of equal width with a thin vertical pale-blue line between them
    (`FolderDivider`). The hints under the folders wrap onto two lines when they do not fit next to their button.
  - Horizontal dividers: below "Start met Windows", above the zero point, and a new one above "Laatste conversies". The one
    between the TopSolid and Duivestein parts is gone.
  - `Dev.: Daan Verhoost | App-versie: …` on one line at the top right (`Strings.CreditSeparator`).
  - Footer at the bottom left: only the ROGIERS logo, a link to rogiers.be (hand cursor, tooltip "ROGIERS NV/SA"). The text
    `ROGIERS NV/SA` was removed.
- **Layout fixes at 125% / 150% screen scaling** (found by Daan on his own screens; at 100% everything was fine):
  - Hints far below their buttons. While Windows scales the window it lays it out for a moment at a much narrower width;
    the hints wrapped onto many lines and the rows grew. A control stretched to fill a table row (`Dock = Fill`) is sized
    by its current height, so the vertical line and the two halves then held the folders row that high, and the last row of
    each half got the extra room. Fixed: the halves are anchored at the top (keep their own height), the line takes its
    height from the halves (`FitDivider`), and the hints get a size computed by the window (`WrapHintBesideButton` /
    `FitHints`, width from the whole folders row) instead of letting the table wrap them.
  - The wide buttons ("Open materiaaltabel", "Herstart mislukte") were scaled twice: the text was measured at the screen's
    scaling and then scaled again. `WideButtonWidth` now measures at 100%. (Already visible in 1.0.4 at 150%.)
  - The logo was squeezed to 2 px wide at 150%: it sat in a `Percent` column of an `AutoSize` table (the same WinForms trap
    as the 1.0.4 drop-down). It now has an `AutoSize` column of its own.
- **How it was checked:** previews were rendered with `DrawToBitmap` (no screen capture), first from a temporary test, then
  from a small throw-away program in the scratchpad that opens the real window per-monitor DPI aware like the app, invisibly,
  on every monitor at its real scaling (see CLAUDE.md §3). It reproduced the 125% and 150% problems and confirmed the fixes in
  all three languages. A dark "VS Code Dark Modern" look was sketched the same way and not chosen.
- Tests: footer/version tests rewritten; new tests for the side-by-side halves, every hint beside its button (three
  languages, at 100% and at a simulated 150%), a window that was narrow for a moment (fails on the broken layout), the wide
  button widths, the logo width, the third divider and the user-guide icon. 431 tests (was 420).

## 1.0.4 — 2026-09-23

- Delivery file: `dist\v1.0.4\TsDssConverter.exe`, 54,668,157 bytes, SHA-256 `d4e7c85f14476afbd88664f6da96c2662cfd241e7735faaf4ad9c20fddfd5e3f`.
  (Built five times under this version number, because it had not been delivered yet: `32923cfb...` (first, no rename/
  schematic), `132bc168...` (rename + schematic, small + centred across the drop-down/checkbox stack), `25bd1e75...` (bigger
  schematic, but this is the build with the broken drop-down width, see below), `70b87929...` (the fix), then this one (the
  selected-corner marker made bigger again, and Daan's own edit to the title text). Only the last one counts, same as 1.0.2.)
- **Renamed the reference-corner section's title (2026-09-23, Daan):** "Nulpunt nesting plaatpuzzel" / "Point zero panneau
  nesting" / "Zero point nesting sheet" (was "Oriëntatie van de plaat (zoals in DSSClient)" / ... ; `Strings.LabelZeroPoint`,
  property name unchanged).
- **A small schematic next to the reference-corner drop-down (2026-09-23, Daan):** the section is now two columns — the
  drop-down and the "Gedraaid" checkbox on the left, and a new custom-drawn control, `ReferenceCornerPreview`
  (`Tray\ReferenceCorner.cs`, control `CornerPreview`), on the right: a filled dark-blue "sheet" rectangle with a light-blue
  square at each corner, the corner matching the current selection drawn bigger and in the brand colour. It follows the
  drop-down at once (before Save), via `SelectedIndexChanged`. Purely decorative, nothing is read back from it. 420 tests
  (was 418): the schematic's value after `LoadFromSettings` and after changing the drop-down, and a layout test for the two
  columns.
- **Follow-up (2026-09-23), after Daan's own screenshot of the built exe:** the schematic made a bit bigger (100 x 76, was
  84 x 64) and asked to line it up with the drop-down's row specifically, instead of centred across the drop-down + checkbox
  stack. The colours themselves were already correct — checked by sampling the exact rendered pixels of Daan's screenshot,
  since the selected corner is easy to misjudge by eye at that small size.
- **Regression, found and fixed the same day (Daan's screenshot):** the first attempt at "line it up with the drop-down"
  restructured `cornerRow` into a `TableLayoutPanel` with a `Percent(100)` column while `AutoSize = true` — a WinForms trap:
  under `AutoSize`, a `Percent` column is silently treated as `AutoSize` too, so it fell back to the ComboBox's own computed
  preferred width instead of the `Width = 260` that was set, shrinking the drop-down to ~75 px (showing "Bas" instead of
  "Bas gauche", and truncating every item in the open list). Fixed by going back to the original, proven structure — two
  `AutoSize` columns, the drop-down and checkbox nested in their own sub-panel — and getting the alignment with
  `CornerPreview.Anchor = Left | Top` instead of restructuring rows. New regression test: `ReferenceCornerBox.Width` must be
  at least 200 px, which fails under the broken version.
- Screenshot note: getting a correct screenshot of any of this took two fixes (a multi-monitor DPI mismatch, then finding the
  right window by PID instead of the unreliable `Process.MainWindowHandle`), still briefly matched an unrelated window on
  Daan's screen once, and later came back solid black from a negative-coordinate monitor position; see CLAUDE.md §3. All
  stray/failed captures were deleted unused. Daan's own screenshots were used to verify the colours and to catch the
  regression, since self-capture was not reliable on his machine.
- **Last tweak before an on-site visit (2026-09-23, Daan):** the selected-corner marker made 1/3 bigger again (21 px, was
  16 px). Daan also edited the section title's text himself directly in `Strings.cs`, adding a clarifying parenthetical:
  "Nulpunt nesting plaatpuzzel (noodzakelijk voor etiket nulpunt)" / "Point zero panneau nesting (nécessaire pour le point
  zéro de l'étiquette)" / "Zero point nesting sheet (required for label zero point)". 420 tests (the language-window test's
  expected title text was updated to match).
- **Reference-corner drop-down + "Gedraaid" checkbox, replacing the FlipX/FlipY checkboxes (2026-09-23, Daan).** The settings
  window now shows one drop-down with DSSClient's own four "Oriëntatie" corner names (Linksonder / Linksboven / Rechtsonder /
  Rechtsboven), and next to it a "Gedraaid" checkbox — matching DSSClient's own panel exactly, so the customer picks the same
  values in both tools without doing any flip math. New Tray class `ReferenceCorner` (+ `ReferenceCornerMapping`) converts the
  corner to/from `AppSettings.FlipX`/`FlipY`, which are unchanged internally (old `settings.json` files keep working).
  New `AppSettings`/`ConverterSettings.PlanRotated`, confirmed by Daan to be the batch XML's per-plan `<Rotation>` node (was
  hardcoded to `0`): 90 when "Gedraaid" is checked, 0 otherwise, the same for every plan (a warehouse-level setting, not
  computed per job). 418 tests (was 406): a new `ReferenceCornerTests.cs` (pure mapping logic), a `Plan.Rotation` test in
  `BatchBuilderTests.cs`, and a `<Rotation>` test in `SampleConversionTests.cs`; the window tests were updated for the new
  controls. Default settings are unchanged (`PlanRotated` off = `<Rotation>0</Rotation>`, byte-identical to the golden XML).
- README shortened (404 → 239 lines): the section "Getting started (for developers)" is removed completely (developer commands,
  CLI options, trying out the tray app, the customer install guide, material mapping and column names; the old text is in git
  history, the facts are in CLAUDE.md §5, §6, §14), and "Built to be safe", "What is verified so far" and "Decisions taken so far"
  are about half as long.

## 1.0.3 — 2026-09-21

- Delivery file: `dist\v1.0.3\TsDssConverter.exe`, 54,664,898 bytes, SHA-256 `e8aa2b95367f69cad3320ef00a16f63bb3522df16a146072ddd55dbe7e19b271`.
- **Batch XML syntax corrected after the test in DSSClient (2026-09-21).** Daan tried our XML in DSSClient: "almost correct",
  and he corrected it by hand (`samples/duivestein/DAAN_ROGIERS-P2026.09_corrected file.xml`). The differences, all now in
  `BatchXmlWriter`:
  - `<PlanDate>` → `<Date>` (same value, `yyyy-MM-dd`);
  - `<AutoExpand>False</AutoExpand>` removed;
  - new header nodes after the date, in this order: `MaxStackHeight`, `EqualStackHeight`, `PlanCount`, `BoardCount`,
    `PartCount` and `CutCount`. `PlanCount`, `BoardCount` and `PartCount` are counted from the batch (2, 3 and 22 for the sample).
    The other three are fixed values, confirmed by Daan: `MaxStackHeight` 1 (never more than one panel is machined at a time),
    `EqualStackHeight` `False`, `CutCount` 0 (not used here);
  - `<Material>` → `<MaterialName>` in every plan; Daan confirmed `MaterialName` is the correct name. (His corrected file still
    had `<Material>` in plan 002 by mistake; that tag was corrected in the file too.)
  - The material NAMES in that file (`standaard_plaat_18mm`, `rugpanelen`) are not a syntax change: they are Daan's real
    `materials.csv` (`defaults\`), which the program already used.
- Golden XML regenerated. `Batch` got `BoardCount` and `PartCount`.
- Tests (406): header nodes and their order, the counts, `MaterialName` in every plan; and a new test that converts the sample with
  Daan's real materials and compares with the corrected file, byte for byte. The golden test now lists its four files explicitly
  (the corrected file lives in the same folder).

## 1.0.2 — 2026-09-20

- Delivery file: `dist\v1.0.2\TsDssConverter.exe`, 54,664,528 bytes, SHA-256 `c4205c9f66cb8ad40f0c61d66a9e6651fbf0d4368762455c5b589573d0385094`.
  (The exe was built twice under this version number, because it had not been delivered yet: the first build, hash `71076d50…`,
  had no margin fix. Only the second one counts.)
- The white room below Save / Cancel is now as wide as the room at the left and right of the window (20 px at 100%, was 8 px):
  bottom padding of the main table in `SettingsForm.cs`. New test `TheWhiteRoomBelowSaveAndCancel_IsAsWideAsTheRoomAtTheLeftAndRight`
  (403 tests). Checked in a screenshot at 150%: about 30 px on all three sides.

- **Retry failed** button (`Herstart mislukte` / `Réessayez échoué` / `Retry failed`) at the right of the title of the list of
  conversions, left of `Open logmap`. New class `FailedProjects`: puts the files of all failed projects back in the export
  folder (TR last, never overwrites, all-or-none, error folders of every language searched). Silent on success.
- `Open materiaaltabel` moved from the list header to a new row under the two `Config` buttons of the TopSolid export section
  (hint text on the left, the wide button ends at the right edge of the Browse buttons).
- Tests: the version asserts read `AppInfo.Version` (a version bump no longer breaks `publish.cmd`), the banner test checks the
  proportions instead of a size, new tests for the retry logic and the new layout. 402 tests.
- Docs: README gets an "Important fix still to do" section (project name as prefix of every CNC file name, to be done in TopSolid);
  `CLAUDE.md` rewritten from the code; this changelog created; SignPath looked at and rejected (open-source projects only).

## 1.0.1 — 2026-09-20

- The banner in the settings window is smaller: 648 x 101 px (1.2 x the 540 x 84 picture, was 810 x 126 = 1.5 x), so the window
  is 678 px wide at 100% (was 840). Version bumped to 1.0.1; the stale test expectations (1.0.0, 810 x 126) were updated.
- Delivery file: `dist\v1.0.1\TsDssConverter.exe`, 54,657,396 bytes, SHA-256 `467bac148bd01652f82aa3a42cff689148b3805c02e21c912b02ea8d2b88a6b6`.

## 1.0.0 — 2026-09-20 (first delivery file)

- `publish.cmd` (a `.cmd`, because PowerShell scripts are blocked by policy on Daan's PC): runs all tests, then publishes ONE
  self-contained, compressed exe (52 MB, was 126 MB uncompressed) to `dist\v<version>\`, checks that it is the only file and prints
  size, version and SHA-256. Not code-signed. No installer (not asked for). Checked in an environment without .NET: the sample
  export became the expected 3 label CSVs + XML and the files moved to `_Verwerkt`. Start 0.7 – 2 s (first start of a new file ~13 s).
- Packed default files (`defaults\`): `settings.json`, `materials.csv` and the three column files are built into the exe and written
  on the first start (never overwriting). They are Daan's own files from `C:\TsDssConverter` (settings with `Language: nl`).
- The folders `_Verwerkt` and `_Fout` are named per language (`_Effectuee`/`_Erreur`, `_Converted`/`_Error`).
- CNC programs are MOVED by the converter to `{export}\CNC\{project}\` (`CncMover`); the batch XML points there; a program more
  than 5 s newer than the TR file is an error. Chosen by Daan from a screenshot of his own `Z:\TopSolid\Export\CNC\{project}\`.
  The question to TopSolid to put the project name in the CNC file name was postponed, and is now flagged as the important fix.
- The new French-header TopSolid export (`DAAN_ROGIERS-P2026.09-*`) became the golden standard; the Verschuren sample was dropped
  (still in git history). `OPLEG2` removed everywhere; `CAM_3` added as a required fixed field (label CSV column `CAM3`).
  Golden files regenerated; `materials.sample.csv` now has `Melamine_18` and `Melamine_08`.
- CNC naming changed from the guess `{BatchName}_{sheet}.xcs` to `{sheet name}.xcs` (the name in all real exports).

## Revision rounds after stage 4 — 2026-09-20 (Daan)

- Ten description columns `DESC1` … `DESC10` (LI + LP, merged LI-first) as the last ten columns of the label CSV; their names in
  the third column file `columns-label.txt` with a third `Config` button under the Duivestein label folder.
- Configurable header names of the LI and LP files (`columns-li.txt`, `columns-lp.txt`, `Config` buttons). New classes `ColumnMap`,
  `ColumnKeys`, `TextFile`; `XlsxTable.RequireColumns` became `ColumnMap.Resolve`; `ColumnNames.cs` removed.
- Settings window: `Open logmap` and `Open materiaaltabel` buttons, footer with the ROGIERS logo (`Dev.: Daan Verhoost`,
  `ROGIERS NV/SA` link), the app version at the top right, three divider lines, an extra `Config` row.
- Windows 11 ("Fluent") restyle: `RoundedButton`, `FluentCheckBox`, `TextBoxFrame`, the Windows 11 font, colours from `Theme`.
  Approved by Daan after a screenshot round; the UI is considered finished.
- The batch XML holds the FULL path of every label file (label folder + file name), decided after Daan's test of stage 4.
- Scaling bug found by running the exe at Daan's real 150% scaling: the window must be built between `SuspendLayout` and
  `ResumeLayout`; `--show` and a screenshot with `CopyFromScreen` became the way to check the window.

## Stage 4 — Folder watching and processing

- `ExportScanner`, `ProjectProcessor`, `ExportWatcher` (one background thread, `FileSystemWatcher` + rescan, fake-clock tests).
  Rules: temporary problems leave the files in place and are reported once; problems with the files go to `_Fout` with
  `fout.txt`; a written batch whose files cannot be moved stays a success and the move is retried; an unreachable export folder
  is reported only after 60 s and recovers by itself; a watcher error rebuilds the watcher; "Scan now" also scans while paused.
- Checked with the real exe: the sample dropped in a folder became the batch and 11 label files identical to the golden files (at
  that time with the first sample).

## Stage 3 — Tray shell

- `TrayApp`, `SettingsForm`, `StartWithWindows`, `Theme`, `Strings`, `AppIcons`, `AppSettings` + `SettingsStore`, `AppDataFolder`,
  `LogWriter`, `ConversionHistory`. Single instance via a `Global\` mutex per data folder.
- Tray icon looks OK / busy / error / paused generated from the same artwork (`media/`); one red (`#D6322C`) outside the palette.
- Three languages (nl / fr / en): `Localizer`, `Messages`, `Strings`, `"Language"` in `settings.json`, a test that no text is missing.
- Install folder = data folder = `C:\TsDssConverter\`, chosen by Daan.

## Stages 1 and 2 — Core, CLI and validation

- Read LI/LP, join, map materials, write the batch XML and one label CSV per sheet, safe writing (`.tmp` + rename, XML last, never
  overwrite a batch). The CLI output of the sample was byte-identical to the golden files.
- Every error and warning of the validation list, all collected in ONE `ConversionException`; nothing is written on an error.
