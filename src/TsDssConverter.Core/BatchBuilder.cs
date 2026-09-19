namespace TsDssConverter.Core;

/// <summary>
/// Joins LI and LP, looks up the materials and builds the <see cref="Batch"/> (nothing is written to disk here).
///
/// Stage 1: problems that make the result wrong stop the conversion with a <see cref="ConversionException"/>.
/// Related problems are collected and reported together (unmatched parts, unknown materials).
/// Warnings and the remaining checks come in stage 2.
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
    }

    public static Batch Build(
        string batchName,
        List<LabelInfoRow> infoRows,
        List<LabelPositionRow> positionRows,
        MaterialTable materials,
        ConverterSettings settings,
        DateTime planDate)
    {
        var problems = new List<string>();

        List<MatchedPart> parts = JoinRows(infoRows, positionRows, problems);
        ThrowIfProblems(problems);

        // Materials in order of first appearance in the LP file: this is the plan order.
        var materialNames = new List<string>();
        foreach (var part in parts)
        {
            if (!materialNames.Contains(part.MaterialName))
            {
                materialNames.Add(part.MaterialName);
            }
        }

        // Check all materials and sheet sizes first, so the user sees every problem at once.
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

        ThrowIfProblems(problems);

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
                    sheet.Labels.Add(CreateLabel(part, settings));
                }

                plan.Sheets.Add(sheet);
            }

            batch.Plans.Add(plan);
        }

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

    private static Label CreateLabel(MatchedPart part, ConverterSettings settings)
    {
        var position = part.Position;
        var info = part.Info;
        string partName = position.SheetName + " / " + position.Description;

        TopSolidText.ParseDimensions(info.Dimensions, partName, out double panelLength, out double panelWidth);

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
            Id = TopSolidText.GetPartId(info.Description),
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
            SheetName = position.SheetName,
        };
    }

    private static string MakeKey(string sheetName, string description)
    {
        return sheetName.Trim() + " | " + description.Trim();
    }

    private static void ThrowIfProblems(List<string> problems)
    {
        if (problems.Count > 0)
        {
            throw new ConversionException(string.Join(Environment.NewLine, problems));
        }
    }
}
