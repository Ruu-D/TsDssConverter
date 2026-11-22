# Changelog

The history of TsDssConverter, newest first. `CLAUDE.md` describes the current state; this file says how it got there.
The version number is `<Version>` in `src/TsDssConverter.Tray/TsDssConverter.Tray.csproj`.

## Open points — 2026-11-22 (docs only, after the 2.0.0 release)

- **Resolved:** TopSolid delivers the CNC programs in a folder per project (`<export>\<project>\<sheet>.xcs`); a batch that
  is sent twice is ignored by Duivestein (the job is already present); the import of our label CSVs at Duivestein worked
  perfectly.
- **Still open:** the physical test sheet: compare our label with the one Duivestein's PrintServer prints automatically
  (arrows, the rotation of the edge-banding information, underlining and other font styles come out differently).
- README *Open points* and CLAUDE.md §7, §15 and §16 updated. No code change.

## 2.0.0 — 2026-11-22 — the "Maestro" settings window (release)

- Delivery file: `dist\v2.0.0\TsDssConverter.exe`, 54,589,530 bytes, file version 2.0.0.0, SHA-256
  `5a4abc73e414cf38096d9e1a4ddc0536c508f3904318f8efc72f3426f51ca713`. Built by `publish.cmd` after all 492 tests passed.
  The published exe was started once with a scratch data folder (`--data`, its own export / batch / label folders): it
  converted the sample by itself (1 batch XML, 3 label CSVs, the LI/LP/TR files to `_Verwerkt`).
- The code is that of 2.0.0-alpha.7, only the version number changed. The first build of the morning that was also
  called 2.0.0 (`20471edd...`, still the light-blue icons) was moved to `dist\_previous-builds\2026-11-22\`.
- **What 2.0.0 brings compared with 1.1.3** (the details are in the alpha entries below): the settings window in the look
  of the SCM machine software Maestro (like DssLabelTemplate 2.0.0), with the normal Windows title bar; the blue bottom bar
  with round buttons Nu scannen / Herstart mislukte / Pauzeren / Open logmap / Materiaallijst and the pills Annuleren /
  Opslaan; the Status tile (with a countdown to the next scan) and the Last conversion tile; a folders panel that folds up
  to one line; a language change and hand edits of `settings.json` shown at once; the flat darker-blue app and tray icons.
  The conversion, the files for Duivestein and the settings are the same as in 1.1.3 (one new window setting,
  `FoldersFolded`). Install it at the customer BEFORE TopSolid switches to the project folder for the CNC programs (1.1.2).
- README: status line, milestone row and the picture of the window (rendered again, with version 2.0.0).

## 2.0.0-alpha.7 — 2026-11-22 — fix: red cross in the Status tile (test build)

- Delivery file: `dist\v2.0.0-alpha.7\TsDssConverter.exe`, 54,589,610 bytes, file version 2.0.0.0, SHA-256
  `5c7e9e70831c7d97beed26a1720e60b8cd1589bdee87150b33c94f7c21a39732`. Built by `publish.cmd` after all 492 tests passed;
  `bin\alpha-test\Start TsDssConverter test build.cmd` now starts this build.
- **The bug (alpha.6, Daan's screenshot):** after a while an "Unhandled exception" dialog, `ExternalException
  (0x8000FFFF): An object could not be created …` in `InfoTile.DrawCountdown`, and the Status tile became a red cross
  until the window was closed and opened again. The text is .NET 10's message for GDI+'s OutOfMemory status, which GDI+
  returns for geometry it refuses; the likely cause is `DrawArc` with round ends and a tiny sweep angle, just before the
  ring reaches 0. It could not be reproduced here (60,000 drawings at a real 150%, no GDI handle leak).
- **The fix:** no arc under 1° (invisible anyway); `InfoTile.OnPaint` catches a GDI+ refusal, skips that one picture and
  paints the whole tile again at the next tick, so WinForms never sees the exception (no dialog, no red cross). NaN or an
  infinite value can no longer reach the ring. The ring now repaints only its own corner 10 times a second.
- Tests: the ring is drawn at many moments (also just before 0, NaN, infinite, a whole wait of 0) without an exception.

## 2.0.0-alpha.6 — 2026-11-22 — wider window, 25 | 25 | 50 middle row, countdown to the next scan (Daan; test build)

- Delivery file: `dist\v2.0.0-alpha.6\TsDssConverter.exe`, 54,589,383 bytes, file version 2.0.0.0, SHA-256
  `635a35d913cefda735af9c3a64aeff46d232b839d6fc878f3b6d4745f643d501`. Built by `publish.cmd` after all 489 tests passed;
  `bin\alpha-test\Start TsDssConverter test build.cmd` now starts this build.
- **Window 10% wider:** `WindowWidth` 1120 → 1232 at 100% (1848 at 150%). The minimum width stays 1040.
- **Middle row 25% | 25% | 50%:** Status and Last conversion each a quarter of the row, the zero point the half. The tile
  widths are set in pixels by `FitMiddleRow` from the row's width (never under the old 230 at 100%); the zero point takes
  the rest.
- **Countdown ring in the Status tile** (top right, like Daan's picture): a navy ring that empties as the time runs out,
  the seconds left in it ("5m" from 100 s on). It shows the REAL next scan: `ExportWatcher.NextScan` (new) gives the time
  and the whole wait of the watcher thread (RescanSeconds, or 2 s while a project waits for its files), so the ring starts
  again after a file event or "Nu scannen". No ring while paused or while a scan runs. Updated 10 times a second, only
  while the window is visible.
- Rendered at a real 150% in French, Dutch and English (README picture rendered again).

## 2.0.0-alpha.5 — 2026-11-22 — seven small tweaks of the window (Daan; test build)

- Delivery file: `dist\v2.0.0-alpha.5\TsDssConverter.exe`, 54,587,980 bytes, file version 2.0.0.0, SHA-256
  `4d73d3caa98ddd1a4e867a0197fe7e90cfa8381cbdef90acd7f13c6d23fe3055`. Built by `publish.cmd` after all 480 tests passed;
  `bin\alpha-test\Start TsDssConverter test build.cmd` now starts this build.
- **Bold in the hints:** `-LI.xlsx`, `-LP.xlsx` and `(DESC1 – DESC10)` (new `BoldPartsLabel`: a label that writes chosen
  parts of its text in bold and wraps word by word; a bold part is never split over two lines). The texts in `Strings`
  did not change.
- **The three folder captions are underlined.**
- **Middle row in a new order:** Status, Last conversion, then the label zero point (which takes the rest of the width).
- **Last conversion tile:** an error without a project no longer shows "-" under the time (the message moves up); the
  message wraps over several lines (`InfoTile.Line2Lines`, 3 for this tile; the row is a little higher for it) and only
  ends in "…" when it still does not fit.
- **List of conversions:** error rows stay red but are no longer bold.
- **Signs:** Scan now is a magnifying glass; Material list is a standing wooden board with grain and an edge band (after
  Daan's picture).
- **Folded folders panel:** the three paths are bold.
- Rendered at a real 150% (Dutch, open and folded; English for the README picture, which was rendered again). 125% not
  seen (no such monitor connected).

## 2.0.0-alpha.4 — 2026-11-22 — Material list in the bar, closer bar buttons, logo height (Daan; test build)

- Delivery file: `dist\v2.0.0-alpha.4\TsDssConverter.exe`, 54,586,527 bytes, file version 2.0.0.0, SHA-256
  `39251f99d1691772adccce740ca0864295e6be97fd47aa0c5cac25011af8fbcb`. Built by `publish.cmd` after all 478 tests passed;
  started with `bin\alpha-test\Start TsDssConverter test build.cmd`.
- **Material list** (`MaterialsButton`, Strings `MaterialsButton`: "Materiaallijst" / "Liste des matériaux" / "Material
  list"): a round button with a small table sign at the right end of the blue band, just before its round end (there its
  white name would run onto the grey). It opens `materials.csv`. The grey "Open materiaaltabel" button and its hint under
  the two Config buttons are gone (the tray menu keeps "Open materiaaltabel"). `BottomBar` takes it as its `end` button;
  the band is at least long enough for it (`SmallestBandRight`). Minimum window width 1040 (was 980): the French bar needs it.
- **Bar buttons about 13% closer:** `BarButton.SlotWidth` 140 → 122. "Herstart mislukte" and "Réessayez échoué" now wrap
  onto two lines under their disc, like "Analyser maintenant".
- **ROGIERS logo** as high as the disc of the "?" (`HelpButton.DiscSize`, 20 at 100%), as wide as its proportions need.
- French app version: "Version app : 2.0.0-alpha.4" (was "Version de l'application").

## 2.0.0-alpha.3 — 2026-11-22 — fold the folders, language and settings.json at once (Daan; test build)

- Delivery file: `dist\v2.0.0-alpha.3\TsDssConverter.exe`, 54,586,344 bytes, file version 2.0.0.0, SHA-256
  `0b180e296c6812e2a4f4a632fbafccae2b7f76ac480ab45a698b12c4113b244d`. Built by `publish.cmd` after all 478 tests passed;
  started with `bin\alpha-test\Start TsDssConverter test build.cmd`.
- **Fold the folders panel** (the proposal of alpha.2, approved): a "v" before "Mappen" (`FoldButton`) folds the panel to
  one line: "> Mappen", then the three folders read only (`FolderSummary`: TopSolid / Batch / Labels, a folder that is not
  reachable in red), "Start met Windows" stays. The window keeps its size, the list of conversions gets the room (about 230
  px at 100%); a window that opens folded is as high as an open one. Saved at once in the new window setting
  `FoldersFolded` (`SettingsStore.SaveChange`: the file is read again, only that value changes, a broken file is never
  overwritten) and used again after a restart.
- **Another language at once:** choosing a language in the drop-down saves only the language (`SaveChange`) and switches
  the whole interface right away: the menu, the tooltip and the window, which is built again in the same place and size
  and keeps what was typed but not saved yet (`TrayApp.RebuildSettingsWindow`, `SettingsForm.ShowEdits`). Before, the
  window changed only after Opslaan and the next opening.
- **Hand edits of `settings.json` at once:** the file is checked every second (was 5 s), and an open window shows the new
  values right away: in another language it is built again, otherwise filled again (the file wins over unsaved typing).
- Checked with the REAL tray app on a scratch data folder (a throw-away program): language switch keeps the typed folder and
  saves only the language, a hand edit to another language switches the open window, a hand edit of RescanSeconds shows in
  the Status tile, folding is saved at once. Rendered folded at a real 150% in Dutch and French.
- New tests: fold / unfold (saved at once, the list gets the room, window size kept), opening folded, the folded line with
  an unreachable folder, choosing a language, keeping typed values, `SaveChange`. 478 tests.

## 2.0.0-alpha.2 — 2026-11-22 — evenly spaced bar buttons, darker icon (Daan; test build)

- Delivery file: `dist\v2.0.0-alpha.2\TsDssConverter.exe`, 54,584,008 bytes, file version 2.0.0.0, SHA-256
  `a94f5a8d1b1df15cc9aad1f7ec48e3f5dc3a2c8e13f9ed55fc752ba18ec407ce`. Built by `publish.cmd` after all 471 tests passed;
  started with `bin\alpha-test\Start TsDssConverter test build.cmd` (the start file now has a name that stays the same).
- **Bottom bar:** every round button is one fixed slot wide (`BarButton.SlotWidth`, 140 at 100%), so the discs are always
  the same distance apart, in every language (in French the long names had pushed them apart). A name that does not fit
  its slot wraps onto a second line under its disc ("Analyser / maintenant"); all discs stay on one line. The bar is 100
  high (was 94) for the two-line names. New test: the same distance between the discs in Dutch, French and English, also
  after Pause turns into Resume.
- **Icon** (Daan chose "darker bar blue"): the square of the app and tray icons is now ONE flat `#3561A6` (the darker end of
  the bar) instead of the gradient. Recoloured again from the original icons.
- **Shorter Status texts**, so they fit the tile (a new test checks every fixed text in three languages): "elke 30 s en bij
  wijziging" / "toutes les 30 s et en direct" / "every 30 s and on changes", "Bezig met converteren" / "Conversion en
  cours", "Opgelost? Herstart mislukte".
- **Proposal, not built:** fold the Mappen panel up with a "v" (like DssLabelTemplate's file lines), so the list gets more
  room. Two artboards and a note on the mockup canvas, waiting for Daan.

## 2.0.0-alpha — 2026-11-22 — the settings window in the "Maestro" skin, blue icons (Daan; test build)

- Delivery file: `dist\v2.0.0-alpha\TsDssConverter.exe`, 54,669,414 bytes, file version 2.0.0.0, SHA-256
  `9c9fd5bd02f4f687bd022b242167b9c9f820ba83d32f722cae2d323715d1c155`. Built by `publish.cmd` after all tests passed; started
  with `bin\alpha-test\Start TsDssConverter 2.0.0-alpha.cmd` (its own data folder with scratch folders): the window opened,
  the log says "2.0.0-alpha gestart".
- A first build the same day was numbered 2.0.0 (`dist\v2.0.0`, SHA-256 `20471edd...`, the old light-blue icons). Daan
  could not start it: almost certainly the single-instance rule (started without `--data` it uses `C:\TsDssConverter`, where
  his 1.1.3 runs, and a second copy quits without a word). The start file of `bin\alpha-test\` avoids that.
- **Blue icons** (Daan): the rounded square of the app and tray icons (all four looks, every size) and the ring around the
  badge are now the vertical gradient of the bottom bar (`#4C7BC0` → `#3561A6`), like DssLabelTemplate 2.0.0's icon. The
  icons were recoloured, not redrawn: every pixel of the old brand blue, also the soft edges against the sheets and badges,
  got the new blue; nothing else changed. `app-icon.svg`, `icon-256.png`, `icon-512.png`, `preview.png` and
  `tray-states.png` follow.
- The version may have a suffix (`<Version>2.0.0-alpha</Version>`): `AppInfo.Version` reads the informational version
  (without the "+commit" of the build), so the window, the log and `dist\v2.0.0-alpha\` show it; the exe's file version
  stays 2.0.0.0.
- **Why:** the customer works all day with the SCM machine software Maestro; DssLabelTemplate 2.0.0 took over its look and
  TsDssConverter follows. Made from a claude.ai Design mockup ("TsDssConverter Maestro skin",
  https://claude.ai/artifact/H3yc3d9NHcBzsd346bymJ4) that Daan changed: no own clock and window buttons (the normal Windows
  title bar stays, unlike DssLabelTemplate), "Herstart mislukte" moved from the list to the bottom bar.
- **The window** (`SettingsForm`, rebuilt): a grey top bar with the white title tab (the logo, now drawn in code, on a blue
  disc; name and subtitle), the language, credit + version, the ROGIERS logo (Shift+click still opens `settings.json`) and a
  flat "?" for the user guide. White panels on a grey frame: **Mappen** (TopSolid | Duivestein side by side, navy Browse
  buttons with a folder sign, flat grey fields, grey Config / Open materiaaltabel buttons with their hint at the right;
  "Start met Windows" at the right of the title), the **label zero point** (drop-down, Gedraaid, the corner drawing in the
  bar blues) with two new tiles, **Status** (Actief / Bezig / Gepauzeerd / Fout, the same as the tray icon, with the watched
  folder) and **Laatste conversie**, then the **list** in a table look (grey bold column titles, thin row lines, error rows
  red and bold) with "Conversielijst wissen". At the bottom a **blue bar** with round buttons and their names: **Nu
  scannen**, **Herstart mislukte**, **Pauzeren / Hervatten** (the first two and the pause were tray-menu only before) and
  **Open logmap**; **Annuleren** and **Opslaan** as pills on the grey at the right.
- Verdana 8.5 pt like Maestro; the Maestro colours in `Theme` (the same values as DssLabelTemplate 2.0.0). Window 1120 x
  about 840 at 100% (1680 wide at 150%), minimum width 980 (the French bottom bar fits).
- New file `MaestroControls.cs` (copied from DssLabelTemplate and adapted: `BarButton`, `PillButton`, `HelpButton`,
  `TitleTab`, `InfoTile`, `BottomBar`, `HistoryListView`). `FluentControls.cs` restyled (navy / grey buttons with a radius,
  folder sign, navy check box, flat text field). `AppIcons.DrawLogo` draws the logo; the old banner (`AppIcons.Banner`,
  `header@2x.png`) and the user-guide picture are no longer built into the exe.
- `TrayApp` gives the window scan now, pause, the paused state and the icon state; the Status tile follows the icon.
- Not changed: the conversion, the tray icons and menu, the settings, all behaviour of the window.
- Tests: the layout tests rewritten for the new layout (what must stay true: panels and grey room, bottom bar order and fit
  in three languages and at the minimum width, hints beside their buttons also at a simulated 150%, Windows title bar kept),
  new tests for scan / pause, the two tiles and the logo. 470 tests. Rendered at a real 150% in Dutch, French and English
  (only one monitor at 150% was connected: 125% not seen).

## README banner in the Maestro blues — 2026-11-21 (Daan, no new version)

- `media/preview/Banner.png` (the README header) redrawn in the blues of DssLabelTemplate 2.0.0's "Maestro" skin: the
  vertical gradient of its bottom bar (`#4C7BC0` → `#3561A6`) with white text (5:1 and more, like the counter on that bar).
  Same size (1080 x 168), logo, fonts and places as before. Rendered by a throw-away program in the scratchpad (the logo
  drawn from the geometry of `media/header.svg`, the gap around the badge in the banner's own gradient). The app itself is
  unchanged: its window still draws the brand-blue banner (`AppIcons.Banner`).

## 1.1.3 — 2026-11-08 — banner, maximize, settings.json by Shift+click (Daan)

- Delivery file: `dist\v1.1.3\TsDssConverter.exe`, 54,685,249 bytes, file version 1.1.3.0, SHA-256
  `f28658c64da336e4e89286eb3c6cc0dc4de0aff2ec70b50c72932749e78bbc4f`. Built by `publish.cmd` after all 460 tests passed.
- **Shift+click on the logo opens `settings.json` in Notepad**; a normal click still opens rogiers.be. Hidden on purpose.
- **Hand edits of `settings.json` are safe and used at once.** Save in the window reads the file again and only replaces the
  window's own settings (`SettingsStore.SaveWindowSettings`, `AppSettings.CopyWindowSettingsFrom`); before, it wrote the values
  read at start and so threw away hand edits made since. A file with a mistake is no longer overwritten by Save (a message
  instead). The tray looks at the file date every 5 s (`SettingsFileChecker`) and uses a changed file at once; a file with a
  mistake gives one balloon and the settings in use stay. Checked with the preview exe in a scratch data folder.
  (While checking, a first start of the preview with an EMPTY scratch data folder got the packed `settings.json` and so
  watched the real `Z:\TopSolid\Export\` for about 4 seconds; it was stopped at once and its log shows no conversion. Lesson: write
  a `settings.json` with scratch folders BEFORE starting a test copy with `--data`.)
- New tests: settings saving / checker (6), Shift+click (1). 460 tests.

- **Banner drawn in code, like DssLabelTemplate's.** The subtitle was "Automatic hand-over of nesting jobs from CAD to the
  panel warehouse." and is now "Automatic nesting job transfer from TOPSOLID to DUIVESTEIN" (`Strings.BannerSubtitle`, English
  in every language). Title and subtitle use the fonts, sizes and places of DssLabelTemplate's banner (Segoe UI Semibold 54 px /
  Segoe UI 27 px on the 1080 x 168 picture, `Theme.CreateBannerTitleFont` / `CreateBannerSubtitleFont`); only the logo is
  still taken from `header@2x.png`. The README header picture still shows the old subtitle. New test
  `TheBanner_HasTheLogoAndTheTextOnTheBrandColour`.
- **The settings window can be maximized** (`MaximizeBox` was false; dragging the edges already worked).
- **Banner 15% smaller:** 551 x 86 at 100% (was 648 x 101 = 1.2 x 540 x 84), the same in DssLabelTemplate. 453 tests.

## 1.1.2 — 2026-11-04 — CNC programs in a folder per project

- Delivery file: `dist\v1.1.2\TsDssConverter.exe`, 54,680,389 bytes, file version 1.1.2.0, SHA-256
  `76ec79913a9379b529838b6ba49698a5106c4cd121ae7847146c5c51a5c5bb60`. Built by `publish.cmd` after all 452 tests passed.
  The exe was started once with a scratch data folder (all folders in a scratch folder, never `Z:`): it converted the sample
  by itself with its three programs in `{export}\DAAN_ROGIERS-P2026.09\` (programs moved to `CNC\DAAN_ROGIERS-P2026.09\`,
  the emptied folder removed, no warnings, files to `_Converted`).
- **CNC programs in `{export}\{project}\` (2026-11-04, agreed with TopSolid; Daan).** TopSolid cannot put the project in the
  name of a CAM file (a program belongs to parts, not to a project), so the project prefix in the sheet name agreed on
  2026-10-30 is replaced: TopSolid makes a folder named after the project (= the project of the XLSX file names) inside the
  export folder and writes the programs there, `{sheet name}.xcs` as before. `Nom_panneau` / `SP` need no project prefix (they
  must still be identical in LI and LP). `CncPathBuilder.BuildSourcePath(settings, project, sheet)` is the new place,
  `BuildOldSourcePath` the old one (directly in the export folder); `CncMover` looks in the project folder first and falls
  back to the old place, so older exports keep working. The "newer than TR" check is only done for programs found in the old
  place (no other job writes in a project folder). After the move, the project folder is removed if it is empty (anything
  else in it stays; a failure there is ignored). The "CNC program not found" warning names the project-folder path. No UI or
  text change. The XML is unchanged (still `{export}\CNC\{project}\`). 452 tests (was 442).

## 1.1.1 — 2026-10-30 — first fixes from the customer tests

- Delivery file: `dist\v1.1.1\TsDssConverter.exe`, 54,680,189 bytes, file version 1.1.1.0, SHA-256
  `837e6d04d404c6d85cc1dcdded428d3de147fd96f1328d59614d5abf1e1025f5`. Built by `publish.cmd` after all 442 tests passed.
  The exe was started once with a scratch data folder (all folders in a scratch folder, never `Z:`): it converted the
  customer's hand-corrected `P2026-Cuisine-ROGIERS` export by itself (2 sheets, 6 labels, CNC programs moved, files to
  `_Converted`). The customer's 1.1.0 REFUSES TopSolid's new prefixed sheet names: install 1.1.1 before TopSolid switches.
- **"Clear conversion list" button (2026-10-30, Daan):** `Conversielijst wissen` / `Effacer la liste` / `Clear conversion
  list`, left of "Retry failed", as wide as its text (`WideButtonWidth`), same style and spacing. It empties ONLY the list
  in the window (`ConversionHistory.Clear`); the log files are written separately and keep everything. The tray tooltip
  keeps the last result (`TrayApp._lastResult`, no longer read from the list). No confirmation question: the list is in
  memory only anyway. Checked with the render program at a real 150%, 125% and 100%, in all three languages. 442 tests.

- **Material from `Matériau`, not from the sheet name (2026-10-30, Daan).** Found with a real customer export
  (`P2026-Keuken`): TopSolid put the project in front of the sheet name in LI (`P2026_Keuken_MELA_BLANC_18#01`) but not in LP
  (`MELA_BLANC_18#01`), so no part matched. Agreed with TopSolid: the project goes in front of the sheet name, the SAME name
  in LI `Nom_panneau`, LP `SP` and the CNC file name (`{sheet name}.xcs`), so two jobs can never overwrite each other's
  programs (open point 1). With the old rule (material = sheet name before `#`) every project would have had a new material
  name, so the material is now the trimmed `Matériau` of the LI file (`BatchBuilder`); the sheet name only gives the sheet
  number after the last `#` (`TopSolidText.TryGetSheetNumber`, was `TryParseSheetName`). `CncPathBuilder` needed no change.
  New errors (three languages): parts of different materials on one sheet (`Messages.DifferentMaterialsOnSheet`, names the
  sheet and every material) and a part with an empty `Matériau` (`Messages.MaterialMissing`). Materials are compared
  case-insensitively everywhere, like `materials.csv`. `BadSheetName` now says "does not end in '#number'". The output for
  the sample is unchanged (its `Matériau` equals the material in its sheet names). For the customer's export the warehouse
  name now comes from the `Matériau` line in `materials.csv` (`18 MELA BLANC` instead of `MELA_BLANC_18`): maintaining
  `materials.csv` is the customer's responsibility. Checked with the real `P2026-Keuken` export, rewritten with the agreed
  prefix (`P2026_(Keuken)_MELA_BLANC_18#01` in LI, LP and the CNC names): converted without warnings, programs moved.
  437 tests (was 432).

## 1.1.0 rebuild — 2026-10-26 (same version number, Daan)

- Delivery file: `dist\v1.1.0\TsDssConverter.exe`, 54,678,900 bytes, file version 1.1.0.0, SHA-256
  `b4ec11f21c9e50f5d107dc571529061d6dc8fc912747b078ba46e8f775f97816`. Built by `publish.cmd` after all 432 tests passed;
  the exe itself was not started. Rebuilt under the SAME number on Daan's request, so two different exes say 1.1.0: tell
  them apart by this checksum. The first 1.1.0 build (`1170f116...`) and Daan's `TsDssConverter_v1.1.0.zip` were copied
  to `dist\_previous-builds\2026-10-26\v1.1.0\` first (publish.cmd empties `dist\v1.1.0`).

- **Zero-point dot in the corner schematic (2026-10-26, Daan):** a round navy dot with a thin white ring sits exactly on the
  selected corner of the sheet (on top of the bigger brand-blue square), so the user sees where the X / Y of the label CSV
  are measured from. It follows the drop-down live, like the square (`ReferenceCornerPreview`, `OriginDot` 9 px +
  `OriginRing` 2 px at 100%). Rendered at a real 150%. New test: the navy dot is on the selected corner and on no other
  corner, for all four corners. 432 tests (was 431).
- **Zero-point section renamed (2026-10-26, Daan):** "Nulpunt etiket nesting (vanuit welke hoek Duivestein meet)" /
  "Point zéro étiquette nesting (le coin à partir duquel Duivestein mesure)" / "Nesting label zero point (which corner
  Duivestein measures from)" (was "Nulpunt nesting plaatpuzzel (noodzakelijk voor etiket nulpunt)" / ...). The old title
  read as if it were the CAD software's zero point; the drop-down is the corner DSSClient measures from (TopSolid is always
  bottom-left). DssLabelTemplate got the matching setting `SheetReferenceCorner`.

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
