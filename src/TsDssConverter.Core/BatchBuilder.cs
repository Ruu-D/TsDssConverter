namespace TsDssConverter.Core;

/// <summary>
/// Joins LI and LP, looks up the materials and builds the <see cref="Batch"/> (nothing is written to disk here).
///
/// Two steps:
///  1. CHECK everything and collect ALL errors. If there is any error, nothing is built and one
///     <see cref="ConversionException"/> lists every problem, so the user can fix them in one go.
///  2. BUILD the plans, sheets and labels. Warnings (which do not stop the conversion) go into Batch.Warnings.
/// </summary>
public static class BatchBuilder
{
    // A part after the LI/LP join, with the sheet name already split up.
    private class MatchedPart
    {
        public LabelPositionRow Position { get; set; } = new();
        public LabelInfoRow Info { get; set; } = new();
        public string MaterialName { get; set; } = "";
        public int SheetNumber { get; set; }
        public int N { get; set; }

        /// <summary>Filled in during the check step.</summary>
        public Label? Label { get; set; }
    }

    public static Batch Build(
        string batchName,
        List<LabelInfoRow> infoRows,
        List<LabelPositionRow> positionRows,
        MaterialTable materials,
        ConverterSettings settings,
        DateTime planDate)
    {
        // ---- Step 1: check everything ----
        var problems = new List<string>();

        List<MatchedPart> parts = JoinRows(infoRows, positionRows, problems);
        List<string> materialNames = GetMaterialNamesInLpOrder(parts);
        CheckMaterialsAndSheetSizes(parts, materialNames, materials, problems);

        foreach (var part in parts)
        {
            try
            {
                part.Label = CreateLabel(part, settings);
            }
            catch (ConversionException error)
            {
                problems.AddRange(error.Problems);
            }
        }

        if (problems.Count > 0)
        {
            throw new ConversionException(problems);
        }

        // ---- Step 2: build ----
        var batch = new Batch { Name = batchName, PlanDate = planDate };
        int labelFileNumber = 0; // running over the whole batch: 001, 002, ...

        foreach (string materialName in materialNames)
        {
            var materialParts = parts.Where(p => p.MaterialName == materialName).ToList();

            var plan = new Plan
            {
                PlanName = (batch.Plans.Count + 1).ToString("000"),
                Material = materials.Find(materialName),
                SheetLength = materialParts[0].Position.SheetLength,
                SheetWidth = materialParts[0].Position.SheetWidth,
            };

            // Sheets by sheet number (#01, #02, ...). Parts inside a sheet keep the LP order.
            var sheetNames = materialParts
                .OrderBy(p => p.SheetNumber) // OrderBy is stable: equal numbers keep their order
                .Select(p => p.Position.SheetName)
                .Distinct();

            foreach (string sheetName in sheetNames)
            {
                labelFileNumber++;
                var sheetParts = materialParts.Where(p => p.Position.SheetName == sheetName).ToList();

                var sheet = new Sheet
                {
                    Name = sheetName,
                    Number = sheetParts[0].SheetNumber,
                    LabelFileName = batchName + "_" + labelFileNumber.ToString("000") + ".csv",
                    CncPath = CncPathBuilder.Build(settings, batchName, sheetName),
                };

                foreach (var part in sheetParts)
                {
                    sheet.Labels.Add(part.Label!);
                }

                plan.Sheets.Add(sheet);
            }

            batch.Plans.Add(plan);
        }

        AddThicknessWarnings(parts, materialNames, materials, batch.Warnings);
        AddDuplicatePartIdWarnings(batch, batch.Warnings);

        return batch;
    }

    /// <summary>Matches every LP row with its LI row on (sheet name, description).</summary>
    private static List<MatchedPart> JoinRows(
        List<LabelInfoRow> infoRows, List<LabelPositionRow> positionRows, List<string> problems)
    {
        var infoByKey = new Dictionary<string, LabelInfoRow>();
        foreach (var info in infoRows)
        {
            if (!infoByKey.TryAdd(MakeKey(info.SheetName, info.Description), info))
            {
                problems.Add(Messages.DuplicateKey("LI", info.SheetName, info.Description));
            }
        }

        var parts = new List<MatchedPart>();
        var positionKeys = new HashSet<string>();
        var partsOnSheet = new Dictionary<string, int>(); // sheet name -> number of parts so far

        foreach (var position in positionRows)
        {
            string key = MakeKey(position.SheetName, position.Description);

            if (!positionKeys.Add(key))
            {
                problems.Add(Messages.DuplicateKey("LP", position.SheetName, position.Description));
                continue;
            }

            if (!infoByKey.TryGetValue(key, out LabelInfoRow? info))
            {
                problems.Add(Messages.PartOnlyInPositionFile(position.SheetName, position.Description));
                continue;
            }

            if (!TopSolidText.TryParseSheetName(position.SheetName, out string materialName, out int sheetNumber))
            {
                problems.Add(Messages.BadSheetName(position.SheetName));
                continue;
            }

            partsOnSheet.TryGetValue(position.SheetName, out int countSoFar);
            partsOnSheet[position.SheetName] = countSoFar + 1;

            parts.Add(new MatchedPart
            {
                Position = position,
                Info = info,
                MaterialName = materialName,
                SheetNumber = sheetNumber,
                N = countSoFar + 1,
            });
        }

        foreach (var info in infoRows)
        {
            if (!positionKeys.Contains(MakeKey(info.SheetName, info.Description)))
            {
                problems.Add(Messages.PartOnlyInInfoFile(info.SheetName, info.Description));
            }
        }

        return parts;
    }

    /// <summary>Materials in order of first appearance in the LP file: this is the plan order.</summary>
    private static List<string> GetMaterialNamesInLpOrder(List<MatchedPart> parts)
    {
        var names = new List<string>();
        foreach (var part in parts)
        {
            if (!names.Contains(part.MaterialName))
            {
                names.Add(part.MaterialName);
            }
        }

        return names;
    }

    /// <summary>Every material must be in materials.csv, and all sheets of one material must have the same size.</summary>
    private static void CheckMaterialsAndSheetSizes(
        List<MatchedPart> parts, List<string> materialNames, MaterialTable materials, List<string> problems)
    {
        foreach (string materialName in materialNames)
        {
            if (!materials.TryFind(materialName, out _))
            {
                problems.Add(Messages.UnknownMaterial(materialName));
            }

            var sizes = parts
                .Where(p => p.MaterialName == materialName)
                .Select(p => (p.Position.SheetLength, p.Position.SheetWidth))
                .Distinct();

            if (sizes.Count() > 1)
            {
                problems.Add(Messages.DifferentSheetSizes(materialName));
            }
        }
    }

    private static Label CreateLabel(MatchedPart part, ConverterSettings settings)
    {
        var position = part.Position;
        var info = part.Info;
        string partName = position.SheetName + " / " + position.Description;

        // Each of these throws a ConversionException that names the part if something is wrong.
        TopSolidText.ParseDimensions(info.Dimensions, partName, out double panelLength, out double panelWidth);
        string partId = TopSolidText.GetPartId(info.Description);

        LabelPosition labelPosition = LabelPositionCalculator.Calculate(
            position.SheetLength, position.SheetWidth,
            position.LabelX, position.LabelY, position.LabelAngle,
            settings.FlipX, settings.FlipY, partName);

        return new Label
        {
            X = labelPosition.X,
            Y = labelPosition.Y,
            Rotation = labelPosition.Rotation,
            N = part.N,
            Id = partId,
            PanelLength = panelLength,
            PanelWidth = panelWidth,
            Description = info.Description,
            MaterialName = info.MaterialText,
            EdgeL1 = info.EdgeL1,
            EdgeL2 = info.EdgeL2,
            EdgeB1 = info.EdgeB1,
            EdgeB2 = info.EdgeB2,
            Cam2 = info.Cam2,
            Opleg2 = info.Opleg2,
            Project = info.Project,
            Descriptions = MergeDescriptions(info, position),
            SheetName = position.SheetName,
        };
    }

    /// <summary>
    /// Both files can have the description fields. The LI value is used; if it is empty (or the LI file has no such
    /// column) the LP value fills the gap.
    /// </summary>
    private static string[] MergeDescriptions(LabelInfoRow info, LabelPositionRow position)
    {
        var merged = new string[ColumnKeys.DescriptionCount];

        for (int i = 0; i < merged.Length; i++)
        {
            merged[i] = info.Descriptions[i] != "" ? info.Descriptions[i] : position.Descriptions[i];
        }

        return merged;
    }

    /// <summary>
    /// Sanity check: the thickness in materials.csv should be the leading number of SUP_DESIGNATION.
    /// A difference is only a warning: materials.csv is the truth (SUP_DESIGNATION may be wrong in the export).
    /// </summary>
    private static void AddThicknessWarnings(
        List<MatchedPart> parts, List<string> materialNames, MaterialTable materials, List<string> warnings)
    {
        foreach (string materialName in materialNames)
        {
            Material material = materials.Find(materialName);
            var alreadyWarned = new HashSet<double>();

            foreach (var part in parts.Where(p => p.MaterialName == materialName))
            {
                bool hasNumber = TopSolidText.TryGetLeadingNumber(part.Position.Designation, out double thickness);
                if (!hasNumber || !alreadyWarned.Add(thickness))
                {
                    continue; // no SUP_DESIGNATION, or already checked this value for this material
                }

                if (Math.Abs(thickness - material.Thickness) > 0.01)
                {
                    warnings.Add(Messages.ThicknessDiffers(materialName, material.Thickness, thickness));
                }
            }
        }
    }

    /// <summary>The part number (ID column) should be unique in the whole batch, but it is not an error.</summary>
    private static void AddDuplicatePartIdWarnings(Batch batch, List<string> warnings)
    {
        var sheetsPerId = new Dictionary<string, List<string>>();

        foreach (var plan in batch.Plans)
        {
            foreach (var sheet in plan.Sheets)
            {
                foreach (var label in sheet.Labels)
                {
                    if (!sheetsPerId.TryGetValue(label.Id, out List<string>? sheets))
                    {
                        sheets = new List<string>();
                        sheetsPerId[label.Id] = sheets;
                    }

                    sheets.Add(sheet.Name);
                }
            }
        }

        foreach (var pair in sheetsPerId.Where(p => p.Value.Count > 1))
        {
            warnings.Add(Messages.DuplicatePartId(pair.Key, pair.Value.Distinct()));
        }
    }

    private static string MakeKey(string sheetName, string description)
    {
        return sheetName.Trim() + " | " + description.Trim();
    }
}
