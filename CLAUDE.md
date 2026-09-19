# TsDssConverter — TopSolid → Duivestein batch converter

## Purpose

A small Windows tray application that watches a folder for TopSolid nesting exports (XLSX) and
automatically converts them into a **Duivestein DSS batch XML** plus **one label CSV per sheet**.

Production flow at the customer:

1. TopSolid (CAD/CAM) nests the parts and writes the CNC programs for an SCM Morbidelli X200 (Maestro).
2. TopSolid exports two XLSX files per project (label info + label positions) and a trigger file.
3. **This tool** converts those into a Duivestein batch job.
4. The Duivestein automatic warehouse picks each sheet, puts it on the label table, prints and places
   the labels (Duivestein generates the label layout itself from our CSV), registers the CNC program
   on the SCM and pushes the sheet onto the machine.

The tool only produces files. It does not print labels, talk to the machine or talk to TopSolid.

## Working rules for Claude

- The developer (Daan) is new to C#. His background is VBA with UserForms and a little Visual Studio.
  Write plain, readable code: simple classes and methods, clear names, comments that explain *why*.
  No dependency-injection containers, no MediatR, no clever abstractions, no async unless needed.
- Work **one stage at a time** (see Roadmap). At the end of every step, give:
  what changed, how to test it (exact commands), and a suggested git commit message. Then **stop and
  wait for confirmation** before starting the next step.
- Ask before adding any NuGet package other than ClosedXML and xUnit.
- The app must **never** require Excel or Office on the PC.
- **Never rely on the PC's regional settings.** Parse numbers with `CultureInfo.InvariantCulture`,
  write numbers with an explicit comma decimal separator. Belgian PCs use comma decimals.
- Do not modify the input files in `samples/topsolid/`. `samples/duivestein/` and
  `samples/materials.sample.csv` are our own expected output / data concept (regenerated on Daan's request).
- Daan writes in Dutch or English: answer in the language of his message.
  Code, identifiers and comments in English. UI text in Dutch (Flemish customer); keep all UI strings
  together in one place so they are easy to change.
- Keep the **Status** section at the bottom of this file up to date at the end of each stage.

## Tech stack

- .NET 10 (LTS), C#.
- `TsDssConverter.Core` — class library (`net10.0`), no Windows or UI dependencies. All conversion logic.
- `TsDssConverter.Cli` — console app for manual testing (stage 1).
- `TsDssConverter.Tray` — WinForms tray app (`net10.0-windows`), the product.
- `TsDssConverter.Tests` — xUnit tests, using the files in `samples/`.
- ClosedXML to read XLSX. `System.Xml.Linq` (`XDocument`) to write XML.
- Final delivery: single self-contained exe, no .NET install needed on the customer PC:
  `dotnet publish src/TsDssConverter.Tray -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

Suggested layout:

```
TsDssConverter/
  CLAUDE.md
  samples/
    topsolid/     Verschuren-P-20-LI.xlsx, Verschuren-P-20-LP.xlsx
    duivestein/   README (format reference from Duivestein, Dutch)
                  Verschuren-P-20.xml, Verschuren-P-20_001.csv … _011.csv  (expected output, golden files)
    materials.sample.csv   (5 TopSolid materials, DSS name = TopSolid name for now)
  docs/           AKSIS___TopSolid-Barbaric.pdf  (background: older installation with the same TopSolid exports)
  src/TsDssConverter.Core/
  src/TsDssConverter.Cli/
  src/TsDssConverter.Tray/
  tests/TsDssConverter.Tests/
```

## Input: TopSolid exports

Per project TopSolid writes three files to the export folder, named `{customer}-{project}-XX.xlsx`,
e.g. `Verschuren-P-20-LI.xlsx`. The project name (`Verschuren-P-20`) is everything before the suffix.

| File | Meaning |
|---|---|
| `…-LI.xlsx` | Label info, one row per part |
| `…-LP.xlsx` | Label position, one row per part |
| `…-TR.xlsx` | Trigger: the very last file TopSolid writes (after LI, LP and the CNC programs). Content is irrelevant |

The sample files come from an earlier installation (other customer, other warehouse brand).
The new customer's TopSolid will produce the same format.

**Always find columns by header name, never by position.** Ignore unknown and unnamed columns.
Read the first worksheet. Stop at the first fully empty row.

### LI columns (label info)

| Header | Example | Use |
|---|---|---|
| `Naam_Plaat` | `White_18#01` | Sheet name = `{TopSolid material}#{sheet number}`. Join key 1 |
| `Omschrijving` | `Verschuren - K2 - Front - 19587` | Part description. Join key 2. Trailing number = part ID |
| `Afmetingen` | `L: 660.0mm X B: 590.0mm` | Text. Parse panel length and width (dot decimals). Pass L and B as-is, never swap |
| `Materiaal` | `White ` | Material description (trim; has trailing spaces) |
| `L1`, `L2`, `B1`, `B2` | `AFS 0.8mm Fin231`, `NA`, `x`, empty | Edge banding per side, pass as text |
| `CAM_2` | `00019587_2.cix` | Program for 2nd setup, pass as text |
| `Opleg_2_?` | `OPLEG_2` or empty | 2nd setup needed, pass as text |
| `Test` | `P-20` | Actually the project number (badly named header) |

### LP columns (label position)

| Header | Example | Use |
|---|---|---|
| `SUP_DESIGNATION` | `18.0_panel 18mm` | Leading number = real thickness. Only for a sanity-check warning |
| `SP` | `White_18#01` | Sheet name. Join key 1 |
| `ID` | `Verschuren - K2 - Zijkant links - 13099` | Part description. Join key 2 |
| `SUP_L`, `SUP_B` | `3050`, `1300` | Sheet length (X) and width (Y) in mm |
| `SUP_D` | `18` | **Unreliable** — 18 for every sheet, also 9 mm and 40 mm boards. Do not use |
| `LABEL_X`, `LABEL_Y` | `2055.9699999999998` | Label position in mm, origin bottom-left, X along sheet length. Floating-point noise |
| `LABEL_ANGLE` | `0, 90, 180, 270, 360` | Label rotation in degrees |

The LP sample also has unnamed columns with `Submap1` / `SP1_1`: probably annotations made for a
presentation. Ignore them.

Join: LI (`Naam_Plaat`, `Omschrijving`) ↔ LP (`SP`, `ID`). In the sample all 63 parts match 1:1.

## Material mapping (`materials.csv`)

TopSolid material names (the part of the sheet name before `#`) differ from the warehouse stock codes.
A mapping file maintained by the customer translates them:

```
TopSolidMaterial;DssMaterial;Thickness;Grain
Paars_18;Paars_18;18;0
White_18;White_18;18;0
H1145_-_ST10_-_Chêne_Bardolino_naturel_Zijdewit_19;H1145_-_ST10_-_Chêne_Bardolino_naturel_Zijdewit_19;19;1
H1145_-_ST10_-_Chêne_Bardolino_naturel_Zijdewit_40;H1145_-_ST10_-_Chêne_Bardolino_naturel_Zijdewit_40;40;1
White_9;White_9;9;0
```

- Semicolon-separated (opens correctly in Belgian Excel), header row.
- **The TopSolid name is the master name.** For now `DssMaterial` is identical to `TopSolidMaterial`,
  so the batch XML and the label CSVs use the TopSolid names. The column stays, so real warehouse
  codes can be filled in later without code changes.
- The key is the material only: **without** the `#nn` sheet number (`White_18`, not `White_18#01`).
  A `#` in the key is an error with a hint ("remove #01").
- Matching: trimmed, case-insensitive.
- Encoding: read as UTF-8 (with or without BOM). If the text contains invalid UTF-8, fall back to
  Windows-1252 (Excel "CSV" saves as ANSI). Note: `Chêne` must survive both ways.
  In .NET, Windows-1252 needs `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`.
- Grain: 0 = none, 1 = along length, 2 = across. GRAINSTR text: `Geen`, `Langs`, `Dwars`.
- Unknown material → error. Never guess a material: the warehouse would pick the wrong board.

## Output: Duivestein files

Format reference: `samples/duivestein/README` (from Duivestein, Dutch). Read it. The original Duivestein
example batch (HP002) was used for syntax only and has been replaced by our own expected output, see below.

`samples/duivestein/` now holds the **expected output for the sample** (`Verschuren-P-20.xml` and
`Verschuren-P-20_001.csv` … `_011.csv`), created from the TopSolid sample files and `materials.sample.csv`
with the rules in this file. Use them as golden files in the tests. `PlanDate` in the XML is the date of
creation (2026-09-19); the test must pass in a fixed date. Not yet confirmed by Duivestein.

### Batch XML — `{BatchName}.xml` in the Duivestein batch folder

```xml
<?xml version="1.0" encoding="utf-8"?>
<DSSBatch>
  <BatchName>Verschuren-P-20</BatchName>
  <BatchDescription>Verschuren-P-20</BatchDescription>
  <PlanDate>2026-09-19</PlanDate>            <!-- today, yyyy-MM-dd -->
  <AutoExpand>False</AutoExpand>
  <Plans>
    <Plan>                                    <!-- one Plan per TopSolid material -->
      <PlanName>001</PlanName>                <!-- 001, 002, … -->
      <Material>PAARS18</Material>            <!-- from materials.csv -->
      <XDimSize>2500</XDimSize>               <!-- SUP_L -->
      <YDimSize>1250</YDimSize>               <!-- SUP_B -->
      <Grain>0</Grain>                        <!-- from materials.csv -->
      <Quantity>1</Quantity>                  <!-- number of sheets of this material -->
      <Rotation>0</Rotation>                  <!-- constant 0 for now -->
      <LabelFilenames>                        <!-- one per sheet, file name only -->
        <LabelFilename>Verschuren-P-20_001.csv</LabelFilename>
      </LabelFilenames>
      <CNCFilenames>                          <!-- one per sheet, full path, same order -->
        <CNCFilename>Z:\TopSolid\Export\Verschuren-P-20_Paars_18_01.xcs</CNCFilename>
      </CNCFilenames>
    </Plan>
  </Plans>
  <Routes>
    <Route>
      <ToStackPosition>LBL</ToStackPosition>
      <Parameters/>
    </Route>
  </Routes>
</DSSBatch>
```

- BatchName = project name from the file name.
- Plan order = order of first appearance of the material in the LP file.
  Sheets within a plan sorted by sheet number (`#01`, `#02`, …).
- Quantity = number of LabelFilename entries = number of CNCFilename entries.
- UTF-8, 2-space indentation, CRLF line endings.

### CNC filename (provisional)

Not present in the TopSolid export. Until TopSolid confirms their naming convention, build it as
`{TopSolidExportPath}\{BatchName}_{sheet name with # replaced by _}{CncExtension}`,
e.g. `Z:\TopSolid\Export\Verschuren-P-20_White_18_01.xcs`.
Put this logic in **one** small class (`CncPathBuilder`) — it will change.
If the CNC file does not exist at conversion time: log a **warning**, not an error
(TopSolid may still be writing the CAM files).
Advanced setting `CncPathPrefixInXml`: if filled, replace the export-folder part of the path with this
prefix in the XML (e.g. a UNC path `\\server\topsolid\Export\`), because the machine side may not know `Z:`.
Decided with the customer: the XML uses the drive-letter path (same `Z:` as the tray settings), so this
setting stays empty for now.

### Label CSV — one per sheet, in the Duivestein label folder

- File name `{BatchName}_{nnn}.csv`, `nnn` = running number over the whole batch (001…),
  in plan order then sheet order — same numbering style as the Duivestein example.
- Tab-separated, header row, CRLF, one row per part (label).
- Column names only letters (no accents), digits, underscore, dash.
- Decimal separator: comma. No thousands separator. Drop trailing `,0` (`660` not `660,0`).
- Text values: trim; replace tabs and line breaks with a space.
- Encoding: UTF-8 by default, kept in **one** constant/setting (`CsvEncoding`) — to be confirmed by
  Duivestein (accented values like `Chêne`).

Columns, in this order. The first 13 are mandatory for Duivestein; the rest are ours and can be used
in Duivestein's label template (send them this list):

| Column | Source |
|---|---|
| `MATERIAL` | DssMaterial (materials.csv) |
| `SHEETLENGTH` | SUP_L |
| `SHEETWIDTH` | SUP_B |
| `SHEETTHICKNESS` | Thickness (materials.csv) — not SUP_D |
| `GRAIN` | Grain (materials.csv) |
| `GRAINSTR` | `Geen` / `Langs` / `Dwars` |
| `X` | LABEL_X, after flip, rounded to whole mm |
| `Y` | LABEL_Y, after flip, rounded to whole mm |
| `ROTATION` | LABEL_ANGLE normalised to 0/90/180/270 (360 → 0) |
| `N` | Sequence number of the part on this sheet, 1…n, in LP order |
| `ID` | Trailing number of the description (`19587`) |
| `PANELLENGTH` | L from `Afmetingen` |
| `PANELWIDTH` | B from `Afmetingen` |
| `DESCRIPTION` | Omschrijving |
| `MATERIALNAME` | Materiaal (trimmed) |
| `EDGE_L1`, `EDGE_L2`, `EDGE_B1`, `EDGE_B2` | L1, L2, B1, B2 |
| `CAM2` | CAM_2 |
| `OPLEG2` | Opleg_2_? |
| `PROJECT` | Test (project number) |
| `SHEET` | Sheet name (`White_18#01`) |

### Zero-point flips (settings FlipX / FlipY)

- FlipX: `X = SUP_L − LABEL_X`. FlipY: `Y = SUP_B − LABEL_Y`. Apply before rounding.
- Default both off: TopSolid measures from bottom-left, and the reference corner is also a setting in
  Duivestein's DSSClient.
- Rotation is **not** changed by a flip for now. Whether it must change (e.g. 90 ↔ 270) will be decided
  after the physical test sheet — keep this in one clearly marked place in the code.
- Rounding: `MidpointRounding.AwayFromZero`.

### Writing safely

- Write all label CSVs first, the batch XML last (DSSClient must never see an XML whose labels are missing).
- Write every file as `name.tmp` in the target folder, then rename to the final name.
- If `{BatchName}.xml` already exists in the batch folder: **error**, write nothing
  ("Batch bestaat al in Duivestein-map"). Never overwrite a job the warehouse may be running.

## Validation

Errors (nothing is written, files go to the error folder with a readable reason):
- Missing required column in LI or LP (name it).
- Part in LP without match in LI, or the other way round (list them).
- Duplicate join key.
- Material not in materials.csv (name it).
- Different SUP_L / SUP_B within one material.
- `Afmetingen` not parseable.
- Angle not a multiple of 90.
- Label position outside the sheet (after flip).
- Batch XML already exists. Output folder not reachable.

Warnings (logged only):
- CNC program file not found.
- Part ID not unique within the batch.
- Thickness in materials.csv differs from the leading number of SUP_DESIGNATION.

## Tray application

Settings window (the only window), Dutch labels:

| # | Setting | Default |
|---|---|---|
| 1 | Checkbox: start with Windows | off |
| 2 | TopSolid export folder (XLSX + CNC programs .xcs) | `Z:\TopSolid\Export\` |
| 3 | Duivestein batch XML folder (jobs) | `Z:\Duivestein\Batch` |
| 4 | Duivestein label CSV folder | `Z:\Duivestein\Label` |
| 5 | Checkbox: label zero point — flip in X | off |
| 6 | Checkbox: label zero point — flip in Y | off |

- Folder fields with a Browse button. Save / Cancel. Warn (don't block) when a folder is not reachable.
- Below the settings: read-only list of the last 20 conversions (time, project, OK/Error, message).
- Closing the window only hides it. The app keeps running in the tray.

Advanced settings, only in `settings.json` (no UI for now): `CncExtension` (`.xcs`),
`CncPathPrefixInXml` (empty), `CsvEncoding` (`utf-8`), `RescanSeconds` (30), `RequireTriggerFile` (true).

Tray behaviour (silent mode):
- Small tray icon (embedded `.ico`) with three states: OK, busy, error.
- Tooltip: last result, short (e.g. `Laatste: Verschuren-P-20 OK 14:32`).
- Double-click: open settings window.
- Right-click menu: Instellingen… / Nu scannen / Pauzeren–Hervatten / Open logmap / Open materiaaltabel / Afsluiten.
- No popups on success. On error: one balloon notification, icon stays red until the next success or
  until the window is opened.
- Only one instance (named `Mutex`); a second start exits quietly.
- Start with Windows: registry value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  (no admin rights needed). The checkbox reads the real registry state when the window opens.
- Data folder `%AppData%\TsDssConverter\`: `settings.json`, `materials.csv`, `logs\yyyy-MM-dd.log`
  (keep 30 days). Simple own log writer, no logging framework.

## Folder watching and processing

- Watch the TopSolid export folder for `*.xlsx` with `FileSystemWatcher`, **plus** a full rescan every
  `RescanSeconds` and at startup (the watcher can miss events, especially on network drives).
- Ignore Excel lock files (`~$*.xlsx`).
- Trigger: a `{project}-TR.xlsx` appears → wait until `{project}-LI.xlsx` and `{project}-LP.xlsx` exist and
  all three can be opened (not locked). Retry every 2 s, give up after 2 minutes → error.
- If `RequireTriggerFile` is false: start when LI and LP both exist and have not changed for 10 seconds.
- One conversion at a time (simple queue on one background thread).
- After success: move LI, LP and TR to `{export}\_Verwerkt\{yyyyMMdd-HHmmss} {project}\`.
- After an error: move them to `{export}\_Fout\{yyyyMMdd-HHmmss} {project}\` together with `fout.txt`
  containing the reason in plain Dutch.
- The `Z:` drive may be unavailable (network, PC just started): never crash. Show the error state, keep
  retrying on the next rescan, recover automatically. Recreate the watcher if it raises an Error event.

## Expected result for the sample (use in tests)

Converting `samples/topsolid/Verschuren-P-20-*.xlsx` with the sample materials file gives:
- `Verschuren-P-20.xml` with 5 plans, in this order:
  `Paars_18` (1 sheet: 6 parts), `White_18` (5 sheets: 10, 9, 7, 10, 8),
  `H1145_…_19` (2 sheets: 4, 2), `H1145_…_40` (1 sheet: 1), `White_9` (2 sheets: 5, 1).
- 11 label CSVs, 63 label rows in total.
- Part `Verschuren - K2 - Zijkant links - 13099` on `White_18#01` (sheet 3050 × 1300):
  X `2056`, Y `289`, ROTATION `180`, ID `13099`, PANELLENGTH `734`, PANELWIDTH `568,5`.
  With FlipX: X `994`. With FlipY: Y `1011`.
- Part `Verschuren - K6 - Top - 17075` on `White_18#01`: LABEL_ANGLE 360 → ROTATION `0`.

## Open points (to confirm; do not invent answers)

TopSolid:
1. Naming, folder and extension of the CNC program per sheet. (Timing settled, see 2: CNC files are
   written before the TR file. Extension **decided: `.xcs`** (setting `CncExtension`, used in the golden XML).
   Naming and folder still open — `CncPathBuilder` stays provisional. The Duivestein example named the
   CNC file like the label file (`HP002_001.XCS`).)
2. ~~Keep writing the TR file, as the very last step.~~ **Confirmed:** the TR file is the very last file
   written. When it appears, LI, LP and the CNC programs are complete.
3. SUP_D is always 18 — fix in the export.
4. Are the unnamed LP columns (`Submap1`, `SP1_1`) part of the real export?
5. Rename LI header `Test` to e.g. `Project`.
6. Are the XLSX files written to the same folder as the CNC programs (`Z:\TopSolid\Export\`)?

Duivestein:
7. ~~Real material codes for this customer.~~ **Confirmed:** the TopSolid name is the master and the same
   name exists in the Duivestein stock (`samples/materials.sample.csv`, `DssMaterial` = `TopSolidMaterial`).
   Duivestein will check that the stock size matches `XDimSize`/`YDimSize`. The long H1145 names are used as
   test data for now; Daan will advise the customer to use shorter material names.
8. ~~CNC path in the XML: drive letter or UNC?~~ **Decided:** drive letter, the same `Z:` drive as the
   tray settings 2, 3 and 4. `CncPathPrefixInXml` stays empty by default (the path is used as built).
9. BatchName restrictions (length, characters) and what happens when a batch is re-sent.
10. CSV encoding for accented characters (UTF-8 or Windows-1252). **Skipped for now:** keep the
    `CsvEncoding` default `utf-8`.

Physical test sheet:
11. Is the label position the centre of the label? Which corner, which rotation direction?
    Does a flip change the rotation? Plan Rotation always 0?

## Roadmap

1. **Core + CLI.** Read LI/LP, join, map, build the batch model, write XML + CSVs.
   `dotnet run --project src/TsDssConverter.Cli -- <LI.xlsx> <LP.xlsx> <materials.csv> <outFolder>`.
   Unit tests for the expected sample result, number parsing/formatting, 360 → 0 and flips.
   *Done when:* tests pass and the output of the sample can be sent to Duivestein for a test import.
2. **Validation.** All errors and warnings above, clear messages, CLI exit code ≠ 0 on error.
3. **Tray shell.** Icon, menu, settings window, `settings.json`, start-with-Windows. No conversion yet.
4. **Watcher + processing.** Trigger logic, queue, `_Verwerkt` / `_Fout` folders, log files, notifications.
5. **Delivery.** Single-file publish, install on the customer PC, physical test sheet, adjust flips/rotation.

## Status

- Stage: 1 (Core + CLI) — first draft done, waiting for confirmation before stage 2.
- Built: `TsDssConverter.slnx` with `Core`, `Cli` and `Tests`. `Tray` does not exist yet (stage 3).
- The CLI output of the sample is byte-identical to the golden files in `samples/duivestein/`.
  96 unit tests pass (`dotnet test`).
- Already in Core (needed for correct output): missing columns, unmatched/duplicate parts, unknown
  material, different sheet sizes per material, unreadable `Afmetingen`, angle not a multiple of 90,
  batch already exists, output folder missing, safe writing (`.tmp` + rename, XML last).
  These stop at the first problem group and throw a `ConversionException` with a Dutch message.
- Not yet (stage 2): label position outside the sheet, all warnings (CNC file missing, duplicate part ID,
  thickness vs `SUP_DESIGNATION`), collecting *all* errors of every kind in one report.
- NuGet packages: ClosedXML (Core), xunit + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` (Tests;
  the last two are needed to run xUnit tests). `coverlet.collector` from the template was removed.
