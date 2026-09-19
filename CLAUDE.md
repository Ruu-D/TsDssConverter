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
  Code, identifiers and comments in English. UI text in **three languages** (Dutch, French, English; Dutch
  is the default because the customer is Flemish). Every text a user can see is written with
  `Localizer.T("nl", "fr", "en")` in ONE of two places: `Core\Messages.cs` (conversion errors and warnings)
  and `Tray\Strings.cs` (everything else). Never a loose text in a form or in code. A new text gets all three
  languages at once; a test checks that no text is missing.
- Keep the **Status** section at the bottom of this file up to date at the end of each stage.
  Do the same for `README.md` (status table, "verified so far", decisions and open questions).
  The README stays in **English** (for Daan's superiors), whatever language Daan writes in.

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
  media/          app.ico, header.png, icon-256.png, … (icon, banner and colour theme, see "Visual identity")
  docs/           AKSIS___TopSolid-Barbaric.pdf  (background: older installation with the same TopSolid exports)
  src/TsDssConverter.Core/
  src/TsDssConverter.Cli/
  src/TsDssConverter.Tray/
  tests/TsDssConverter.Tests/
```

## Visual identity (colour theme)

The graphics in `media/` are the base of all visible parts of the project: the icon, the tray, the settings
window and the README. Do not invent other colours or another icon style.

| File | Use |
|---|---|
| `media/app.ico` | The application icon: exe icon, settings window icon and base of the tray icon. Contains 16, 20, 24, 32, 40, 48, 64, 128 and 256 px |
| `media/app-icon.svg`, `icon-256.png`, `icon-512.png` | Source / large versions of the icon |
| `media/header.png`, `header@2x.png`, `header.svg` | Banner (540 × 84, and 2× for high-DPI) with name and tagline. README header and top of the settings window |
| `media/preview.png` | Overview of the icon at 16 – 128 px on light and dark backgrounds (for documentation) |
| `media/app-busy.ico`, `app-error.ico`, `app-paused.ico` | The tray icon in the "busy", "error" and "paused" state (made in stage 3 from `app.ico`) |
| `media/tray-states.png` | Overview of the four tray icon looks on light and dark backgrounds |
| `media/ROGIERS-transp-small.png` | Logo of ROGIERS (transparent background), shown small in the footer of the settings window (added by Daan) |

Palette (taken from the SVG files):

| Colour | Hex | Role |
|---|---|---|
| Brand blue | `#2CABE2` | Main colour: banner, primary buttons, accents |
| Dark blue | `#1480B5` | Hover / pressed state, lines, secondary accents |
| Light blue | `#7CCAED` | Highlights, progress and "busy" accents |
| Pale blue | `#A3D7EF` | Selected row, borders on light backgrounds |
| Very pale blue | `#D7EFFA` | Panel and list backgrounds |
| Navy | `#0A3A56` | Text and the dark badge in the icon |
| White | `#FFFFFF` | Window background, text on dark blue |

Rules:
- **Text is navy** (`#0A3A56`) on white, pale blue and brand blue (contrast 12.0, 10.1 and 4.6).
  Do **not** put white text on brand blue `#2CABE2` (contrast only 2.6). White text is only for the large
  title in the banner and on dark blue `#1480B5` (4.4: large or bold text only).
- The **error state** needs a red that is not in the palette. Use one clearly recognisable red, defined once,
  and only for errors (tray icon, balloon, error rows in the list).
- All colours live in **one** small `Theme` class in the Tray project (like the UI texts in one place),
  never as loose hex values in forms.
- Tray icon looks (OK / busy / error / paused): OK is the base icon; the others are variants of the SAME artwork
  with a round badge at the bottom right (where the sync badge of the base icon is, like OneDrive):
  busy = navy badge with three dots, error = red badge with "!", paused = white badge with a navy ring and a
  navy pause symbol. Made in stage 3, approved by Daan. New looks are always shown to Daan first.

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
      <LabelFilenames>                        <!-- one per sheet: label folder (3rd setting) + file name -->
        <LabelFilename>Z:\Duivestein\Label\Verschuren-P-20_001.csv</LabelFilename>
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
- LabelFilename = the label folder from the settings (the third path, `Z:\Duivestein\Label`) + a backslash + the file
  name (decided by Daan): Duivestein gets the full path, in the same way as the CNC paths (`BatchXmlWriter.BuildText`
  takes the label folder). The label CSV is written to that same folder. No `CncPathPrefixInXml` equivalent for labels.
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

Settings window (the only window), in the selected language (Dutch, French or English):

| # | Setting | Default |
|---|---|---|
| 0 | Language: drop-down right above "start with Windows", with the items `Dutch - Nederlands`, `French - Français`, `English - Engels` (title `Taal / Langue / Language`). Changes the whole interface, applied on Save | Dutch (`"Language": "nl"` in settings.json; `nl`, `fr` or `en`) |
| 1 | Checkbox: start with Windows | off |
| 2 | TopSolid export folder (XLSX + CNC programs .xcs) | `Z:\TopSolid\Export\` |
| 3 | Duivestein batch XML folder (jobs) | `Z:\Duivestein\Batch` |
| 4 | Duivestein label CSV folder | `Z:\Duivestein\Label` |
| 5 | Checkbox: label zero point — flip in X | off |
| 6 | Checkbox: label zero point — flip in Y | off |

- Folder fields with a Browse button. Save / Cancel at the bottom right. Warn (don't block) when a folder is not reachable.
- Below the settings: read-only list of the last 20 conversions (time, project, OK/Error, message).
- Closing the window only hides it. The app keeps running in the tray.
- **Footer, bottom left, below the list of conversions**, in the normal font size (an earlier version in half
  size was unreadable), with the small ROGIERS logo (`media/ROGIERS-transp-small.png`, embedded; `AppIcons.CompanyLogo`
  cuts off its transparent margin and scales it once to 128 px) at the left of the two lines:
  `Dev.: Daan Verhoost  |  ROGIERS NV/SA` (only the company name is a link to
  https://www.rogiers.be/) and below it `App version: 1.0.0` (translated: `App-versie`, `Version de l'application`).
  The version comes from `<Version>` in `TsDssConverter.Tray.csproj` (`AppInfo.Version`). The credit texts and
  the URL are in `Strings` (not translated).
- **Scaling and size of the window** (must keep working at 100%, 125%, 150%, ...): sizes are written for 96 dpi with
  `AutoScaleMode.Dpi`, and the window MUST be built between `SuspendLayout()` and `ResumeLayout()` (without that
  WinForms does not scale the pixel sizes: on a 150% screen the text grew but the window stayed small and the
  list of conversions was out of view). A ListView does not scale its columns: `ScaleHistoryColumns`.
  On opening, the window takes the height of its content (`SizeToContent`, room for the folder warnings
  reserved) limited to the free screen area (`FitToArea`); if the screen is too small the settings scroll and
  the list keeps a minimum height. Test changes to the window at a scaling above 100% (`--show`, see Status).
  All buttons (Browse, Save, Cancel) have ONE fixed size, 100 x 28 (`ButtonSize`, not AutoSize: bold Save text made
  it a different size than Cancel). Save and Cancel sit in one line, **at the bottom right of the window, below the
  list of conversions and on the same line as the footer** (`BuildBottomRow`), and end at the same right edge as
  the Browse buttons, the list and the divider lines (no default control margins on those rows).
- What the language switch covers: the window, tray menu, tooltip, balloons, dialogs, log lines AND the
  conversion messages (also the future `fout.txt`). It does NOT cover the files for Duivestein (label CSV and
  batch XML are data: `GRAINSTR` stays `Geen`/`Langs`/`Dwars`) and not the banner picture (fixed, English).

Advanced settings, only in `settings.json` (no UI for now): `CncExtension` (`.xcs`),
`CncPathPrefixInXml` (empty), `CsvEncoding` (`utf-8`), `RescanSeconds` (30), `RequireTriggerFile` (true).

Tray behaviour (silent mode):
- Small tray icon (embedded, based on `media/app.ico`) with four looks: OK, busy, error and paused
  (a pause symbol at the bottom right while the user has paused the program).
  Priority when several apply: error, then busy, then paused, then OK (`TrayStateRules.Displayed`).
  The settings window uses the colour theme and the banner from `media/` (see "Visual identity").
- Tooltip: last result, short (e.g. `Laatste: Verschuren-P-20 OK 14:32`).
- Double-click: open settings window.
- Right-click menu: Instellingen… / Nu scannen / Pauzeren–Hervatten / Open logmap / Open materiaaltabel / Afsluiten.
- No popups on success. On error: one balloon notification, icon stays red until the next success or
  until the window is opened.
- Only one instance (named `Mutex`, `Global\` so it also holds across Windows users, one per data folder);
  a second start exits quietly.
- Start with Windows: registry value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  (no admin rights needed). The checkbox reads the real registry state when the window opens.
- **Install folder = data folder = `C:\TsDssConverter\`** (decided by Daan: the tool is very light, so
  everything lives together): `TsDssConverter.exe`, `settings.json`, `materials.csv`, `logs\yyyy-MM-dd.log`
  (keep 30 days). Uninstall = delete the folder. The path is one constant (`AppDataFolder.DefaultRoot`).
  A development run can use another folder with `--data <folder>`, so it never touches the real install.
  If the folder cannot be created (C:\ locked down), show a clear Dutch message and exit.
  Simple own log writer, no logging framework.

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
  containing the reason in plain language, in the language selected in the settings.
- The `Z:` drive may be unavailable (network, PC just started): never crash. Show the error state, keep
  retrying on the next rescan, recover automatically. Recreate the watcher if it raises an Error event.
- Decided while building stage 4 (first draft, to be confirmed by Daan):
  - A problem that is the *situation* and not the files (Duivestein folder or materials.csv not reachable, a read or
    write error) does NOT move the files to `_Fout`: they wait in the export folder and are tried again at every scan.
    The user gets one balloon for it, not one per scan.
  - A problem with the files (unknown material, corrupt xlsx, batch already exists, missing/locked file after 2
    minutes) moves them to `_Fout` with `fout.txt`. Moving them back into the export folder starts a new attempt.
  - When the export folder itself is not reachable, the red icon and balloon come only after 60 seconds (the network
    drive is often not connected yet just after Windows started). It is logged at once, and the icon recovers by itself.
  - "Nu scannen" also scans while paused. Resume and Save settings scan at once.
  - The list of conversions is still not saved between runs (the log file has everything).

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

- Stage: 4 (Watcher + processing) — first draft done, waiting for confirmation before stage 5 (Delivery).
- Built: `TsDssConverter.slnx` with `Core`, `Cli`, `Tray` and `Tests`.
- The CLI output of the sample is byte-identical to the golden files in `samples/duivestein/`.
  299 unit tests pass (`dotnet test`). The Tests project targets `net10.0-windows` so it can test `Tray`.
  Tests run one after the other (the language is one global switch).
- Stage 1: read, join, map, write XML + CSVs, safe writing (`.tmp` + rename, XML last), never overwrite a batch.
- Stage 2, errors: everything in the Validation section is implemented, including "label outside the sheet
  (after flip)". `BatchBuilder` first CHECKS and collects all problems, then throws ONE `ConversionException`
  whose `Problems` list holds every problem (the message shows the first 25). `Converter` reads LI, LP and
  materials.csv first and reports problems in all three together. Nothing is written when there is an error.
  Only one problem per part is reported (the first one found for that part).
- Stage 2, warnings: `ConversionResult.Warnings` gets CNC program not found (one warning if the export folder
  itself is unreachable), part ID not unique in the batch, thickness in materials.csv vs `SUP_DESIGNATION`.
  The CLI prints them as `WAARSCHUWING`; the exit code is 1 for an error, 2 for an unexpected error.
- The CNC check uses `TopSolidExportPath` on THIS PC, never `CncPathPrefixInXml`. The CLI default
  `Z:\TopSolid\Export\` gives one "not reachable" warning on a PC without that drive.
- Stage 3, Core (no Windows dependency, all tested): `AppSettings` + `SettingsStore` (settings.json: comments
  and trailing commas allowed, a broken file gives defaults + a Dutch note and is never overwritten, wrong
  values are repaired by `Normalize()`), `AppDataFolder` (`C:\TsDssConverter`), `LogWriter` (`logs\yyyy-MM-dd.log`,
  30 days, never throws), `ConversionHistory` (last 20, thread-safe, `Changed` event).
- Stage 3, Tray (`TsDssConverter.exe`): `TrayApp` (icon, menu, tooltip, balloon on errors, window opening),
  `SettingsForm` (built in code, no designer; banner + theme from `media/`; folder warnings are checked in the
  background so a dead `Z:` cannot freeze the window; the X only hides), `StartWithWindows` (HKCU Run key),
  `Theme` (all colours), `Strings` (all Dutch UI texts), `AppIcons` (icons built into the exe).
  Single instance = a `Global\` mutex per data folder (two Windows users, or two copies, cannot both run).
- Tray icons: `media/app.ico` (OK), `media/app-busy.ico` (navy badge with three dots), `media/app-error.ico`
  (red badge with "!") and `media/app-paused.ico` (white badge, navy pause symbol), generated from the same
  artwork; overview in `media/tray-states.png`. A single red (`#D6322C`) is the only colour outside the icon
  palette. The pause menu item switches the paused look on and off; `SetState` only takes Ok, Busy or Error.
- Settings window (feedback round after the first review): banner 1.5x (810 x 126), a divider line below
  "Start met Windows" and one above "Nulpunt van het label", bold folder titles. Window is 840 px wide (at 100%).
- Second feedback round: the credit moved from under the banner (unreadable at half size) to a footer at the
  bottom left in normal size, with the app version. The scaling bug (see "Scaling and size of the window") was
  found by running the exe on this PC at its real 150% scaling: `bin\latest\TsDssConverter.exe --show --data <folder>`
  opens the settings window at start, and a screenshot of that window (with `SetProcessDPIAware` and
  `CopyFromScreen` in PowerShell) shows exactly what the user sees.
- Languages (nl / fr / en): `Core\Localizer.cs` (the switch), all texts in `Core\Messages.cs` and `Tray\Strings.cs`.
  `settings.json` has `"Language"`. `TrayApp.ApplyLanguage` rebuilds the tray menu at once and the settings window
  the next time it is opened. `TrayApp` reads the language before writing its first log line. The CLI has `--lang`.
- Try it: `dotnet run --project src/TsDssConverter.Tray -- --demo --show --data C:\Temp\TsDssData`.
  `--demo` adds menu items for the busy and error looks (the pause look is the real Pauzeren menu item);
  `--show` opens the settings window at start; `--data` keeps the real `C:\TsDssConverter` untouched.
- A build for a quick look without disturbing a running copy: `dotnet build src/TsDssConverter.Tray -o bin\latest`
  (`bin\` is ignored by git). A running exe locks the files in `bin\Debug`, so exit it first for a normal build.
- `TrayApp.ReportResult(project, success, message)` and `SetState(...)` can be called from any thread (the watcher
  does). The history list is in memory only (empty after a restart).
- The single-file publish works (`-r win-x64 --self-contained -p:PublishSingleFile=true`), 126 MB. Stage 5:
  consider `-p:EnableCompressionInSingleFile=true` to make it smaller.
- Stage 4, Core (no Windows dependency, all tested with a fake clock): `ExportScanner` (which projects are ready: finds
  LI/LP/TR by name, ignores `~$` files and other xlsx files, only the top level so `_Verwerkt` / `_Fout` are never
  scanned; trigger mode waits until all three files can be opened exclusively, gives up after 2 minutes; without a
  trigger file LI + LP must be unchanged for 10 s), `ProjectProcessor` (convert one project, move LI/LP/TR to
  `_VerwerktyyyyMMdd-HHmmss project` or to `_Fout...` with `fout.txt`; the CNC files stay; never throws) and
  `ExportWatcher` (ONE background thread = the queue; `FileSystemWatcher` + rescan every `RescanSeconds` + rescan at
  start; scans every 2 s while a project waits; `RunOnce()` is one round and is what the tests call).
- Stage 4, rules that are easy to forget: (1) a *temporary* problem (Duivestein folder or materials.csv not reachable,
  read/write error; `ConversionException.IsTemporary`) leaves the files where they are and is retried at every scan,
  but the user hears about it only ONCE (`ProcessOutcome.IsRepeat`); (2) a problem with the files themselves (bad
  material, corrupt xlsx, batch exists already, gave up waiting) goes to `_Fout`; (3) if the batch is written but the
  files cannot be moved, it stays a success and the move is retried (`_pendingMoves`), never a second conversion;
  (4) an export folder that is not reachable is reported (red icon, one balloon, one list line) only after 60 s
  (`FolderProblemGrace`: the network drive is often not connected yet when Windows has just started) and
  `FolderProblemSolved` sets the icon back by itself; (5) a FileSystemWatcher Error rebuilds the watcher;
  (6) "Nu scannen" scans also while paused; Resume and Save scan at once.
- Stage 4 in the tray: `TrayApp` starts the watcher and listens to its events (`ConversionStarted` = busy icon,
  `ConversionFinished` = `ReportResult`, `FolderProblemFound` / `Solved`). All texts of the watcher are in
  `Messages` (nl/fr/en), `fout.txt` is written in the selected language.
- Checked with the real exe (`--data <scratch folder>`, files dropped while it runs, TR last): the sample became a
  batch with 11 label files identical to the golden files, the files went to `_Verwerkt`, a corrupt LI went to
  `_Fout` with `fout.txt`, and both lines showed in the list of conversions.
- After Daan's test of stage 4 (2026-09-20): (1) the batch XML now holds the FULL path of every label file
  (`Z:\Duivestein\Label\Verschuren-P-20_001.csv`), the golden XML `samples/duivestein/Verschuren-P-20.xml` was changed
  for that, and the golden test swaps its temp folder for `Z:\Duivestein\Label` before comparing; (2) the footer got
  the ROGIERS logo. Daan tested his own files as `samples/topsolid/Daan-*.xlsx` (not part of the samples, do not commit).
- Not yet (stage 5 or later): the list of conversions is not saved between runs; the real TopSolid output and the
  Duivestein import have not been tried; no installer.
- NuGet packages: ClosedXML (Core), xunit + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` (Tests;
  the last two are needed to run xUnit tests). `coverlet.collector` from the template was removed.
