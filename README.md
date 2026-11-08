<h1 align="center"><img src="media/preview/Banner.png" width="540" alt="TsDssConverter - Automatic nesting job transfer from TOPSOLID to DUIVESTEIN"></h1>

**Automatic nesting job transfer from TopSolid to the Duivestein panel warehouse.**

TsDssConverter is a small Windows tray application. It watches the folder where TopSolid exports its nesting jobs and turns
every finished job, without any manual work, into a batch job for the Duivestein automatic warehouse.
Sister tool: **[DssLabelTemplate](https://github.com/Ruu-D/DssLabelTemplate)** (view, edit and reprint the labels).

> **Status:** version **1.1.3** (2026-11-08). In use at the customer since version 1.1.0 (2026-10-24); the real-world tests
> with the warehouse and the machine are going on. Full history: [CHANGELOG.md](CHANGELOG.md).

## At a glance

- **One file**, `TsDssConverter.exe`: no installer, no .NET installation, no Excel or Office. It lives in `C:\TsDssConverter\`,
  together with its settings, the materials table and the log files. Uninstalling = deleting that folder.
- **Works silently** in the Windows tray. Only when a job fails: one notification and a red icon.
- **Dutch, French and English** interface.
- **Strict on purpose:** it never guesses a material, never overwrites a job and never lets the warehouse see half a job.

## What it looks like

The program lives in the Windows tray, next to the clock. Double-click the icon to open the settings window.

<p align="center">
  <img src="media/preview/TrayIcon.png" width="420" alt="The TsDssConverter icon in the Windows tray, next to the clock.">
</p>

<p align="center">
  <img src="media/preview/MainUI-preview.png" width="720" alt="The TsDssConverter settings window in English: the TopSolid export folder and the two Duivestein folders side by side, the label zero point with its small schematic, and the list of the last conversions with two successful jobs and one failed job.">
</p>

## Why this tool exists

At the customer, panels are nested and programmed in **TopSolid** (CAD/CAM) for an **SCM Morbidelli X200** CNC machine. The
boards are handled by a **Duivestein automatic warehouse**, which picks each sheet, prints and places its labels, and pushes
the sheet onto the machine. The warehouse needs a *batch job* (XML) that says which sheets to pick, which labels to print and
where each label goes. **TopSolid does not produce that format: TsDssConverter is the missing link.**

```mermaid
flowchart LR
    A["TopSolid<br/>nesting + CNC programs"] --> B["Two XLSX exports<br/>per project + trigger file"]
    B --> C["TsDssConverter"]
    M["materials.csv<br/>(customer maintained)"] --> C
    C --> D["Batch XML<br/>+ one label CSV per sheet"]
    D --> E["Duivestein warehouse<br/>picks sheets, prints labels"]
    E --> F["SCM Morbidelli X200"]
```

## How it works

1. TopSolid writes two XLSX files per project (label information `-LI` and label positions `-LP`), the CNC programs in a
   folder named after the project, and a trigger file `-TR` as the very last one.
2. When the trigger file appears, the program converts the project: it joins the two exports per part, translates the
   TopSolid material names with `materials.csv`, and writes **one batch XML** (one plan per material) and **one label CSV per
   sheet** (position, rotation, dimensions, edge banding and other data for the label layout).
3. The CNC programs are moved to `CNC\<project>\` and the XLSX files to `_Converted\` (folder names follow the language), so
   the export folder only holds what is not converted yet.
4. If something is wrong with a job, its files go to `_Error\` with a `fout.txt` that explains in plain language what to
   correct. Once fixed, **Retry failed** puts them back for a new attempt. A network drive that is not there yet only
   delays the job: it is retried by itself.

The sample project in [`samples/`](samples/) (22 parts) becomes one batch XML with 2 plans and 3 label files; the expected
output is in [`samples/duivestein/`](samples/duivestein/).

## Built to be safe

A wrong job means a wrong board or a wrong program on the machine, so the tool refuses rather than repairs:

- **No guessed materials:** an unknown material, an empty one or two materials on one sheet stop the job, with the name.
- **No overwritten jobs:** if the batch already exists in the warehouse folder, nothing is written.
- **No mixed-up CNC programs:** each project's programs stay in a folder of their own.
- **Never half a job:** label files first, batch XML last, each file renamed into place only when complete.
- **Every problem at once,** in plain language, naming the part or column. Small issues are warnings only.

## Main milestones

| Version | Date | |
|---|---|---|
| 1.0.0 | 2026-09-20 | First complete version: conversion, all checks, tray application, folder watching, one-file delivery |
| 1.0.3 | 2026-09-21 | Batch XML in the format accepted by Duivestein's DSSClient |
| 1.1.0 | 2026-10-24 | **First customer release**, with the redesigned settings window |
| 1.1.1 | 2026-10-30 | Fixes from the first real customer exports (material from TopSolid's *Matériau* column) |
| 1.1.2 | 2026-11-04 | CNC programs read from a folder per project, as agreed with TopSolid |
| 1.1.3 | 2026-11-08 | `settings.json` can be edited while the program runs (see below); banner in the style of DssLabelTemplate |

## Settings that are only in `settings.json`

The folders, language and zero point are set in the window. A few advanced settings are only in
`C:\TsDssConverter\settings.json`: **Shift+click on the ROGIERS logo** (bottom left of the window) opens it in Notepad. A saved
change is used within seconds, without a restart, and is never overwritten by the window.

| Setting | Default | Meaning |
|---|---|---|
| `CncExtension` | `.xcs` | File extension of the CNC programs |
| `CncPathPrefixInXml` | empty | Replaces the export folder in the CNC paths of the batch XML (e.g. a `\\server\...` path) |
| `CsvEncoding` | `utf-8` | Encoding of the label CSV files |
| `RescanSeconds` | `30` | How often the export folder is scanned besides the file events (5 – 3600) |
| `RequireTriggerFile` | `true` | Wait for the `-TR` file before converting |

The TopSolid column names and the extra label columns are set with the *Config* buttons, the materials with
*Open materials table*.

## Open points

| From | Question |
|---|---|
| TopSolid | **Deliver the agreed folder per project for the CNC programs** (`<export>\<project>\<sheet>.xcs`, agreed 2026-11-04). The program is ready for it; until then programs directly in the export folder still work, protected by a time check. |
| Duivestein | Restrictions on the batch name, what happens when a batch is sent twice, and an import test of the label CSVs |
| Physical test sheet | Which point of the label the position refers to, the rotation direction, and how square and non-square labels are turned |

## Technology

C# on .NET 10 with Windows Forms; [ClosedXML](https://github.com/ClosedXML/ClosedXML) reads the XLSX files (no Excel needed).
460 automated tests (xUnit) cover the conversion, every error and warning, the window at several screen scalings and all three
languages; the sample converts byte for byte to the expected files.

## Credits

Developed by Daan Verhoost, together with my best friend Claude AI for [ROGIERS NV/SA](https://www.rogiers.be/).
