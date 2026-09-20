# Changelog

The history of TsDssConverter, newest first. `CLAUDE.md` describes the current state; this file says how it got there.
The version number is `<Version>` in `src/TsDssConverter.Tray/TsDssConverter.Tray.csproj`.

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
